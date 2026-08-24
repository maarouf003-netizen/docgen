using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IReviewLetterService
{
    /// <summary>قائمة كتب المطالعة بحسب الدور: المحامي كتبه، ورئيس القسم كتب فرعه، والمدير/المشرف الجميع.</summary>
    Task<PagedResult<ReviewLetterListItemDto>> SearchAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, int page, int perPage,
        CancellationToken ct = default);

    /// <summary>كتاب مطالعة برسائله — بعد التحقق من حق الوصول.</summary>
    Task<ReviewLetterDto> GetByIdAsync(int id, int actorUserId, UserRole role, int? actorBranchId,
        CancellationToken ct = default);

    /// <summary>تسطير كتاب مطالعة (مربوط بملف أو عام) وتوليد رقمه وتاريخه تلقائيًا.</summary>
    Task<ReviewLetterDto> CreateAsync(CreateReviewLetterRequest request, int actorUserId,
        string? actorName, int actorBranchId, CancellationToken ct = default);

    /// <summary>إضافة لاحق إلى كتاب — محامي الكتاب فقط؛ يعيد الكتاب إلى بانتظار الرد.</summary>
    Task<ReviewLetterMessageDto> AddAddendumAsync(int letterId, AddReviewLetterAddendumRequest request,
        int actorUserId, string? actorName, CancellationToken ct = default);

    /// <summary>رد رئيس القسم على كتاب المطالعة — يولّد رقم الرد وتاريخه ويعلّم الكتاب «تم الرد».</summary>
    Task<ReviewLetterMessageDto> ReplyAsync(int letterId, ReplyReviewLetterRequest request,
        int actorUserId, string? actorName, int actorBranchId, CancellationToken ct = default);

    /// <summary>عدد كتب الفرع بانتظار الرد (جرس رئيس القسم الأحمر).</summary>
    Task<int> CountPendingForHeadAsync(int branchId, CancellationToken ct = default);

    /// <summary>
    /// عدد كتب المحامي التي فيها ردّ لم يطّلع عليه بعد — شارة بند المطالعات.
    /// </summary>
    Task<int> CountUnseenRepliesForLawyerAsync(int actorUserId, CancellationToken ct = default);

    /// <summary>
    /// تعليم ردود الكتاب كمطّلع عليها من محاميه — يُستدعى عند فتح الكتاب بعد الرد.
    /// </summary>
    Task<bool> MarkRepliesSeenAsync(int letterId, int actorUserId, CancellationToken ct = default);

    /// <summary>
    /// كتب ملف محدد — لمالك الملف ورئيس قسمه والمدير/المشرف، ولكل محامٍ
    /// أُحيل إليه الملف أو يتابع إنابةً أو استئنافًا عليه.
    /// </summary>
    Task<List<ReviewLetterListItemDto>> ListByDocumentAsync(int documentId, int actorUserId,
        UserRole role, int? actorBranchId, CancellationToken ct = default);
}

/// <summary>
/// كتب المطالعة: مراسلات رسمية بين المحامي ورئيس القسم. النص المرسل غير قابل للتعديل أو الحذف،
/// والتسلسل الزمني للرسائل هو المرجع، والكتابة ضمن معاملات مع سجل التدقيق.
/// </summary>
public sealed class ReviewLetterService : IReviewLetterService
{
    private const int NumberRandomDigits = 4;
    private const int MaxNumberGenerationAttempts = 20;

    private readonly IReviewLetterRepository _letters;
    private readonly IDocumentRepository _documents;
    private readonly IBranchRepository _branches;
    private readonly IAppealRepository _appeals;
    private readonly IDelegationRepository _delegations;
    private readonly IHeadAlertRepository _headAlerts;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public ReviewLetterService(
        IReviewLetterRepository letters,
        IDocumentRepository documents,
        IBranchRepository branches,
        IAppealRepository appeals,
        IDelegationRepository delegations,
        IHeadAlertRepository headAlerts,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _letters = letters;
        _documents = documents;
        _branches = branches;
        _appeals = appeals;
        _delegations = delegations;
        _headAlerts = headAlerts;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    public async Task<PagedResult<ReviewLetterListItemDto>> SearchAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, int page, int perPage,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (items, totalCount) = role switch
        {
            UserRole.Lawyer => await _letters.SearchForLawyerAsync(actorUserId, q, page, perPage, ct),
            UserRole.Head when actorBranchId is not null
                => await _letters.SearchForBranchAsync(actorBranchId.Value, q, page, perPage, ct),
            UserRole.Head
                => throw new ArgumentException("رئيس القسم دون فرع لا يمكنه عرض كتب المطالعة"),
            UserRole.Manager or UserRole.Admin
                => await _letters.SearchAllAsync(q, page, perPage, ct),
            _ => throw new ArgumentException("الدور غير مخوّل لعرض كتب المطالعة"),
        };

        // حالة الإطلاع (هل طالع محامي الكتاب الرد؟) معلومة خاصة بمحامي الكتاب نفسه،
        // فلا تُكشف لرئيس القسم ولا للإدارة حفاظًا على خصوصية المحامي.
        var revealUnseen = role == UserRole.Lawyer;

        return new PagedResult<ReviewLetterListItemDto>
        {
            Items = items.Select(l => ToListItem(l, revealUnseen)).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = totalCount,
        };
    }

    public async Task<ReviewLetterDto> GetByIdAsync(int id, int actorUserId, UserRole role,
        int? actorBranchId, CancellationToken ct = default)
    {
        var letter = await _letters.GetByIdWithDetailsAsync(id, ct)
            ?? throw new ArgumentException("كتاب المطالعة غير موجود");

        if (!await CanViewAsync(letter, actorUserId, role, actorBranchId, ct))
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على كتاب المطالعة هذا");

        return ToDto(letter);
    }

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
        var letterNumber = await GenerateUniqueNumberAsync(branch.Code, now, ct);

        var letter = new ReviewLetter
        {
            BranchId = actorBranchId,
            CreatedById = actorUserId,
            DocumentId = document?.Id,
            LetterNumber = letterNumber,
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
                    MessageNumber = letterNumber,
                    MessageDate = now,
                    AuthorId = actorUserId,
                    AuthorName = actorName ?? string.Empty,
                    AuthorRole = nameof(UserRole.Lawyer).ToLowerInvariant(),
                },
            ],
        };

        var scope = document is null ? "عام" : $"ملف {document.Id}";
        await _tx.RunAsync(async token =>
        {
            await _letters.AddAsync(letter, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_review_letter",
                details: $"سطّر كتاب مطالعة ({scope}) برقم {letter.LetterNumber}",
                ct: token);
        }, ct);

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
            ?? throw new ArgumentException("كتاب المطالعة غير موجود");

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
        CancellationToken ct = default)
    {
        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(bodyHtml)))
            throw new ArgumentException("نص الرد مطلوب");

        var letter = await _letters.GetByIdAsync(letterId, ct)
            ?? throw new ArgumentException("كتاب المطالعة غير موجود");

        if (letter.BranchId != actorBranchId)
            throw new UnauthorizedAccessException("رد رئيس القسم مقصور على كتب فرعه");

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
            AuthorRole = nameof(UserRole.Head).ToLowerInvariant(),
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
    /// تجهيز تنبيه الرد لمحامي الكتاب (TargetType=lawyer): يُحدَّث آخر تنبيه ردٍّ غير مقروء
    /// للكتاب نفسه إن وُجد، وإلا أُنشئ تنبيه جديد برابط مباشر إلى الكتاب.
    /// </summary>
    private async Task StageReplyAlertAsync(ReviewLetter letter, ReviewLetterMessage reply,
        int headUserId, string? headName, CancellationToken token)
    {
        var snippet = reply.BodyPlainText.Length > 80
            ? reply.BodyPlainText[..80] + "…"
            : reply.BodyPlainText;
        var message = $"{headName ?? "رئيس القسم"} ردّ على كتاب مطالعتك رقم {letter.LetterNumber}: {snippet}";

        var existing = await _headAlerts.FindLatestUnseenByReviewLetterAsync(letter.Id, letter.CreatedById, token);
        if (existing is not null)
        {
            existing.Message = message;
            existing.CreatedAt = DateTime.UtcNow;
            return;
        }

        await _headAlerts.AddAsync(new HeadAlert
        {
            BranchId = letter.BranchId,
            CreatedById = headUserId,
            TargetType = HeadAlertTargetType.Lawyer,
            TargetLawyerId = letter.CreatedById,
            DocumentId = letter.DocumentId,
            ReviewLetterId = letter.Id,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            Recipients = [new HeadAlertRecipient { UserId = letter.CreatedById }],
        }, token);
    }

    public Task<int> CountUnseenRepliesForLawyerAsync(int actorUserId, CancellationToken ct = default)
        => _letters.CountUnseenReplyLettersForLawyerAsync(actorUserId, ct);

    public async Task<bool> MarkRepliesSeenAsync(int letterId, int actorUserId,
        CancellationToken ct = default)
    {
        var letter = await _letters.GetTrackedWithMessagesAsync(letterId, ct)
            ?? throw new ArgumentException("كتاب المطالعة غير موجود");

        if (letter.CreatedById != actorUserId)
            throw new UnauthorizedAccessException("تعليم الإطلاع متاح لمحامي الكتاب نفسه");

        var changed = false;
        foreach (var m in letter.Messages.Where(
                     m => m.Kind == ReviewLetterMessage.KindReply && !m.IsSeenByLawyer))
        {
            m.IsSeenByLawyer = true;
            changed = true;
        }

        if (!changed)
            return true;

        await _tx.RunAsync(async token => { await _uow.SaveChangesAsync(token); }, ct);
        return true;
    }

    public Task<int> CountPendingForHeadAsync(int branchId, CancellationToken ct = default)
        => _letters.CountPendingForBranchAsync(branchId, ct);

    public async Task<List<ReviewLetterListItemDto>> ListByDocumentAsync(int documentId,
        int actorUserId, UserRole role, int? actorBranchId, CancellationToken ct = default)
    {
        var document = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new ArgumentException("الملف غير موجود");

        var isOwnerOrSupervisor = role is UserRole.Manager or UserRole.Admin
            || (role == UserRole.Head && actorBranchId == document.BranchId)
            || document.CreatedById == actorUserId;

        if (!isOwnerOrSupervisor &&
            !await FollowsDocumentAsync(documentId, actorUserId, ct))
        {
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على كتب هذا الملف");
        }

        var letters = await _letters.ListByDocumentAsync(documentId, ct);
        return letters
            .Select(l => ToListItem(
                l,
                revealUnseen: role == UserRole.Lawyer && l.CreatedById == actorUserId))
            .ToList();
    }

    /// <summary>
    /// متابعة الملف: إسناد متابعة استئناف عليه، أو إنابة مصدره/منابُه هذا الملف
    /// ومسندة إلى المحامي نفسه.
    /// </summary>
    private async Task<bool> FollowsDocumentAsync(int documentId, int userId, CancellationToken ct)
    {
        if (await _appeals.IsAssignedFollowerAsync(documentId, userId, ct))
            return true;

        var sourceDelegations = await _delegations.ListBySourceAsync(documentId, ct);
        if (sourceDelegations.Any(d => d.AssignedLawyerId == userId))
            return true;

        var targetDelegation = await _delegations.FindByTargetAsync(documentId, ct);
        return targetDelegation is { AssignedLawyerId: not null } target
            && target.AssignedLawyerId == userId;
    }

    private async Task<bool> CanViewAsync(ReviewLetter letter, int actorUserId, UserRole role,
        int? actorBranchId, CancellationToken ct)
    {
        switch (role)
        {
            case UserRole.Lawyer:
                // محامي الكتاب، أو أي محامٍ يتابع الملف المربوط (إحالة/إنابة/استئناف).
                if (letter.CreatedById == actorUserId)
                    return true;
                return letter.DocumentId is not null
                    && await FollowsDocumentAsync(letter.DocumentId.Value, actorUserId, ct);

            case UserRole.Head:
                return letter.BranchId == actorBranchId;

            case UserRole.Manager or UserRole.Admin:
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// الرقم بصيغة {رمز الفرع}-{السنة}-{عشوائي 4 خانات} مع ضمان التفرّد بإعادة المحاولة.
    /// </summary>
    private async Task<string> GenerateUniqueNumberAsync(string branchCode, DateTime at,
        CancellationToken ct)
    {
        var prefix = NormalizePrefix(branchCode);
        var year = at.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);

        for (var attempt = 0; attempt < MaxNumberGenerationAttempts; attempt++)
        {
            var random = Random.Shared.NextInt64(0, 10_000)
                .ToString(System.Globalization.CultureInfo.InvariantCulture)
                .PadLeft(NumberRandomDigits, '0');
            var candidate = $"{prefix}-{year}-{random}";
            if (!await _letters.NumberExistsAsync(candidate, ct))
                return candidate;
        }

        throw new InvalidOperationException("تعذر توليد رقم فريد لكتاب المطالعة، حاول مجدداً");
    }

    private static string NormalizePrefix(string code)
    {
        var trimmed = code?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return "ML";
        var builder = new System.Text.StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
            builder.Append(char.IsWhiteSpace(ch) ? '-' : ch);
        return builder.ToString().ToUpperInvariant();
    }

    private async Task<string> ResolveBranchCodeAsync(int branchId, CancellationToken ct)
    {
        var branch = await _branches.GetByIdAsync(branchId, ct)
            ?? throw new ArgumentException("الفرع غير موجود");
        return branch.Code;
    }

    private static ReviewLetterMessageDto ToMessageDto(ReviewLetterMessage m) => new(
        m.Id,
        m.Kind,
        m.BodyHtml,
        m.MessageNumber,
        m.MessageDate,
        m.AuthorId,
        m.AuthorName,
        m.AuthorRole);

    private static ReviewLetterFileContextDto? FileContextOf(ReviewLetter letter)
    {
        var doc = letter.Document;
        if (doc is null)
            return null;

        var name = string.Join(' ', new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(name))
            name = doc.DocumentType ?? string.Empty;

        return new ReviewLetterFileContextDto(name, doc.FileNumber, doc.FileType, doc.FileYear, doc.Court);
    }

    private static ReviewLetterDto ToDto(ReviewLetter letter)
    {
        var messages = letter.Messages.OrderBy(m => m.Id).ToList();
        return new ReviewLetterDto(
            letter.Id,
            letter.LetterNumber,
            letter.LetterDate,
            letter.IsAnswered,
            letter.DocumentId,
            FileContextOf(letter),
            letter.BranchId,
            letter.CreatedBy?.FullName ?? string.Empty,
            messages.Any(m => m.Kind == ReviewLetterMessage.KindReply && !m.IsSeenByLawyer),
            messages.Select(ToMessageDto).ToList(),
            letter.CreatedAt);
    }

    private static ReviewLetterListItemDto ToListItem(ReviewLetter letter, bool revealUnseen)
    {
        var messages = letter.Messages.OrderBy(m => m.Id).ToList();
        var snippet = messages.FirstOrDefault(m => m.Kind == ReviewLetterMessage.KindLetter)?.BodyPlainText
            ?? messages.FirstOrDefault()?.BodyPlainText
            ?? string.Empty;
        var lastKind = messages.Count > 0 ? messages[^1].Kind : ReviewLetterMessage.KindLetter;
        var hasUnseenReply = revealUnseen
            && messages.Any(m => m.Kind == ReviewLetterMessage.KindReply && !m.IsSeenByLawyer);

        return new ReviewLetterListItemDto(
            letter.Id,
            letter.LetterNumber,
            letter.LetterDate,
            letter.IsAnswered,
            letter.DocumentId,
            FileContextOf(letter),
            letter.CreatedBy?.FullName ?? string.Empty,
            snippet.Length > 160 ? snippet[..160] + "…" : snippet,
            lastKind,
            hasUnseenReply,
            messages.Count,
            letter.UpdatedAt);
    }
}
