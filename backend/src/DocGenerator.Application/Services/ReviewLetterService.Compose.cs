using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class ReviewLetterService : IReviewLetterService
{
    public async Task<ReviewLetterDto> CreateAsync(CreateReviewLetterRequest request, int actorUserId,
        string? actorName, int actorBranchId, CancellationToken ct = default)
    {
        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(bodyHtml)))
            throw new ArgumentException("نص كتاب المطالعة مطلوب");

        var branch = await _branches.GetByIdAsync(actorBranchId, ct)
            ?? throw new ArgumentException("الفرع غير موجود");

        Document? document = null;
        if (request.DocumentId is not null)
        {
            document = await _documents.GetByIdAsync(request.DocumentId.Value, ct)
                ?? throw new ArgumentException("الملف غير موجود");

            var mayAttach = document.CreatedById == actorUserId
                || await FollowsDocumentAsync(document.Id, actorUserId, ct);
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
            recipientSectionId = await FileOwnerSectionAsync(document, ct);
        }
        else
        {
            recipientSection = await ResolveGeneralRecipientSectionAsync(
                request.RecipientSectionId, actorBranchId, ct);
            recipientSectionId = recipientSection?.Id;
        }

        var letter = new ReviewLetter
        {
            BranchId = actorBranchId,
            CreatedById = actorUserId,
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
                    AuthorId = actorUserId,
                    AuthorName = actorName ?? string.Empty,
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
        var candidate = await GenerateUniqueNumberAsync(branch.Code, now, ct);
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
                    await _audit.LogAsync(actorName, "create_review_letter",
                        details: $"سطّر كتاب مطالعة ({scope}) برقم {letter.LetterNumber} {recipient}",
                        ct: token);
                }, ct);
                break;
            }
            catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
            {
                if (attempt + 1 >= MaxNumberGenerationAttempts)
                    throw new DocumentConflictException("تعذر توليد رقم فريد لكتاب المطالعة، حاول مجدداً", ex);
                candidate = await GenerateUniqueNumberAsync(branch.Code, now, ct);
            }
        }

        // اسم الشعبة المستلمة للعرض (الكيان المنشأ بلا روابط محمّلة).
        if (letter.RecipientSection is null && letter.RecipientSectionId.HasValue)
            letter.RecipientSection = await _sections.GetByIdAsync(letter.RecipientSectionId.Value, ct);

        return ToDto(letter);
    }

    /// <summary>
    /// تجهيز تنبيه الرد لصاحب الكتاب (لا لمنفّذ الرد): الجمهور حسب دوره (محامٍ أو رئيس)،
    /// والنص الكامل للرد بلا قصّ، عبر Outbox ليُدمج مع أي تنبيه نشط لنفس الكتاب.
    /// </summary>
    private async Task StageReplyAlertAsync(ReviewLetter letter, ReviewLetterMessage reply,
        int headUserId, string? headName, CancellationToken token)
    {
        var owner = await _users.GetByIdAsync(letter.CreatedById, token);
        var audience = owner?.Role is UserRole.Head or UserRole.SubHead
            ? HeadAlertAudience.SingleHead
            : HeadAlertAudience.SingleLawyer;

        await _alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                HeadAlertKindCatalog.ReviewLetterReply,
                HeadAlertMessages.ReviewLetterReply(new(
                    headName ?? "رئيس القسم", letter.LetterNumber, reply.BodyPlainText)),
                audience,
                letter.BranchId,
                headUserId,
                DocumentId: letter.DocumentId,
                ReviewLetterId: letter.Id,
                RecipientUserId: letter.CreatedById),
            supersedePlan: null,
            occurredAt: DateTime.UtcNow,
            ct: token);
    }
}
