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

    Task<List<ExecutionCircuitDto>> ListForLawyerAsync(int branchId, int? ownerSectionId, bool limitToOwner, CancellationToken ct = default);
    Task<List<ExecutionCircuitDto>> ListForDelegationAsync(string? governorate, CancellationToken ct = default);
    Task<List<PendingRegistrationDto>> MyPendingRegistrationsAsync(int lawyerId, int? circuitId, CancellationToken ct = default);
    Task<List<CircuitStatsDto>> CircuitStatsAsync(int? branchId, int? ownerSectionId, bool fullAccess, CancellationToken ct = default);
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
/// بقيت في الخدمة القراءات وخطافات التنبيه؛ وكل الكتابات صارت أوامر تملك فحوصها
/// (وجود/تفعيل/فرع) داخل معاملتها (ضد TOCTOU) مع تدقيقها.
/// </summary>
public sealed partial class ExecutionCircuitService : IExecutionCircuitService
{
    public const string EmptyingReminderBanner = "لا تنسى نقل ملفات هذه الدائرة لمحامي أو محامين اخرين ان كان لذلك مقتضى";

    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<Section> _sections;
    private readonly IDocumentRepository _documents;
    private readonly IDelegationRepository _delegations;
    private readonly IHeadAlertRepository _alerts;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IReferenceDataCache? _cache;

    public ExecutionCircuitService(
        IRepository<ExecutionCircuit> circuits,
        IRepository<Branch> branches,
        IRepository<Section> sections,
        IDocumentRepository documents,
        IDelegationRepository delegations,
        IHeadAlertRepository alerts,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IHeadAlertOutbox alertOutbox,
        IReferenceDataCache? cache = null)
    {
        _circuits = circuits;
        _branches = branches;
        _sections = sections;
        _documents = documents;
        _delegations = delegations;
        _alerts = alerts;
        _users = users;
        _uow = uow;
        _tx = tx;
        _alertOutbox = alertOutbox;
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
