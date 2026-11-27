using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Delegations;

/// <summary>
/// تسجيل الإنابة أصولًا من محامي الفرع المناب: إدخال رقم أساس الإنابة وتاريخ قيدها
/// (بيانات الملف المناب) فيُصبح الملف المناب مقيدًا. محامي الملف المناب فقط.
/// </summary>
public sealed record RegisterDocumentDelegationCommand(
    int DelegationId,
    RegisterDelegationRequest Request,
    int UserId,
    string? ActorName) : ICommand<DelegationDto?>;

public sealed class RegisterDocumentDelegationCommandHandler
    : CommandHandlerBase<RegisterDocumentDelegationCommand, DelegationDto?>
{
    private readonly IDelegationRepository _delegations;
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IRepository<DocumentRegistrationDate> _registrationDates;
    private readonly IDocumentNumberRegistry? _numberRegistry;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IHeadAlertService _alerts;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public RegisterDocumentDelegationCommandHandler(
        IDelegationRepository delegations,
        IDocumentRepository documents,
        IUserRepository users,
        IRepository<ExecutionCircuit> circuits,
        IRepository<DocumentRegistrationDate> registrationDates,
        IDocumentNumberRegistry? numberRegistry,
        IHeadAlertOutbox alertOutbox,
        IHeadAlertService alerts,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<RegisterDocumentDelegationCommand, DelegationDto?>> behaviors)
        : base(behaviors)
    {
        _delegations = delegations;
        _documents = documents;
        _users = users;
        _circuits = circuits;
        _registrationDates = registrationDates;
        _numberRegistry = numberRegistry;
        _alertOutbox = alertOutbox;
        _alerts = alerts;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _dbErrors = dbErrors;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<DelegationDto?> HandleCoreAsync(
        RegisterDocumentDelegationCommand command, CancellationToken ct)
    {
        var delegationId = command.DelegationId;
        var request = command.Request;
        var userId = command.UserId;
        var actorName = command.ActorName;
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new ArgumentException("الإنابة غير موجودة");

        if (delegation.Status != DelegationStatusCatalog.Assigned)
            throw new ArgumentException("لا يمكن تسجيل إنابة لم تُعتمد");
        var target = delegation.TargetDocument
            ?? throw new ArgumentException("الملف المناب غير موجود");
        if (target.CreatedById != userId)
            throw new ArgumentException("لا يمكنك تسجيل هذا الملف المناب");

        // N5: إعادة التحقق من حالة المنيب لحظة التسجيل — لو أصابه كتاب تريث فتُرفض التسجيل برسالة E5
        // الخاصة (تريثُ المنيب يحرم تسجيل إنابته لوجوب وقف إجراءاتها)، ولو أصبح منفَّذًا/مشطوبًا
        // بعد التسطير فبرسائل ValidateSource القائمة.
        if (delegation.SourceDocument is { } source)
        {
            if (source.ExecStatus == ExecutionStatusCatalog.Deferred)
                throw new ArgumentException("لا يمكن تسجيل الانابة لورود كتاب تريث في الملف المنيب");
            if (source.ExecStatus == ExecutionStatusCatalog.ReferredToStart)
                throw new ArgumentException("لا يمكن تسجيل الانابة لكون الملف المنيب «محال الى البداية»");
            DocumentDelegationService.ValidateSourceForDelegation(source);
        }

        var fileNumber = DigitNormalizer.NormalizeDigitsOrNull(DocumentDelegationService.Normalize(request.FileNumber));
        if (string.IsNullOrWhiteSpace(fileNumber))
            throw new ArgumentException("رقم أساس الإنابة مطلوب");
        var fileYear = DigitNormalizer.NormalizeDigitsOrNull(DocumentDelegationService.Normalize(request.FileYear));
        if (string.IsNullOrWhiteSpace(fileYear))
            throw new ArgumentException("سنة قيد الإنابة مطلوبة");
        DocumentValidator.ValidateFileYearNotFuture(fileYear, ServerClock.CurrentYear(_clock, _timeZone));
        var registrationDate = FreeDateParser.Parse(request.FileRegistrationDate, "تاريخ قيد الإنابة");
        if (registrationDate is null)
            throw new ArgumentException("تاريخ قيد الإنابة مطلوب");

        // V-18: لا رقم بلا دائرة. الإنابة الخارجية تصل بدائرة الإدخال في الطلب؛
        // الداخلية تستخدم دائرة الإنابة؛ وإن كان للملف دائرة نبقّيها.
        var circuitId = request.ExecutionCircuitId
            ?? delegation.DelegatedCircuitId
            ?? target.ExecutionCircuitId;
        if (target.ExecutionCircuitId is null)
        {
            if (circuitId is null)
            {
                // الإلزام في مسار الإنتاج (سجل الأرقام موجود). بناءات الاختبار القديمة
                // بلا سجل أرقام تُبقي السلوك الانتقالي (رقم بلا دائرة).
                if (_numberRegistry is not null && delegation.IsExternal)
                    throw new ArgumentException("دائرة إدخال رقم الإنابة مطلوبة — لا يمكن تسجيل رقم بلا دائرة");
            }
            else
            {
                var circuit = await _circuits.GetByIdAsync(circuitId.Value, ct)
                    ?? throw new ArgumentException("دائرة إدخال رقم الإنابة غير موجودة");
                if (target.BranchId is not null && circuit.BranchId != target.BranchId)
                    throw new ArgumentException("دائرة إدخال رقم الإنابة ليست ضمن فرع الملف");
                target.ExecutionCircuitId = circuit.Id;
                target.Court = circuit.Name;
                target.CourtNorm = ArabicNameNormalizer.Normalize(circuit.Name);
            }
        }

        // الوحدانية عبر الكاتب الوحيد عند توفره (داخل معاملة التسجيل)؛
        // وإلا الفحص الخدمي القديم للاختبارات المبنية يدويًا.
        if (_numberRegistry is null
            && await _documents.ExistsActiveWithNumberByCircuitAsync(target.Id, target.ExecutionCircuitId, target.CourtNorm, fileNumber, target.FileType, fileYear, ct))
            throw new DocumentConflictException($"رقم الأساس {fileNumber} مكرر في {target.Court} لسنة {fileYear} — تحقق من الدائرة والرقم والنوع");

        // الاستهداف (§7.1 + §7.3): الموجَّهة لشعبة لرئيس شعبته، وإلا مالك
        // دائرة الملف المناب (رئيس شعبته وإلا قسمه).
        int completionOwnerBranch = 0;
        int? completionOwnerId = null;
        var targetBranchId = target.BranchId;
        var hasCompletionBranch = targetBranchId.HasValue;
        if (targetBranchId is not null)
        {
            completionOwnerBranch = targetBranchId.Value;
            if (delegation.RedirectedToSectionId.HasValue)
            {
                completionOwnerId = (await _users.FindActiveHeadAsync(
                        UserRole.SubHead, completionOwnerBranch, delegation.RedirectedToSectionId, ct)
                    ?? await _users.FindActiveHeadAsync(UserRole.Head, completionOwnerBranch, null, ct))?.Id;
            }
            else
            {
                (completionOwnerBranch, completionOwnerId) = await DocumentDelegationRules.ResolvePendingAlertTargetAsync(
                    isExternal: false, externalBranchId: null,
                    sourceBranchId: target.BranchId, circuitId: target.ExecutionCircuitId,
                    _users, _circuits, ct);
            }
        }

        try
        {
            await _tx.RunAsync(async token =>
            {
                // W1/B6: قفل صف **المصدر** (لا الهدف) — ترتيب قفل موحد مع الإنابة/الشطب،
                // ثم قراءة طازجة بلا تتبع وإعادة التحقق (الكيان المتتبَّع قد يكون قديمًا).
                await _documents.GetByIdForUpdateAsync(delegation.SourceDocumentId, token);
                var freshSource = await _documents.GetByIdWithDelegationsNoTrackingAsync(
                    delegation.SourceDocumentId, token)
                    ?? throw new ArgumentException("الملف المنيب غير موجود");
                DocumentDelegationService.ValidateSourceForDelegation(freshSource);
                target.FileNumber = fileNumber;
                target.FileYear = fileYear;
                target.IsDraft = false;
                target.UpdatedAt = DateTime.UtcNow;
                target.Version++;
                await DocumentDelegationRules.ApplyRegistrationDateAsync(target, registrationDate.Value, _registrationDates, token);
                _documents.Update(target);
                if (_numberRegistry is not null)
                    await _numberRegistry.SyncRegistrationAsync(
                        target, userId, actorName, request.WarningToken, token);

                delegation.Status = DelegationStatusCatalog.Registered;
                delegation.UpdatedAt = DateTime.UtcNow;
                // تحوّل الحالة يرفع الرمز — تسجيل مزدوج متزامن يفشل برمز التزامن (409).
                delegation.Version++;
                _delegations.Update(delegation);

                if (hasCompletionBranch)
                {
                    await _alertOutbox.EnqueueSystemAsync(
                        new CreateSystemHeadAlertRequest(
                            HeadAlertKindCatalog.DelegationPendingCompletion,
                            HeadAlertMessages.DelegationPendingCompletion(new(
                                delegation.Id, DocumentDelegationService.SourceLabel(delegation.SourceDocument))),
                            HeadAlertAudience.SingleHead,
                            completionOwnerBranch,
                            userId,
                            DocumentId: target.Id,
                            DelegationId: delegation.Id,
                            RecipientUserId: completionOwnerId),
                        new HeadAlertSupersedePlan(
                            delegation.Id,
                            AppealId: null,
                            HeadAlertKindCatalog.DelegationKinds,
                            "delegation_registered"),
                        occurredAt: DateTime.UtcNow,
                        ct: token);
                }
                else
                {
                    await _alerts.ArchiveByDelegationAsync(
                        delegation.Id,
                        HeadAlertKindCatalog.DelegationKinds,
                        "delegation_registered_no_branch",
                        token);
                }

                await _uow.SaveChangesAsync(token);

                await _audit.LogAsync(actorName, "register_delegation",
                    delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                    $"سجّل إنابة (رقم {delegation.Id}) أصولًا برقم أساس {fileNumber}", token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            // RF-009: شبكة السباق — تعارض القيد عند التسجيل المتزامن يُترجَم 409 وديًا.
            throw new DocumentConflictException($"رقم الأساس {fileNumber} مكرر — أُدخل أثناء الحفظ من مستخدم آخر، أعد المحاولة برقم مختلف", ex);
        }

        return await _delegations.GetByIdWithDetailsAsync(delegation.Id, ct) is { } reloaded
            ? DocumentDelegationService.ToDto(reloaded, reloaded.SourceDocument, ServerClock.CurrentYear(_clock, _timeZone))
            : null;
    }
}

/// <summary>
/// إتمام الإنابة من محامي الملف المناب: بيع الأموال موضوع الإنابة بالمزاد العلني
/// (بدل المبيع لكل أصل بالليرة) وتاريخ إعادة الملف للدائرة المنيبة، فيُصبح الملف المناب
/// «منفذ إنابة» (حالة نهائية تُعامل منفذًا في القوائم والإحصاءات).
/// </summary>
public sealed record CompleteDocumentDelegationCommand(
    int DelegationId,
    CompleteDelegationRequest Request,
    int UserId,
    string? ActorName) : ICommand<DelegationDto?>;

public sealed class CompleteDocumentDelegationCommandHandler
    : CommandHandlerBase<CompleteDocumentDelegationCommand, DelegationDto?>
{
    private readonly IDelegationRepository _delegations;
    private readonly IDocumentRepository _documents;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IHeadAlertService _alerts;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public CompleteDocumentDelegationCommandHandler(
        IDelegationRepository delegations,
        IDocumentRepository documents,
        IRepository<DocumentOccurrence> occurrences,
        IHeadAlertOutbox alertOutbox,
        IHeadAlertService alerts,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<CompleteDocumentDelegationCommand, DelegationDto?>> behaviors)
        : base(behaviors)
    {
        _delegations = delegations;
        _documents = documents;
        _occurrences = occurrences;
        _alertOutbox = alertOutbox;
        _alerts = alerts;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<DelegationDto?> HandleCoreAsync(
        CompleteDocumentDelegationCommand command, CancellationToken ct)
    {
        var delegationId = command.DelegationId;
        var request = command.Request;
        var userId = command.UserId;
        var actorName = command.ActorName;
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new ArgumentException("الإنابة غير موجودة");

        if (delegation.Status != DelegationStatusCatalog.Registered)
            throw new ArgumentException("لا يمكن إتمام إنابة لم تُسجَّل أصولًا");
        var target = delegation.TargetDocument
            ?? throw new ArgumentException("الملف المناب غير موجود");
        if (target.CreatedById != userId)
            throw new ArgumentException("لا يمكنك إتمام هذا الملف المناب");

        // C2/E3: الإتمام «لمتداول فقط» — الملف المناب نفسه يجب أن يكون متداولًا
        // (ExecStatus فارغًا). أي حالة منقولة (تريث/منفذ إنابة/مشطوب/مسترد/…) تمنع الإتمام.
        if (!string.IsNullOrEmpty(target.ExecStatus))
            throw new ArgumentException("لا يمكن تنفيذ انابة في ملف غير متداول");

        // توسيع B (F10): الإتمام لا يقع إلا إذا كان الملف المنيب «متداولًا أو منفذًا جزئيًا»
        // (يحفظ N1 وتعدد الإنابات). المصدر المتريث/المنفذ/المسترد/المشطوب/المنفذ-إنابة يمنع
        // الإتمام بإعادة E3 نفسها — لا يُتمّ إنابة متداخلة على مناب منتهٍ.
        if (delegation.SourceDocument is { } sourceDoc)
        {
            var sourceTrading = string.IsNullOrEmpty(sourceDoc.ExecStatus)
                || (sourceDoc.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                    && sourceDoc.ExecSubStatus == ExecutionStatusCatalog.SubPartiallyExecuted);
            if (!sourceTrading)
                throw new ArgumentException("لا يمكن تنفيذ انابة في ملف غير متداول");
        }

        var returnDate = FreeDateParser.Parse(request.ReturnDate, "تاريخ إعادة الملف للدائرة المنيبة");
        if (returnDate is null)
            throw new ArgumentException("تاريخ إعادة الملف للدائرة المنيبة مطلوب");

        // «تاريخ قرار الإحالة القطعية»: يدخله محامي الملف المناب مع تاريخ الإعادة وبدل المبيع
        // (إلزامي)، ويُحفظ على الملف المنيب عند تفعيله «منفذ جبريا» (نص حر مُوحَّد الأرقام).
        var forcedExecutionDateRaw = (request.ForcedExecutionDate ?? string.Empty).Trim();
        if (FreeDateParser.Parse(forcedExecutionDateRaw, "تاريخ قرار الإحالة القطعية") is null)
            throw new ArgumentException("تاريخ قرار الإحالة القطعية مطلوب");
        var forcedExecutionDate = ArabicDigitNormalizer.Normalize(forcedExecutionDateRaw);

        var sales = request.Sales ?? new List<DelegationSaleDto>();
        var salesByAssetId = sales.ToDictionary(s => s.DelegationAssetId);
        // RF-017 (INT-005): التقاط البدلات قبل الكتابة لتوثيق قبل/بعد في تفاصيل التدقيق.
        var oldPrices = delegation.Assets.ToDictionary(a => a.Id, a => a.SalePrice);
        foreach (var asset in delegation.Assets)
        {
            if (!salesByAssetId.TryGetValue(asset.Id, out var sale))
                throw new ArgumentException($"يجب إدخال بدل المبيع لكل أصل موضوع الإنابة ({asset.AssetLabel})");
            if (sale.SalePrice <= 0)
                throw new ArgumentException($"بدل المبيع غير صالح للأصل ({asset.AssetLabel})");
            DocumentAmountPolicy.EnsureValid(sale.SalePrice, $"بدل المبيع ({asset.AssetLabel})");
            asset.SalePrice = sale.SalePrice;
        }

        if (request.SaleCoversFullDebt is null)
            throw new ArgumentException("يجب تحديد ما إذا كان بدل المبيع غطى كامل المديونية");

        var sourceBranchId = delegation.SourceDocument.BranchId;

        await _tx.RunAsync(async token =>
        {
            // W1/B6: قفل المصدر والهدف بترتيب معرّف تصاعدي حتمي لمنع دورة قفل مع الشطب.
            var lockIds = new[] { delegation.SourceDocumentId, delegation.TargetDocument?.Id ?? 0 }
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id);
            foreach (var lockId in lockIds)
                await _documents.GetByIdForUpdateAsync(lockId, token);
            delegation.ReturnDate = returnDate;
            delegation.Status = DelegationStatusCatalog.Executed;
            delegation.SaleCoversFullDebt = request.SaleCoversFullDebt.Value;
            delegation.UpdatedAt = DateTime.UtcNow;
            // تحوّل الحالة يرفع الرمز — إتمام مزدوج متزامن يفشل برمز التزامن (409).
            delegation.Version++;
            _delegations.Update(delegation);

            // الملف المناب يُصبح «منفذ إنابة»: حالة نهائية تُعامل منفذًا في القوائم والإحصاءات.
            target.ExecStatus = ExecutionStatusCatalog.DelegationExecuted;
            target.UpdatedAt = DateTime.UtcNow;
            _documents.Update(target);

            // قاعدة «يسري على المنيب»: يُفعَّل المنيب تلقائيًا «منفذ جبريا (منفذ جزئيا)» فيُحتسب
            // متداولًا في القوائم والإحصاءات، وعلى محامي المنيب لاحقًا «اعتبار الملف منفذًا
            // كاملًا بهذا البيع» (تاريخ تحويل البدل ورقم الإشعار) من نافذة تغيير الحالة — وحينها
            // فقط يدخل مبلغ الإنابة ضمن «إحصاءات منفذ جبريا» مرة واحدة (مسار DelegationSalesAmount).
            var source = delegation.SourceDocument;
            var markApplied = false;
            if (source is not null)
            {
                var alreadyMarked = source.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                    && source.ExecSubStatus == ExecutionStatusCatalog.SubPartiallyExecuted;
                if (!alreadyMarked)
                {
                    source.ExecStatus = ExecutionStatusCatalog.ExecutedForcibly;
                    source.ExecSubStatus = ExecutionStatusCatalog.SubPartiallyExecuted;
                    source.ForcedExecutionDate = forcedExecutionDate;
                    DocumentDelegationService.ClearTarithAndSayerFields(source);
                    source.UpdatedAt = DateTime.UtcNow;
                    _documents.Update(source);
                    markApplied = true;
                }
            }

            if (sourceBranchId is not null)
            {
                var targetLabel = DocumentDelegationService.TargetFileLabel(target, ServerClock.CurrentYear(_clock, _timeZone));
                var assetsLine = string.Join(" و", delegation.Assets.Select(a => $"للتنفيذ على {a.AssetLabel}"));
                var coverageText = delegation.SaleCoversFullDebt == true
                    ? "غطى كامل المديونية"
                    : delegation.SaleCoversFullDebt == false
                        ? "لم يغطِ كامل المديونية"
                        : "—";
                var court = DocumentDelegationService.TargetCourt(target, delegation.DelegatedCourt) ?? "الدائرة المنابة";

                await _alertOutbox.EnqueueSystemAsync(
                    new CreateSystemHeadAlertRequest(
                        HeadAlertKindCatalog.DelegationCompletedReturned,
                        HeadAlertMessages.DelegationCompletedReturned(new(
                            DocumentDelegationService.AlertCourtAfterDaira(court), DocumentDelegationService.SourceLabel(delegation.SourceDocument),
                            coverageText, targetLabel, assetsLine)),
                        HeadAlertAudience.DocumentCreator,
                        sourceBranchId.Value,
                        userId,
                        DocumentId: delegation.SourceDocumentId,
                        DelegationId: delegation.Id),
                    new HeadAlertSupersedePlan(
                        delegation.Id,
                        AppealId: null,
                        HeadAlertKindCatalog.DelegationKinds,
                        "delegation_completed"),
                    occurredAt: DateTime.UtcNow,
                    ct: token);
            }
            else
            {
                await _alerts.ArchiveByDelegationAsync(
                    delegation.Id,
                    HeadAlertKindCatalog.DelegationKinds,
                    "delegation_completed_no_branch",
                    token);
            }

            await _uow.SaveChangesAsync(token);

            // وقعة تغيير حالة المنيب (تفعيل تلقائي «منفذ جبريا — منفذ جزئيا») ضمن المعاملة نفسها،
            // لتُسجَّل في «وقوعات الملف» كأي وقعة «منفذ جبريا» (تُسجَّل مرة واحدة لكل تفعيل).
            if (markApplied && source is not null)
            {
                await _occurrences.AddAsync(new DocumentOccurrence
                {
                    DocumentId = source.Id,
                    Source = OccurrenceSourceCatalog.System,
                    OccurrenceType = OccurrenceTypeCatalog.Forcible,
                    EventDate = DateTime.UtcNow,
                    Details = DocumentDelegationService.SerializeDetails(new Dictionary<string, string>
                    {
                        ["execSubStatus"] = ExecutionStatusCatalog.SubPartiallyExecuted,
                        ["forcedExecutionDate"] = source.ForcedExecutionDate ?? string.Empty,
                    }),
                    CreatedById = source.CreatedById,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                }, token);
                await _uow.SaveChangesAsync(token);
            }

            await _audit.LogAsync(actorName, "complete_delegation",
                delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                $"أتم إنابة (رقم {delegation.Id}): بيع الأموال موضوع الإنابة وإعادة الملف للدائرة المنيبة — اعتُبر الملف المنيب «منفذ جبريا (منفذ جزئيا)» تلقائيًا حتى اعتباره منفذًا كاملًا بهذا البيع — بدلات المبيع: {DocumentDelegationService.FormatSalePrices(delegation, oldPrices)} — التغطية: {(request.SaleCoversFullDebt.Value ? "غطّى كامل المديونية" : "لم يغطِّ كامل المديونية")}", token);
        }, ct);

        return await _delegations.GetByIdWithDetailsAsync(delegation.Id, ct) is { } reloaded
            ? DocumentDelegationService.ToDto(reloaded, reloaded.SourceDocument, ServerClock.CurrentYear(_clock, _timeZone))
            : null;
    }
}
