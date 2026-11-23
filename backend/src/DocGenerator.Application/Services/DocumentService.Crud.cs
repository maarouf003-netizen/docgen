using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentService : IDocumentService
{
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
        DocumentValidator.ValidateFileYearNotFuture(request.FileYear, CurrentYear());
        DocumentValidator.ValidateRepresentativeHeirExclusivity(request);
        DocumentValidator.ValidateAnnexes(request);

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

        await IdempotencyGuard.CompleteAsync(_idempotency, ticket, IdempotencyGuard.Snapshot(result), ct: ct);
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
            // الكاتب الوحيد: مزامنة صف القيد بعد حصول المستند على معرّفه (داخل المعاملة).
            await _numberRegistry.SyncRegistrationAsync(doc, userId, actorName, request.WarningToken, token, isNewDocument: true);
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
        DocumentValidator.ValidateFileYearNotFuture(request.FileYear, CurrentYear());
        DocumentValidator.ValidateRepresentativeHeirExclusivity(request);
        DocumentValidator.ValidateAnnexes(request);

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
            // الكاتب الوحيد: مزامنة صف القيد/فحص التفرد بعد حفظ حقول المستند.
            await _numberRegistry.SyncRegistrationAsync(doc, userId, actorName, request.WarningToken, token);
            // تبديل الدائرة من التعديل يسجل الوقوعتين تلقائيًا (قديم/جديد) — وإلا قفزت بلا أثر.
            if (circuitSwitched)
                await _writeGuard.AddCircuitSwitchOccurrencesAsync(doc, oldCircuitName, userId, token);
            await _delegationService.SyncSnapshotsAsync(doc, token);
            // المرآة: نسخ تغييرات المنيب إلى مناباته المعلقة (داخل المعاملة نفسها) وإعادة اشتقاق
            // الحقول المحسوبة فيها — تعيد إشعارات تُطلق بعد نجاح المعاملة.
            mirrorNotifications = await _delegationService.MirrorSourceEditsAsync(doc, token);
            await _writeGuard.LogDocumentChangesAsync(before, doc, actorName, "update",
                $"عدّل المستند (رقم {doc.Id})", token);
            // التنبيهات تُسجَّل في Outbox داخل معاملة الحفظ نفسها:
            // (1) تنبيهات المرآة إلى المحامين المنابين بتحديث نسخة الملف،
            // (2) تنبيه المنيب بالإضافات المحلية المضافة على الملف المناب (ورثة/ممثل شرعي) —
            //     وتُثبَّت الإضافة في سجل الإنابة (عدّاد + ملخص) قبل الإطلاق.
            await _delegationService.FireMirrorNotificationsAsync(mirrorNotifications, token);
            if (targetEditReport.HasAdditions)
                await _delegationService.RecordLocalAdditionsAndFireReportAsync(doc, targetEditReport, token);
            await _actionService.SeedInitialActionsAsync(doc, request.InitialActions, userId, actorName, token);
            return DocumentResponse.FromEntity(doc, CurrentYear());
        }, ct), doc.FileNumber));

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
            // الكاتب الوحيد: إلغاء صفوف الأرقام (document-deleted) قبل رفع علم الحذف
            // حتى تبقى الصفوف مرئية لاستعلام الإلغاء، وتبقى معروضة في السجل.
            await _numberRegistry.VoidForDocumentDeleteAsync(doc, doc.CreatedById, actorName, token);
            doc.IsDeleted = true;
            doc.DeletedAt = DateTime.UtcNow;
            doc.UpdatedAt = DateTime.UtcNow;
            doc.Version++;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "delete", documentId, doc.DocumentType,
                DocumentText.AuditWithActor($"حذف المستند (رقم {documentId})", doc), token);
            return true;
        }, ct);
    }

}
