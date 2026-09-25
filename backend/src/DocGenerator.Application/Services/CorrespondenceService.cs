using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface ICorrespondenceService
{
    /// <summary>
    /// قائمة المراسلات بحسب الدور: الطرف (منشئ/مستلم) مراسلاته، ورئيس القسم مراسلات
    /// محافظته (فرعه + العامة من مندوبيها)، والمدير/المشرف بمحافظة منتقاة إجباريًا.
    /// </summary>
    Task<PagedResult<CorrespondenceListItemDto>> SearchAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, string? governorate,
        string? importance, int page, int perPage, CancellationToken ct = default);

    /// <summary>مراسلة برسائلها وتوثيق مشاهداتها — بعد التحقق من حق الوصول.</summary>
    Task<CorrespondenceDto> GetByIdAsync(int id, int actorUserId, UserRole role,
        int? actorBranchId, CancellationToken ct = default);

    /// <summary>تسطير مراسلة (مربوطة بملف أو عامة) لطرف معيَّن بالاسم، برقم وتاريخ تلقائيين.</summary>
    Task<CorrespondenceDto> CreateAsync(CreateCorrespondenceRequest request, int actorUserId,
        string? actorName, UserRole role, int? actorBranchId, CancellationToken ct = default);

    /// <summary>إضافة لاحق إلى مراسلة — منشئ المراسلة نفسه فقط.</summary>
    Task<CorrespondenceMessageDto> AddAddendumAsync(int correspondenceId,
        AddCorrespondenceAddendumRequest request, int actorUserId, string? actorName,
        CancellationToken ct = default);

    /// <summary>رد الطرف المستلم على المراسلة — الطرف المستلم فقط.</summary>
    Task<CorrespondenceMessageDto> ReplyAsync(int correspondenceId,
        ReplyCorrespondenceRequest request, int actorUserId, string? actorName,
        CancellationToken ct = default);

    /// <summary>
    /// تأكيد المشاهدة الصريح (زر «تمت المشاهدة») — يسجّل (من؟ متى؟) مرة واحدة؛
    /// التكرار يُبقي أول توثيق ولا يجدّده.
    /// </summary>
    Task<CorrespondenceReceiptDto> MarkSeenAsync(int correspondenceId, int actorUserId,
        string? actorName, UserRole role, int? actorBranchId, CancellationToken ct = default);

    /// <summary>عدد مراسلات المستخدم العاجلة (كمستلم) بلا تأكيد مشاهدة — عدّاد الجرس.</summary>
    Task<int> CountUrgentUnseenAsync(int actorUserId, CancellationToken ct = default);

    /// <summary>
    /// مراسلات ملف محدد: لمالك الملف ورئيس قسمه والمدير/المشرف ومتابعيه (إحالة/إنابة/استئناف)،
    /// ولمندوب الجهة المراسلات التي هو طرف فيها على هذا الملف.
    /// </summary>
    Task<List<CorrespondenceListItemDto>> ListByDocumentAsync(int documentId, int actorUserId,
        UserRole role, int? actorBranchId, CancellationToken ct = default);

    /// <summary>المحافظات المميزة لفلتر المدير/المشرف.</summary>
    Task<List<string>> GetGovernoratesAsync(CancellationToken ct = default);

    /// <summary>
    /// مرشحو الاستلام بالاسم — محامٍ/رئيس قسم/مندوب جهة نشط، بلا المنشئ نفسه.
    /// مع documentId تُقيَّد الأهلية بنوع المراسلة: مندوب الجهة ← محامو الملف (مالكه
    /// ومتابعوه) حصرًا؛ المحامي/رئيس القسم ← مناديب نطاق الملف فقط. عامة بلا ملف = بحث حر.
    /// </summary>
    Task<List<CorrespondenceTargetDto>> SearchTargetsAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, int? documentId,
        CancellationToken ct = default);
}

/// <summary>
/// المراسلات: تواصل رسمي ثنائي بين مندوب الجهة العامة والمحامي/رئيس القسم (أو بينهما
/// لمندوبٍ معيَّن بالاسم). النص المرسل غير قابل للتعديل أو الحذف، والتسلسل الزمني
/// للرسائل مع توثيق المشاهدات هو المرجع، والكتابة ضمن معاملات مع سجل التدقيق.
/// كتابة المندوب تمر حصرًا عبر مسارات البوابة المخصصة (لا تُضعِف حارس العزل البنيوي).
/// </summary>
public sealed class CorrespondenceService : ICorrespondenceService
{
    private const int NumberRandomDigits = 4;
    private const int MaxNumberGenerationAttempts = 20;
    private const int TargetsLimit = 20;

    private readonly ICorrespondenceRepository _letters;
    private readonly IDocumentRepository _documents;
    private readonly IBranchRepository _branches;
    private readonly IUserRepository _users;
    private readonly IAppealRepository _appeals;
    private readonly IDelegationRepository _delegations;
    private readonly IPortalRepository _portal;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _errors;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public CorrespondenceService(
        ICorrespondenceRepository letters,
        IDocumentRepository documents,
        IBranchRepository branches,
        IUserRepository users,
        IAppealRepository appeals,
        IDelegationRepository delegations,
        IPortalRepository portal,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier errors,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _letters = letters;
        _documents = documents;
        _branches = branches;
        _users = users;
        _appeals = appeals;
        _delegations = delegations;
        _portal = portal;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _errors = errors;
        _clock = clock;
        _timeZone = timeZone;
    }

    public async Task<PagedResult<CorrespondenceListItemDto>> SearchAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, string? governorate,
        string? importance, int page, int perPage, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);
        var importanceFilter = NormalizeImportanceFilter(importance);

        // المدير/المشرف لا يرى أي مراسلة قبل اختيار المحافظة (حجب تام على مستوى الخدمة).
        if (role is UserRole.Manager or UserRole.Admin
            && string.IsNullOrWhiteSpace(governorate))
        {
            return new PagedResult<CorrespondenceListItemDto>
            {
                Page = page,
                PerPage = perPage,
            };
        }

        var (items, totalCount) = role switch
        {
            UserRole.Lawyer or UserRole.EntityManager
                => await _letters.SearchForPartyAsync(actorUserId, q, importanceFilter, page, perPage, ct),
            UserRole.Head when actorBranchId is not null
                => await _letters.SearchForBranchAsync(
                    actorBranchId.Value, await ResolveHeadGovernorateAsync(actorBranchId.Value, ct),
                    q, importanceFilter, page, perPage, ct),
            UserRole.Head
                => throw new ArgumentException("رئيس القسم دون فرع لا يمكنه عرض المراسلات"),
            UserRole.Manager or UserRole.Admin
                => await _letters.SearchAllAsync(governorate, q, importanceFilter, page, perPage, ct),
            _ => throw new ArgumentException("الدور غير مخوّل لعرض المراسلات"),
        };

        return new PagedResult<CorrespondenceListItemDto>
        {
            Items = items.Select(l => ToListItem(l, actorUserId)).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = totalCount,
        };
    }

    public async Task<CorrespondenceDto> GetByIdAsync(int id, int actorUserId, UserRole role,
        int? actorBranchId, CancellationToken ct = default)
    {
        var letter = await _letters.GetByIdWithDetailsAsync(id, ct)
            ?? throw new ArgumentException("المراسلة غير موجودة");

        if (!await CanViewAsync(letter, actorUserId, role, actorBranchId, ct))
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على هذه المراسلة");

        return ToDto(letter, actorUserId);
    }

    public async Task<CorrespondenceDto> CreateAsync(CreateCorrespondenceRequest request,
        int actorUserId, string? actorName, UserRole role, int? actorBranchId,
        CancellationToken ct = default)
    {
        if (!CanWrite(role))
            throw new UnauthorizedAccessException("الدور غير مخوّل لتسطير المراسلات");

        if (!Correspondence.IsValidImportance(request.Importance))
            throw new ArgumentException("الأهمية غير صالحة — اختر: عادي أو هام أو عاجل");

        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(bodyHtml)))
            throw new ArgumentException("نص المراسلة مطلوب");

        // الطرف المستلم معيَّن بالاسم: حساب نشط من الأدوار الثلاثة، غير المنشئ نفسه.
        var target = await _users.GetByIdAsync(request.TargetUserId, ct)
            ?? throw new ArgumentException("المستلم غير موجود");
        if (!target.IsActive)
            throw new ArgumentException("حساب المستلم معطّل — اختر مستلمًا نشطًا");
        if (target.Id == actorUserId)
            throw new ArgumentException("لا يمكن تسطير مراسلة لنفسك — حدّد مستلمًا آخر");
        if (target.Role is not (UserRole.Lawyer or UserRole.Head or UserRole.EntityManager))
            throw new ArgumentException("المستلم يجب أن يكون محاميًا أو رئيس قسم أو مندوب جهة");

        Document? document = null;
        if (request.DocumentId is not null)
        {
            document = await _documents.GetByIdAsync(request.DocumentId.Value, ct)
                ?? throw new ArgumentException("الملف غير موجود");

            if (!await MayAttachAsync(document, actorUserId, role, actorBranchId, ct))
                throw new UnauthorizedAccessException("لا تملك صلاحية تسطير مراسلة على هذا الملف");

            // فرض الأهلية بالمصدر نفسه: مستلم مراسلة مربوطة بملف يجب أن يكون ضمن
            // المؤهلين (مندوب←محامو الملف؛ محامٍ/رئيس←مناديب نطاق الملف) — حتى لا
            // تصل مراسلة مرتبطة لطرف خارج نطاقها عبر مُرسِلٍ متعمّد لتجاوز البحث.
            if (!await IsEligibleTargetAsync(document, role, target.Id, ct))
            {
                var eligibilityMessage = role == UserRole.EntityManager
                    ? "المستلم يجب أن يكون محامي الملف نفسه أو أحد متابعيه لهذه المراسلة"
                    : "المستلم يجب أن يكون مندوب جهة ضمن نطاق هذا الملف";
                throw new ArgumentException(eligibilityMessage);
            }
        }

        // الفرع/المحافظة/بادئة الرقم: للمرتبطة بملف من فرع الملف، وللعامة من فرع
        // المنشئ، وللعامة من مندوب (بلا فرع) من محافظة جهته.
        var (branchId, governorate, prefix) = await ResolveScopeAsync(
            document, role, actorUserId, actorBranchId, ct);

        var now = DateTime.UtcNow;
        var number = await GenerateUniqueNumberAsync(prefix, now, ct);

        var letter = new Correspondence
        {
            BranchId = branchId,
            Governorate = governorate,
            CreatedById = actorUserId,
            TargetUserId = target.Id,
            DocumentId = document?.Id,
            CorrespondenceNumber = number,
            CorrespondenceDate = now,
            Importance = request.Importance,
            CreatedAt = now,
            UpdatedAt = now,
            Messages =
            [
                new CorrespondenceMessage
                {
                    Kind = CorrespondenceMessage.KindLetter,
                    BodyHtml = bodyHtml,
                    BodyPlainText = HtmlInputSanitizer.ToPlainText(bodyHtml),
                    MessageNumber = number,
                    MessageDate = now,
                    AuthorId = actorUserId,
                    AuthorName = actorName ?? string.Empty,
                    AuthorRole = role.ToString().ToLowerInvariant(),
                },
            ],
        };

        var scope = document is null ? "عامة" : $"ملف {document.Id}";
        await _tx.RunAsync(async token =>
        {
            await _letters.AddAsync(letter, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_correspondence",
                details: $"سطّر مراسلة ({scope}) برقم {letter.CorrespondenceNumber} إلى {target.FullName}",
                ct: token);
        }, ct);

        var stored = await _letters.GetByIdWithDetailsAsync(letter.Id, ct) ?? letter;
        return ToDto(stored, actorUserId);
    }

    public async Task<CorrespondenceMessageDto> AddAddendumAsync(int correspondenceId,
        AddCorrespondenceAddendumRequest request, int actorUserId, string? actorName,
        CancellationToken ct = default)
    {
        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(bodyHtml)))
            throw new ArgumentException("نص اللاحق مطلوب");

        var letter = await _letters.GetTrackedWithDetailsAsync(correspondenceId, ct)
            ?? throw new ArgumentException("المراسلة غير موجودة");

        // اللاحق من المنشئ نفسه حصرًا (أحد الأطراف) — كتابة المندوب تصل عبر مسار البوابة.
        if (letter.CreatedById != actorUserId)
            throw new UnauthorizedAccessException("اللاحق يُضاف من منشئ المراسلة نفسه");

        var prefix = await ResolvePrefixAsync(letter, ct);
        var now = DateTime.UtcNow;
        var addendum = new CorrespondenceMessage
        {
            CorrespondenceId = letter.Id,
            Kind = CorrespondenceMessage.KindAddendum,
            BodyHtml = bodyHtml,
            BodyPlainText = HtmlInputSanitizer.ToPlainText(bodyHtml),
            MessageNumber = await GenerateUniqueNumberAsync(prefix, now, ct),
            MessageDate = now,
            AuthorId = actorUserId,
            AuthorName = actorName ?? string.Empty,
            AuthorRole = (await _users.GetByIdAsync(actorUserId, ct))?.Role.ToString().ToLowerInvariant()
                ?? "lawyer",
        };
        letter.Messages.Add(addendum);
        letter.UpdatedAt = now;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "add_correspondence_addendum",
                details: $"أضاف لاحقًا إلى المراسلة رقم {letter.CorrespondenceNumber}",
                ct: token);
        }, ct);

        return ToMessageDto(addendum);
    }

    public async Task<CorrespondenceMessageDto> ReplyAsync(int correspondenceId,
        ReplyCorrespondenceRequest request, int actorUserId, string? actorName,
        CancellationToken ct = default)
    {
        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(bodyHtml)))
            throw new ArgumentException("نص الرد مطلوب");

        var letter = await _letters.GetTrackedWithDetailsAsync(correspondenceId, ct)
            ?? throw new ArgumentException("المراسلة غير موجودة");

        // الرد من الطرف المستلم المعيَّن حصرًا (أحد الأطراف) — لا رد لرئيس غير طرف.
        if (letter.TargetUserId != actorUserId)
            throw new UnauthorizedAccessException("الرد متاح للطرف المستلم المعيَّن فقط");

        var prefix = await ResolvePrefixAsync(letter, ct);
        var now = DateTime.UtcNow;
        var reply = new CorrespondenceMessage
        {
            CorrespondenceId = letter.Id,
            Kind = CorrespondenceMessage.KindReply,
            BodyHtml = bodyHtml,
            BodyPlainText = HtmlInputSanitizer.ToPlainText(bodyHtml),
            MessageNumber = await GenerateUniqueNumberAsync(prefix, now, ct),
            MessageDate = now,
            AuthorId = actorUserId,
            AuthorName = actorName ?? string.Empty,
            AuthorRole = (await _users.GetByIdAsync(actorUserId, ct))?.Role.ToString().ToLowerInvariant()
                ?? "entitymanager",
        };
        letter.Messages.Add(reply);
        letter.UpdatedAt = now;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "reply_correspondence",
                details: $"رد على المراسلة رقم {letter.CorrespondenceNumber}",
                ct: token);
        }, ct);

        return ToMessageDto(reply);
    }

    public async Task<CorrespondenceReceiptDto> MarkSeenAsync(int correspondenceId,
        int actorUserId, string? actorName, UserRole role, int? actorBranchId,
        CancellationToken ct = default)
    {
        var letter = await _letters.GetTrackedWithDetailsAsync(correspondenceId, ct)
            ?? throw new ArgumentException("المراسلة غير موجودة");

        if (!await CanViewAsync(letter, actorUserId, role, actorBranchId, ct))
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على هذه المراسلة");

        var existing = letter.Receipts.FirstOrDefault(r => r.UserId == actorUserId);
        if (existing is not null)
            return new CorrespondenceReceiptDto(existing.UserId, existing.UserName, existing.SeenAt);

        var receipt = new CorrespondenceReceipt
        {
            CorrespondenceId = letter.Id,
            UserId = actorUserId,
            UserName = actorName ?? string.Empty,
            SeenAt = DateTime.UtcNow,
        };
        letter.Receipts.Add(receipt);

        try
        {
            await _tx.RunAsync(async token =>
            {
                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(actorName, "mark_correspondence_seen",
                    details: $"أكّد مشاهدة المراسلة رقم {letter.CorrespondenceNumber}",
                    ct: token);
            }, ct);
        }
        catch (Exception ex) when (_errors.IsUniqueViolation(ex))
        {
            // سباق تأكيد متزامن (نقر مزدوج/تبويبان): القيد الفريد منع التكرار —
            // يُرجَع التوثيق الفائز بدل خطأ 500، فأول تأكيد يبقى المرجع.
            var winner = await _letters.FindReceiptAsync(letter.Id, actorUserId, ct);
            if (winner is null)
                throw;
            return new CorrespondenceReceiptDto(winner.UserId, winner.UserName, winner.SeenAt);
        }

        return new CorrespondenceReceiptDto(receipt.UserId, receipt.UserName, receipt.SeenAt);
    }

    public Task<int> CountUrgentUnseenAsync(int actorUserId, CancellationToken ct = default)
        => _letters.CountUrgentUnseenForTargetAsync(actorUserId, ct);

    public Task<List<string>> GetGovernoratesAsync(CancellationToken ct = default)
        => _letters.GetGovernoratesAsync(ct);

    public async Task<List<CorrespondenceTargetDto>> SearchTargetsAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, int? documentId,
        CancellationToken ct = default)
    {
        if (!CanWrite(role))
            throw new UnauthorizedAccessException("الدور غير مخوّل لتسطير المراسلات");

        Document? document = null;
        if (documentId is not null)
        {
            document = await _documents.GetByIdAsync(documentId.Value, ct)
                ?? throw new ArgumentException("الملف غير موجود");

            // بوابة البحث = صلاحية التسطير على هذا الملف (نفس فحص الإنشاء) فلا نكشف
            // مرشحين لمن لا يملك الكتابة عليه مهما كان دوره.
            if (!await MayAttachAsync(document, actorUserId, role, actorBranchId, ct))
                throw new UnauthorizedAccessException("لا تملك صلاحية تسطير مراسلة على هذا الملف");
        }

        return await ResolveEligibleTargetsAsync(actorUserId, role, q, document, ct);
    }

    /// <summary>
    /// مصدر الحقيقة الوحيد لأهلية المستلمين: عامة بلا ملف = البحث الحر المعتاد؛
    /// مربوطة بملف = مندوب←محامو الملف، محامٍ/رئيس←مناديب نطاق الملف.
    /// يُستخدم من البحث والإنشاء معًا فلا يمكن أن يُترشح مستلمٌ ثم يُرفض إنشاؤه أو العكس.
    /// </summary>
    private async Task<List<CorrespondenceTargetDto>> ResolveEligibleTargetsAsync(
        int actorUserId, UserRole role, string? q, Document? document, CancellationToken ct)
    {
        if (document is null)
        {
            var users = await _users.SearchCorrespondenceTargetsAsync(actorUserId, q, TargetsLimit, ct);
            return users.Select(ToTargetDto).ToList();
        }

        if (role == UserRole.EntityManager)
        {
            var lawyers = await _users.SearchDocumentLawyerTargetsAsync(
                document.Id, document.CreatedById, actorUserId, q, TargetsLimit, ct);
            return lawyers.Select(ToTargetDto).ToList();
        }

        // محامٍ/رئيس قسم: مناديب نطاق الملف فقط — لا رؤساء ولا محامين آخرين.
        var scope = await _portal.GetDocumentScopeKeysAsync(document.Id, ct);
        var delegates = await _users.SearchScopeDelegateTargetsAsync(
            scope.EntryIds, scope.GroupIds, actorUserId, q, TargetsLimit, ct);
        return delegates.Select(ToTargetDto).ToList();
    }

    /// <summary>هل المستلم المؤهل فعلًا لهذه المراسلة؟ (العامة دائمًا نعم؛ المربوطة حسب الدور).</summary>
    private async Task<bool> IsEligibleTargetAsync(
        Document document, UserRole role, int targetUserId, CancellationToken ct)
    {
        if (role == UserRole.EntityManager)
            return await _users.IsDocumentLawyerTargetAsync(
                document.Id, document.CreatedById, targetUserId, ct);

        var scope = await _portal.GetDocumentScopeKeysAsync(document.Id, ct);
        return await _users.IsScopeDelegateTargetAsync(
            scope.EntryIds, scope.GroupIds, targetUserId, ct);
    }

    private static CorrespondenceTargetDto ToTargetDto(User u) => new(
        u.Id,
        u.FullName,
        u.Role.ToString().ToLowerInvariant(),
        u.Branch?.Name,
        u.Branch?.Governorate ?? u.PortalEntry?.Governorate);

    public async Task<List<CorrespondenceListItemDto>> ListByDocumentAsync(int documentId,
        int actorUserId, UserRole role, int? actorBranchId, CancellationToken ct = default)
    {
        var document = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new ArgumentException("الملف غير موجود");

        if (!await MayListDocumentAsync(document, actorUserId, role, actorBranchId, ct))
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على مراسلات هذا الملف");

        var letters = await _letters.ListByDocumentAsync(documentId, ct);

        // مندوب الجهة يرى من مراسلات الملف ما هو طرف فيه فقط — حتى داخل النطاق.
        if (role == UserRole.EntityManager)
            letters = letters
                .Where(l => l.CreatedById == actorUserId || l.TargetUserId == actorUserId)
                .ToList();

        // رئيس القسم يرى من مراسلات الملف ما يقع في فرعه أو محافظته فقط —
        // يغلق تسريبًا عبر ملف قديم بلا فرع يحمل مراسلة فرع آخر.
        if (role == UserRole.Head && actorBranchId is not null)
        {
            var headGovernorate = await ResolveHeadGovernorateAsync(actorBranchId.Value, ct);
            letters = letters
                .Where(l => l.BranchId == actorBranchId
                    || (l.BranchId is null && l.Governorate == headGovernorate))
                .ToList();
        }

        return letters.Select(l => ToListItem(l, actorUserId)).ToList();
    }

    private async Task<bool> MayAttachAsync(Document document, int actorUserId, UserRole role,
        int? actorBranchId, CancellationToken ct)
    {
        switch (role)
        {
            case UserRole.Lawyer:
                return document.CreatedById == actorUserId
                    || await FollowsDocumentAsync(document.Id, actorUserId, ct);
            case UserRole.Head:
                // حارس null صريح: مقارنة int?==int? المباشرة تعدّ null==null صوابًا،
                // فيرى رئيس بلا فرع ملفًا بلا فرع — ثغرة حسابات مشوّهة.
                // الملف القديم بلا فرع متاح لرئيس أي فرع (مراسلاته تُرشَّح لاحقًا
                // لفرعه/محافظته في ListByDocumentAsync، ورقمها من فرع المنشئ).
                return actorBranchId is not null
                    && (actorBranchId == document.BranchId || document.BranchId is null);
            case UserRole.EntityManager:
                return await IsDocumentInDelegateScopeAsync(document.Id, actorUserId, ct);
            default:
                return false;
        }
    }

    private async Task<bool> MayListDocumentAsync(Document document, int actorUserId,
        UserRole role, int? actorBranchId, CancellationToken ct)
    {
        switch (role)
        {
            case UserRole.Manager or UserRole.Admin:
                return true;
            case UserRole.Head:
                return actorBranchId is not null
                    && (actorBranchId == document.BranchId || document.BranchId is null);
            case UserRole.Lawyer:
                return document.CreatedById == actorUserId
                    || await FollowsDocumentAsync(document.Id, actorUserId, ct);
            case UserRole.EntityManager:
                return await IsDocumentInDelegateScopeAsync(document.Id, actorUserId, ct);
            default:
                return false;
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

    private async Task<bool> CanViewAsync(Correspondence letter, int actorUserId, UserRole role,
        int? actorBranchId, CancellationToken ct)
    {
        // الطرفان (المنشئ والمستلم المعيَّن) يريان دائمًا.
        if (letter.CreatedById == actorUserId || letter.TargetUserId == actorUserId)
            return true;

        switch (role)
        {
            case UserRole.Head when actorBranchId is not null:
                // رئيس القسم يرى مراسلات محافظته: فرعه + العامة من مندوبيها.
                return string.Equals(
                    letter.Governorate,
                    await ResolveHeadGovernorateAsync(actorBranchId.Value, ct),
                    StringComparison.Ordinal);
            case UserRole.Manager or UserRole.Admin:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// الفرع/المحافظة/بادئة الرقم عند التسطير: للمرتبطة بملف من فرع الملف،
    /// وللعامة من فرع المنشئ، وللعامة من مندوب (بلا فرع) من محافظة جهته.
    /// </summary>
    private async Task<(int? BranchId, string Governorate, string Prefix)> ResolveScopeAsync(
        Document? document, UserRole role, int actorUserId, int? actorBranchId,
        CancellationToken ct)
    {
        if (document is not null)
        {
            // الملفات القديمة بلا فرع: يرتد المحامي/الرئيس إلى فرعه (لتظل المراسلة
            // ضمن محافظته ورقمه)، أما المندوب فلا فرع له — يُرفض بوضوح.
            if (document.BranchId is null)
            {
                if (role is not (UserRole.Lawyer or UserRole.Head) || actorBranchId is null)
                    throw new ArgumentException("فرع الملف غير موجود");
                var actorBranch = await _branches.GetByIdAsync(actorBranchId.Value, ct)
                    ?? throw new ArgumentException("الفرع غير موجود");
                if (string.IsNullOrWhiteSpace(actorBranch.Governorate))
                    throw new ArgumentException("تعذّر تحديد محافظة الفرع");
                return (actorBranch.Id, actorBranch.Governorate.Trim(), actorBranch.Code);
            }
            var branch = await _branches.GetByIdAsync(document.BranchId.Value, ct)
                ?? throw new ArgumentException("فرع الملف غير موجود");
            if (string.IsNullOrWhiteSpace(branch.Governorate))
                throw new ArgumentException("تعذّر تحديد محافظة الملف");
            return (branch.Id, branch.Governorate.Trim(), branch.Code);
        }

        if (role is UserRole.Lawyer or UserRole.Head)
        {
            if (actorBranchId is null)
                throw new ArgumentException("الحساب دون فرع لا يمكنه تسطير مراسلة عامة");
            var branch = await _branches.GetByIdAsync(actorBranchId.Value, ct)
                ?? throw new ArgumentException("الفرع غير موجود");
            if (string.IsNullOrWhiteSpace(branch.Governorate))
                throw new ArgumentException("تعذّر تحديد محافظة الفرع");
            return (branch.Id, branch.Governorate.Trim(), branch.Code);
        }

        // عامة من مندوب جهة: بلا فرع — المحافظة والبادئة من نطاق جهته.
        var scope = await _portal.ResolveForUserAsync(actorUserId, ct)
            ?? throw new ArgumentException("حساب المندوب بلا نطاق جهة");
        if (scope.Entries.Count == 0)
            throw new ArgumentException("حساب المندوب بلا نطاق نشط");
        // القيود المرجعة من المحلل نشطة حتمًا (يفلتر النهائي/غير المراجَع/النشط)؛
        // الفحص الصريح على IsActive يحمي من أي انحراف مستقبلي في العقد بدل
        // الانهيار، وأول قيد هو المرجع الحتمي نفسه في المسار الحالي.
        // (القيود ValueTuple فلا ?. عليها — الافتراضي IsActive=false فيسقط للاحتياط.)
        var activeEntry = scope.Entries.FirstOrDefault(e => e.IsActive);
        var governorate = activeEntry.IsActive
            ? activeEntry.Governorate
            : scope.Entries[0].Governorate;
        if (string.IsNullOrWhiteSpace(governorate))
            throw new ArgumentException("حساب المندوب بلا نطاق نشط");
        return (null, governorate.Trim(), governorate.Trim());
    }

    private async Task<string> ResolveHeadGovernorateAsync(int branchId, CancellationToken ct)
    {
        var branch = await _branches.GetByIdAsync(branchId, ct)
            ?? throw new ArgumentException("الفرع غير موجود");
        return branch.Governorate?.Trim() ?? string.Empty;
    }

    private async Task<bool> IsDocumentInDelegateScopeAsync(int documentId, int userId,
        CancellationToken ct)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        if (scope is null || scope.EntryIds.Count == 0)
            return false;
        return await _portal.IsDocumentInScopeAsync(documentId, scope.EntryIds, ct);
    }

    private async Task<string> ResolvePrefixAsync(Correspondence letter, CancellationToken ct)
    {
        if (letter.BranchId is not null)
        {
            var branch = await _branches.GetByIdAsync(letter.BranchId.Value, ct)
                ?? throw new ArgumentException("الفرع غير موجود");
            return branch.Code;
        }
        return letter.Governorate;
    }

    /// <summary>
    /// الرقم بصيغة {الرمز}-{السنة}-{عشوائي 4 خانات} مع ضمان التفرّد بإعادة المحاولة.
    /// </summary>
    private async Task<string> GenerateUniqueNumberAsync(string code, DateTime at,
        CancellationToken ct)
    {
        var prefix = NormalizePrefix(code);
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

        throw new InvalidOperationException("تعذر توليد رقم فريد للمراسلة، حاول مجدداً");
    }

    private static string NormalizePrefix(string code)
    {
        var trimmed = code?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return "CR";
        var builder = new System.Text.StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
            builder.Append(char.IsWhiteSpace(ch) ? '-' : ch);
        return builder.ToString().ToUpperInvariant();
    }

    private static string? NormalizeImportanceFilter(string? importance)
    {
        if (string.IsNullOrWhiteSpace(importance))
            return null;
        var term = importance.Trim().ToLowerInvariant();
        if (!Correspondence.IsValidImportance(term))
            throw new ArgumentException("فلتر الأهمية غير صالح");
        return term;
    }

    /// <summary>
    /// أدوار الكتابة في المراسلات: محامٍ/رئيس قسم/مندوب جهة (تُفرض أيضًا عبر
    /// RolePermissions في المتحكمات؛ هذا الفحص دفاع داخلي للخدمة).
    /// </summary>
    private static bool CanWrite(UserRole role)
        => role is UserRole.Lawyer or UserRole.Head or UserRole.EntityManager;

    private static CorrespondenceMessageDto ToMessageDto(CorrespondenceMessage m) => new(
        m.Id,
        m.Kind,
        m.BodyHtml,
        m.MessageNumber,
        m.MessageDate,
        m.AuthorId,
        m.AuthorName,
        m.AuthorRole);

    private CorrespondenceFileContextDto? FileContextOf(Correspondence letter)
    {
        var doc = letter.Document;
        if (doc is null)
            return null;

        var name = string.Join(' ', new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(name))
            name = doc.DocumentType ?? string.Empty;

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        return new CorrespondenceFileContextDto(
            name,
            EffectiveFileIdentity.Number(doc, currentYear),
            doc.FileType,
            EffectiveFileIdentity.Year(doc, currentYear),
            doc.Court);
    }

    private CorrespondenceDto ToDto(Correspondence letter, int viewerUserId)
    {
        var messages = letter.Messages.OrderBy(m => m.Id).ToList();
        var receipts = letter.Receipts.OrderBy(r => r.SeenAt).ToList();
        return new CorrespondenceDto(
            letter.Id,
            letter.CorrespondenceNumber,
            letter.CorrespondenceDate,
            letter.Importance,
            letter.DocumentId,
            FileContextOf(letter),
            letter.BranchId,
            letter.Governorate,
            letter.Branch?.Name,
            letter.CreatedById,
            letter.CreatedBy?.FullName ?? string.Empty,
            letter.CreatedBy?.Role.ToString().ToLowerInvariant() ?? string.Empty,
            letter.TargetUserId,
            letter.TargetUser?.FullName ?? string.Empty,
            letter.TargetUser?.Role.ToString().ToLowerInvariant() ?? string.Empty,
            receipts.Any(r => r.UserId == viewerUserId),
            messages.Select(ToMessageDto).ToList(),
            receipts.Select(r => new CorrespondenceReceiptDto(r.UserId, r.UserName, r.SeenAt)).ToList(),
            letter.CreatedAt);
    }

    private CorrespondenceListItemDto ToListItem(Correspondence letter, int viewerUserId)
    {
        var messages = letter.Messages.OrderBy(m => m.Id).ToList();
        var snippet = messages.FirstOrDefault(m => m.Kind == CorrespondenceMessage.KindLetter)?.BodyPlainText
            ?? messages.FirstOrDefault()?.BodyPlainText
            ?? string.Empty;
        var lastKind = messages.Count > 0 ? messages[^1].Kind : CorrespondenceMessage.KindLetter;
        var seenByMe = letter.Receipts.Any(r => r.UserId == viewerUserId);
        // شارة الانتباه للمستلم وحده: المنشئ يعرف محتواه، واطّلاع الطرف يُقاس بعدّاد التوثيق.
        var isUrgentUnseen = Correspondence.IsUrgent(letter.Importance)
            && !seenByMe
            && letter.TargetUserId == viewerUserId;

        return new CorrespondenceListItemDto(
            letter.Id,
            letter.CorrespondenceNumber,
            letter.CorrespondenceDate,
            letter.Importance,
            letter.DocumentId,
            FileContextOf(letter),
            letter.CreatedBy?.FullName ?? string.Empty,
            letter.TargetUser?.FullName ?? string.Empty,
            snippet.Length > 160 ? snippet[..160] + "…" : snippet,
            lastKind,
            seenByMe,
            isUrgentUnseen,
            messages.Count,
            letter.Receipts.Count,
            letter.Branch?.Name,
            letter.Governorate,
            letter.UpdatedAt);
    }
}
