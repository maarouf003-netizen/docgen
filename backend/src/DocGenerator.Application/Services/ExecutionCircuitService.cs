using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IExecutionCircuitService
{
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
/// بقيت في الخدمة خطافات التنبيه (كتابة داخل معاملة النقل)؛ وكل القراءات صارت
/// استعلامات `UseCases/ExecutionCircuits` (`P4-01ct`)، وكل الكتابات المستقلة أوامر
/// تملك فحوصها (وجود/تفعيل/فرع) داخل معاملتها (ضد TOCTOU) مع تدقيقها.
/// </summary>
public sealed partial class ExecutionCircuitService : IExecutionCircuitService
{
    public const string EmptyingReminderBanner = "لا تنسى نقل ملفات هذه الدائرة لمحامي أو محامين اخرين ان كان لذلك مقتضى";

    private readonly IDocumentRepository _documents;
    private readonly IHeadAlertRepository _alerts;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IReferenceDataCache? _cache;

    public ExecutionCircuitService(
        IDocumentRepository documents,
        IHeadAlertRepository alerts,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IHeadAlertOutbox alertOutbox,
        IReferenceDataCache? cache = null)
    {
        _documents = documents;
        _alerts = alerts;
        _users = users;
        _uow = uow;
        _tx = tx;
        _alertOutbox = alertOutbox;
        _cache = cache;
    }
}
