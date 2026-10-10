using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IReviewLetterService
{
    /// <summary>
    /// قائمة كتب المطالعة بحسب الدور: المحامي كتبه، ورئيس القسم كتب قسمه
    /// (دوائر القسم وبلا دائرة)، ورئيس الشعبة كتب شعبته، والمدير/المشرف كتب
    /// فرع الإدارة المنتقى (لا عرض قبل اختيار الفرع).
    /// </summary>
    Task<PagedResult<ReviewLetterListItemDto>> SearchAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, int page, int perPage,
        string? administrativeBranch, CancellationToken ct = default, int? actorSectionId = null);

    /// <summary>كتاب مطالعة برسائله — بعد التحقق من حق الوصول (نطاق المالك §10).</summary>
    Task<ReviewLetterDto> GetByIdAsync(int id, int actorUserId, UserRole role, int? actorBranchId,
        CancellationToken ct = default, int? actorSectionId = null);

    /// <summary>تسطير كتاب مطالعة (مربوط بملف أو عام) وتوليد رقمه وتاريخه تلقائيًا.</summary>
    Task<ReviewLetterDto> CreateAsync(CreateReviewLetterRequest request, int actorUserId,
        string? actorName, int actorBranchId, CancellationToken ct = default);

    /// <summary>إضافة لاحق إلى كتاب — محامي الكتاب فقط؛ يعيد الكتاب إلى بانتظار الرد.</summary>
    Task<ReviewLetterMessageDto> AddAddendumAsync(int letterId, AddReviewLetterAddendumRequest request,
        int actorUserId, string? actorName, CancellationToken ct = default);

    /// <summary>رد مالك الكتاب على كتاب المطالعة — يولّد رقم الرد وتاريخه ويعلّم الكتاب «تم الرد».</summary>
    Task<ReviewLetterMessageDto> ReplyAsync(int letterId, ReplyReviewLetterRequest request,
        int actorUserId, string? actorName, int actorBranchId, CancellationToken ct = default,
        UserRole role = UserRole.Head, int? actorSectionId = null);

    /// <summary>عدد كتب النطاق بانتظار الرد (جرس المالك الأحمر §10).</summary>
    Task<int> CountPendingForHeadAsync(int branchId, int? ownerSectionId = null, CancellationToken ct = default);

    /// <summary>
    /// عدد كتب المحامي التي فيها ردّ لم يطّلع عليه بعد — شارة بند المطالعات.
    /// </summary>
    Task<int> CountUnseenRepliesForLawyerAsync(int actorUserId, CancellationToken ct = default);

    /// <summary>
    /// تعليم ردود الكتاب كمطّلع عليها من محاميه — يُستدعى عند فتح الكتاب بعد الرد.
    /// </summary>
    Task<bool> MarkRepliesSeenAsync(int letterId, int actorUserId, CancellationToken ct = default);

    /// <summary>
    /// كتب ملف محدد — لمالك الملف ومتابعيه (إحالة/إنابة/استئناف) والمدير/المشرف،
    /// ولرئيس نطاق الملف (قسمه لدوائر القسم وبلا دائرة، وشعبته لدوائر شعبته).
    /// </summary>
    Task<List<ReviewLetterListItemDto>> ListByDocumentAsync(int documentId, int actorUserId,
        UserRole role, int? actorBranchId, CancellationToken ct = default, int? actorSectionId = null);

    /// <summary>أسماء فروع الإدارة المميزة لفلتر المدير/المشرف.</summary>
    Task<List<string>> GetAdministrativeBranchesAsync(CancellationToken ct = default);
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
    private readonly IUserRepository _users;
    private readonly IRepository<Section> _sections;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IAppealRepository _appeals;
    private readonly IDelegationRepository _delegations;
    private readonly IHeadAlertRepository _headAlerts;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public ReviewLetterService(
        IReviewLetterRepository letters,
        IDocumentRepository documents,
        IBranchRepository branches,
        IAppealRepository appeals,
        IDelegationRepository delegations,
        IHeadAlertRepository headAlerts,
        IDbExceptionClassifier dbErrors,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IUserRepository users,
        IRepository<Section> sections,
        IRepository<ExecutionCircuit> circuits)
    {
        _letters = letters;
        _documents = documents;
        _branches = branches;
        _users = users;
        _sections = sections;
        _circuits = circuits;
        _appeals = appeals;
        _delegations = delegations;
        _headAlerts = headAlerts;
        _dbErrors = dbErrors;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    public async Task<PagedResult<ReviewLetterListItemDto>> SearchAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, int page, int perPage,
        string? administrativeBranch, CancellationToken ct = default, int? actorSectionId = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        // المدير/المشرف لا يرى أي كتاب قبل اختيار فرع الإدارة (حجب تام على مستوى الخدمة).
        if (role is UserRole.Manager or UserRole.Admin
            && string.IsNullOrWhiteSpace(administrativeBranch))
        {
            return new PagedResult<ReviewLetterListItemDto>
            {
                Page = page,
                PerPage = perPage,
            };
        }

        var (items, totalCount) = role switch
        {
            UserRole.Lawyer => await _letters.SearchForLawyerAsync(actorUserId, q, page, perPage, ct),
            // نطاق المالك (§10): القسم لدوائر القسم وبلا دائرة، والشعبة لدوائر شعبته.
            UserRole.Head when actorBranchId is not null
                => await _letters.SearchForScopeAsync(actorBranchId.Value, null, q, page, perPage, ct),
            UserRole.Head
                => throw new ArgumentException("رئيس القسم دون فرع لا يمكنه عرض كتب المطالعة"),
            UserRole.SubHead when actorBranchId is not null && actorSectionId is not null
                => await _letters.SearchForScopeAsync(actorBranchId.Value, actorSectionId, q, page, perPage, ct),
            UserRole.SubHead
                => throw new ArgumentException("حسابك بلا شعبة — أعد الدخول"),
            UserRole.Manager or UserRole.Admin
                => await _letters.SearchAllAsync(administrativeBranch, q, page, perPage, ct),
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
        int? actorBranchId, CancellationToken ct = default, int? actorSectionId = null)
    {
        var letter = await _letters.GetByIdWithDetailsAsync(id, ct)
            ?? throw new ArgumentException("كتاب المطالعة غير موجود");

        if (!await CanViewAsync(letter, actorUserId, role, actorBranchId, actorSectionId, ct))
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
        CancellationToken ct = default, UserRole role = UserRole.Head, int? actorSectionId = null)
    {
        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(bodyHtml)))
            throw new ArgumentException("نص الرد مطلوب");

        var letter = await _letters.GetByIdAsync(letterId, ct)
            ?? throw new ArgumentException("كتاب المطالعة غير موجود");

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

    public Task<int> CountPendingForHeadAsync(int branchId, int? ownerSectionId = null, CancellationToken ct = default)
        => _letters.CountPendingForScopeAsync(branchId, ownerSectionId, ct);

    public Task<List<string>> GetAdministrativeBranchesAsync(CancellationToken ct = default)
        => _letters.GetAdministrativeBranchesAsync(ct);

    public async Task<List<ReviewLetterListItemDto>> ListByDocumentAsync(int documentId,
        int actorUserId, UserRole role, int? actorBranchId, CancellationToken ct = default,
        int? actorSectionId = null)
    {
        var document = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new ArgumentException("الملف غير موجود");

        var isOwnerOrSupervisor = role is UserRole.Manager or UserRole.Admin
            || document.CreatedById == actorUserId;

        if (!isOwnerOrSupervisor &&
            !await FollowsDocumentAsync(documentId, actorUserId, ct) &&
            !await InFileScopeAsync(document, role, actorBranchId, actorSectionId, ct))
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
    /// مالك الكتاب (§10): شعبة دائرة ملفه (بلا دائرة → القسم)، وبلا ملف الشعبة
    /// المستلمة (`null` → القسم). الملف المحذوف مصدره يعامَل كبلا دائرة.
    /// </summary>
    private async Task<int?> OwnerSectionOfAsync(ReviewLetter letter, CancellationToken ct)
    {
        if (letter.DocumentId is null)
            return letter.RecipientSectionId;
        var doc = letter.Document ?? await _documents.GetByIdAsync(letter.DocumentId.Value, ct);
        if (doc?.ExecutionCircuitId is null)
            return null;
        var circuit = doc.ExecutionCircuit ?? await _circuits.GetByIdAsync(doc.ExecutionCircuitId.Value, ct);
        return circuit?.SectionId;
    }

    /// <summary>مالك دائرة ملف (§10.1) — يُشتق منه مستلم الكتاب المرتبط تلقائيًا.</summary>
    private async Task<int?> FileOwnerSectionAsync(Document document, CancellationToken ct)
    {
        if (document.ExecutionCircuitId is null)
            return null;
        var circuit = document.ExecutionCircuit
            ?? await _circuits.GetByIdAsync(document.ExecutionCircuitId.Value, ct);
        return circuit?.SectionId;
    }

    /// <summary>
    /// اختيار مستلم الكتاب العام (§10.2 + قرار 28): فارغٌ يعني رئيس القسم،
    /// وغيره شعبة نشطة بفرع الكتاب ولها رئيس مفعّل — وإلا «يجب اختيار المستلم».
    /// </summary>
    private async Task<Section?> ResolveGeneralRecipientSectionAsync(
        int? recipientSectionId, int actorBranchId, CancellationToken ct)
    {
        if (recipientSectionId is null)
            return null;
        var section = await _sections.GetByIdAsync(recipientSectionId.Value, ct);
        var head = section is not null
            ? await _users.FindActiveHeadAsync(UserRole.SubHead, section.BranchId, section.Id, ct)
            : null;
        if (section is null || section.BranchId != actorBranchId || !section.IsActive || head is null)
            throw new ArgumentException("يجب اختيار المستلم — حدّد رئيس القسم أو شعبة نشطة برئيس مفعّل");
        return section;
    }

    /// <summary>
    /// نطاق الملف (§10): القسم لدوائر القسم وبلا دائرة، والشعبة لدوائر شعبته —
    /// بلا تدهور (رمز بلا شعبة مرفوض).
    /// </summary>
    private async Task<bool> InFileScopeAsync(Document document, UserRole role,
        int? actorBranchId, int? actorSectionId, CancellationToken ct)
    {
        // حارس null صريح: مقارنة int?==int? تعدّ null==null صوابًا، فيرى رئيس
        // بلا فرع ملفًا إرثيًا بلا فرع — ثغرة حسابات مشوّهة (مرآة نمط المراسلات).
        if (actorBranchId is null || actorBranchId != document.BranchId)
            return false;
        var ownerSection = await FileOwnerSectionAsync(document, ct);
        return role switch
        {
            UserRole.Head => ownerSection is null,
            UserRole.SubHead => actorSectionId is not null && ownerSection == actorSectionId,
            _ => false,
        };
    }

    /// <summary>
    /// حارس المالك للرد (§10): القسم لكتبه، والشعبة لكتبها — «مقصور على كتب نطاقه».
    /// </summary>
    private static void EnsureLetterOwner(UserRole role, int? actorSectionId, int? ownerSectionId)
    {
        if (role == UserRole.Head)
        {
            if (ownerSectionId.HasValue)
                throw new UnauthorizedAccessException("مقصور على كتب نطاقه");
        }
        else if (role == UserRole.SubHead)
        {
            if (actorSectionId is null)
                throw new UnauthorizedAccessException("حسابك بلا شعبة — أعد الدخول");
            if (ownerSectionId != actorSectionId)
                throw new UnauthorizedAccessException("مقصور على كتب نطاقه");
        }
        else
        {
            throw new UnauthorizedAccessException("رد المطالعات للرؤساء فقط");
        }
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
        int? actorBranchId, int? actorSectionId, CancellationToken ct)
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
                if (letter.BranchId != actorBranchId)
                    return false;
                return await OwnerSectionOfAsync(letter, ct) is null;

            case UserRole.SubHead:
                if (letter.BranchId != actorBranchId || actorSectionId is null)
                    return false;
                return await OwnerSectionOfAsync(letter, ct) == actorSectionId;

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

    private ReviewLetterFileContextDto? FileContextOf(ReviewLetter letter)
    {
        var doc = letter.Document;
        if (doc is null)
            return null;

        var name = string.Join(' ', new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(name))
            name = doc.DocumentType ?? string.Empty;

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        return new ReviewLetterFileContextDto(
            name,
            EffectiveFileIdentity.Number(doc, currentYear),
            doc.FileType,
            EffectiveFileIdentity.Year(doc, currentYear),
            doc.Court);
    }

    private ReviewLetterDto ToDto(ReviewLetter letter)
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
            letter.Branch?.Name,
            letter.CreatedBy?.FullName ?? string.Empty,
            messages.Any(m => m.Kind == ReviewLetterMessage.KindReply && !m.IsSeenByLawyer),
            messages.Select(ToMessageDto).ToList(),
            letter.CreatedAt,
            letter.RecipientSectionId,
            letter.RecipientSection?.Name);
    }

    private ReviewLetterListItemDto ToListItem(ReviewLetter letter, bool revealUnseen)
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
            letter.Branch?.Name,
            letter.UpdatedAt,
            letter.RecipientSectionId,
            letter.RecipientSection?.Name);
    }
}
