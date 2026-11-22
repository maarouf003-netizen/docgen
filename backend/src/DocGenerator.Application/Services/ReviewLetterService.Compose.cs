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

    public async Task<ReviewLetterMessageDto> AddAddendumAsync(int letterId,
        AddReviewLetterAddendumRequest request, int actorUserId, string? actorName,
        CancellationToken ct = default)
    {
        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(bodyHtml)))
            throw new ArgumentException("نص اللاحق مطلوب");

        // قراءة متتبَّعة (GetByIdAsync بلا AsNoTracking) لتُحدَّث حالة الكتاب في المعاملة نفسها.
        var letter = await _letters.GetByIdAsync(letterId, ct)
            ?? throw new KeyNotFoundException("كتاب المطالعة غير موجود");

        if (letter.CreatedById != actorUserId)
            throw new UnauthorizedAccessException("اللاحق يُضاف من محامي الكتاب نفسه");

        var branchCode = await ResolveBranchCodeAsync(letter.BranchId, ct);
        var now = DateTime.UtcNow;
        var addendumNumber = await GenerateUniqueNumberAsync(branchCode, now, ct);

        var addendum = new ReviewLetterMessage
        {
            ReviewLetterId = letter.Id,
            Kind = ReviewLetterMessage.KindAddendum,
            BodyHtml = bodyHtml,
            BodyPlainText = HtmlInputSanitizer.ToPlainText(bodyHtml),
            MessageNumber = addendumNumber,
            MessageDate = now,
            AuthorId = actorUserId,
            AuthorName = actorName ?? string.Empty,
            AuthorRole = nameof(UserRole.Lawyer).ToLowerInvariant(),
        };
        letter.Messages.Add(addendum);

        // أي لاحق يعيد الكتاب إلى «بانتظار رد» حتى لو سبق الرد عليه.
        letter.IsAnswered = false;
        letter.UpdatedAt = now;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "add_review_letter_addendum",
                details: $"أضاف لاحقاً إلى كتاب المطالعة رقم {letter.LetterNumber}",
                ct: token);
        }, ct);

        return ToMessageDto(addendum);
    }

    public async Task<ReviewLetterMessageDto> ReplyAsync(int letterId,
        ReplyReviewLetterRequest request, int actorUserId, string? actorName, int actorBranchId,
        CancellationToken ct = default, UserRole role = UserRole.Head, int? actorSectionId = null)
    {
        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(bodyHtml)))
            throw new ArgumentException("نص الرد مطلوب");

        var letter = await _letters.GetByIdAsync(letterId, ct)
            ?? throw new KeyNotFoundException("كتاب المطالعة غير موجود");

        if (letter.BranchId != actorBranchId)
            throw new UnauthorizedAccessException("رد المطالعات مقصور على كتب الفرع نفسه");
        EnsureLetterOwner(role, actorSectionId, await OwnerSectionOfAsync(letter, ct));

        var branchCode = await ResolveBranchCodeAsync(actorBranchId, ct);
        var now = DateTime.UtcNow;
        var replyNumber = await GenerateUniqueNumberAsync(branchCode, now, ct);

        var reply = new ReviewLetterMessage
        {
            ReviewLetterId = letter.Id,
            Kind = ReviewLetterMessage.KindReply,
            BodyHtml = bodyHtml,
            BodyPlainText = HtmlInputSanitizer.ToPlainText(bodyHtml),
            MessageNumber = replyNumber,
            MessageDate = now,
            AuthorId = actorUserId,
            AuthorName = actorName ?? string.Empty,
            AuthorRole = role.ToString().ToLowerInvariant(),
        };
        letter.Messages.Add(reply);

        letter.IsAnswered = true;
        letter.UpdatedAt = now;

        await _tx.RunAsync(async token =>
        {
            // تنبيه المحامي صاحب الكتاب بالرد — ضمن المعاملة نفسها، مع دمج الردود
            // المتتالية غير المقروءة في تنبيه واحد بدل تراكمها.
            await StageReplyAlertAsync(letter, reply, actorUserId, actorName, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "reply_review_letter",
                details: $"رد على كتاب المطالعة رقم {letter.LetterNumber}",
                ct: token);
        }, ct);

        return ToMessageDto(reply);
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
