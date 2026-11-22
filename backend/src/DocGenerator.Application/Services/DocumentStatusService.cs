using System.Globalization;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة الحالات (مستخرجة من `DocumentService` — المرحلة 4، نقل ميكانيكي بلا سلوك):
/// آلة حالات نظام «طالبة تنفيذ» + حالات وضع «منفذ عليه»/«عرض وايداع» + التجديد/الشطب
/// + توريث/استرداد المناب. الاسم الموحّد `IDocumentStatusService
/// (تقاعُد اسم `StatusTransitionService` المؤجل).
/// الاعتمادات الدقيقة: `_documents` و`_occurrences` و`_baseNumbers`
/// و`_alertService` (استثناء البنية التحتية — القاعدة 4) و`_delegations` و`_tx` و`_uow`
/// والساعة/المنطقة + `IDocumentDelegationService` (تنبيهات الإنابة) + `IDocumentWriteGuard`
/// (الحراس المشتركة — أي حاجة عكسية تُحل بنقل المساعد لا بنداء خدمة).
/// ثابت ملكية المعاملة: معاملة `_tx.RunAsync` واحدة لكل نقطة دخول عامة —
/// المساعدات (`ApplyRenewalAsync`، `AddStruckOffOccurrenceAsync`، المزامنة) تنضم
/// لمعاملة المستدعي، وأرشفة/إطلاق التنبيهات داخل معاملة الحالة نفسها (ذرّية مع الحدث).
/// </summary>
public sealed partial class DocumentStatusService : IDocumentStatusService
{
    private readonly IDocumentRepository _documents;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IRepository<DocumentBaseNumber> _baseNumbers;
    private readonly IHeadAlertService _alertService;
    private readonly IDelegationRepository _delegations;
    private readonly ITransactionRunner _tx;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IDocumentDelegationService _delegationService;
    private readonly IDocumentWriteGuard _writeGuard;
    private readonly IDocumentNumberRegistry? _numberRegistry;

    public DocumentStatusService(
        IDocumentRepository documents,
        IRepository<DocumentOccurrence> occurrences,
        IRepository<DocumentBaseNumber> baseNumbers,
        IHeadAlertService alertService,
        IDelegationRepository delegations,
        ITransactionRunner tx,
        IUnitOfWork uow,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IDocumentDelegationService delegationService,
        IDocumentWriteGuard writeGuard,
        IDocumentNumberRegistry? numberRegistry = null)
    {
        _documents = documents;
        _occurrences = occurrences;
        _baseNumbers = baseNumbers;
        _alertService = alertService;
        _delegations = delegations;
        _tx = tx;
        _uow = uow;
        _clock = clock;
        _timeZone = timeZone;
        _delegationService = delegationService;
        _writeGuard = writeGuard;
        _numberRegistry = numberRegistry;
    }

    /// <summary>
    /// D3: أرشفة تنبيهات الإنابة لكل مسار يجعل مناب مسجلة أصولًا نهائيًا —
    /// استرداد (تسوية/جبرية كاملة → «مسترد») أو شطب يدوي للمناب. تعمل داخل
    /// معاملة المتصل، وidempotent (إعادة الأرشفة آمنة). البوابة Registered حصرًا:
    /// المعلّقة/المحالة لها تنبيهات اعتماد لا تُمس.
    /// </summary>
    private async Task CleanupPendingCompletionAlertsAsync(int documentId, CancellationToken ct)
    {
        // (1) المنيب: إناباته المسجلة التي صار منابها نهائيًا (أي فرع من IsTargetTerminal —
        // مشطوب بجهتيه/مسترد/منفذ إنابة — لا إعادة تعريف جزئية هنا).
        var recovered = (await _delegations.ListBySourceAsync(documentId, ct))
            .Where(d => d.Status == DelegationStatusCatalog.Registered
                && d.TargetDocument is not null
                && DelegationActivityPolicy.IsTargetTerminal(d.TargetDocument))
            .Select(d => d.Id)
            .ToList();
        foreach (var id in recovered)
            await _alertService.ArchiveByDelegationAsync(
                id,
                HeadAlertKindCatalog.DelegationKinds,
                "delegation_target_terminal",
                ct);
        // (2) الملف نفسه مناب صار نهائيًا يدويًا: إنابته المسجلة تحررت.
        var own = await _delegations.FindByTargetAsync(documentId, ct);
        if (own is not null && own.Status == DelegationStatusCatalog.Registered)
        {
            var targetDoc = await _documents.GetByIdAsync(documentId, ct);
            if (targetDoc is not null && DelegationActivityPolicy.IsTargetTerminal(targetDoc))
                await _alertService.ArchiveByDelegationAsync(
                    own.Id,
                    HeadAlertKindCatalog.DelegationKinds,
                    "delegation_target_terminal",
                    ct);
        }
    }

}
