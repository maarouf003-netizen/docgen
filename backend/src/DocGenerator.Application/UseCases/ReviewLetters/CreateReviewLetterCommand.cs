using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.ReviewLetters;

/// <summary>تسطير كتاب مطالعة (مربوط بملف أو عام) وتوليد رقمه وتاريخه تلقائيًا.</summary>
public sealed record CreateReviewLetterCommand(
    CreateReviewLetterRequest Request,
    int ActorUserId,
    string? ActorName,
    int ActorBranchId) : ICommand<ReviewLetterDto>;

internal sealed class CreateReviewLetterCommandHandler
    : CommandHandlerBase<CreateReviewLetterCommand, ReviewLetterDto>
{
    private const int MaxNumberGenerationAttempts = 20;

    private readonly IReviewLetterCreateWriteRules _rules;
    private readonly IBranchRepository _branches;
    private readonly IDocumentRepository _documents;
    private readonly IReviewLetterRepository _letters;
    private readonly IRepository<Section> _sections;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;

    public CreateReviewLetterCommandHandler(
        IReviewLetterCreateWriteRules rules,
        IBranchRepository branches,
        IDocumentRepository documents,
        IReviewLetterRepository letters,
        IRepository<Section> sections,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        IEnumerable<IPipelineBehavior<CreateReviewLetterCommand, ReviewLetterDto>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _branches = branches;
        _documents = documents;
        _letters = letters;
        _sections = sections;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _dbErrors = dbErrors;
    }

    protected override async Task<ReviewLetterDto> HandleCoreAsync(
        CreateReviewLetterCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(bodyHtml)))
            throw new ArgumentException("نص كتاب المطالعة مطلوب");

        var branch = await _branches.GetByIdAsync(command.ActorBranchId, ct)
            ?? throw new ArgumentException("الفرع غير موجود");

        Document? document = null;
        if (request.DocumentId is not null)
        {
            document = await _documents.GetByIdAsync(request.DocumentId.Value, ct)
                ?? throw new ArgumentException("الملف غير موجود");

            var mayAttach = document.CreatedById == command.ActorUserId
                || await _rules.FollowsDocumentAsync(document.Id, command.ActorUserId, ct);
            if (!mayAttach)
                throw new UnauthorizedAccessException("لا تملك صلاحية تسطير مطالعة على هذا الملف");
        }

        var now = DateTime.UtcNow;

        // توجيه المستلم (§10): مع ملف تلقائي لمالك دائرته (بلا منسدل ويُتجاهل
        // المُرسَل)؛ وبلا ملف اختيار المنسدل (رئيس القسم افتراضيًا — قرار 28).
        Section? recipientSection = null;
        int? recipientSectionId;
        if (document is not null)
        {
            recipientSectionId = await _rules.FileOwnerSectionAsync(document, ct);
        }
        else
        {
            recipientSection = await _rules.ResolveGeneralRecipientSectionAsync(
                request.RecipientSectionId, command.ActorBranchId, ct);
            recipientSectionId = recipientSection?.Id;
        }

        var letter = new ReviewLetter
        {
            BranchId = command.ActorBranchId,
            CreatedById = command.ActorUserId,
            DocumentId = document?.Id,
            RecipientSectionId = recipientSectionId,
            RecipientSection = recipientSection,
            LetterNumber = string.Empty, // يُضبط أدناه مع رقم الرسالة الأولى = المرشّح النهائي بعد حل أي سباق.
            LetterDate = now,
            IsAnswered = false,
            CreatedAt = now,
            UpdatedAt = now,
            Messages =
            [
                new ReviewLetterMessage
                {
                    Kind = ReviewLetterMessage.KindLetter,
                    BodyHtml = bodyHtml,
                    BodyPlainText = HtmlInputSanitizer.ToPlainText(bodyHtml),
                    MessageNumber = string.Empty, // يُضبط أدناه = رقم الكتاب النهائي.
                    MessageDate = now,
                    AuthorId = command.ActorUserId,
                    AuthorName = command.ActorName ?? string.Empty,
                    AuthorRole = nameof(UserRole.Lawyer).ToLowerInvariant(),
                },
            ],
        };

        var scope = document is null ? "عام" : $"ملف {document.Id}";
        var recipient = recipientSectionId.HasValue ? $"للشعبة {recipientSectionId.Value}" : "للقسم";
        // سباق الترقيم: فحص التفرّد خارج المعاملة قد يتجاوزه إدراج متزامن بالمرشّح
        // نفسه؛ القيد الفريد على LetterNumber هو الحارس الأخير، وعند اصطدامه
        // يُعاد التوليد بمرشّح جديد على الكيان نفسه بدل خطأ 500 — فإعادة الحفظ
        // بعد فشل سباق تُكرّر الإدراج الوحيد المعلّق لا غير، والتدقيق لا يُسجَّل
        // إلا داخل المحاولة الناجحة (مرآة نمط المراسلات).
        var candidate = await _rules.GenerateUniqueNumberAsync(branch.Code, now, ct);
        for (var attempt = 0; attempt < MaxNumberGenerationAttempts; attempt++)
        {
            letter.LetterNumber = candidate;
            letter.Messages.Single(m => m.Kind == ReviewLetterMessage.KindLetter).MessageNumber = candidate;
            try
            {
                await _tx.RunAsync(async token =>
                {
                    await _letters.AddAsync(letter, token);
                    await _uow.SaveChangesAsync(token);
                    await _audit.LogAsync(command.ActorName, "create_review_letter",
                        details: $"سطّر كتاب مطالعة ({scope}) برقم {letter.LetterNumber} {recipient}",
                        ct: token);
                }, ct);
                break;
            }
            catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
            {
                if (attempt + 1 >= MaxNumberGenerationAttempts)
                    throw new DocumentConflictException("تعذر توليد رقم فريد لكتاب المطالعة، حاول مجدداً", ex);
                candidate = await _rules.GenerateUniqueNumberAsync(branch.Code, now, ct);
            }
        }

        // اسم الشعبة المستلمة للعرض (الكيان المنشأ بلا روابط محمّلة).
        if (letter.RecipientSection is null && letter.RecipientSectionId.HasValue)
            letter.RecipientSection = await _sections.GetByIdAsync(letter.RecipientSectionId.Value, ct);

        return _rules.ToDto(letter);
    }
}
