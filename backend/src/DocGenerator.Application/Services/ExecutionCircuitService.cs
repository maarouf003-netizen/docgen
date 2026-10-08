using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IExecutionCircuitService
{
    Task<List<ExecutionCircuitDto>> ListMineAsync(int branchId, int? ownerSectionId, CancellationToken ct = default);
    Task<ExecutionCircuitDto> CreateAsync(int branchId, int actorUserId, string? actorName, string name, CancellationToken ct = default);
    Task<ExecutionCircuitDto> RenameAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, string name, long? version, CancellationToken ct = default, int? actorUserId = null);
    Task<ExecutionCircuitDto> SetActiveAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, bool isActive, long? version, CancellationToken ct = default);
    Task DeleteAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, CancellationToken ct = default);
    Task<List<ExecutionCircuitDto>> ListForLawyerAsync(int branchId, int? ownerSectionId, bool limitToOwner, CancellationToken ct = default);
    Task<List<ExecutionCircuitDto>> ListForDelegationAsync(string? governorate, CancellationToken ct = default);
    Task<ReferCircuitFilesResult> ReferFilesAsync(int sourceCircuitId, int branchId, int? ownerSectionId, int actorUserId, string? actorName, ReferCircuitFilesRequest request, CancellationToken ct = default, string? idempotencyKey = null);
    Task<List<PendingRegistrationDto>> MyPendingRegistrationsAsync(int lawyerId, int? circuitId, CancellationToken ct = default);
    Task<int> CompleteRegistrationsAsync(int lawyerId, string? actorName, CompleteRegistrationsRequest request, CancellationToken ct = default);
    Task<List<CircuitStatsDto>> CircuitStatsAsync(int? branchId, int? ownerSectionId, bool fullAccess, CancellationToken ct = default);
    /// <summary>
    /// نقل ملكية دائرة لمالك جديد داخل الفرع نفسه (قسم الفرع أو شعبة فيه —
    /// قرار §2.5): المفتوح والمغلق يتبع الجديد، والإحالات المفتوحة تُعاد توجيه
    /// تنبيهاتها للمالك الجديد (إنشاء بديلة لا تعديل القديمة — §6.6)، والتدقيق
    /// ثابت بأسماء الفاعلين الأصليين. `ForwardState` لا يتغير (استثناء فرعي).
    /// `version` للتفاؤلية كالتسمية والتفعيل.
    /// </summary>
    Task<ExecutionCircuitDto> TransferCircuitAsync(int circuitId, int? targetSectionId, int actorUserId, string? actorName, CancellationToken ct = default, long? version = null);
    /// <summary>
    /// خطاف التنظيف المسمّى DeleteByPendingRegistration (على نمط DeleteByDelegationAsync):
    /// يُصفّى تنبيه المحامي تلقائيًا عند الصفر.
    /// </summary>
    Task<bool> DeleteByPendingRegistrationAsync(int lawyerId, CancellationToken ct = default);

    /// <summary>
    /// مزامنة تنبيهات إعادة القيد بعد نقل ملكية (H2): تُنشأ تنبيهات المالك الجديد
    /// لدوائر معلقاته الحالية (إنشاء عند الغياب فقط — بلا لمس الموجود)، وتُصفَّى تنبيهات
    /// المالك القديم التي لم يعد لها معلقات. تُستدعى بعد نجاح النقل لا داخله.
    /// </summary>
    Task SyncPendingAlertsAfterTransferAsync(int sourceOwnerId, int targetOwnerId, int branchId, int actorUserId, CancellationToken ct = default);
}

/// <summary>
/// سجل دوائر التنفيذ (BQ-004): الدائرة تابعة للفرع/المحافظة — والقفل من سجل يعبئه رئيس القسم.
/// النطاق إجباري خلفيًا من فرع رئيس القسم (لا يُقبل أي نطاق من العميل).
/// كل عمليات الكتابة: فحوصات (وجود/تفعيل/فرع) داخل المعاملة (ضد TOCTOU) + audit.
/// </summary>
public sealed class ExecutionCircuitService : IExecutionCircuitService
{
    public const string PendingAlertPrefix = "أحال لك رئيس القسم ملفات من دائرة ";
    public const string PendingAlertSuffix = " — يرجى تحديث معلوماتها";
    public const string EmptyingReminderBanner = "لا تنسى نقل ملفات هذه الدائرة لمحامي أو محامين اخرين ان كان لذلك مقتضى";

    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<Section> _sections;
    private readonly IDocumentRepository _documents;
    private readonly IDelegationRepository _delegations;
    private readonly IAppealRepository _appeals;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IRepository<DocumentBaseNumber> _baseNumbers;
    private readonly IRepository<HeadSuccession> _successions;
    private readonly IHeadAlertRepository _alerts;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly IIdempotencyStore? _idempotency;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public ExecutionCircuitService(
        IRepository<ExecutionCircuit> circuits,
        IRepository<Branch> branches,
        IRepository<Section> sections,
        IDocumentRepository documents,
        IDelegationRepository delegations,
        IAppealRepository appeals,
        IRepository<DocumentOccurrence> occurrences,
        IRepository<DocumentBaseNumber> baseNumbers,
        IRepository<HeadSuccession> successions,
        IHeadAlertRepository alerts,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IIdempotencyStore? idempotency = null)
    {
        _circuits = circuits;
        _branches = branches;
        _sections = sections;
        _documents = documents;
        _delegations = delegations;
        _appeals = appeals;
        _occurrences = occurrences;
        _baseNumbers = baseNumbers;
        _successions = successions;
        _alerts = alerts;
        _users = users;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _dbErrors = dbErrors;
        _idempotency = idempotency;
        _clock = clock;
        _timeZone = timeZone;
    }

    public static string BuildPendingMessage(string circuitName) =>
        $"{PendingAlertPrefix}{circuitName}{PendingAlertSuffix}";

    private async Task<Branch> RequireBranchWithGovernorateAsync(int branchId, CancellationToken ct)
    {
        var branch = await _branches.GetByIdAsync(branchId, ct)
            ?? throw new ArgumentException("الفرع غير موجود");
        if (string.IsNullOrWhiteSpace(branch.Governorate) || !GovernorateCatalog.IsGovernorate(branch.Governorate!.Trim()))
            throw new ArgumentException("فرعك بلا محافظة معتمدة — راجع الإدارة لتثبيت محافظة الفرع قبل إدارة الدوائر");
        return branch;
    }

    /// <summary>
    /// شعبة المنشئ المالكة للدائرة الجديدة (§8.4 + اتساق §4.4 خدميًا: الشعبة
    /// في الفرع نفسه — لا قيد بين جدولين في المزوّدين). `null` = ملك القسم.
    /// </summary>
    private async Task<Section?> ResolveOwnerSectionAsync(int branchId, int actorUserId, CancellationToken ct)
    {
        var actor = await _users.GetByIdAsync(actorUserId, ct)
            ?? throw new ArgumentException("المنشئ غير موجود");
        if (actor.Role == UserRole.Head)
            return null;
        if (actor.Role != UserRole.SubHead)
            throw new ArgumentException("إنشاء الدوائر مقصور على الرؤساء — الإدارة تنقل فقط");
        if (actor.SectionId is null)
            throw new ArgumentException("حسابك بلا شعبة — راجع الإدارة");
        var section = await _sections.GetByIdAsync(actor.SectionId.Value, ct)
            ?? throw new ArgumentException("شعبتك غير موجودة — راجع الإدارة");
        if (section.BranchId != branchId)
            throw new ArgumentException("شعبتك ليست ضمن هذا الفرع");
        return section;
    }

    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("اسم الدائرة مطلوب");
        var trimmed = name.Trim();
        if (trimmed.Length > 200)
            throw new ArgumentException("اسم الدائرة أطول من 200 حرف");
        return trimmed;
    }

    private async Task<ExecutionCircuit> GetOwnedAsync(int circuitId, int branchId, int? ownerSectionId, CancellationToken ct)
    {
        var circuit = await _circuits.GetByIdAsync(circuitId, ct)
            ?? throw new ArgumentException("الدائرة غير موجودة");
        if (circuit.BranchId != branchId)
            throw new ArgumentException("الدائرة ليست ضمن فرعك");
        // ملكية النطاق (§5.3 — قرار §2.21): رئيس القسم لدوائر القسم (`null`)،
        // ورئيس الشعبة لدوائر شعبته — وغيرها «ليست ضمن نطاقك» (§14).
        if (circuit.SectionId != ownerSectionId)
            throw new ArgumentException("الدائرة ليست ضمن نطاقك");
        return circuit;
    }

    public async Task<List<ExecutionCircuitDto>> ListMineAsync(int branchId, int? ownerSectionId, CancellationToken ct = default)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        var all = await _circuits.ListAsync(ct);
        var circuits = all
            .Where(c => c.BranchId == branchId && c.SectionId == ownerSectionId)
            .OrderBy(c => c.Name)
            .ToList();
        var counts = await _documents.CountByCircuitsAsync(circuits.Select(c => c.Id).ToList(), ct);
        var sectionNames = await SectionNamesAsync(ct);
        return circuits.Select(c => new ExecutionCircuitDto(
            c.Id, c.BranchId, null, c.Name, c.IsActive,
            counts.TryGetValue(c.Id, out var v) ? v.FileCount : 0,
            counts.TryGetValue(c.Id, out var v2) ? v2.PendingCount : 0,
            c.Version, c.SectionId,
            c.SectionId.HasValue ? sectionNames.GetValueOrDefault(c.SectionId.Value) : null)).ToList();
    }

    private async Task<Dictionary<int, string>> SectionNamesAsync(CancellationToken ct)
    {
        var sections = await _sections.ListAsync(ct);
        return sections.ToDictionary(s => s.Id, s => s.Name);
    }

    public async Task<ExecutionCircuitDto> CreateAsync(int branchId, int actorUserId, string? actorName, string name, CancellationToken ct = default)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        var trimmed = NormalizeName(name);
        var norm = ArabicNameNormalizer.Normalize(trimmed);
        if (string.IsNullOrEmpty(norm))
            throw new ArgumentException("اسم الدائرة مطلوب");

        // ملكية المنشئ (§8.4): دائرة رئيس الشعبة تلحق بشعبته تلقائيًا، ودائرة
        // رئيس القسم ملك القسم (`null`)؛ وغير الرؤساء مرفوض (الإدارة تنقل فقط).
        var ownerSection = await ResolveOwnerSectionAsync(branchId, actorUserId, ct);

        var circuit = new ExecutionCircuit
        {
            BranchId = branchId,
            SectionId = ownerSection?.Id,
            Name = trimmed,
            NameNorm = norm,
            IsActive = true,
            CreatedById = actorUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        try
        {
            await _tx.RunAsync(async token =>
            {
                await _circuits.AddAsync(circuit, token);
                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(actorName, "create_circuit", null, null,
                    $"أدخل دائرة تنفيذ «{trimmed}» في فرعه", token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            throw new DocumentConflictException($"الدائرة «{trimmed}» موجودة مسبقًا في فرعك", ex);
        }
        return new ExecutionCircuitDto(circuit.Id, circuit.BranchId, null, circuit.Name, circuit.IsActive, 0, 0, circuit.Version,
            circuit.SectionId, ownerSection?.Name);
    }

    public async Task<ExecutionCircuitDto> RenameAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, string name, long? version, CancellationToken ct = default, int? actorUserId = null)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        var trimmed = NormalizeName(name);
        var norm = ArabicNameNormalizer.Normalize(trimmed);

        ExecutionCircuitDto result = null!;
        try
        {
            await _tx.RunAsync(async token =>
            {
                var circuit = await GetOwnedAsync(circuitId, branchId, ownerSectionId, token);
                if (version is not null && circuit.Version != version.Value)
                    throw new DocumentConflictException("تعارض تزامن — أعد تحميل الدائرة وحاول مجددًا");
                if (circuit.Name != trimmed)
                {
                    var oldName = circuit.Name;
                    circuit.Name = trimmed;
                    circuit.NameNorm = norm;
                    circuit.UpdatedAt = DateTime.UtcNow;
                    circuit.Version++;

                    // تحديث جماعي فوري لكل الملفات في نفس المعاملة + رفع Version يدويًا + إعادة بناء الثلاثية.
                    var now = DateTime.UtcNow;
                    var docs = await _documents.ListByCircuitAsync(circuitId, includeDeleted: false, token);
                    var renamedAudit = new List<(Document Doc, Dictionary<string, string?> Before)>();
                    foreach (var d in docs)
                    {
                        // لقطة «قبل» للتدقيق الحقلي (S5) — قبل أي طفرة.
                        var before = DocumentChangeTracker.Capture(d);
                        d.Court = trimmed;
                        d.CourtNorm = string.IsNullOrWhiteSpace(trimmed) ? null : ArabicNameNormalizer.Normalize(trimmed);
                        d.SearchText = DocumentSearchTextBuilder.Build(d);
                        d.FullData = DocumentSearchTextBuilder.BuildFullData(d);
                        d.UpdatedAt = now;
                        d.Version++;
                        _documents.Update(d);

                        // وقوعّة التسمية على كل ملف متأثر (§5.2) — وإلا مرّت الجماعية بلا أثر ملفي.
                        await _occurrences.AddAsync(new DocumentOccurrence
                        {
                            DocumentId = d.Id,
                            OccurrenceType = OccurrenceTypeCatalog.CircuitRenamed,
                            Source = OccurrenceSourceCatalog.System,
                            EventDate = now,
                            FileNumber = d.FileNumber,
                            FileType = d.FileType,
                            Year = int.TryParse(d.FileYear, out var oy) ? oy : null,
                            FromCircuitName = oldName,
                            ToCircuitName = trimmed,
                            CreatedById = actorUserId ?? circuit.CreatedById,
                            CreatedAt = now,
                            UpdatedAt = now,
                        }, token);
                        renamedAudit.Add((d, before));
                    }
                    // إعادة التسمية تُحدث أيضًا DelegatedCourt/Norm في صفوف DocumentDelegation حيث DelegatedCircuitId = الدائرة.
                    var delegations = await _delegations.ListByDelegatedCircuitAsync(circuitId, token);
                    foreach (var g in delegations)
                    {
                        g.DelegatedCourt = trimmed;
                        g.DelegatedCourtNorm = string.IsNullOrWhiteSpace(trimmed) ? null : ArabicNameNormalizer.Normalize(trimmed);
                        g.UpdatedAt = now;
                        _delegations.Update(g);
                    }
                    // ترحيل نص التنبيهات المدمجة (المفتاح النصي هش — يُرحَّل مع التسمية نفسها
                    // وإلا يتمت معلقات الاسم الجديد بلا تنبيه وبقي القديم شبحًا).
                    // نطاق الفرع في SQL نفسه (C2+S4): الوحدانية الاسمية داخل الفرع فقط،
                    // فاسمان متطابقان في فرعين يتشاركان الرسالة — ولا يُسحب تنبيه
                    // الفرع الآخر أصلًا (لا تصفية لاحقة قابلة للنسيان).
                    var staleAlerts = await _alerts.FindByMessageAndBranchAsync(BuildPendingMessage(oldName), branchId, token);
                    foreach (var a in staleAlerts)
                    {
                        a.Message = BuildPendingMessage(trimmed);
                        _alerts.Update(a);
                    }
                    _circuits.Update(circuit);
                    await _uow.SaveChangesAsync(token);
                    // تدقيق حقلي لكل ملف (S5) — داخل المعاملة بعد الحفظ الرئيسي.
                    foreach (var (doc, before) in renamedAudit)
                        await LogCircuitDocumentChangeAsync(doc, actorName, "rename_circuit",
                            $"أعاد تسمية دائرة الملف ({doc.Id}) من «{oldName}» إلى «{trimmed}»", before, token);
                    await _audit.LogAsync(actorName, "rename_circuit", null, null,
                        $"أعاد تسمية دائرة التنفيذ من «{oldName}» إلى «{trimmed}» مع تحديث {docs.Count} ملفًا و{delegations.Count} إنابة", token);
                }
                result = await ToDtoAsync(circuit, token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            throw new DocumentConflictException($"الدائرة «{trimmed}» موجودة مسبقًا في فرعك", ex);
        }
        return result;
    }

    public async Task<ExecutionCircuitDto> SetActiveAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, bool isActive, long? version, CancellationToken ct = default)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        ExecutionCircuitDto result = null!;
        await _tx.RunAsync(async token =>
        {
            var tracked = await GetOwnedAsync(circuitId, branchId, ownerSectionId, token);
            if (version is not null && tracked.Version != version.Value)
                throw new DocumentConflictException("تعارض تزامن — أعد تحميل الدائرة وحاول مجددًا");
            tracked.IsActive = isActive;
            tracked.UpdatedAt = DateTime.UtcNow;
            tracked.Version++;
            _circuits.Update(tracked);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, isActive ? "activate_circuit" : "deactivate_circuit",
                null, null, $"{(isActive ? "فعّل" : "عطّل")} دائرة التنفيذ «{tracked.Name}»", token);
            result = await ToDtoAsync(tracked, token);
        }, ct);
        return result;
    }

    public async Task DeleteAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, CancellationToken ct = default)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        await _tx.RunAsync(async token =>
        {
            var circuit = await GetOwnedAsync(circuitId, branchId, ownerSectionId, token);
            // حارس الحذف: كل الصفوف غير المطهّرة بما فيها المحذوفة منطقيًا + الإنابات الواردة المعلقة.
            var fileCount = await _documents.CountAllByCircuitAsync(circuitId, token);
            if (fileCount > 0)
                throw new DocumentConflictException($"لا يمكن حذف الدائرة — أفرغها أولًا (بها {fileCount} ملفًا)");
            // C1: متجاوزًا فلتر المصدر المحذوف — المعلقة تحظر أيًا كان حال مصدرها.
            var pendingIncoming = await _delegations.CountPendingIncomingByCircuitIncludingDeletedAsync(circuitId, token);
            if (pendingIncoming > 0)
                throw new DocumentConflictException($"لا يمكن حذف الدائرة — بها {pendingIncoming} إنابة واردة معلقة تستهدفها");
            // التاريخ المجمد (البند 45): الإنابات غير المعلقة التي استهدفت الدائرة تُفك
            // عن المرجع (DelegatedCircuitId=null) مع إبقاء الاسم النصي — وإلا انفجر الحذف
            // بانتهاك FK (Restrict) رغم اجتياز الحارس. المعلقة محظورة أعلاه فلا تصل هنا.
            // C1: متجاوزًا الفلتر أيضًا — إنابة مصدرها محذوف ما زال صفها يشير للدائرة.
            var incoming = await _delegations.ListByDelegatedCircuitIncludingDeletedAsync(circuitId, token);
            var detached = 0;
            var now = DateTime.UtcNow;
            foreach (var g in incoming)
            {
                if (g.Status == DelegationStatusCatalog.PendingHead)
                    throw new DocumentConflictException("لا يمكن حذف الدائرة — بها إنابة واردة معلقة تستهدفها");
                g.DelegatedCircuitId = null;
                g.UpdatedAt = now;
                _delegations.Update(g);
                detached++;
            }
            _circuits.Remove(circuit);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "delete_circuit", null, null,
                $"حذف دائرة التنفيذ «{circuit.Name}» مع فك {detached} إنابة مكتملة عن مرجعها (الاسم محفوظ)", token);
        }, ct);
    }

    /// <summary>
    /// نقل ملكية دائرة لمالك جديد داخل الفرع نفسه (قرار §2.5 + §8.3): المفتوح
    /// والمغلق يتبع الجديد (الملكية على الدائرة نفسها)، والتدقيق ثابت. الإحالات
    /// المفتوحة (§6.6): تُنشأ تنبيهات بديلة للمالك الجديد (لا تعديل القديمة) —
    /// و`ForwardState` لا يتغير (استثناء فرعي لا شعبي).
    /// </summary>
    public async Task<ExecutionCircuitDto> TransferCircuitAsync(
        int circuitId, int? targetSectionId, int actorUserId, string? actorName, CancellationToken ct = default, long? version = null)
    {
        ExecutionCircuitDto result = null!;
        try
        {
            await _tx.RunAsync(async token =>
            {
            var circuit = await _circuits.GetByIdAsync(circuitId, token)
                ?? throw new ArgumentException("الدائرة غير موجودة");
            if (version is not null && circuit.Version != version.Value)
                throw new DocumentConflictException("تعارض تزامن — أعد تحميل الدائرة وحاول مجددًا");

            Section? targetSection = null;
            if (targetSectionId.HasValue)
            {
                targetSection = await _sections.GetByIdAsync(targetSectionId.Value, token)
                    ?? throw new ArgumentException("الشعبة الهدف غير موجودة");
                if (targetSection.BranchId != circuit.BranchId)
                    throw new ArgumentException("النقل داخل الفرع نفسه — الشعبة من فرع آخر");
                if (!targetSection.IsActive)
                    throw new ArgumentException("الشعبة الهدف معطلة — اختر شعبة نشطة");
            }

            if (circuit.SectionId == targetSectionId)
            {
                result = await ToDtoAsync(circuit, token);
                return;
            }

            var oldOwner = circuit.SectionId.HasValue
                ? (await _sections.GetByIdAsync(circuit.SectionId.Value, token))?.Name ?? "؟"
                : "رئيس القسم";
            var newOwner = targetSection?.Name ?? "رئيس القسم";

            circuit.SectionId = targetSectionId;
            circuit.UpdatedAt = DateTime.UtcNow;
            circuit.Version++;
            _circuits.Update(circuit);
            await _uow.SaveChangesAsync(token);
            // النتيجة تُثبَّت هنا قبل أي خروج مبكر (مالك بلا رئيس) — وإلا عاد
            // `null` فتحوّل `Ok` إلى `204`.
            result = await ToDtoAsync(circuit, token);

            await _audit.LogAsync(actorName, "transfer_circuit", null, null,
                $"نقل الدائرة «{circuit.Name}» من {oldOwner} إلى {newOwner} (الملفات المفتوحة والمغلقة تتبع المالك الجديد)", token);

            // المالك الجديد: رئيس الشعبة الهدف، أو رئيس القسم للقسم.
            var newHead = targetSectionId.HasValue
                ? await _users.FindActiveHeadAsync(UserRole.SubHead, circuit.BranchId, targetSectionId, token)
                : await _users.FindActiveHeadAsync(UserRole.Head, circuit.BranchId, null, token);
            if (newHead is null)
                return;

            await _successions.AddAsync(new HeadSuccession
            {
                BranchId = circuit.BranchId,
                SectionId = targetSectionId,
                UserId = newHead.Id,
                Role = newHead.Role,
                Event = HeadSuccessionEventCatalog.CircuitTransferred,
                At = DateTime.UtcNow,
                ActorName = actorName,
                Reason = $"نُقلت الدائرة «{circuit.Name}» من {oldOwner} إلى {newOwner}",
            }, token);

            // بدائل المعلّق للمالك الجديد (§6.6): إنابات واردة معلقة + استئنافات
            // بلا إسناد على ملفات الدائرة — تنبيه واحد جامع (لا تعديل القديمة).
            var fileIds = (await _documents.ListByCircuitAsync(circuit.Id, includeDeleted: false, token))
                .Select(d => d.Id)
                .ToList();
            var pendingDelegations = (await _delegations.ListByDelegatedCircuitAsync(circuit.Id, token))
                .Count(g => g.Status == DelegationStatusCatalog.PendingHead);
            var pendingAppeals = 0;
            if (fileIds.Count > 0)
            {
                pendingAppeals = (await _appeals.ListByDocumentIdsAsync(fileIds, token))
                    .Count(a => a.Status == AppealStatusCatalog.Pending && a.AssignedLawyerId == null);
            }

            await _alerts.AddAsync(new HeadAlert
            {
                BranchId = circuit.BranchId,
                CreatedById = actorUserId,
                TargetType = HeadAlertTargetType.Head,
                Message = $"نُقلت إليك دائرة «{circuit.Name}» — {fileIds.Count} ملفًا، {pendingDelegations} إنابة معلقة، {pendingAppeals} استئنافًا بانتظار الإسناد",
                CreatedAt = DateTime.UtcNow,
                Recipients = new List<HeadAlertRecipient> { new() { UserId = newHead.Id } },
            }, token);
            await _uow.SaveChangesAsync(token);
        }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsConcurrencyViolation(ex))
        {
            // كتابتان متزامنتان حقيقيتان على الدائرة نفسها — 409 ودية بدل 500 خام.
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الدائرة وحاول مجددًا");
        }
        return result;
    }

    /// <summary>
    /// قوائم الاختيار للمحامين (تسطير الملفات في أي دائرة — قرار §2.11) مقابل
    /// الرؤساء (دوائر نطاقهم فقط). `limitToOwner` يميّز الحالتين صراحةً — لا
    /// قيمة سحرية (`null` تعني القسم للرئيس، ولا شيء للمحامي).
    /// </summary>
    public async Task<List<ExecutionCircuitDto>> ListForLawyerAsync(int branchId, int? ownerSectionId, bool limitToOwner, CancellationToken ct = default)
    {
        var all = await _circuits.ListAsync(ct);
        var sectionNames = await SectionNamesAsync(ct);
        return all.Where(c => c.BranchId == branchId && c.IsActive && (!limitToOwner || c.SectionId == ownerSectionId))
            .OrderBy(c => c.Name)
            .Select(c => new ExecutionCircuitDto(
                c.Id, c.BranchId, null, c.Name, c.IsActive, 0, 0, c.Version,
                c.SectionId, c.SectionId.HasValue ? sectionNames.GetValueOrDefault(c.SectionId.Value) : null)).ToList();
    }

    public async Task<List<ExecutionCircuitDto>> ListForDelegationAsync(string? governorate, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(governorate) || !GovernorateCatalog.IsGovernorate(governorate!.Trim()))
            return new List<ExecutionCircuitDto>();
        var gov = governorate!.Trim();
        var branches = await _branches.ListAsync(ct);
        var branchIds = branches.Where(b => b.Governorate == gov).Select(b => b.Id).ToHashSet();
        if (branchIds.Count == 0) return new List<ExecutionCircuitDto>();
        var all = await _circuits.ListAsync(ct);
        return all.Where(c => branchIds.Contains(c.BranchId) && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new ExecutionCircuitDto(
                c.Id, c.BranchId, null, c.Name, c.IsActive, 0, 0, c.Version)).ToList();
    }

    public async Task<ReferCircuitFilesResult> ReferFilesAsync(
        int sourceCircuitId, int branchId, int? ownerSectionId, int actorUserId, string? actorName,
        ReferCircuitFilesRequest request, CancellationToken ct = default, string? idempotencyKey = null)
    {
        // RF-011: حجز المفتاح قبل أي تحقق يستحق 400 بلا حجز — التحققات الثقيلة داخل
        // النواة؛ التكرار بنفس البصمة يُعيد اللقطة المخزنة نفسها. البصمة بمعرفات
        // مرتبة حتى لا تُبطل إعادة ترتيب المصفوفة التكرار.
        var orderedIds = (request.FileIds ?? new List<int>()).Distinct().OrderBy(id => id).ToList();
        var ticket = await IdempotencyGuard.BeginAsync(_idempotency, "circuits.refer-files",
            actorUserId, idempotencyKey,
            IdempotencyGuard.Fingerprint(new
            {
                sourceCircuitId,
                fileIds = orderedIds,
                request.TargetLawyerId,
                request.TargetCircuitId,
                branchId,
                ownerSectionId,
            }), ct);

        ReferCircuitFilesResult result;
        try
        {
            result = await ReferFilesCoreAsync(sourceCircuitId, branchId, ownerSectionId, actorUserId, actorName, request, ct);
        }
        catch
        {
            await IdempotencyGuard.ReleaseAsync(_idempotency, ticket, ct);
            throw;
        }

        // اللقطة بشكل جسم الاستجابة نفسه (مع البانر) ليُعاد حرفيًا عند التكرار.
        await IdempotencyGuard.CompleteAsync(_idempotency, ticket,
            IdempotencyGuard.Snapshot(new
            {
                result.ReferredCount,
                result.SkippedCount,
                result.RemainingCount,
                reminder = EmptyingReminderBanner,
            }), ct);
        return result;
    }

    /// <summary>نواة الإحالة (تحققات + نقل + تدقيق) — تُستدعى بعد حجز مفتاح عدم التكرار.</summary>
    private async Task<ReferCircuitFilesResult> ReferFilesCoreAsync(
        int sourceCircuitId, int branchId, int? ownerSectionId, int actorUserId, string? actorName,
        ReferCircuitFilesRequest request, CancellationToken ct)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        if (request.FileIds is null || request.FileIds.Count == 0)
            throw new ArgumentException("حدد ملفًا واحدًا على الأقل للإحالة");
        if (sourceCircuitId == request.TargetCircuitId)
            throw new ArgumentException("دائرة المصدر والهدف متماثلتان");

        var distinctIds = request.FileIds.Distinct().ToList();
        int referred = 0, skipped = 0, remaining = 0;

        await _tx.RunAsync(async token =>
        {
            var source = await GetOwnedAsync(sourceCircuitId, branchId, ownerSectionId, token);
            var target = await GetOwnedAsync(request.TargetCircuitId, branchId, ownerSectionId, token);
            if (!target.IsActive)
                throw new ArgumentException("دائرة الهدف معطلة — اختر دائرة نشطة");
            var sourceName = source.Name;
            var targetName = target.Name;

            // بدء الإفراغ يعطّل الدائرة الملغاة تلقائيًا (لا ملفات جديدة أثناء التفريغ).
            if (source.IsActive)
            {
                source.IsActive = false;
                source.UpdatedAt = DateTime.UtcNow;
                source.Version++;
                _circuits.Update(source);
            }

            var lawyer = await _users.GetByIdAsync(request.TargetLawyerId, token);
            if (lawyer is null || lawyer.Role != UserRole.Lawyer || !lawyer.IsActive)
                throw new ArgumentException("المحامي الهدف غير موجود أو معطل");
            if (lawyer.BranchId != branchId)
                throw new ArgumentException("المحامي الهدف ليس ضمن فرع الدائرة");
            var targetFullName = string.IsNullOrWhiteSpace(lawyer.FullName) ? lawyer.Username : lawyer.FullName;

            var docs = await _documents.ListByIdsForUpdateAsync(distinctIds, token);
            var byId = docs.ToDictionary(d => d.Id);
            foreach (var id in distinctIds)
            {
                if (!byId.TryGetValue(id, out var doc) || doc.IsDeleted)
                    throw new ArgumentException($"الملف ({id}) غير موجود في دائرة المصدر");
                if (doc.ExecutionCircuitId != sourceCircuitId)
                    throw new ArgumentException($"الملف ({id}) ليس من دائرة المصدر");
            }
            var now = DateTime.UtcNow;
            var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
            var referredAudit = new List<(Document Doc, Dictionary<string, string?> Before)>();
            foreach (var doc in docs)
            {
                // تخطي المسند لمالكه الحالي بصمت مع عدّاد متخطىً.
                if (doc.CreatedById == request.TargetLawyerId && doc.ExecutionCircuitId == request.TargetCircuitId)
                {
                    skipped++;
                    continue;
                }
                var oldNumber = EffectiveFileIdentity.Number(doc, currentYear) ?? doc.FileNumber;
                var oldType = doc.FileType;
                var oldYear = EffectiveFileIdentity.Year(doc, currentYear) ?? doc.FileYear;

                // لقطة «قبل» للتدقيق الحقلي (S5) — قبل أي طفرة.
                var before = DocumentChangeTracker.Capture(doc);
                doc.CreatedById = request.TargetLawyerId;
                doc.Lawyer = targetFullName;
                doc.ExecutionCircuitId = request.TargetCircuitId;
                doc.Court = target.Name;
                doc.CourtNorm = ArabicNameNormalizer.Normalize(target.Name);
                doc.FileNumber = null;
                doc.FileType = null;
                doc.FileYear = null;
                doc.NeedsRegistration = true;
                doc.IsDraft = false;
                doc.UpdatedAt = now;
                doc.Version++;
                // مصير BaseNumbers عند الإحالة: تُمسح (التاريخ محفوظ في وقوعّة circuit-referred).
                var baseRows = doc.BaseNumbers.ToList();
                foreach (var b in baseRows)
                    _baseNumbers.Remove(b);
                doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
                _documents.Update(doc);

                var occ = new DocumentOccurrence
                {
                    DocumentId = doc.Id,
                    OccurrenceType = OccurrenceTypeCatalog.CircuitReferred,
                    Source = OccurrenceSourceCatalog.System,
                    EventDate = now,
                    FileNumber = oldNumber,
                    FileType = oldType,
                    Year = int.TryParse(oldYear, out var oy) ? oy : null,
                    FromCircuitName = sourceName,
                    ToCircuitName = targetName,
                    Details = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["fromCircuit"] = sourceName,
                        ["toCircuit"] = targetName,
                        ["oldNumber"] = oldNumber ?? string.Empty,
                        ["oldType"] = oldType ?? string.Empty,
                        ["oldYear"] = oldYear ?? string.Empty,
                    }),
                    CreatedById = actorUserId,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                await _occurrences.AddAsync(occ, token);

                // الإنابات تتبع ملفاتها: المنيب المنقول يسحب إناباته كما هي؛ المناب المنقول يُحدَّث DelegatedCourt لوجهته.
                var outgoing = await _delegations.ListBySourceAsync(doc.Id, token);
                foreach (var g in outgoing)
                {
                    g.UpdatedAt = now;
                    _delegations.Update(g);
                }
                // إن كان الملف منابًا (له SourceDelegationId) وإنابته داخلية تستهدف دائرة المصدر، تُحدَّث للوجهة.
                if (doc.SourceDelegationId is not null)
                {
                    var src = await _delegations.GetByIdAsync(doc.SourceDelegationId.Value, token);
                    if (src is not null && !src.IsExternal && src.DelegatedCircuitId == sourceCircuitId)
                    {
                        src.DelegatedCircuitId = request.TargetCircuitId;
                        src.DelegatedCourt = target.Name;
                        src.DelegatedCourtNorm = ArabicNameNormalizer.Normalize(target.Name);
                        src.UpdatedAt = now;
                        _delegations.Update(src);
                    }
                }
                referredAudit.Add((doc, before));
                referred++;
            }
            await _uow.SaveChangesAsync(token);

            // الواردة المعلقة بلا ملف مناب تُعاد توجيهها للدائرة الجديدة.
            var incoming = await _delegations.ListByDelegatedCircuitAsync(sourceCircuitId, token);
            var now2 = DateTime.UtcNow;
            foreach (var g in incoming.Where(g => g.Status == DelegationStatusCatalog.PendingHead))
            {
                g.DelegatedCircuitId = request.TargetCircuitId;
                g.DelegatedCourt = target.Name;
                g.DelegatedCourtNorm = ArabicNameNormalizer.Normalize(target.Name);
                g.UpdatedAt = now2;
                _delegations.Update(g);
            }
            await _uow.SaveChangesAsync(token);

            // تدقيق حقلي لكل ملف محال (S5) — داخل المعاملة بعد الحفظ الرئيسي.
            foreach (var (doc, before) in referredAudit)
                await LogCircuitDocumentChangeAsync(doc, actorName, "refer_circuit_files",
                    $"أحال الملف ({doc.Id}) من دائرة «{sourceName}» إلى دائرة «{targetName}» للمحامي {targetFullName}", before, token);

            await _audit.LogAsync(actorName, "refer_circuit_files", null, null,
                $"أحال {referred} ملفًا من دائرة «{sourceName}» إلى دائرة «{targetName}» للمحامي {targetFullName} (متخطى {skipped})", token);

            // تنبيه المحامي المدمج (لكل محامٍ × دائرة): يُنشأ/يُحدَّث.
            await UpsertPendingAlertAsync(request.TargetLawyerId, branchId, actorUserId, targetName, docs.FirstOrDefault()?.Id, token);
            await _uow.SaveChangesAsync(token);

            var counts = await _documents.CountByCircuitsAsync(new List<int> { sourceCircuitId }, token);
            remaining = counts.TryGetValue(sourceCircuitId, out var v) ? v.FileCount : 0;
        }, ct);

        return new ReferCircuitFilesResult(referred, skipped, remaining);
    }

    private async Task UpsertPendingAlertAsync(int lawyerId, int branchId, int actorUserId, string circuitName, int? sampleDocumentId, CancellationToken token)
    {
        var message = BuildPendingMessage(circuitName);
        var existing = await _alerts.FindPendingAlertAsync(lawyerId, message, token);
        if (existing is not null)
        {
            existing.CreatedAt = DateTime.UtcNow;
            if (existing.DocumentId is null && sampleDocumentId is not null)
                existing.DocumentId = sampleDocumentId;
            _alerts.Update(existing);
            return;
        }
        await CreatePendingAlertAsync(lawyerId, branchId, actorUserId, message, sampleDocumentId, token);
    }

    /// <summary>
    /// ضمان تنبيه معلق (إنشاء عند الغياب فقط — بلا لمس الموجود): تُستخدم بعد النقل
    /// حتى لا تُقفز التنبيهات القائمة زمنيًا بلا حدث جديد.
    /// </summary>
    private async Task EnsurePendingAlertAsync(int lawyerId, int branchId, int actorUserId, string circuitName, int? sampleDocumentId, CancellationToken token)
    {
        var message = BuildPendingMessage(circuitName);
        var existing = await _alerts.FindPendingAlertAsync(lawyerId, message, token);
        if (existing is not null)
            return;
        await CreatePendingAlertAsync(lawyerId, branchId, actorUserId, message, sampleDocumentId, token);
    }

    private async Task CreatePendingAlertAsync(int lawyerId, int branchId, int actorUserId, string message, int? sampleDocumentId, CancellationToken token)
    {
        var alert = new HeadAlert
        {
            BranchId = branchId,
            CreatedById = actorUserId,
            TargetType = HeadAlertTargetType.Lawyer,
            DocumentId = sampleDocumentId,
            TargetLawyerId = lawyerId,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            Recipients = new List<HeadAlertRecipient> { new() { UserId = lawyerId } },
        };
        await _alerts.AddAsync(alert, token);
    }

    public async Task SyncPendingAlertsAfterTransferAsync(int sourceOwnerId, int targetOwnerId, int branchId, int actorUserId, CancellationToken ct = default)
    {
        // تنسيق من المتحكم بعد نجاح النقل (مفرد/جماعي): التنبيه يتبع المالك —
        // تُنشأ تنبيهات المالك الجديد لدوائر معلقاته الحالية، وتُصفَّى تنبيهات
        // القديم التي لم يعد لها معلقات. فشلها لا يُفشل النقل (تُستدعى بعده).
        await _tx.RunAsync(async token =>
        {
            var targetPending = await _documents.ListPendingForLawyerAsync(targetOwnerId, null, token);
            foreach (var g in targetPending.GroupBy(d => d.ExecutionCircuit?.Name ?? d.Court ?? string.Empty))
            {
                if (string.IsNullOrWhiteSpace(g.Key))
                    continue;
                await EnsurePendingAlertAsync(targetOwnerId, branchId, actorUserId, g.Key, g.FirstOrDefault()?.Id, token);
            }
            await CleanupPendingAlertsAsync(sourceOwnerId, token);
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    public async Task<List<PendingRegistrationDto>> MyPendingRegistrationsAsync(int lawyerId, int? circuitId, CancellationToken ct = default)
    {
        var docs = await _documents.ListPendingForLawyerAsync(lawyerId, circuitId, ct);
        if (docs.Count == 0) return new List<PendingRegistrationDto>();
        var referrals = await _documents.ListLastCircuitReferralsAsync(docs.Select(d => d.Id).ToList(), ct);
        var referralByDoc = referrals
            .GroupBy(o => o.DocumentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.Id).First());
        var circuits = await _circuits.ListAsync(ct);
        var circuitNames = circuits.ToDictionary(c => c.Id, c => c.Name);
        return docs.OrderBy(d => d.Id).Select(d =>
        {
            // المعروض حاليًا (قديم — من EffectiveFileIdentity).
            referralByDoc.TryGetValue(d.Id, out var lastReferred);
            string? circuitName = null;
            if (d.ExecutionCircuitId is not null)
                circuitNames.TryGetValue(d.ExecutionCircuitId.Value, out circuitName);
            return new PendingRegistrationDto(
                d.Id,
                d.ExecutionCircuitId ?? 0,
                circuitName ?? d.Court,
                d.BorrowerName,
                lastReferred?.FileNumber ?? d.FileNumber,
                lastReferred?.FileType ?? d.FileType,
                lastReferred?.Year?.ToString() ?? d.FileYear);
        }).ToList();
    }

    public async Task<int> CompleteRegistrationsAsync(int lawyerId, string? actorName, CompleteRegistrationsRequest request, CancellationToken ct = default)
    {
        if (request.Entries is null || request.Entries.Count == 0)
            throw new ArgumentException("لا توجد ملفات لإعادة القيد");
        var ids = request.Entries.Select(e => e.DocumentId).Distinct().ToList();
        if (ids.Count != request.Entries.Count)
            throw new ArgumentException("تكرار في الملفات المرسلة");

        var saved = 0;
        try
        {
            await _tx.RunAsync(async token =>
            {
                var docs = await _documents.ListByIdsForUpdateAsync(ids, token);
                if (docs.Count != ids.Count)
                    throw new ArgumentException("أحد الملفات غير موجود");
                var byId = docs.ToDictionary(d => d.Id);
                // فحص الملكية + flag داخل المعاملة.
                foreach (var e in request.Entries)
                {
                    var doc = byId[e.DocumentId];
                    if (doc.IsDeleted)
                        throw new ArgumentException($"الملف ({e.DocumentId}) محذوف");
                    if (doc.CreatedById != lawyerId)
                        throw new ArgumentException($"الملف ({e.DocumentId}) ليس من ملفاتك");
                    if (!doc.NeedsRegistration)
                        throw new ArgumentException($"الملف ({e.DocumentId}) ليس بانتظار إعادة القيد");
                }
                // تطبيع رقمي إلزامي + تحقق وحدانية ذري لكل ملف.
                var now = DateTime.UtcNow;
                var completedAudit = new List<(Document Doc, Dictionary<string, string?> Before, string Number, string Type, string Year)>();
                foreach (var e in request.Entries)
                {
                    var doc = byId[e.DocumentId];
                    var number = DigitNormalizer.NormalizeDigits(e.FileNumber);
                    var year = DigitNormalizer.NormalizeDigits(e.FileYear);
                    var type = (e.FileType ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(number))
                        throw new ArgumentException($"الرقم الجديد إلزامي للملف ({e.DocumentId})");
                    if (string.IsNullOrWhiteSpace(type))
                        throw new ArgumentException($"النوع إلزامي للملف ({e.DocumentId})");
                    if (string.IsNullOrWhiteSpace(year))
                        throw new ArgumentException($"السنة إلزامية للملف ({e.DocumentId})");
                    if (doc.ExecutionCircuitId is null)
                        throw new ArgumentException($"الملف ({e.DocumentId}) بلا دائرة");
                    // وحدانية (circuitId): فحص خدمي داخل المعاملة + قيد فريد ظهرًا للسباق.
                    if (await _documents.ExistsActiveWithNumberByCircuitAsync(
                        doc.Id, doc.ExecutionCircuitId, doc.CourtNorm, number, type, year, token))
                        throw new DocumentConflictException($"رقم الأساس {number} مكرر في دائرة الملف ({e.DocumentId}) لسنة {year}");

                    // لقطة «قبل» للتدقيق الحقلي (S5) — قبل أي طفرة (بعد كل الحراس).
                    var before = DocumentChangeTracker.Capture(doc);
                    doc.FileNumber = number;
                    doc.FileType = type;
                    doc.FileYear = year;
                    doc.NeedsRegistration = false;
                    doc.IsDraft = false;
                    doc.UpdatedAt = now;
                    doc.Version++;
                    doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                    doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
                    _documents.Update(doc);

                    var occ = new DocumentOccurrence
                    {
                        DocumentId = doc.Id,
                        OccurrenceType = OccurrenceTypeCatalog.CircuitReregistered,
                        Source = OccurrenceSourceCatalog.System,
                        EventDate = now,
                        FileNumber = number,
                        FileType = doc.FileType,
                        Year = int.TryParse(year, out var yy) ? yy : null,
                        FromCircuitName = doc.Court,
                        ToCircuitName = doc.Court,
                        CreatedById = lawyerId,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    await _occurrences.AddAsync(occ, token);
                    completedAudit.Add((doc, before, number, type, year));
                    saved++;
                }
                await _uow.SaveChangesAsync(token);
                // تدقيق حقلي لكل ملف (S5) — داخل المعاملة بعد الحفظ الرئيسي.
                foreach (var (doc, before, number, type, year) in completedAudit)
                    await LogCircuitDocumentChangeAsync(doc, actorName, "complete_registrations",
                        $"أعاد قيد الملف ({doc.Id}) برقم {number}/{type}/{year}", before, token);
                await _audit.LogAsync(actorName, "complete_registrations", null, null,
                    $"أعاد قيد {saved} ملفًا بأرقام جديدة", token);

                // خطاف التنظيف DeleteByPendingRegistration: يُستدعى عند اكتمال إعادة قيد آخر ملف معلق للمحامي.
                await CleanupPendingAlertsAsync(lawyerId, token);
                await _uow.SaveChangesAsync(token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            throw new DocumentConflictException("رقم الأساس مكرر — أُدخل أثناء الحفظ من مستخدم آخر، أعد المحاولة برقم مختلف", ex);
        }
        return saved;
    }

    /// <summary>
    /// خطاف التنظيف المسمّى DeleteByPendingRegistration (على نمط DeleteByDelegationAsync):
    /// يُصفّى تنبيه المحامي تلقائيًا عند الصفر (لكل دائرة)، ويتبع المالك عند إعادة الإسناد.
    /// </summary>
    public async Task<bool> DeleteByPendingRegistrationAsync(int lawyerId, CancellationToken ct = default)
    {
        var removed = false;
        await _tx.RunAsync(async token =>
        {
            removed = await CleanupPendingAlertsAsync(lawyerId, token);
            if (removed)
                await _uow.SaveChangesAsync(token);
        }, ct);
        return removed;
    }

    private async Task<bool> CleanupPendingAlertsAsync(int lawyerId, CancellationToken token)
    {
        var alerts = await _alerts.ListPendingAlertsForLawyerAsync(lawyerId, PendingAlertPrefix, token);
        if (alerts.Count == 0) return false;
        var pending = await _documents.ListPendingForLawyerAsync(lawyerId, null, token);
        var circuits = await _circuits.ListAsync(token);
        var circuitNames = circuits.ToDictionary(c => c.Id, c => c.Name);
        var pendingMessages = new HashSet<string>(pending.Select(d =>
        {
            string? n = null;
            if (d.ExecutionCircuitId is not null)
                circuitNames.TryGetValue(d.ExecutionCircuitId.Value, out n);
            n ??= d.Court ?? string.Empty;
            return BuildPendingMessage(n);
        }).Where(m => m != BuildPendingMessage(string.Empty)));
        var removedAny = false;
        foreach (var alert in alerts)
        {
            if (!pendingMessages.Contains(alert.Message))
            {
                _alerts.Remove(alert);
                removedAny = true;
            }
        }
        return removedAny;
    }

    /// <summary>
    /// صفوف إحصاءات الدوائر بالنطاق (قرار §2.27 — التفصيل الرباعي في المرحلة 8):
    /// الرئيس لدوائر نطاقه فقط، والمدير/المشرف للكل.
    /// </summary>
    public async Task<List<CircuitStatsDto>> CircuitStatsAsync(int? branchId, int? ownerSectionId, bool fullAccess, CancellationToken ct = default)
    {
        var all = await _circuits.ListAsync(ct);
        var circuits = (branchId is null ? all : all.Where(c => c.BranchId == branchId.Value));
        if (!fullAccess)
            circuits = circuits.Where(c => c.SectionId == ownerSectionId);
        var ordered = circuits.OrderBy(c => c.BranchId).ThenBy(c => c.Name).ToList();
        if (ordered.Count == 0) return new List<CircuitStatsDto>();
        var branches = await _branches.ListAsync(ct);
        var branchNames = branches.ToDictionary(b => b.Id, b => b.Name);
        // أسماء الشعب دفعة واحدة — بلا استعلام لكل دائرة (N+1).
        var sectionNames = (await _sections.ListAsync(ct))
            .Where(s => ordered.Any(c => c.SectionId == s.Id))
            .ToDictionary(s => s.Id, s => s.Name);
        var counts = await _documents.CountByCircuitsAsync(ordered.Select(c => c.Id).ToList(), ct);
        // المحامون النشطون (≥ ملف واحد): استعلام تجميعي واحد — بلا تحميل كيانات (N+1).
        var lawyerCounts = await _documents.CountLawyersByCircuitsAsync(ordered.Select(c => c.Id).ToList(), ct);
        return ordered.Select(c =>
        {
            counts.TryGetValue(c.Id, out var v);
            return new CircuitStatsDto(c.Id, c.Name, c.BranchId,
                branchNames.TryGetValue(c.BranchId, out var n) ? n : null,
                c.IsActive, v.FileCount, lawyerCounts.TryGetValue(c.Id, out var lc) ? lc : 0, v.PendingCount,
                c.SectionId,
                c.SectionId.HasValue && sectionNames.TryGetValue(c.SectionId.Value, out var s) ? s : null,
                c.Version);
        }).ToList();
    }

    private async Task<ExecutionCircuitDto> ToDtoAsync(ExecutionCircuit c, CancellationToken token)
    {
        var counts = await _documents.CountByCircuitsAsync(new List<int> { c.Id }, token);
        counts.TryGetValue(c.Id, out var v);
        string? sectionName = null;
        if (c.SectionId.HasValue)
            sectionName = (await _sections.GetByIdAsync(c.SectionId.Value, token))?.Name;
        return new ExecutionCircuitDto(c.Id, c.BranchId, null, c.Name, c.IsActive, v.FileCount, v.PendingCount, c.Version,
            c.SectionId, sectionName);
    }

    /// <summary>
    /// تدقيق حقلي لكل ملف في المسارات الجماعية (S5) — مرآة LogDocumentChangesAsync
    /// في المسار المفرد: لقطة «قبل» تُلتقط قبل الطفرة، وصفوف «حقل/قبل/بعد»
    /// تُكتب داخل المعاملة نفسها بعد الحفظ الرئيسي.
    /// </summary>
    private async Task LogCircuitDocumentChangeAsync(
        Document doc,
        string? actorName,
        string actionType,
        string detail,
        Dictionary<string, string?> before,
        CancellationToken token)
    {
        var changes = DocumentChangeTracker.Diff(before, doc);
        if (changes.Count == 0)
        {
            await _audit.LogAsync(actorName, actionType, doc.Id, doc.DocumentType, detail, token);
            return;
        }
        await _audit.LogDocumentChangeAsync(actorName, actionType, doc.Id, doc.DocumentType,
            $"{detail} — غيّر {changes.Count} حقلًا", changes, token);
    }
}
