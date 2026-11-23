using System.Globalization;
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

    /// <summary>
    /// كل ملفات الدائرة (غير المحذوفة) أيًا كانت حالتها — لمعالج الإفراغ:
    /// المخفية عن فلتر البحث العام («منفذ إنابة» وغيرها) شاملة. نطاق المالك إلزامي.
    /// </summary>
    Task<List<CircuitFileDto>> ListFilesAsync(int circuitId, int branchId, int? ownerSectionId, CancellationToken ct = default);

    Task<ExecutionCircuitDto> RenameAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, string name, long? version, CancellationToken ct = default, int? actorUserId = null);
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
public sealed partial class ExecutionCircuitService : IExecutionCircuitService
{
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
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly IIdempotencyStore? _idempotency;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IDocumentNumberRegistry? _numberRegistry;
    private readonly IDocumentBaseNumberRepository? _baseNumberRepository;
    private readonly IReferenceDataCache? _cache;

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
        IHeadAlertOutbox alertOutbox,
        IDbExceptionClassifier dbErrors,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IIdempotencyStore? idempotency = null,
        IDocumentNumberRegistry? numberRegistry = null,
        IDocumentBaseNumberRepository? baseNumberRepository = null,
        IReferenceDataCache? cache = null)
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
        _alertOutbox = alertOutbox;
        _dbErrors = dbErrors;
        _idempotency = idempotency;
        _clock = clock;
        _timeZone = timeZone;
        _numberRegistry = numberRegistry;
        _baseNumberRepository = baseNumberRepository;
        _cache = cache;
    }

    // قراءات مرجعية بحتة (أسماء/معرّفات) — العدّادات الحيّة تُحسب خارج الكاش دائمًا.
    private Task<List<ExecutionCircuit>> ListCircuitsAsync(CancellationToken ct)
        => _cache is null
            ? _circuits.ListAsync(ct)
            : _cache.GetOrCreateAsync(ReferenceDataGroups.Circuits, "all", _circuits.ListNoTrackingAsync, ct);

    private Task<List<Section>> ListSectionsAsync(CancellationToken ct)
        => _cache is null
            ? _sections.ListAsync(ct)
            : _cache.GetOrCreateAsync(ReferenceDataGroups.Sections, "all", _sections.ListNoTrackingAsync, ct);

    private Task<List<Branch>> ListBranchesAsync(CancellationToken ct)
        => _cache is null
            ? _branches.ListAsync(ct)
            : _cache.GetOrCreateAsync(ReferenceDataGroups.Branches, "all", _branches.ListNoTrackingAsync, ct);
}
