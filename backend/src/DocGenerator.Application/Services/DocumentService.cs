using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IDocumentService
{
    Task<DocumentResponse?> GetAsync(int documentId, CancellationToken ct = default);
    Task<DocumentResponse?> GetDeletedAsync(int documentId, CancellationToken ct = default);
    Task<DocumentResponse> CreateAsync(DocumentUpsertRequest request, int userId, string? actorName, int? branchId, CancellationToken ct = default, string? idempotencyKey = null);
    Task<DocumentResponse?> UpdateAsync(int documentId, DocumentUpsertRequest request, string? actorName, int? userId = null, CancellationToken ct = default);
    Task<bool> DeleteAsync(int documentId, string? actorName, CancellationToken ct = default);
    Task<bool> RestoreAsync(int documentId, string? actorName, CancellationToken ct = default);
    Task<DocumentResponse> TransferAsync(int documentId, int targetLawyerId, string? actorName, CancellationToken ct = default);
    /// <summary>عدد ملفات المحامي غير المحذوفة (للمعاينة قبل النقل الجماعي).</summary>
    Task<int> CountFilesByOwnerAsync(int ownerId, int? scopeBranchId, CancellationToken ct = default, int? ownerSectionId = null);
    /// <summary>
    /// نقل كامل ملفات محامٍ إلى محامٍ آخر بجميع الحالات — رئيس القسم (ضمن فرعه) فقط.
    /// scopeBranchId يُقيّد النطاق بفرع رئيس القسم ويُرجع عدد الملفات المنقولة.
    /// </summary>
    Task<int> TransferAllAsync(int sourceLawyerId, int targetLawyerId, int? scopeBranchId, string? actorName, CancellationToken ct = default, string? idempotencyKey = null, int? ownerSectionId = null);
    Task IncrementViewCountAsync(int documentId, CancellationToken ct = default);
    /// <summary>جدول تدوير أرقام الأساس للمحامي (ترحّلي): ملفاته المؤهلة مع رقم أساس السنة الحالية إن وُجد.</summary>
    Task<PagedResult<RotationDocumentDto>> GetRotationListAsync(int userId, int page, int perPage, CancellationToken ct = default);
    /// <summary>
    /// تاريخ أرقام الأساس للملف (سنة + رقم) مرتبًا تنازليًا بالسنوات — لعرضه عند الضغط على رقم الملف.
    /// </summary>
    Task<List<BaseNumberHistoryDto>> GetBaseNumberHistoryAsync(int documentId, CancellationToken ct = default);
    /// <summary>
    /// حفظ أرقام أساس السنة الحالية ذرّيًا: رقم جديد يُنشأ، موجود يُحدَّث، فارغ يحذف سجل
    /// السنة الحالية. يتحقق من ملكية المحامي وأهلية الملف (مقيد وغير منفَّذ وغير محذوف).
    /// </summary>
    Task SaveBaseNumbersAsync(int userId, List<BaseNumberEntry> entries, string? actorName, CancellationToken ct = default);
}

public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IRepository<DocumentBaseNumber> _baseNumbers;
    private readonly IRepository<DocumentRegistrationDate> _registrationDates;
    private readonly IAppealRepository _appeals;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IExecutionActionService _actionService;
    private readonly IDocumentDelegationService _delegationService;
    private readonly IDocumentStatusService _statusService;
    private readonly IDocumentWriteGuard _writeGuard;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IIdempotencyStore? _idempotency;
    private readonly IRepository<ExecutionCircuit> _circuits;

    public DocumentService(
        IDocumentRepository documents,
        IUserRepository users,
        IRepository<DocumentBaseNumber> baseNumbers,
        IRepository<DocumentRegistrationDate> registrationDates,
        IAppealRepository appeals,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IExecutionActionService actionService,
        IDocumentDelegationService delegationService,
        IDocumentStatusService statusService,
        IDocumentWriteGuard writeGuard,
        IIdempotencyStore? idempotency = null,
        IRepository<ExecutionCircuit>? circuits = null)
    {
        _circuits = circuits ?? new UnconfiguredCircuitRepository();
        _documents = documents;
        _users = users;
        _baseNumbers = baseNumbers;
        _registrationDates = registrationDates;
        _appeals = appeals;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _actionService = actionService;
        _delegationService = delegationService;
        _statusService = statusService;
        _writeGuard = writeGuard;
        _clock = clock;
        _timeZone = timeZone;
        _idempotency = idempotency;
    }

    public async Task<DocumentResponse?> GetAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        return doc is null ? null : DocumentResponse.FromEntity(doc, ServerClock.CurrentYear(_clock, _timeZone));
    }

    public async Task<DocumentResponse?> GetDeletedAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetDeletedByIdAsync(documentId, ct);
        return doc is null || !doc.IsDeleted ? null : DocumentResponse.FromEntity(doc, ServerClock.CurrentYear(_clock, _timeZone));
    }

    public async Task<DocumentResponse> CreateAsync(DocumentUpsertRequest request, int userId, string? actorName, int? branchId, CancellationToken ct = default, string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(request.BorrowerName)
            && (request.GeneralEntitySide == GeneralEntitySideCatalog.Applicant
                || string.IsNullOrEmpty(request.GeneralEntitySide)))
            throw new ArgumentException("اسم المقترض مطلوب");

        DocumentValidator.NormalizeCollections(request);
        DocumentValidator.ValidateSide(request);
        DocumentValidator.ValidateExecutedRequest(request);
        DocumentValidator.ValidateGuarantorCount(request);
        DocumentValidator.ValidateRegistrationDate(request);

        // المحامي المختص هو الذي سجّل الدخول وأنشأ الملف: يُسنَد الملف إليه تلقائياً
        // باسمه الكامل (وليس من الطلب)، ويبقى محصّناً من أي كتابة عبر نموذج التعديل.
        var actor = await _users.GetByIdAsync(userId, ct);
        if (actor is null)
            throw new ArgumentException("المستخدم الذي ينشئ الملف غير موجود");

        var doc = new Document
        {
            BranchId = branchId,
            CreatedById = userId,
            Lawyer = string.IsNullOrWhiteSpace(actor.FullName) ? actor.Username : actor.FullName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        ApplyRequest(doc, request);
        // سجل الدوائر: اشتقاق الدائرة (إلزامية) قبل المشتقات — يتجاهل نص العميل.
        var createCircuit = await _writeGuard.ResolveCircuitForWriteAsync(request, branchId, isExternalTarget: false, ct);
        DocumentText.ApplyCircuitDerivation(doc, createCircuit, request);
        DocumentText.FillDerivedFields(doc);
        ApplyRegistrationDate(doc, request.FileRegistrationDate);

        // RF-011: حجز المفتاح بعد كل التحققات (المدخل الباطل 400 بلا حجز) وقبل أي
        // كتابة — التكرار بنفس البصمة يُعيد المخزن (`IdempotentReplayException`)،
        // والقيد المعالَج يُرفَض `409`، والبصمة المختلفة نية جديدة تُنفَّذ طازجة.
        var ticket = await IdempotencyGuard.BeginAsync(_idempotency, "documents.create",
            userId, idempotencyKey, IdempotencyGuard.Fingerprint(request), ct);

        DocumentResponse result;
        try
        {
            result = await CreateCoreAsync(doc, request, userId, actor, actorName, ct);
        }
        catch
        {
            await IdempotencyGuard.ReleaseAsync(_idempotency, ticket, ct);
            throw;
        }

        await IdempotencyGuard.CompleteAsync(_idempotency, ticket, IdempotencyGuard.Snapshot(result), ct);
        return result;
    }

    /// <summary>نواة الإنشاء (فحص الترقيم + المعاملة) — تُستدعى بعد حجز مفتاح عدم التكرار.</summary>
    private async Task<DocumentResponse> CreateCoreAsync(
        Document doc, DocumentUpsertRequest request, int userId, User actor, string? actorName, CancellationToken ct)
    {
        // RF-009 بتوقيع الدائرة (البند 28/34): مطابقة FK مباشرة بدل النص.
        await _writeGuard.EnsureNumberUniqueByCircuitAsync(null, doc.ExecutionCircuitId, doc.CourtNorm,
            doc.FileNumber, doc.FileType, doc.FileYear, ct);

        return await _writeGuard.WithNumberGuardAsync(() => _tx.RunAsync(async token =>
        {
            await _documents.AddAsync(doc, token);
            await _uow.SaveChangesAsync(token);
            // أول سجل تعاقب: منشئ الملف (Lawyer هو اسم المنشئ عند الإنشاء).
            await _documents.AddAssignmentAsync(doc.Id, AssignmentKindCatalog.Create,
                doc.Lawyer ?? actor.Username, null, DateTime.UtcNow, token);
            await _audit.LogAsync(actorName, "create", doc.Id, doc.DocumentType,
                DocumentText.AuditWithActor($"أنشأ المستند (رقم {doc.Id})", doc), token);
            // ملف أُنشئ مشطوبًا من البداية: يُسجَّل وقعة شطب تلقائيًا ليتسق سجل الوقوعات
            // مع البيانات المُرحَّلة (بعد حفظ المستند فيُعرف رقمه داخل المعاملة نفسها).
            if (doc.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
                await _statusService.AddStruckOffOccurrenceAsync(doc, userId, token);
            await _uow.SaveChangesAsync(token);
            await _actionService.SeedInitialActionsAsync(doc, request.InitialActions, userId, actorName, token);
            return DocumentResponse.FromEntity(doc, CurrentYear());
        }, ct), doc.FileNumber);
    }

    public async Task<DocumentResponse?> UpdateAsync(int documentId, DocumentUpsertRequest request, string? actorName, int? userId = null, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return null;

        DocumentValidator.NormalizeCollections(request);
        DocumentValidator.ValidateSide(request);
        DocumentValidator.ValidateExecutedRequest(request);
        DocumentValidator.ValidateGuarantorCount(request);
        DocumentValidator.ValidateRegistrationDate(request);

        // لقطة ما قبل التعديل — لتوليد صفوف «حقل/قبل/بعد» في سجل التعديلات.
        var before = Common.Audit.DocumentChangeTracker.Capture(doc);

        // التجديد عند تعديل ملف مشطوب إلى متداول: يُطبَّق بيان التجديد (رقم الملف الجديد إلزامي)
        // فيعود الملف برقمه ونوعه الجديدين لسنة الإعادة. التعديل دون تغيير الحالة لا يُجدَّد.
        var wasStruckOff = ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus);

        // حارس انتقالات وضع «منفذ عليه» (§4.6): يطابق قواعد نافذة تغيير الحالة، فلا تنتقل
        // الحالة عبر نموذج التعديل إلا بما تسمح به النافذة (رحّلات الشطب والتجديد الشرعية).
        ValidateExecutedTransitionOnEdit(doc, request);

        // حارس تعديل الملف المناب (قرار 2: «الملف المناب مرآةً للمنيب»): يرفض الفروق القيمية
        // في الحقول المقفولة، ويرصد الإضافات المحلية المسموحة (وريث جديد/ممثل جديد) لتنبيه
        // منيب الملف بها بعد نجاح الحفظ.
        var targetEditReport = _delegationService.ValidateTargetEdit(doc, request);

        // قفل «الحجز والأصل بعد التسطير» على الملف المنيب (قرار المستخدم): تاريخ القاء
        // الحجز على أصلٍ تحجبه إنابة سارية يُعدَّل فقط ولا يُحذف، وحذف صف الأصل نفسه
        // مرفوض — قبل أي تغيير (ApplyRequest يستبدل الأصول بالكامل).
        await _delegationService.ValidateSeizureLockAsync(doc, request, ct);

        // سجل الدوائر: تبديل الدائرة من التعديل مسموح (فحص 409) — يُحفظ القديم للوقوعتين.
        // إن لم يُرسل تغيير دائرة (لا معرف ولا نص)، يُبقى الحالي بلا إعادة حل (توافق الطلبات الجزئية).
        var oldCircuitId = doc.ExecutionCircuitId;
        var oldCircuitName = doc.Court;
        var oldCourtNorm = doc.CourtNorm;
        var wasPending = doc.NeedsRegistration;

        ApplyRequest(doc, request);
        // سجل الدوائر: تبديل الدائرة من التعديل مسموح (فحص 409) — يُحفظ القديم للوقوعتين.
        // القواعد: (1) بلا معرف ولا نص → إبقاء الحالي (توافق الطلبات الجزئية)؛
        // (2) نفس المعرف → إبقاء (قاعدة الجد: ملف الدائرة المعطلة يُعدَّل ويبقى عليها)؛
        // (3) سواهما → حل صريح بالمعرف أو بالنص (خارج السجل = 400، لا تجاهل صامت).
        var idProvided = request.ExecutionCircuitId is not null;
        var textProvided = !string.IsNullOrWhiteSpace(request.Court);
        if ((!idProvided && !textProvided) || (idProvided && request.ExecutionCircuitId == oldCircuitId))
        {
            doc.ExecutionCircuitId = oldCircuitId;
            doc.Court = oldCircuitName;
            doc.CourtNorm = oldCourtNorm;
        }
        else
        {
            var editCircuit = await _writeGuard.ResolveCircuitForWriteAsync(request, doc.BranchId, isExternalTarget: false, ct);
            DocumentText.ApplyCircuitDerivation(doc, editCircuit, request);
        }
        // تعديل ملف «بانتظار إعادة القيد» برقم غير فارغ يُسقط الـ flag (مع إعادة تحقق الوحدانية).
        if (wasPending && !string.IsNullOrWhiteSpace(doc.FileNumber) && !string.IsNullOrWhiteSpace(doc.FileYear))
            doc.NeedsRegistration = false;
        DocumentText.FillDerivedFields(doc);
        ApplyRegistrationDate(doc, request.FileRegistrationDate);
        doc.UpdatedAt = DateTime.UtcNow;

        // RF-009 بتوقيع الدائرة: فحص التكرار بعد تطبيق الطلب.
        await _writeGuard.EnsureNumberUniqueByCircuitAsync(documentId, doc.ExecutionCircuitId, doc.CourtNorm,
            doc.FileNumber, doc.FileType, doc.FileYear, ct);

        // RF-010: فحص التزامن المبكر (409 ودي) + زيادة العدّاد وشبكة السباق.
        _writeGuard.EnsureConcurrency(doc, request.Version);

        List<MirrorNotification> mirrorNotifications = [];
        var circuitSwitched = oldCircuitId != doc.ExecutionCircuitId;
        var result = await _writeGuard.WithConcurrencyGuardAsync(() => _writeGuard.WithNumberGuardAsync(() => _tx.RunAsync(async token =>
        {
            if (wasStruckOff && doc.ExecutedStatus == ExecutedStatusCatalog.None)
                await _statusService.ApplyRenewalAsync(doc, request, true, userId, token);
            else if (!wasStruckOff && ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus))
                await _statusService.AddStruckOffOccurrenceAsync(doc, userId, token);
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            // تبديل الدائرة من التعديل يسجل الوقوعتين تلقائيًا (قديم/جديد) — وإلا قفزت بلا أثر.
            if (circuitSwitched)
                await _writeGuard.AddCircuitSwitchOccurrencesAsync(doc, oldCircuitName, userId, token);
            await _delegationService.SyncSnapshotsAsync(doc, token);
            // المرآة: نسخ تغييرات المنيب إلى مناباته المعلقة (داخل المعاملة نفسها) وإعادة اشتقاق
            // الحقول المحسوبة فيها — تعيد إشعارات تُطلق بعد نجاح المعاملة.
            mirrorNotifications = await _delegationService.MirrorSourceEditsAsync(doc, token);
            await _writeGuard.LogDocumentChangesAsync(before, doc, actorName, "update",
                $"عدّل المستند (رقم {doc.Id})", token);
            await _actionService.SeedInitialActionsAsync(doc, request.InitialActions, userId, actorName, token);
            return DocumentResponse.FromEntity(doc, CurrentYear());
        }, ct), doc.FileNumber));

        // تُطلق التنبيهات بعد نجاح المعاملة ولا تفشل الحفظ أبدًا:
        // (1) تنبيهات المرآة إلى المحامين المنابين بتحديث نسخة الملف،
        // (2) تنبيه المنيب بالإضافات المحلية المضافة على الملف المناب (ورثة/ممثل شرعي).
        await _delegationService.FireMirrorNotificationsAsync(mirrorNotifications, ct);
        if (targetEditReport.HasAdditions)
            await _delegationService.FireTargetEditReportAsync(doc, targetEditReport, ct);

        return result;
    }

    /// <summary>
    /// حارس انتقالات وضع «منفذ عليه/عرض وايداع» في نموذج التعديل (§4.6): لا تمر التغييرات
    /// المباشرة عبر التحرير إلا ما تسمح به نافذة تغيير الحالة — الشطبَ والتجديدَ الشرعيين.
    /// (مشطوب → منفذ مباشر ممنوع؛ «منفذ» في «منفذ عليها» نهائية؛ «عرض وايداع» من منفذ
    /// لا تُشطب ولا تُعاد مباشرة بل عبر نافذة الحالة بكتاب السير.) — معزول عن نظام «طالبة
    /// تنفيذ» الذي تحكمه آلة حالات مغلقة منفصلة (لا يسند UpdateAsync الحالةَ إطلاقًا).
    /// </summary>
    private static void ValidateExecutedTransitionOnEdit(Document doc, DocumentUpsertRequest request)
    {
        if (!GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            return;

        var currentStatus = doc.ExecutedStatus;
        var current = currentStatus is not null && ExecutedStatusCatalog.IsStored(currentStatus)
            ? currentStatus
            : ExecutedStatusCatalog.None;
        var normalized = request.ExecutedStatus?.Trim();
        var requested = string.IsNullOrEmpty(normalized) ? ExecutedStatusCatalog.None : normalized;

        if (current == ExecutedStatusCatalog.StruckOff && requested == ExecutedStatusCatalog.Executed)
            throw new ArgumentException("لا يمكن نقل ملف مشطوب إلى «منفذ» مباشرة — يجب إعادته أولًا إلى المتداول (تجديد)");
        if (current != ExecutedStatusCatalog.Executed || requested == ExecutedStatusCatalog.Executed)
            return;

        if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed)
            throw new ArgumentException("وضع «منفذ» في صفة «الجهة العامة منفذ عليها» نهائي لا يُغيَّر بالتحرير");
        if (requested == ExecutedStatusCatalog.StruckOff)
            throw new ArgumentException("لا يمكن شطب «عرض وايداع» من وضع «منفذ»");
        throw new ArgumentException("إرجاع «عرض وايداع» من وضع «منفذ» إلى المتداول يتم عبر نافذة تغيير الحالة بكتاب السير بالملف لا عبر التحرير");
    }

    public async Task<bool> DeleteAsync(int documentId, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;

        // منع حذف ملفٍ مرتبط بإنابة (صادرة عنه أو مناب عنها — بأي حالة) أو باستئناف
        // (بأي حالة: منظور/محسوم/مشطوب): الحذف المنطقي يُخفي الملف عن القوائم بينما تبقى
        // الإنابة/الاستئناف تشير إليه، فيُيتّم سجلات حية. تحقق قبل تغيير وخارج المعاملة كسائر الحراس.
        // (doc.Delegations وSourceDelegationId محمّلتان في GetByIdAsync — صفر استعلامات إضافية.)
        if (doc.Delegations.Count > 0)
            throw new ArgumentException("لا يمكن حذف الملف المنيب لوجود إنابة صادرة عنه");
        if (doc.SourceDelegationId is not null)
            throw new ArgumentException("لا يمكن حذف الملف المناب لوجود إنابة مرتبطة به");
        if ((await _appeals.ListByDocumentAsync(doc.Id, ct)).Count > 0)
            throw new ArgumentException("لا يمكن حذف الملف لوجود استئناف مرتبط به");

        return await _tx.RunAsync(async token =>
        {
            doc.IsDeleted = true;
            doc.DeletedAt = DateTime.UtcNow;
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "delete", documentId, doc.DocumentType,
                DocumentText.AuditWithActor($"حذف المستند (رقم {documentId})", doc), token);
            return true;
        }, ct);
    }

    public async Task<bool> RestoreAsync(int documentId, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetDeletedByIdAsync(documentId, ct);
        if (doc is null || !doc.IsDeleted)
            return false;

        // RF-009 (INT-008): الاستعادة تفحص تعارض المفتاح الفعّال (قد أُعيد استعمال رقمه
        // أثناء حذفه) — 409 بدل تكرار ظاهري. بتوقيع الدائرة (البند 34).
        // مسار إفراغ المحذوفة منطقيًا الوحيد: استعادة ثم إحالة — Restore يفحص بالرقم الجديد.
        var currentYear = CurrentYear();
        await _writeGuard.EnsureNumberUniqueByCircuitAsync(documentId, doc.ExecutionCircuitId, doc.CourtNorm,
            Common.EffectiveFileIdentity.Number(doc, currentYear), doc.FileType,
            Common.EffectiveFileIdentity.Year(doc, currentYear), ct);

        return await _writeGuard.WithNumberGuardAsync(() => _tx.RunAsync(async token =>
        {
            // الاستعادة تعيد اشتقاق الدائرة من السجل (M3): التسمية الجماعية أثناء
            // الحذف المنطقي لا تمسّ المحذوف، فتُزامَن هنا وإلا عُرض الاسم القديم.
            if (doc.ExecutionCircuitId is not null)
            {
                var circuit = await _circuits.GetByIdAsync(doc.ExecutionCircuitId.Value, token);
                if (circuit is not null)
                {
                    doc.Court = circuit.Name;
                    doc.CourtNorm = ArabicNameNormalizer.Normalize(circuit.Name);
                    doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                    doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
                }
            }
            doc.IsDeleted = false;
            doc.DeletedAt = null;
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "restore", documentId, doc.DocumentType,
                DocumentText.AuditWithActor($"استعادة المستند (رقم {documentId})", doc), token);
            return true;
        }, ct), doc.FileNumber);
    }

    public async Task<DocumentResponse> TransferAsync(int documentId, int targetLawyerId, string? actorName, CancellationToken ct = default)
    {
        return await _tx.RunAsync(async token =>
        {
            // كل التحققات داخل المعاملة نفسها لتفادي أي TOCTOU: حالة الهدف (وجود/دور/تفعيل)
            // تُقرأ في نفس لقطة المعاملة التي يُنفَّذ فيها النقل الذرّي، فلو عُطّل المحامي
            // أو تغيّرت بياناته لحظياً أثناء النقل لن يُنقل الملف إليه.
            var target = await _users.GetByIdAsync(targetLawyerId, token);
            if (target is null || target.Role != UserRole.Lawyer)
                throw new ArgumentException("المحامي المستهدف غير موجود");
            if (!target.IsActive)
                throw new ArgumentException("المحامي المستهدف غير مفعل");

            var doc = await _documents.GetByIdAsync(documentId, token);
            if (doc is null)
                throw new KeyNotFoundException();

            if (doc.CreatedById == target.Id)
                throw new ArgumentException("لا يمكن نقل الملف إلى المحامي المختص به حاليًا");
            if (target.BranchId != doc.BranchId)
                throw new ArgumentException("لا يمكن نقل الملف إلى محامٍ من فرع آخر");

            var transferred = await _documents.TransferOwnerAsync(
                documentId, doc.CreatedById, target.Id, target.FullName,
                doc.Lawyer ?? doc.CreatedBy?.FullName ?? string.Empty, token);
            if (transferred is null)
                throw new DocumentConflictException("تغيّر المحامي المختص للملف أثناء النقل — أعد المحاولة");

            // سجل تعاقب: الملف أُحيل إلى المحامي المستهدف بتاريخ النقل.
            await _documents.AddAssignmentAsync(documentId, AssignmentKindCatalog.Transfer,
                target.FullName, actorName, DateTime.UtcNow, token);

            await _audit.LogAsync(actorName, "transfer", documentId, doc.DocumentType,
                DocumentText.AuditWithActor($"نقل الملف إلى المحامي: {target.FullName}", doc), token);

            return DocumentResponse.FromEntity(transferred, CurrentYear());
        }, ct);
    }

    public async Task<int> CountFilesByOwnerAsync(int ownerId, int? scopeBranchId, CancellationToken ct = default, int? ownerSectionId = null)
    {
        var owner = await _users.GetByIdAsync(ownerId, ct);
        if (owner is null || owner.Role != UserRole.Lawyer)
            throw new ArgumentException("المحامي غير موجود");
        if (scopeBranchId.HasValue && owner.BranchId != scopeBranchId)
            throw new ArgumentException("لا يمكن عرض ملفات محامٍ من فرع آخر");
        // المعاينة بالنطاق نفسه (§5.5): تطابق المنقول فعلًا.
        return await _documents.CountByOwnerInScopeAsync(ownerId, scopeBranchId, ownerSectionId, ct);
    }

    public async Task<int> TransferAllAsync(int sourceLawyerId, int targetLawyerId, int? scopeBranchId, string? actorName, CancellationToken ct = default, string? idempotencyKey = null, int? ownerSectionId = null)
    {
        // RF-011: حجز المفتاح أولًا (لا تحقق مسبق يستحق `400` هنا — التحققات داخل
        // المعاملة عمدًا ضد `TOCTOU`) — النطاق محامي المصدر (النية = نقل ملفاته) —
        // التكرار يُعيد العدد المخزن نفسه.
        var ticket = await IdempotencyGuard.BeginAsync(_idempotency, "documents.transfer-all",
            sourceLawyerId, idempotencyKey,
            IdempotencyGuard.Fingerprint(new { sourceLawyerId, targetLawyerId, scopeBranchId, ownerSectionId }), ct);

        int transferred;
        try
        {
            transferred = await TransferAllCoreAsync(sourceLawyerId, targetLawyerId, scopeBranchId, ownerSectionId, actorName, ct);
        }
        catch
        {
            await IdempotencyGuard.ReleaseAsync(_idempotency, ticket, ct);
            throw;
        }

        // اللقطة بشكل الاستجابة نفسه (`{"transferredCount":N}`) ليُعاد حرفيًا عند التكرار.
        await IdempotencyGuard.CompleteAsync(_idempotency, ticket,
            IdempotencyGuard.Snapshot(new { transferredCount = transferred }), ct);
        return transferred;
    }

    /// <summary>نواة النقل الجماعي (تحققات + نقل + تدقيق) — تُستدعى بعد حجز مفتاح عدم التكرار.</summary>
    private async Task<int> TransferAllCoreAsync(
        int sourceLawyerId, int targetLawyerId, int? scopeBranchId, int? ownerSectionId, string? actorName, CancellationToken ct)
    {
        return await _tx.RunAsync(async token =>
        {
            // كل التحققات داخل المعاملة نفسها لتفادي أي TOCTOU: حالة المصدر والهدف
            // (وجود/دور/تفعيل/فرع) تُقرأ في نفس لقطة المعاملة التي يُنفَّذ فيها النقل،
            // ويُتحقق من تطابق عدد الملفات المتوقعة مع الصفوف المتأثرة فعلياً.
            var source = await _users.GetByIdAsync(sourceLawyerId, token);
            if (source is null || source.Role != UserRole.Lawyer)
                throw new ArgumentException("المحامي المطلوب نقل ملفاته غير موجود");
            if (source.Id == targetLawyerId)
                throw new ArgumentException("لا يمكن نقل الملفات إلى المحامي صاحبها");
            if (scopeBranchId.HasValue && source.BranchId != scopeBranchId)
                throw new ArgumentException("لا يمكن نقل ملفات محامٍ من فرع آخر");

            var target = await _users.GetByIdAsync(targetLawyerId, token);
            if (target is null || target.Role != UserRole.Lawyer)
                throw new ArgumentException("المحامي المستهدف غير موجود");
            if (!target.IsActive)
                throw new ArgumentException("المحامي المستهدف غير مفعل");
            if (source.BranchId != target.BranchId)
                throw new ArgumentException("لا يمكن نقل الملفات إلى محامٍ من فرع آخر");

            var files = await _documents.ListByOwnerInScopeAsync(sourceLawyerId, scopeBranchId, ownerSectionId, token);
            if (files.Count == 0)
                return 0;

            var moved = await _documents.TransferAllOwnerAsync(sourceLawyerId, target.Id, target.FullName, source.FullName, scopeBranchId, ownerSectionId, token);
            if (moved != files.Count)
                throw new DocumentConflictException("تغيّرت بيانات الملفات أثناء النقل الجماعي — أعد المحاولة");

            var now = ServerClock.Now(_clock, _timeZone);
            foreach (var file in files)
            {
                // سجل تعاقب لكل ملف أُحيل إلى المحامي المستهدف.
                await _documents.AddAssignmentAsync(file.Id, AssignmentKindCatalog.Transfer,
                    target.FullName, actorName, DateTime.UtcNow, token);
                await _audit.LogAsync(actorName, "transfer", file.Id, file.DocumentType,
                    $"تم إحالة هذا الملف إلى المحامي: {target.FullName} بتاريخ {now:d/M/yyyy} — المنفذ عليه: {DocumentText.ActorFullName(file)}", token);
            }

            return moved;
        }, ct);
    }

    public async Task IncrementViewCountAsync(int documentId, CancellationToken ct = default)
    {
        await _documents.IncrementViewCountAsync(documentId, ct);
    }

    /// <summary>سنة «قرار السنة» الحالية من ساعة النظام ومنطقته — مصدر واحد لا تكرار قرار.</summary>
    private int CurrentYear() => ServerClock.CurrentYear(_clock, _timeZone);
    private static void ApplyRequest(Document doc, DocumentUpsertRequest r)
    {
        // صفة الملف تُثبَّت عند الإنشاء ولا تُغيَّر عند التعديل (الموافقة المعتمدة):
        // على التعديل يبقى جانب الملف كما هو مهما أُرسل في الطلب.
        // (ValidateSide تطبَّعت القيمة مسبقًا إلى قيمة صالحة غير فارغة).
        if (doc.Id == 0 || doc.GeneralEntitySide == r.GeneralEntitySide)
            doc.GeneralEntitySide = r.GeneralEntitySide!;

        doc.DocumentType = r.DocumentType;
        doc.BorrowerName = r.BorrowerName;
        doc.BorrowerFather = r.BorrowerFather;
        doc.BorrowerFamily = r.BorrowerFamily;
        doc.BorrowerMother = r.BorrowerMother;
        doc.BorrowerBirth = r.BorrowerBirth;
        doc.BorrowerRegister = r.BorrowerRegister;
        doc.BorrowerNationalId = r.BorrowerNationalId;
        doc.BorrowerAddress = r.BorrowerAddress;
        doc.BorrowerAddressType = r.BorrowerAddressType;

        var borrowerHasRep = !DocumentText.IsEmptyRepresentative(r.BorrowerRepresentativeName, r.BorrowerRepresentativeFather, r.BorrowerRepresentativeFamily);
        doc.BorrowerRepresentativeName = borrowerHasRep ? (r.BorrowerRepresentativeName ?? string.Empty).Trim() : null;
        doc.BorrowerRepresentativeFather = borrowerHasRep ? (r.BorrowerRepresentativeFather ?? string.Empty).Trim() : null;
        doc.BorrowerRepresentativeFamily = borrowerHasRep ? (r.BorrowerRepresentativeFamily ?? string.Empty).Trim() : null;
        doc.BorrowerRepresentativeCapacity = borrowerHasRep ? DocumentText.NormalizeRepresentativeCapacity(r.BorrowerRepresentativeCapacity) : null;
        doc.BorrowerRepresentativeAddressType = borrowerHasRep ? DocumentText.NormalizeRepresentativeAddressType(r.BorrowerRepresentativeAddressType) : null;
        doc.BorrowerRepresentativeAddress = borrowerHasRep ? (r.BorrowerRepresentativeAddress ?? string.Empty).Trim() : null;

        // طبيعة المقترض: الاعتباري يحمل اسم الشخص الاعتباري في BorrowerName وتُصفَّر حقول الهوية
        // الطبيعية والممثل الشرعي والورثة (مفاهيم تخص الشخص الطبيعي)، ويُحتفظ برقم التسجيل ومن يمثله.
        // الطبيعي يُصفِّر الحقول الاعتبارية.
        doc.BorrowerNature = DocumentText.NormalizePartyNature(r.BorrowerNature);
        if (PartyNatureCatalog.IsLegal(doc.BorrowerNature))
        {
            doc.BorrowerFather = null;
            doc.BorrowerFamily = null;
            doc.BorrowerMother = null;
            doc.BorrowerBirth = null;
            doc.BorrowerRegister = null;
            doc.BorrowerNationalId = null;
            doc.BorrowerRegistrationNumber = (r.BorrowerRegistrationNumber ?? string.Empty).Trim();
            doc.BorrowerRepresentedBy = (r.BorrowerRepresentedBy ?? string.Empty).Trim();
            doc.BorrowerRepresentativeName = null;
            doc.BorrowerRepresentativeFather = null;
            doc.BorrowerRepresentativeFamily = null;
            doc.BorrowerRepresentativeCapacity = null;
            doc.BorrowerRepresentativeAddressType = null;
            doc.BorrowerRepresentativeAddress = null;
        }
        else
        {
            doc.BorrowerRegistrationNumber = null;
            doc.BorrowerRepresentedBy = null;
        }

        doc.ContractType = r.ContractType;
        doc.ContractTypeSelector = r.ContractTypeSelector;
        doc.ContractNumber = r.ContractNumber;
        doc.ContractDate = r.ContractDate;
        doc.AnnexType = r.AnnexType;
        doc.AnnexNumber = r.AnnexNumber;
        doc.AnnexDate = r.AnnexDate;
        doc.InclusionText = r.InclusionText;
        doc.InclusionNotes = r.InclusionNotes;
        doc.AmountNumeric = r.AmountNumeric ?? 0;
        doc.AmountWords = r.AmountWords;
        doc.Currency = r.Currency;
        doc.Amount2Numeric = r.Amount2Numeric ?? 0;
        doc.Amount2Words = r.Amount2Words;
        doc.Currency2 = r.Currency2;
        doc.Amount3Numeric = r.Amount3Numeric ?? 0;
        doc.Amount3Words = r.Amount3Words;
        doc.Currency3 = r.Currency3;
        doc.InclusionAmountNumeric = r.InclusionAmountNumeric ?? 0;
        doc.InclusionAmountWords = r.InclusionAmountWords;
        doc.InclusionCurrency = r.InclusionCurrency;
        doc.InclusionAmount2Numeric = r.InclusionAmount2Numeric ?? 0;
        doc.InclusionAmount2Words = r.InclusionAmount2Words;
        doc.InclusionCurrency2 = r.InclusionCurrency2;
        doc.InclusionAmount3Numeric = r.InclusionAmount3Numeric ?? 0;
        doc.InclusionAmount3Words = r.InclusionAmount3Words;
        doc.InclusionCurrency3 = r.InclusionCurrency3;
        // العقد: الخادم يشتق Court/CourtNorm من الدائرة ويتجاهل نص العميل دائمًا (لا 400) —
        // يُضبط في Create/Update عبر ApplyCircuitDerivation بعد ApplyRequest.
        // التطبيع الرقمي مركزيًا: الثقب 123 مقابل ١٢٣ يُغلق هنا للإنشاء/التعديل.
        doc.FileNumber = DigitNormalizer.NormalizeDigitsOrNull(r.FileNumber);
        doc.FileType = r.FileType;
        doc.FileYear = DigitNormalizer.NormalizeDigitsOrNull(r.FileYear);
        doc.FileIncoming = r.FileIncoming;
        doc.FileIncomingDate = r.FileIncomingDate;
        doc.UnderFilingNumber = r.UnderFilingNumber;
        doc.BranchName = r.BranchName;
        doc.SeizureDate = r.SeizureDate;
        doc.ImmediateActions = r.ImmediateActions;
        doc.Notes = r.Notes;

        // حقلا ورود الملف خاصان بوضع «طالبة تنفيذ» فقط ويُصفَّران بغيرها.
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            doc.FileArrivalNumber = null;
            doc.FileArrivalDate = null;
        }
        else
        {
            var arrivalNumber = (r.FileArrivalNumber ?? string.Empty).Trim();
            var arrivalDate = (r.FileArrivalDate ?? string.Empty).Trim();
            doc.FileArrivalNumber = string.IsNullOrEmpty(arrivalNumber) ? null : arrivalNumber;
            doc.FileArrivalDate = string.IsNullOrEmpty(arrivalDate) ? null : arrivalDate;
        }

        // حقول عائلة وضع «منفذ عليه» (Executed + Deposit): تُطبَّق على ملفات هذه الصفة فقط،
        // وتُصفَّر خارجها. صفة العرض لا تحمل وصفًا إضافيًا (ExecutedDescription) بل تاريخ إيداع.
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            var executedStatus = string.IsNullOrWhiteSpace(r.ExecutedStatus)
                ? ExecutedStatusCatalog.None
                : r.ExecutedStatus.Trim();
            if (!ExecutedStatusCatalog.ValidStatuses.Contains(executedStatus))
                throw new ArgumentException("حالة وضع (متداول/منفذ/مشطوب) غير صالحة");

            // شطبٌ جديد بتاريخ مصرّح يُحفظ كما هو؛ شطبٌ جديد بتاريـخ فارغ يحمل تاريخ الآن؛
            // وإعادة شطب (كان مشطوبًا قبل التعديل) بلا تاريخ صريح تُبقي تاريخها السابق —
            // فلا يُعاد استخدام تاريخ شطب قديمٍ كحدثٍ جديد (يوحّد مع قاعدة Status.cs).
            var wasStruckOff = ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus);
            doc.ExecutedStatus = ExecutedStatusCatalog.IsStored(executedStatus) ? executedStatus : ExecutedStatusCatalog.None;
            if (doc.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
            {
                var submitted = DocumentValidator.ParseDateTime(r.StruckOffDate, "تاريخ الشطب");
                if (submitted is not null)
                    doc.StruckOffDate = submitted;
                else if (!wasStruckOff)
                    doc.StruckOffDate = DateTime.UtcNow;
            }
            doc.ExecutedDescription = doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                ? (r.ExecutedDescription ?? string.Empty).Trim()
                : null;
            doc.FileReceiptDate = DocumentValidator.ParseDateTime(r.FileReceiptDate, "تاريخ ورود الاخطار");
            doc.FileReceiptNumber = (r.FileReceiptNumber ?? string.Empty).Trim();
            doc.ExecutedRequiredAmount = r.ExecutedRequiredAmount;
            doc.ExecutedRequiredCurrency = r.ExecutedRequiredCurrency;
            doc.ExecutedRequiredAmount2 = r.ExecutedRequiredAmount2;
            doc.ExecutedRequiredCurrency2 = r.ExecutedRequiredCurrency2;
            doc.ExecutedRequiredAmount3 = r.ExecutedRequiredAmount3;
            doc.ExecutedRequiredCurrency3 = r.ExecutedRequiredCurrency3;
            doc.ExecutedPaidAmount = r.ExecutedPaidAmount;
            doc.ExecutedPaidCurrency = r.ExecutedPaidCurrency;
            doc.ExecutedPaidAmount2 = r.ExecutedPaidAmount2;
            doc.ExecutedPaidCurrency2 = r.ExecutedPaidCurrency2;
            doc.ExecutedPaidAmount3 = r.ExecutedPaidAmount3;
            doc.ExecutedPaidCurrency3 = r.ExecutedPaidCurrency3;
            doc.ExecutedDepositDate = doc.GeneralEntitySide == GeneralEntitySideCatalog.Deposit
                ? DocumentValidator.ParseDateTime(r.ExecutedDepositDate, "تاريخ ايداعه حساب الجهة العامة")
                : null;
            doc.ExecutedExecutionDate = doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                ? DocumentValidator.ParseDateTime(r.ExecutedExecutionDate, "تاريخ التنفيذ")
                : null;
        }
        else
        {
            doc.ExecutedStatus = ExecutedStatusCatalog.None;
            doc.ExecutedDescription = null;
            doc.FileReceiptDate = null;
            doc.FileReceiptNumber = null;
            doc.ExecutedRequiredAmount = null;
            doc.ExecutedRequiredCurrency = null;
            doc.ExecutedRequiredAmount2 = null;
            doc.ExecutedRequiredCurrency2 = null;
            doc.ExecutedRequiredAmount3 = null;
            doc.ExecutedRequiredCurrency3 = null;
            doc.ExecutedPaidAmount = null;
            doc.ExecutedPaidCurrency = null;
            doc.ExecutedPaidAmount2 = null;
            doc.ExecutedPaidCurrency2 = null;
            doc.ExecutedPaidAmount3 = null;
            doc.ExecutedPaidCurrency3 = null;
            doc.ExecutedDepositDate = null;
            doc.ExecutedExecutionDate = null;
            doc.StruckOffDate = null;
        }

        doc.ExecutionApplicants.Clear();
        foreach (var a in NormalizeExecutionApplicants(r.ExecutionApplicants))
        {
            doc.ExecutionApplicants.Add(a);
            // ربط الورثة بالملف مباشرة (DocumentId) وبمورثهم (ExecutionApplicantId):
            // EF يرتب كل مفتاح أجنبي عبر مجموعة المورث ومجموعة الملف معًا.
            foreach (var heir in a.Heirs)
                doc.ExecutedHeirs.Add(heir);
        }

        doc.ExecutedPublicEntities.Clear();
        foreach (var e in NormalizeExecutedPublicEntities(r.ExecutedPublicEntities))
            doc.ExecutedPublicEntities.Add(e);

        // قائمة الجهات طالبة التنفيذ: تخص وضع «طالبة تنفيذ» فقط وتُصفَّر بغيره.
        doc.ApplicantPublicEntities.Clear();
        if (!GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            var applicantList = r.ApplicantPublicEntities;
            // توافق مع الطلبات القديمة: نص «طالب التنفيذ» المرسل بلا قائمة يُعامَل كجهة واحدة.
            if ((applicantList is null || applicantList.Count == 0) && !string.IsNullOrWhiteSpace(r.Applicant))
                applicantList = new List<ApplicantPublicEntityDto> { new(null, r.Applicant, null) };
            foreach (var a in DocumentText.NormalizeApplicantPublicEntities(applicantList))
                doc.ApplicantPublicEntities.Add(a);
        }

        doc.ExecutedNaturalPersons.Clear();
        foreach (var p in NormalizeExecutedNaturalPersons(r.ExecutedNaturalPersons))
        {
            doc.ExecutedNaturalPersons.Add(p);
            foreach (var heir in p.Heirs)
                doc.ExecutedHeirs.Add(heir);
        }

        doc.Guarantors.Clear();
        foreach (var g in r.Guarantors.OrderBy(g => g.GuarantorNumber))
            doc.Guarantors.Add(DocumentText.BuildGuarantor(g));

        // الورثة: صفوف بلا اسم ثلاثي تُتجاهل، ونوع العنوان غير الصالح يُعيَّر إلى «عنوان».
        // لا ورثة لشخص اعتباري (ورثة تخص الشخص الطبيعي المتوفى فقط).
        doc.Heirs.Clear();
        if (!PartyNatureCatalog.IsLegal(doc.BorrowerNature))
            foreach (var h in DocumentText.NormalizeHeirs(r.BorrowerHeirs, null))
                doc.Heirs.Add(h);
        foreach (var g in r.Guarantors)
            if (!PartyNatureCatalog.IsLegal(DocumentText.NormalizePartyNature(g.Nature)))
                foreach (var h in DocumentText.NormalizeHeirs(g.Heirs, g.GuarantorNumber))
                    doc.Heirs.Add(h);

        doc.Assets.Clear();
        foreach (var re in r.Assets)
            doc.Assets.Add(DocumentText.BuildAsset(re));
    }




    /// <summary>
    /// تطبيع طلبات التنفيذ: يُتجاهل الطلب بلا اسم ثلاثي، ويُقيَّد نوع التمثيل بالقيم المسموح بها
    /// («أصالة»/«إضافة لتركة»/«أصالة وإضافة») مع معاملة أي قيمة أخرى أو فارغة كـ«أصالة»، ويُقصّ
    /// الاسم الثلاثي للمورث إن لم يُحدَّد مع «إضافة لتركة» أو «أصالة وإضافة». وترتبط ورثة كل مورث
    /// بمجموعته مباشرة، ويُطبَّع الممثل الشرعي (إن وُجد بغير اسم ثلاثي فارغ) حقولَه فيُصفَّر
    /// عند الغياب.
    /// </summary>
    private static List<ExecutionApplicant> NormalizeExecutionApplicants(IEnumerable<ExecutionApplicantDto>? applicants)
    {
        var result = new List<ExecutionApplicant>();
        if (applicants is null)
            return result;

        foreach (var a in applicants)
        {
            var name = (a.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var nature = DocumentText.NormalizePartyNature(a.Nature);
            var isLegal = PartyNatureCatalog.IsLegal(nature);

            // الشخص الاعتباري بلا تمثيل بالتركة ولا ممثل شرعي: تُصفَّر حقول الهوية الطبيعية
            // ويُحتفظ برقم التسجيل ومن يمثلها ونوع العنوان وعنوانه.
            var representationType = isLegal
                ? "أصالة"
                : NormalizeApplicantRepresentation(a.RepresentationType);
            var hasEstate = !isLegal && representationType is "إضافة لتركة" or "أصالة وإضافة";
            var hasRep = !isLegal && !DocumentText.IsEmptyRepresentative(a.RepresentativeName, a.RepresentativeFather, a.RepresentativeFamily);

            var applicant = new ExecutionApplicant
            {
                Name = name,
                ApplicantNature = nature,
                ApplicantRegistrationNumber = isLegal ? (a.RegistrationNumber ?? string.Empty).Trim() : null,
                ApplicantRepresentedBy = isLegal ? (a.RepresentedBy ?? string.Empty).Trim() : null,
                ApplicantAddressType = isLegal ? (a.AddressType ?? string.Empty).Trim() : null,
                ApplicantAddress = isLegal ? (a.Address ?? string.Empty).Trim() : null,
                Father = isLegal ? null : (a.Father ?? string.Empty).Trim(),
                Family = isLegal ? null : (a.Family ?? string.Empty).Trim(),
                LegalRepresentative = isLegal ? null : (a.LegalRepresentative ?? string.Empty).Trim(),
                RepresentationType = representationType,
                DeceasedName = hasEstate ? (a.DeceasedName ?? string.Empty).Trim() : null,
                DeceasedFather = hasEstate ? (a.DeceasedFather ?? string.Empty).Trim() : null,
                DeceasedFamily = hasEstate ? (a.DeceasedFamily ?? string.Empty).Trim() : null,
                RepresentativeName = hasRep ? (a.RepresentativeName ?? string.Empty).Trim() : null,
                RepresentativeFather = hasRep ? (a.RepresentativeFather ?? string.Empty).Trim() : null,
                RepresentativeFamily = hasRep ? (a.RepresentativeFamily ?? string.Empty).Trim() : null,
                RepresentativeCapacity = hasRep ? DocumentText.NormalizeRepresentativeCapacity(a.RepresentativeCapacity) : null,
                RepresentativeLegalRepresentative = hasRep ? (a.RepresentativeLegalRepresentative ?? string.Empty).Trim() : null,
                RegistryId = isLegal ? a.RegistryId : null,
            };
            if (!isLegal)
            {
                foreach (var heir in NormalizeExecutedHeirs(a.Heirs))
                    applicant.Heirs.Add(heir);
            }
            result.Add(applicant);
        }

        return result;
    }

    /// <summary>
    /// تطبيع المنفذ عليهم الاعتباريين (جهة عامة أو شخص اعتباري): يُتجاهل ما بلا اسم، ويُقصّ
    /// اسمه وفرعه. عند الطبيعة (legal) تُعبَّأ حقول الشخص الاعتباري (رقم التسجيل/من يمثلها/العنوان)
    /// ويُصفَّر فرع الجهة العامة؛ وعند (public) تُصفَّر حقول الشخص الاعتباري.
    /// </summary>
    private static List<ExecutedPublicEntity> NormalizeExecutedPublicEntities(IEnumerable<ExecutedPublicEntityDto>? entities)
    {
        var result = new List<ExecutedPublicEntity>();
        if (entities is null)
            return result;

        foreach (var e in entities)
        {
            var name = (e.EntityName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var nature = NormalizeEntityNature(e.Nature);
            var isLegal = PartyNatureCatalog.IsLegal(nature);
            result.Add(new ExecutedPublicEntity
            {
                EntityName = name,
                EntityBranch = isLegal ? null : (e.EntityBranch ?? string.Empty).Trim(),
                Governorate = (e.Governorate ?? string.Empty).Trim(),
                // ربط السجل المرجعي خاص بجهات الدولة العامة لا بالأشخاص الاعتباريين.
                RegistryId = isLegal ? null : e.RegistryId,
                EntityNature = nature,
                RegistrationNumber = isLegal ? (e.RegistrationNumber ?? string.Empty).Trim() : null,
                RepresentedBy = isLegal ? (e.RepresentedBy ?? string.Empty).Trim() : null,
                AddressType = isLegal ? (e.AddressType ?? string.Empty).Trim() : null,
                Address = isLegal ? (e.Address ?? string.Empty).Trim() : null,
            });
        }

        return result;
    }


    /// <summary>
    /// تطبيع الأشخاص الطبيعيين المنفذ عليهم: يُتجاهل ما بلا اسم ثلاثي، ويُقيَّد نوع العنوان
    /// («عنوان»/«وكيل») مع معاملة أي قيمة أخرى كـ«عنوان»، ونوع التمثيل («أصالة»/«إضافة لتركة»/
    /// «أصالة وإضافة») مع معاملة أي قيمة أخرى كـ«أصالة». وترتبط ورثة كل مورث بمجموعته مباشرة،
    /// ويُطبَّع الممثل الشرعي (إن وُجد) حقولَه فيُصفَّر عند الغياب.
    /// </summary>
    private static List<ExecutedNaturalPerson> NormalizeExecutedNaturalPersons(IEnumerable<ExecutedNaturalPersonDto>? persons)
    {
        var result = new List<ExecutedNaturalPerson>();
        if (persons is null)
            return result;

        foreach (var p in persons)
        {
            var name = (p.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var addressType = (p.AddressType ?? string.Empty).Trim();
            if (addressType != "وكيل")
                addressType = "عنوان";

            var representationType = (p.RepresentationType ?? string.Empty).Trim();
            if (representationType != "إضافة لتركة" && representationType != "أصالة وإضافة")
                representationType = "أصالة";

            var hasEstate = representationType is "إضافة لتركة" or "أصالة وإضافة";
            var hasRep = !DocumentText.IsEmptyRepresentative(p.RepresentativeName, p.RepresentativeFather, p.RepresentativeFamily);

            var person = new ExecutedNaturalPerson
            {
                Name = name,
                Father = (p.Father ?? string.Empty).Trim(),
                Family = (p.Family ?? string.Empty).Trim(),
                AddressType = addressType,
                AddressOrRepresentative = (p.AddressOrRepresentative ?? string.Empty).Trim(),
                RepresentationType = representationType,
                DeceasedName = hasEstate ? (p.DeceasedName ?? string.Empty).Trim() : null,
                DeceasedFather = hasEstate ? (p.DeceasedFather ?? string.Empty).Trim() : null,
                DeceasedFamily = hasEstate ? (p.DeceasedFamily ?? string.Empty).Trim() : null,
                RepresentativeName = hasRep ? (p.RepresentativeName ?? string.Empty).Trim() : null,
                RepresentativeFather = hasRep ? (p.RepresentativeFather ?? string.Empty).Trim() : null,
                RepresentativeFamily = hasRep ? (p.RepresentativeFamily ?? string.Empty).Trim() : null,
                RepresentativeCapacity = hasRep ? DocumentText.NormalizeRepresentativeCapacity(p.RepresentativeCapacity) : null,
                RepresentativeAddressType = hasRep ? DocumentText.NormalizeRepresentativeAddressType(p.RepresentativeAddressType) : null,
                RepresentativeAddress = hasRep ? (p.RepresentativeAddress ?? string.Empty).Trim() : null,
            };
            foreach (var heir in NormalizeExecutedHeirs(p.Heirs))
                person.Heirs.Add(heir);
            result.Add(person);
        }

        return result;
    }

    /// <summary>
    /// تصفية صفوف الورثة الصالحة: يُتجاهل الوريث بلا اسم ثلاثي، ويُقيَّد نوع العنوان
    /// («عنوان»/«وكيل») مع معاملة أي قيمة أخرى أو فارغة كـ«عنوان».
    /// </summary>
    private static List<ExecutedHeir> NormalizeExecutedHeirs(IEnumerable<ExecutedHeirDto>? heirs)
    {
        var result = new List<ExecutedHeir>();
        if (heirs is null)
            return result;

        foreach (var h in heirs)
        {
            var name = (h.HeirName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var addressType = (h.AddressType ?? string.Empty).Trim();
            if (addressType != "عنوان" && addressType != "وكيل")
                addressType = "عنوان";

            result.Add(new ExecutedHeir
            {
                HeirName = name,
                HeirFather = (h.HeirFather ?? string.Empty).Trim(),
                HeirFamily = (h.HeirFamily ?? string.Empty).Trim(),
                AddressType = addressType,
                HeirAddress = (h.HeirAddress ?? string.Empty).Trim(),
            });
        }

        return result;
    }





    /// <summary>
    /// طبيعة المنفذ عليه الاعتباري في وضع «منفذ عليه»: جهة عامة (public) أو شخص اعتباري (legal)؛
    /// أي قيمة أخرى أو فارغة تُعيَّر إلى «جهة عامة».
    /// </summary>
    private static string NormalizeEntityNature(string? nature)
    {
        var value = (nature ?? string.Empty).Trim();
        return PartyNatureCatalog.ValidEntityNatures.Contains(value) ? value : PartyNatureCatalog.PublicEntity;
    }

    /// <summary>
    /// نوع تمثيل طالب التنفيذ المقبول: أصالة / إضافة لتركة / أصالة وإضافة؛ أي قيمة أخرى أو فارغة
    /// تُعيَّر إلى «أصالة».
    /// </summary>
    private static string NormalizeApplicantRepresentation(string? representationType)
    {
        var value = (representationType ?? string.Empty).Trim();
        return value is "إضافة لتركة" or "أصالة وإضافة" ? value : "أصالة";
    }

    private void ApplyRegistrationDate(Document doc, string? value)
    {
        var date = value?.Trim();
        if (string.IsNullOrWhiteSpace(date))
        {
            if (doc.RegistrationDate is not null)
            {
                _registrationDates.Remove(doc.RegistrationDate);
                doc.RegistrationDate = null;
            }
            return;
        }

        if (doc.RegistrationDate is null)
            doc.RegistrationDate = new DocumentRegistrationDate { Date = date };
        else
            doc.RegistrationDate.Date = date;

        doc.RegistrationDate.DateParsed = ActionDateParser.TryParse(date);
    }
    public async Task<PagedResult<RotationDocumentDto>> GetRotationListAsync(int userId, int page, int perPage, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var currentYear = CurrentYear();
        var (total, docs) = await _documents.GetRotationCandidatesAsync(userId, currentYear, page, perPage, ct);
        var items = docs
            .Select(d => new RotationDocumentDto(
                d.Id,
                d.Court,
                d.BorrowerName,
                d.BorrowerFather,
                d.BorrowerFamily,
                d.FileNumber,
                d.FileType,
                d.BaseNumbers.Where(b => b.Year == currentYear).OrderByDescending(b => b.CreatedAt).FirstOrDefault()?.BaseNumber,
                DocumentText.RotationDisplayName(d),
                EffectiveFileIdentity.Number(d, currentYear),
                EffectiveFileIdentity.Year(d, currentYear)))
            .ToList();

        return new PagedResult<RotationDocumentDto>
        {
            Items = items,
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<List<BaseNumberHistoryDto>> GetBaseNumberHistoryAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return new List<BaseNumberHistoryDto>();

        return doc.BaseNumbers
            .OrderByDescending(b => b.Year)
            .ThenByDescending(b => b.CreatedAt)
            .Select(b => new BaseNumberHistoryDto(b.Year, b.BaseNumber))
            .ToList();
    }

    public async Task SaveBaseNumbersAsync(int userId, List<BaseNumberEntry> entries, string? actorName, CancellationToken ct = default)
    {
        if (entries is null || entries.Count == 0)
            return;

        // حماية من طلب غير صالح يحتوي عنصرًا فارغًا — نرفضه بدل خطأ داخلي.
        if (entries.Any(e => e is null))
            throw new ArgumentException("طلب تدوير غير صالح");

        var year = CurrentYear();

        // منع تكرار نفس الملف داخل الطلب (سلوك غامض) — يُرفض الطلب كاملًا.
        var unique = entries
            .GroupBy(e => e.DocumentId)
            .Select(g => g.Last())
            .ToList();
        if (unique.Count != entries.Count)
            throw new ArgumentException("لا يمكن تكرار نفس الملف في طلب التدوير");

        await _writeGuard.WithNumberGuardAsync(() => _tx.RunAsync(async token =>
        {
            var docs = await _documents.GetByIdsAsync(unique.Select(e => e.DocumentId).ToList(), token);
            var found = docs.ToDictionary(d => d.Id);
            var auditEntries = new List<AuditLogEntry>(unique.Count);

            foreach (var entry in unique)
            {
                if (!found.TryGetValue(entry.DocumentId, out var doc))
                    throw new ArgumentException("أحد الملفات غير موجود أو محذوف");
                if (doc.CreatedById != userId)
                    throw new ArgumentException("لا يمكن تعديل رقم أساس لملف لا يملكه المحامي");

                // الأهلية: مقيد برقم ملف وغير مقيد في السنة الحالية — حتى لا يُدوَّر محتوى
                // الملف تحت رفع أو ملفٌ حديث عهد قيده (رقم ملفه الأصلي هو نفسه رقم أساس سنته،
                // فلا يحتاج رقم أساس جديدًا لهذه السنة).
                // الفحص العائلي قطعي ومطابق حرفيًا لعبارة القائمة في المستودع: عائلتا وضع
                // «منفذ عليه» (Executed + Deposit) تُحكمان حصريًا بحالة الوضع (متداولٌ فقط = لا
                // منفذ ولا مشطوب) وبشرط وجود رقم أساس من سنة سابقة — التدوير فيهما استمرار لدوران
                // سبق أن بدأ، ولا تُعدُّ حالة نظام «طالبة تنفيذ» (ExecStatus/ExecSubStatus) ذات
                // فائدة فيهما. أما نظام «طالبة تنفيذ» فشرطه القديم نفسه.
                if (doc.IsDraft || doc.FileYear == year.ToString(CultureInfo.InvariantCulture))
                    throw new ArgumentException($"الملف (رقم {doc.Id}) غير مؤهل للتدوير");

                // حارس التدوير للمعلق (البند 53): يُستبعد المعلق من التدوير دائمًا —
                // لا اعتماد على IsDraft (الذي صار false للمعلق بموجب البند 31).
                if (doc.NeedsRegistration)
                    throw new ArgumentException($"الملف (رقم {doc.Id}) بانتظار إعادة القيد — غير مؤهل للتدوير");

                var eligible = GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide)
                    ? doc.ExecutedStatus == ExecutedStatusCatalog.None
                        && doc.BaseNumbers.Any(b => b.Year < year)
                    : !ExecutionStatusCatalog.IsExecuted(doc.ExecStatus, doc.ExecSubStatus);
                if (!eligible)
                    throw new ArgumentException($"الملف (رقم {doc.Id}) غير مؤهل للتدوير");

                var normalized = entry.BaseNumber?.Trim();
                if (string.IsNullOrEmpty(normalized))
                {
                    // إلغاء رقم أساس السنة الحالية: حذف كل سجلاتها مع الاحتفاظ بأرقام السنوات السابقة.
                    var currentYearRows = doc.BaseNumbers.Where(b => b.Year == year).ToList();
                    if (currentYearRows.Count == 0)
                        continue;

                    auditEntries.Add(new AuditLogEntry(actorName, "rotate", doc.Id, doc.DocumentType,
                        DocumentText.AuditWithActor($"ألغى رقم أساس {year}: {string.Join("، ", currentYearRows.Select(r => r.BaseNumber))}", doc)));
                    foreach (var row in currentYearRows)
                        _baseNumbers.Remove(row);
                    continue;
                }

                if (normalized.Length > 50)
                    throw new ArgumentException("رقم الأساس يتجاوز الطول المسموح");

                // RF-009 بتوقيع الدائرة (البند 34): مطابقة FK مباشرة — قبل إدراجه.
                await _writeGuard.EnsureNumberUniqueByCircuitAsync(doc.Id, doc.ExecutionCircuitId, doc.CourtNorm,
                    normalized, doc.FileType, year.ToString(CultureInfo.InvariantCulture), token);

                // سجل جديد دائمًا: كل تدوير يلحق سجلًا بسنة التدوير، والأحدث (Year ثم CreatedAt) هو المعتبر.
                var record = new DocumentBaseNumber
                {
                    DocumentId = doc.Id,
                    Year = year,
                    BaseNumber = normalized,
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                auditEntries.Add(new AuditLogEntry(actorName, "rotate", doc.Id, doc.DocumentType,
                    DocumentText.AuditWithActor($"دوّر الملف برقم أساس {year}: {normalized}", doc)));
                await _baseNumbers.AddAsync(record, ct);

                // إلحاق رقم الأساس الجديد لنص البحث القائم (إلحاق لا إعادة بناء) ليُلتقط البحث.
                doc.SearchText = DocumentSearchTextBuilder.Append(doc.SearchText, normalized);
            }

            // حفظ ذرّي واحد لكل تغييرات الأرقام ثم دفعة تدقيق واحدة — بدل حفظٍ لكل ملف
            // (مهم في الحسابات الكبيرة التي قد تصل لآلاف الملفات).
            await _uow.SaveChangesAsync(token);
            await _audit.LogManyAsync(auditEntries, token);
        }, ct), entries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e?.BaseNumber))?.BaseNumber);
    }
}

