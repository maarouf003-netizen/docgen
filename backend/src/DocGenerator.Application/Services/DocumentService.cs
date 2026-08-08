using System.Globalization;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IDocumentService
{
    Task<DocumentResponse?> GetAsync(int documentId, CancellationToken ct = default);
    Task<DocumentResponse?> GetDeletedAsync(int documentId, CancellationToken ct = default);
    Task<DocumentResponse> CreateAsync(DocumentUpsertRequest request, int userId, string? actorName, int? branchId, CancellationToken ct = default);
    Task<DocumentResponse?> UpdateAsync(int documentId, DocumentUpsertRequest request, string? actorName, int? userId = null, CancellationToken ct = default);
    Task<bool> DeleteAsync(int documentId, string? actorName, CancellationToken ct = default);
    Task<bool> RestoreAsync(int documentId, string? actorName, CancellationToken ct = default);
    Task<DocumentResponse> TransferAsync(int documentId, int targetLawyerId, string? actorName, CancellationToken ct = default);
    /// <summary>عدد ملفات المحامي غير المحذوفة (للمعاينة قبل النقل الجماعي).</summary>
    Task<int> CountFilesByOwnerAsync(int ownerId, int? scopeBranchId, CancellationToken ct = default);
    /// <summary>
    /// نقل كامل ملفات محامٍ إلى محامٍ آخر بجميع الحالات — رئيس القسم (ضمن فرعه) فقط.
    /// scopeBranchId يُقيّد النطاق بفرع رئيس القسم ويُرجع عدد الملفات المنقولة.
    /// </summary>
    Task<int> TransferAllAsync(int sourceLawyerId, int targetLawyerId, int? scopeBranchId, string? actorName, CancellationToken ct = default);
    Task<PagedResult<DocumentResponse>> SearchDeletedAsync(string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default);
    Task<PagedResult<DocumentResponse>> SearchAsync(string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default);
    /// <summary>
    /// بحث ترحّلي عن ملفات وضع «منفذ عليه» المشطوبة فقط — صفحة «الملفات المشطوبة».
    /// بنفس صلاحيات المحذوفات: محامٍ (ملفاته) / رئيس قسم (فرعه) / مشرف (الكل).
    /// </summary>
    Task<PagedResult<DocumentResponse>> SearchStruckOffAsync(string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default);
    Task<List<DocumentResponse>> ExportAsync(string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default);
    Task<DocumentFilterOptions> GetFilterOptionsAsync(string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(int documentId, string status, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default);
    Task<bool> CancelStatusAsync(int documentId, string? actorName, CancellationToken ct = default);
    /// <summary>
    /// تعيين حالة وضع «منفذ عليه» (ExecutedStatusCatalog): منفذ/مشطوب. عند الشطب يُثبَّت
    /// StruckOffDate (لحظة الشطب UTC) فيُخفى الملف من القوائم والتصدير ويظهر في صفحة المشطوبة.
    /// إعادة الحالة إلى متداول (سلسلة فارغة) تُبقي StruckOffDate محفوظًا لعرضه بعد الإعادة.
    /// </summary>
    Task<bool> UpdateExecutedStatusAsync(int documentId, string status, string? actorName, CancellationToken ct = default);
    /// <summary>
    /// إعادة ملف مشطوب إلى المتداول في وضع «منفذ عليه» (فك الشطب): تُصفَّر ExecutedStatus
    /// وتبقى StruckOffDate محفوظة (تُعرض في تفاصيل الملف بعد الإعادة).
    /// </summary>
    Task<bool> RestoreStruckOffAsync(int documentId, string? actorName, CancellationToken ct = default);
    Task IncrementViewCountAsync(int documentId, CancellationToken ct = default);
    Task<List<ExecutionActionDto>> GetExecutionActionsAsync(int documentId, CancellationToken ct = default);
    Task<ExecutionActionDto> AddExecutionActionAsync(int documentId, AddExecutionActionRequest request, int userId, string? actorName, CancellationToken ct = default);
    Task<ExecutionActionDto?> UpdateExecutionActionAsync(int documentId, int actionId, UpdateExecutionActionRequest request, string? actorName, CancellationToken ct = default);
    Task<bool> DeleteExecutionActionAsync(int documentId, int actionId, string? actorName, CancellationToken ct = default);
    Task<bool> ClearReminderAsync(int documentId, int actionId, string? actorName, CancellationToken ct = default);
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
    private readonly IRepository<Guarantor> _guarantors;
    private readonly IRepository<RealEstate> _realEstates;
    private readonly IRepository<ExecutionAction> _actions;
    private readonly IRepository<DocumentBaseNumber> _baseNumbers;
    private readonly IRepository<DocumentRegistrationDate> _registrationDates;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public DocumentService(
        IDocumentRepository documents,
        IUserRepository users,
        IRepository<Guarantor> guarantors,
        IRepository<RealEstate> realEstates,
        IRepository<ExecutionAction> actions,
        IRepository<DocumentBaseNumber> baseNumbers,
        IRepository<DocumentRegistrationDate> registrationDates,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _documents = documents;
        _users = users;
        _guarantors = guarantors;
        _realEstates = realEstates;
        _actions = actions;
        _baseNumbers = baseNumbers;
        _registrationDates = registrationDates;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    public async Task<DocumentResponse?> GetAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        return doc is null ? null : DocumentResponse.FromEntity(doc);
    }

    public async Task<DocumentResponse?> GetDeletedAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetDeletedByIdAsync(documentId, ct);
        return doc is null || !doc.IsDeleted ? null : DocumentResponse.FromEntity(doc);
    }

    public async Task<DocumentResponse> CreateAsync(DocumentUpsertRequest request, int userId, string? actorName, int? branchId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.BorrowerName)
            && (request.GeneralEntitySide == GeneralEntitySideCatalog.Applicant
                || string.IsNullOrEmpty(request.GeneralEntitySide)))
            throw new ArgumentException("اسم المقترض مطلوب");

        ValidateSide(request);
        ValidateExecutedRequest(request);
        ValidateRegistrationDate(request);

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
        FillDerivedFields(doc);
        ApplyRegistrationDate(doc, request.FileRegistrationDate);

        return await _tx.RunAsync(async token =>
        {
            await _documents.AddAsync(doc, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create", doc.Id, doc.DocumentType,
                AuditWithActor($"أنشأ المستند (رقم {doc.Id})", doc), token);
            await SeedInitialActionsAsync(doc, request.InitialActions, userId, actorName, token);
            return DocumentResponse.FromEntity(doc);
        }, ct);
    }

    public async Task<DocumentResponse?> UpdateAsync(int documentId, DocumentUpsertRequest request, string? actorName, int? userId = null, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return null;

        ValidateSide(request);
        ValidateExecutedRequest(request);
        ValidateRegistrationDate(request);

        ApplyRequest(doc, request);
        FillDerivedFields(doc);
        ApplyRegistrationDate(doc, request.FileRegistrationDate);
        doc.UpdatedAt = DateTime.UtcNow;

        return await _tx.RunAsync(async token =>
        {
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update", doc.Id, doc.DocumentType,
                AuditWithActor($"عدّل المستند (رقم {doc.Id})", doc), token);
            await SeedInitialActionsAsync(doc, request.InitialActions, userId, actorName, token);
            return DocumentResponse.FromEntity(doc);
        }, ct);
    }

    public async Task<bool> DeleteAsync(int documentId, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;

        return await _tx.RunAsync(async token =>
        {
            doc.IsDeleted = true;
            doc.DeletedAt = DateTime.UtcNow;
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "delete", documentId, doc.DocumentType,
                AuditWithActor($"حذف المستند (رقم {documentId})", doc), token);
            return true;
        }, ct);
    }

    public async Task<bool> RestoreAsync(int documentId, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetDeletedByIdAsync(documentId, ct);
        if (doc is null || !doc.IsDeleted)
            return false;

        return await _tx.RunAsync(async token =>
        {
            doc.IsDeleted = false;
            doc.DeletedAt = null;
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "restore", documentId, doc.DocumentType,
                AuditWithActor($"استعادة المستند (رقم {documentId})", doc), token);
            return true;
        }, ct);
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
                documentId, doc.CreatedById, target.Id, target.FullName, token);
            if (transferred is null)
                throw new DocumentConflictException("تغيّر المحامي المختص للملف أثناء النقل — أعد المحاولة");

            await _audit.LogAsync(actorName, "transfer", documentId, doc.DocumentType,
                AuditWithActor($"نقل الملف إلى المحامي: {target.FullName}", doc), token);

            return DocumentResponse.FromEntity(transferred);
        }, ct);
    }

    public async Task<int> CountFilesByOwnerAsync(int ownerId, int? scopeBranchId, CancellationToken ct = default)
    {
        var owner = await _users.GetByIdAsync(ownerId, ct);
        if (owner is null || owner.Role != UserRole.Lawyer)
            throw new ArgumentException("المحامي غير موجود");
        if (scopeBranchId.HasValue && owner.BranchId != scopeBranchId)
            throw new ArgumentException("لا يمكن عرض ملفات محامٍ من فرع آخر");
        return await _documents.CountByOwnerAsync(ownerId, ct);
    }

    public async Task<int> TransferAllAsync(int sourceLawyerId, int targetLawyerId, int? scopeBranchId, string? actorName, CancellationToken ct = default)
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

            var files = await _documents.ListByOwnerAsync(sourceLawyerId, token);
            if (files.Count == 0)
                return 0;

            var transferred = await _documents.TransferAllOwnerAsync(sourceLawyerId, target.Id, target.FullName, token);
            if (transferred != files.Count)
                throw new DocumentConflictException("تغيّرت بيانات الملفات أثناء النقل الجماعي — أعد المحاولة");

            var now = DateTime.Now;
            foreach (var file in files)
            {
                await _audit.LogAsync(actorName, "transfer", file.Id, file.DocumentType,
                    $"تم إحالة هذا الملف إلى المحامي: {target.FullName} بتاريخ {now:d/M/yyyy} — المنفذ عليه: {ActorFullName(file)}", token);
            }

            return transferred;
        }, ct);
    }

    public async Task<PagedResult<DocumentResponse>> SearchDeletedAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchDeletedAsync(query, visibleBranchId, visibleUserId, page, perPage, ct);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(DocumentResponse.FromEntity).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<PagedResult<DocumentResponse>> SearchStruckOffAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchStruckOffAsync(query, null, null, null, null, null, visibleBranchId, visibleUserId, page, perPage, ct);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(DocumentResponse.FromEntity).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<PagedResult<DocumentResponse>> SearchAsync(
        string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, int page, int perPage,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchAsync(
            query, status, applicant, court, lawyer, branch, administrativeBranch, visibleBranchId, visibleUserId, page, perPage, ct);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(DocumentResponse.FromEntity).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<DocumentFilterOptions> GetFilterOptionsAsync(
        string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
        => await _documents.GetFilterOptionsAsync(status, applicant, court, lawyer, branch, administrativeBranch, visibleBranchId, visibleUserId, ct);

    public async Task<List<DocumentResponse>> ExportAsync(
        string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
    {
        var items = await _documents.ExportAsync(
            query, status, applicant, court, lawyer, branch, administrativeBranch, visibleBranchId, visibleUserId, ct);
        return items.Select(DocumentResponse.FromEntity).ToList();
    }

    public async Task<bool> UpdateStatusAsync(int documentId, string status, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;

        status = (status ?? string.Empty).Trim();
        if (!ExecutionStatusCatalog.ValidStatuses.Contains(status))
            throw new ArgumentException("حالة غير صالحة");

        doc.ExecStatus = status;
        var executionStatus = ExecutionStatusCatalog.Classify(status);
        switch (executionStatus)
        {
            case ExecutionStatus.ExecutedForcibly:
                var sub = fields.GetValueOrDefault("execSubStatus");
                if (sub is null || !ExecutionStatusCatalog.ValidSubStatuses.Contains(sub))
                    throw new ArgumentException("نوع التنفيذ الفرعي غير صالح");
                doc.ExecSubStatus = sub;
                doc.CollectedAmount = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount"));
                ClearBaraetFields(doc);
                ClearTarithFields(doc);
                break;
            case ExecutionStatus.ExecutedBySettlement:
                RequireField(fields, "baraetNumber", "رقم كتاب براءة الذمة");
                RequireField(fields, "baraetDate", "تاريخ كتاب براءة الذمة");
                doc.BaraetNumber = fields.GetValueOrDefault("baraetNumber");
                doc.BaraetDate = fields.GetValueOrDefault("baraetDate");
                doc.BaraetRegNumber = fields.GetValueOrDefault("baraetRegNumber");
                doc.BaraetRegDate = fields.GetValueOrDefault("baraetRegDate");
                doc.CollectedAmount = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount"));
                ClearTarithFields(doc);
                doc.ExecSubStatus = null;
                break;
            case ExecutionStatus.Deferred:
                RequireField(fields, "tarithNumber", "رقم كتاب التريث");
                RequireField(fields, "tarithDate", "تاريخ كتاب التريث");
                doc.TarithNumber = fields.GetValueOrDefault("tarithNumber");
                doc.TarithDate = fields.GetValueOrDefault("tarithDate");
                doc.TarithRegNumber = fields.GetValueOrDefault("tarithRegNumber");
                doc.TarithRegDate = fields.GetValueOrDefault("tarithRegDate");
                ClearBaraetFields(doc);
                doc.ExecSubStatus = null;
                doc.CollectedAmount = null;
                break;
            default:
                ClearBaraetFields(doc);
                ClearTarithFields(doc);
                doc.ExecSubStatus = null;
                doc.CollectedAmount = null;
                break;
        }

        return await _tx.RunAsync(async token =>
        {
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            var auditDetail = executionStatus == ExecutionStatus.None
                ? "إلغاء الحالة"
                : $"حالة {ExecutionStatusCatalog.ToLabel(executionStatus)}";
            await _audit.LogAsync(actorName, "status", doc.Id, doc.DocumentType,
                AuditWithActor(auditDetail, doc), token);
            return true;
        }, ct);
    }

    public async Task<bool> CancelStatusAsync(int documentId, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;

        doc.ExecStatus = ExecutionStatusCatalog.None;
        doc.ExecSubStatus = null;
        doc.CollectedAmount = null;
        ClearBaraetFields(doc);
        ClearTarithFields(doc);

        return await _tx.RunAsync(async token =>
        {
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "status", doc.Id, doc.DocumentType,
                AuditWithActor("إلغاء الحالة", doc), token);
            return true;
        }, ct);
    }

    public async Task<bool> UpdateExecutedStatusAsync(int documentId, string status, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;
        if (doc.GeneralEntitySide != GeneralEntitySideCatalog.Executed)
            throw new ArgumentException("حالة وضع «الجهة العامة منفذ عليها» تخص ملفات «الجهة العامة منفذ عليها» فقط");

        status = (status ?? string.Empty).Trim();
        if (!ExecutedStatusCatalog.ValidStatuses.Contains(status))
            throw new ArgumentException("حالة غير صالحة");

        doc.ExecutedStatus = ExecutedStatusCatalog.IsStored(status) ? status : ExecutedStatusCatalog.None;
        if (doc.ExecutedStatus == ExecutedStatusCatalog.StruckOff && doc.StruckOffDate is null)
            doc.StruckOffDate = DateTime.UtcNow;

        return await _tx.RunAsync(async token =>
        {
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            var label = ExecutedStatusCatalog.ToLabel(doc.ExecutedStatus);
            await _audit.LogAsync(actorName, "executed-status", doc.Id, doc.DocumentType,
                AuditWithActor($"حالة وضع «الجهة العامة منفذ عليها»: {label}", doc), token);
            return true;
        }, ct);
    }

    public async Task<bool> RestoreStruckOffAsync(int documentId, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;
        if (doc.GeneralEntitySide != GeneralEntitySideCatalog.Executed)
            throw new ArgumentException("فك الشطب يخص ملفات «الجهة العامة منفذ عليها» فقط");
        if (!ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus))
            return false;

        // فك الشطب: العودة إلى متداول مع الإبقاء على تاريخ الشطب محفوظًا لعرضه بعد الإعادة.
        doc.ExecutedStatus = ExecutedStatusCatalog.None;

        return await _tx.RunAsync(async token =>
        {
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "restore-struck-off", doc.Id, doc.DocumentType,
                AuditWithActor("أعاد ملفًا مشطوبًا إلى المتداول", doc), token);
            return true;
        }, ct);
    }

    public async Task IncrementViewCountAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return;
        doc.ViewCount++;
        _documents.Update(doc);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<List<ExecutionActionDto>> GetExecutionActionsAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return new List<ExecutionActionDto>();
        return doc.ExecutionActions
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ExecutionActionDto(a.Id, a.Type, a.Text, a.ActionDate,
                a.ReminderDuration, a.ReminderColor, a.CreatedBy?.FullName, a.CreatedAt))
            .ToList();
    }

    public async Task<ExecutionActionDto> AddExecutionActionAsync(int documentId, AddExecutionActionRequest request, int userId, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            throw new KeyNotFoundException();

        var (type, text, actionDate) = NormalizeAction(request.Type, request.Text, request.ActionDate);
        var (reminderDuration, reminderColor) = NormalizeReminder(request.ReminderDuration, request.ReminderColor);

        var action = new ExecutionAction
        {
            DocumentId = documentId,
            Type = type,
            Text = text,
            ActionDate = actionDate,
            ReminderDuration = reminderDuration,
            ReminderColor = reminderColor,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _actions.AddAsync(action, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", doc.Id, doc.DocumentType,
                AuditWithActor($"أضاف {TypeLabel(type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), token);
        }, ct);
        return new ExecutionActionDto(action.Id, action.Type, action.Text, action.ActionDate,
            action.ReminderDuration, action.ReminderColor, actorName, action.CreatedAt);
    }

    public async Task<ExecutionActionDto?> UpdateExecutionActionAsync(int documentId, int actionId, UpdateExecutionActionRequest request, string? actorName, CancellationToken ct = default)
    {
        var (type, text, actionDate) = NormalizeAction(request.Type, request.Text, request.ActionDate);
        var (reminderDuration, reminderColor) = NormalizeReminder(request.ReminderDuration, request.ReminderColor);

        var action = await _actions.GetByIdAsync(actionId, ct);
        if (action is null || action.DocumentId != documentId)
            throw new KeyNotFoundException();

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            throw new KeyNotFoundException();

        action.Type = type;
        action.Text = text;
        action.ActionDate = actionDate;
        action.ReminderDuration = reminderDuration;
        action.ReminderColor = reminderColor;

        await _tx.RunAsync(async token =>
        {
            _actions.Update(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", documentId, doc.DocumentType,
                AuditWithActor($"عدّل {TypeLabel(type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), token);
        }, ct);
        return new ExecutionActionDto(action.Id, action.Type, action.Text, action.ActionDate,
            action.ReminderDuration, action.ReminderColor, actorName, action.CreatedAt);
    }

    public async Task<bool> DeleteExecutionActionAsync(int documentId, int actionId, string? actorName, CancellationToken ct = default)
    {
        var action = await _actions.GetByIdAsync(actionId, ct);
        if (action is null || action.DocumentId != documentId)
            return false;

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;

        return await _tx.RunAsync(async token =>
        {
            _actions.Remove(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", documentId, doc.DocumentType,
                AuditWithActor($"حذف {TypeLabel(action.Type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), token);
            return true;
        }, ct);
    }

    public async Task<bool> ClearReminderAsync(int documentId, int actionId, string? actorName, CancellationToken ct = default)
    {
        var action = await _actions.GetByIdAsync(actionId, ct);
        if (action is null || action.DocumentId != documentId)
            return false;

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;

        action.ReminderDuration = null;
        action.ReminderColor = null;

        return await _tx.RunAsync(async token =>
        {
            _actions.Update(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", documentId, doc.DocumentType,
                AuditWithActor($"ألغى التذكير عن {TypeLabel(action.Type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), token);
            return true;
        }, ct);
    }

    public async Task<PagedResult<RotationDocumentDto>> GetRotationListAsync(int userId, int page, int perPage, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, docs) = await _documents.GetRotationCandidatesAsync(userId, page, perPage, ct);
        var currentYear = DateTime.Today.Year;
        var items = docs
            .Select(d => new RotationDocumentDto(
                d.Id,
                d.Court,
                d.BorrowerName,
                d.BorrowerFather,
                d.BorrowerFamily,
                d.FileNumber,
                d.FileType,
                d.BaseNumbers.FirstOrDefault(b => b.Year == currentYear)?.BaseNumber))
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

        var year = DateTime.Today.Year;

        // منع تكرار نفس الملف داخل الطلب (سلوك غامض) — يُرفض الطلب كاملًا.
        var unique = entries
            .GroupBy(e => e.DocumentId)
            .Select(g => g.Last())
            .ToList();
        if (unique.Count != entries.Count)
            throw new ArgumentException("لا يمكن تكرار نفس الملف في طلب التدوير");

        await _tx.RunAsync(async token =>
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

                // الأهلية: مقيد برقم ملف وغير منفَّذ وغير مقيد في السنة الحالية — حتى لا يُدوَّر
                // منفَّذ أو تحت رفع أو ملفٌ حديث عهد قيده (رقم ملفه الأصلي هو نفسه رقم أساس سنته،
                // فلا يحتاج رقم أساس جديدًا لهذه السنة).
                if (doc.IsDraft
                    || ExecutionStatusCatalog.IsExecuted(doc.ExecStatus, doc.ExecSubStatus)
                    || doc.FileYear == year.ToString())
                    throw new ArgumentException($"الملف (رقم {doc.Id}) غير مؤهل للتدوير");

                var normalized = entry.BaseNumber?.Trim();
                if (string.IsNullOrEmpty(normalized))
                {
                    // إلغاء رقم أساس السنة الحالية: حذف السجل فقط مع الاحتفاظ بأرقام السنوات السابقة.
                    var existing = doc.BaseNumbers.FirstOrDefault(b => b.Year == year);
                    if (existing is null)
                        continue;

                    auditEntries.Add(new AuditLogEntry(actorName, "rotate", doc.Id, doc.DocumentType,
                        AuditWithActor($"ألغى رقم أساس {year}: {existing.BaseNumber}", doc)));
                    _baseNumbers.Remove(existing);
                    continue;
                }

                if (normalized.Length > 50)
                    throw new ArgumentException("رقم الأساس يتجاوز الطول المسموح");

                var record = doc.BaseNumbers.FirstOrDefault(b => b.Year == year);
                if (record is null)
                {
                    record = new DocumentBaseNumber
                    {
                        DocumentId = doc.Id,
                        Year = year,
                        BaseNumber = normalized,
                        CreatedById = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    };
                    auditEntries.Add(new AuditLogEntry(actorName, "rotate", doc.Id, doc.DocumentType,
                        AuditWithActor($"دوّر الملف برقم أساس {year}: {normalized}", doc)));
                    await _baseNumbers.AddAsync(record, token);
                }
                else
                {
                    record.BaseNumber = normalized;
                    record.UpdatedAt = DateTime.UtcNow;
                    auditEntries.Add(new AuditLogEntry(actorName, "rotate", doc.Id, doc.DocumentType,
                        AuditWithActor($"حدّث رقم أساس {year}: {normalized}", doc)));
                    _baseNumbers.Update(record);
                }
            }

            // حفظ ذرّي واحد لكل تغييرات الأرقام ثم دفعة تدقيق واحدة — بدل حفظٍ لكل ملف
            // (مهم في الحسابات الكبيرة التي قد تصل لآلاف الملفات).
            await _uow.SaveChangesAsync(token);
            await _audit.LogManyAsync(auditEntries, token);
        }, ct);
    }

    /// <summary>
    /// يزرع إجراءات/ملاحظات «الإدخال المبدئي» (InitialActions) في جدول الإجراءات والملاحظات
    /// ذرّيًا ضمن معاملة حفظ المستند نفسها. كل عنصر يمر بـ NormalizeAction/NormalizeReminder
    /// فنُرفض الطلبات الخبيثة أو الناقصة ذرّيًا، وتُتجاهل الحقول الفارغة، ولا يُنشأ أي سجل
    /// مكرر (لا مقابل سجلات الملف القائمة ولا بين عناصر الطلب نفسه).
    /// </summary>
    private async Task SeedInitialActionsAsync(
        Document doc,
        List<AddExecutionActionRequest>? initialActions,
        int? userId,
        string? actorName,
        CancellationToken ct)
    {
        if (initialActions is null || initialActions.Count == 0 || userId is null)
            return;

        // منع التكرار عند التعديل: تُقارَن النصوص بعد التعقيم (نفس ما يُخزَّن) مع إجراءات الملف
        // القائمة، ومع بعضها داخل الطلب نفسه، بحيث لا يتضاعف السجل عند إعادة الحفظ.
        var existing = new HashSet<string>(doc.ExecutionActions
            .Where(a => a.Type is "action" or "note")
            .Select(a => $"{a.Type}|{a.Text}"));

        foreach (var request in initialActions)
        {
            // حقل لم يُعبأ أصلًا: يُتجاهل ولا يُفشل حفظ الملف.
            if (request is null || string.IsNullOrWhiteSpace(request.Text))
                continue;

            var (type, text, actionDate) = NormalizeAction(request.Type, request.Text, request.ActionDate);
            var (reminderDuration, reminderColor) = NormalizeReminder(request.ReminderDuration, request.ReminderColor);

            if (!existing.Add($"{type}|{text}"))
                continue;

            var action = new ExecutionAction
            {
                DocumentId = doc.Id,
                Type = type,
                Text = text,
                ActionDate = actionDate,
                ReminderDuration = reminderDuration,
                ReminderColor = reminderColor,
                CreatedById = userId.Value,
                CreatedAt = DateTime.UtcNow,
            };

            await _actions.AddAsync(action, ct);
            await _uow.SaveChangesAsync(ct);
            await _audit.LogAsync(actorName, "action", doc.Id, doc.DocumentType,
                AuditWithActor($"أضاف {TypeLabel(type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), ct);
        }
    }

    private static (string Type, string Text, string? ActionDate) NormalizeAction(string type, string text, string? actionDate)
    {
        var sanitizedText = HtmlInputSanitizer.Sanitize(text);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(sanitizedText)))
            throw new ArgumentException("نص الإجراء أو الملاحظة مطلوب");

        type = (type ?? "action").Trim();
        if (type is not ("action" or "note"))
            throw new ArgumentException("نوع غير صالح");

        var trimmedDate = actionDate?.Trim();

        if (type == "action")
        {
            if (string.IsNullOrWhiteSpace(trimmedDate))
                throw new ArgumentException("يجب إدخال تاريخ الإجراء");
        }
        else if (string.IsNullOrWhiteSpace(trimmedDate))
        {
            trimmedDate = DateTime.Today.ToString("yyyy-MM-dd");
        }

        return (type, sanitizedText, trimmedDate);
    }

    private static string TypeLabel(string type) => type == "note" ? "ملاحظة" : "إجراء";

    private static (string? Duration, string? Color) NormalizeReminder(string? duration, string? color)
    {
        var trimmedDuration = duration?.Trim();
        var trimmedColor = color?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedDuration) && string.IsNullOrWhiteSpace(trimmedColor))
            return (null, null);

        var validDurations = new[] { "3 أيام", "أسبوع", "أسبوعين", "شهر" };
        if (!string.IsNullOrWhiteSpace(trimmedDuration) && !validDurations.Contains(trimmedDuration))
            throw new ArgumentException("مدة تذكير غير صالحة");

        var validColors = new[] { "أحمر", "بنفسجي", "أصفر" };
        if (!string.IsNullOrWhiteSpace(trimmedColor) && !validColors.Contains(trimmedColor))
            throw new ArgumentException("لون تذكير غير صالح");

        return (trimmedDuration, trimmedColor);
    }

    /// <summary>
    /// الاسم الثلاثي للمنفذ عليه من بيانات المستند، يُلحق بكل أحداث التدقيق
    /// ليُعرف أي ملف وقع عليه الإجراء دون فتح صفحة المستند.
    /// </summary>
    private static string ActorFullName(Document doc) => string.Join(' ',
        new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }
            .Where(v => !string.IsNullOrWhiteSpace(v)));

    private static string AuditWithActor(string action, Document doc) =>
        string.IsNullOrWhiteSpace(ActorFullName(doc))
            ? action
            : $"{action} — المنفذ عليه: {ActorFullName(doc)}";

    private static void ClearBaraetFields(Document doc)
    {
        doc.BaraetNumber = null;
        doc.BaraetDate = null;
        doc.BaraetRegNumber = null;
        doc.BaraetRegDate = null;
    }

    private static void ClearTarithFields(Document doc)
    {
        doc.TarithNumber = null;
        doc.TarithDate = null;
        doc.TarithRegNumber = null;
        doc.TarithRegDate = null;
    }

    private static void RequireField(Dictionary<string, string?> fields, string key, string label)
    {
        if (string.IsNullOrWhiteSpace(fields.GetValueOrDefault(key)))
            throw new ArgumentException($"يجب إدخال {label} على الأقل");
    }

    private static decimal? ParseCollectedAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            || decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out parsed))
        {
            if (parsed < 0)
                throw new ArgumentException("المبلغ المحصل لا يمكن أن يكون سالباً");
            return parsed;
        }
        throw new ArgumentException("المبلغ المحصل غير صالح");
    }

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
        doc.ContractType = r.ContractType;
        doc.ContractTypeSelector = r.ContractTypeSelector;
        doc.ContractNumber = r.ContractNumber;
        doc.ContractDate = r.ContractDate;
        doc.InclusionText = r.InclusionText;
        doc.AmountNumeric = r.AmountNumeric ?? 0;
        doc.AmountWords = r.AmountWords;
        doc.Currency = r.Currency;
        doc.Amount2Numeric = r.Amount2Numeric ?? 0;
        doc.Amount2Words = r.Amount2Words;
        doc.Currency2 = r.Currency2;
        doc.InclusionAmountNumeric = r.InclusionAmountNumeric ?? 0;
        doc.InclusionAmountWords = r.InclusionAmountWords;
        doc.InclusionCurrency = r.InclusionCurrency;
        doc.Court = r.Court;
        doc.Applicant = r.Applicant;
        doc.FileNumber = r.FileNumber;
        doc.FileType = r.FileType;
        doc.FileYear = r.FileYear;
        doc.FileIncoming = r.FileIncoming;
        doc.FileIncomingDate = r.FileIncomingDate;
        doc.UnderFilingNumber = r.UnderFilingNumber;
        doc.BranchName = r.BranchName;
        doc.SeizureDate = r.SeizureDate;
        doc.ImmediateActions = r.ImmediateActions;
        doc.Notes = r.Notes;

        // حقول وضع «الجهة العامة منفذ عليها»: تُطبَّق على ملفات هذه الصفة فقط، وتُصَفَّر خارجها.
        if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed)
        {
            var executedStatus = string.IsNullOrWhiteSpace(r.ExecutedStatus)
                ? ExecutedStatusCatalog.None
                : r.ExecutedStatus.Trim();
            if (!ExecutedStatusCatalog.ValidStatuses.Contains(executedStatus))
                throw new ArgumentException("حالة وضع «الجهة العامة منفذ عليها» غير صالحة");

            doc.ExecutedStatus = ExecutedStatusCatalog.IsStored(executedStatus) ? executedStatus : ExecutedStatusCatalog.None;
            if (doc.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
            {
                var submitted = ParseDateTime(r.StruckOffDate, "تاريخ الشطب");
                doc.StruckOffDate = submitted ?? doc.StruckOffDate ?? DateTime.UtcNow;
            }
            doc.ExecutedDescription = (r.ExecutedDescription ?? string.Empty).Trim();
            doc.FileReceiptDate = ParseDateTime(r.FileReceiptDate, "تاريخ ورود الملف");
            doc.ExecutedRequiredAmount = r.ExecutedRequiredAmount;
            doc.ExecutedPaidAmount = r.ExecutedPaidAmount;
        }
        else
        {
            doc.ExecutedStatus = ExecutedStatusCatalog.None;
            doc.ExecutedDescription = null;
            doc.FileReceiptDate = null;
            doc.ExecutedRequiredAmount = null;
            doc.ExecutedPaidAmount = null;
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

        doc.ExecutedNaturalPersons.Clear();
        foreach (var p in NormalizeExecutedNaturalPersons(r.ExecutedNaturalPersons))
        {
            doc.ExecutedNaturalPersons.Add(p);
            foreach (var heir in p.Heirs)
                doc.ExecutedHeirs.Add(heir);
        }

        doc.Guarantors.Clear();
        foreach (var g in r.Guarantors.OrderBy(g => g.GuarantorNumber))
        {
            doc.Guarantors.Add(new Guarantor
            {
                GuarantorNumber = g.GuarantorNumber,
                GuarantorName = g.Name,
                GuarantorFather = g.Father,
                GuarantorFamily = g.Family,
                GuarantorMother = g.Mother,
                GuarantorBirth = g.Birth,
                GuarantorRegister = g.Register,
                GuarantorNationalId = g.NationalId,
                GuarantorAddress = g.Address,
                AddressType = g.AddressType,
            });
        }

        // الورثة: صفوف بلا اسم ثلاثي تُتجاهل، ونوع العنوان غير الصالح يُعيَّر إلى «عنوان».
        doc.Heirs.Clear();
        foreach (var h in NormalizeHeirs(r.BorrowerHeirs, null))
            doc.Heirs.Add(h);
        foreach (var g in r.Guarantors)
            foreach (var h in NormalizeHeirs(g.Heirs, g.GuarantorNumber))
                doc.Heirs.Add(h);

        doc.RealEstates.Clear();
        foreach (var re in r.RealEstates)
        {
            var estate = new RealEstate
            {
                Property = re.Property,
                PropertyNumber = re.PropertyNumber,
                PropertyDistrict = re.PropertyDistrict,
                LandRegistry = re.LandRegistry,
                ShareType = re.ShareType,
            };
            estate.Owners = NormalizeOwners(re.Owners);
            // تمام العقار لا يكون إلا لمالك واحد؛ عند تعدد الملاك تُفرض الحصة السهمية
            // حتى لو أُرسل نوع حصة آخر (حماية البيانات على مستوى الخدمة).
            if (estate.Owners.Count > 1)
                estate.ShareType = "حصة سهمية";
            doc.RealEstates.Add(estate);
        }
    }

    /// <summary>
    /// تطبيع قائمة ملاك العقار: يُتجاهل الاسم الفارغ، ويُقصّ الاسم من الطرفين،
    /// وتُلغى التكرارات مع الحفاظ على ترتيب الاختيار الأصلي.
    /// </summary>
    private static List<RealEstateOwner> NormalizeOwners(IEnumerable<string>? owners)
    {
        var result = new List<RealEstateOwner>();
        if (owners is null)
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var order = 0;
        foreach (var owner in owners)
        {
            var name = (owner ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                continue;

            result.Add(new RealEstateOwner { Name = name, Order = order++ });
        }

        return result;
    }

    /// <summary>
    /// تصفية صفوف الورثة الصالحة فقط: يُتجاهل الوريث بلا اسم ثلاثي، ويُقيَّد نوع العنوان
    /// بالقيمتين المسموحتين («عنوان»/«وكيل») مع معاملة أي قيمة أخرى أو فارغة كـ«عنوان».
    /// </summary>
    private static List<Heir> NormalizeHeirs(IEnumerable<HeirDto>? heirs, int? guarantorNumber)
    {
        var result = new List<Heir>();
        if (heirs is null)
            return result;

        foreach (var h in heirs)
        {
            var name = (h.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var addressType = (h.AddressType ?? string.Empty).Trim();
            if (addressType != "عنوان" && addressType != "وكيل")
                addressType = "عنوان";

            result.Add(new Heir
            {
                GuarantorNumber = guarantorNumber,
                HeirName = name,
                AddressType = addressType,
                HeirAddress = (h.Address ?? string.Empty).Trim(),
            });
        }

        return result;
    }

    /// <summary>
    /// تطبيع طلبات التنفيذ: يُتجاهل الطلب بلا اسم ثلاثي، ويُقيَّد نوع التمثيل بالقيمتين
    /// («أصالة»/«إضافة لتركة») مع معاملة أي قيمة أخرى أو فارغة كـ«أصالة»، ويُقصّ الاسم الثلاثي
    /// للمورث إن لم يُحدَّد مع «إضافة لتركة». وترتبط ورثة كل مورث بمجموعته مباشرة.
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

            var representationType = (a.RepresentationType ?? string.Empty).Trim();
            if (representationType != "إضافة لتركة")
                representationType = "أصالة";

            var applicant = new ExecutionApplicant
            {
                Name = name,
                Father = (a.Father ?? string.Empty).Trim(),
                Family = (a.Family ?? string.Empty).Trim(),
                LegalRepresentative = (a.LegalRepresentative ?? string.Empty).Trim(),
                RepresentationType = representationType,
                DeceasedName = representationType == "إضافة لتركة" ? (a.DeceasedName ?? string.Empty).Trim() : null,
                DeceasedFather = representationType == "إضافة لتركة" ? (a.DeceasedFather ?? string.Empty).Trim() : null,
                DeceasedFamily = representationType == "إضافة لتركة" ? (a.DeceasedFamily ?? string.Empty).Trim() : null,
            };
            foreach (var heir in NormalizeExecutedHeirs(a.Heirs))
                applicant.Heirs.Add(heir);
            result.Add(applicant);
        }

        return result;
    }

    /// <summary>
    /// تطبيع الجهات العامة المنفذ عليها: يُتجاهل ما بلا اسم جهة، ويُقصّ اسم الجهة وفرعها.
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

            result.Add(new ExecutedPublicEntity
            {
                EntityName = name,
                EntityBranch = (e.EntityBranch ?? string.Empty).Trim(),
            });
        }

        return result;
    }

    /// <summary>
    /// تطبيع الأشخاص الطبيعيين المنفذ عليهم: يُتجاهل ما بلا اسم ثلاثي، ويُقيَّد نوع العنوان
    /// («عنوان»/«وكيل») مع معاملة أي قيمة أخرى كـ«عنوان»، ونوع التمثيل («أصالة»/«إضافة لتركة»)
    /// مع معاملة أي قيمة أخرى كـ«أصالة». وترتبط ورثة كل مورث بمجموعته مباشرة.
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
            if (representationType != "إضافة لتركة")
                representationType = "أصالة";

            var person = new ExecutedNaturalPerson
            {
                Name = name,
                Father = (p.Father ?? string.Empty).Trim(),
                Family = (p.Family ?? string.Empty).Trim(),
                AddressType = addressType,
                AddressOrRepresentative = (p.AddressOrRepresentative ?? string.Empty).Trim(),
                RepresentationType = representationType,
                DeceasedName = representationType == "إضافة لتركة" ? (p.DeceasedName ?? string.Empty).Trim() : null,
                DeceasedFather = representationType == "إضافة لتركة" ? (p.DeceasedFather ?? string.Empty).Trim() : null,
                DeceasedFamily = representationType == "إضافة لتركة" ? (p.DeceasedFamily ?? string.Empty).Trim() : null,
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
    /// صفة الملف تُثبَّت عند الإنشاء: تُقبل القيم الصالحة فقط (applicant/executed)،
    /// والقيمة الفارغة تُفسَّر على أنها «الجهة العامة طالبة التنفيذ» للحفاظ على توافق الطلبات القائمة.
    /// </summary>
    private static void ValidateSide(DocumentUpsertRequest request)
    {
        var side = string.IsNullOrWhiteSpace(request.GeneralEntitySide)
            ? GeneralEntitySideCatalog.Applicant
            : request.GeneralEntitySide.Trim();

        if (!GeneralEntitySideCatalog.ValidSides.Contains(side))
            throw new ArgumentException("صفة الجهة العامة غير صالحة");

        request.GeneralEntitySide = side;
    }

    /// <summary>
    /// قيود وضع «الجهة العامة منفذ عليها»: عادي فقط (لا مصرفي)، مقيد (لا مسودة)،
    /// وبلا مقترض/كفلاء/عقارات. وتُطبق أيضًا على الملفات الحالية التي تُحرَّر بوضعها الجديد.
    /// </summary>
    private static void ValidateExecutedRequest(DocumentUpsertRequest request)
    {
        if (request.GeneralEntitySide != GeneralEntitySideCatalog.Executed)
            return;

        if (string.IsNullOrWhiteSpace(request.FileNumber) || string.IsNullOrWhiteSpace(request.FileYear))
            throw new ArgumentException("ملف «الجهة العامة منفذ عليها» يجب أن يكون مقيدًا برقم وسنة الملف");

        var selector = string.IsNullOrWhiteSpace(request.ContractTypeSelector)
            ? "عادي"
            : request.ContractTypeSelector.Trim();
        if (selector == "مصرفي")
            throw new ArgumentException("ملف «الجهة العامة منفذ عليها» يكون بعقد عادي فقط (لا مصرفي)");

        if (!string.IsNullOrWhiteSpace(request.BorrowerName)
            || request.Guarantors.Count > 0
            || request.RealEstates.Count > 0)
            throw new ArgumentException("ملف «الجهة العامة منفذ عليها» لا يتضمن مقترضًا أو كفلاء أو عقارات");
    }

    /// <summary>
    /// الملف المقيّد (بعد إدخال رقم الملف وسنة الملف) لا بد أن يحمل تاريخ قيد صالحًا،
    /// لأنه المعيار الوحيد في إحصاءات المتداول. ويُستثنى وضع «الجهة العامة منفذ عليها»
    /// لأن ملفها يقيده الخصم لا محامي الدولة، فتاريخ ورود الملف يغني عن تاريخ القيد.
    /// </summary>
    private static void ValidateRegistrationDate(DocumentUpsertRequest request)
    {
        if (request.GeneralEntitySide == GeneralEntitySideCatalog.Executed)
            return;

        var hasFileNumber = !string.IsNullOrWhiteSpace(request.FileNumber);
        var hasFileYear = !string.IsNullOrWhiteSpace(request.FileYear);
        if (!hasFileNumber || !hasFileYear)
            return;

        if (string.IsNullOrWhiteSpace(request.FileRegistrationDate))
            throw new ArgumentException("تاريخ قيد الملف مطلوب عند إدخال رقم الملف وسنة الملف");

        if (!TryParseDate(request.FileRegistrationDate, out _))
            throw new ArgumentException("تاريخ قيد الملف غير صالح — استخدم مثال: 1/8/2026");
    }

    private static bool TryParseDate(string? value, out DateTime date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = default;
            return false;
        }

        var formats = new[]
        {
            "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy",
            "yyyy-MM-dd", "d/M/yy", "dd/MM/yy",
        };
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date))
            return true;

        return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
    }

    /// <summary>
    /// التاريخ في وضع «منفذ عليه» يُرسَل نصًا حرًا (مثال: 1/8/2026) فيُفسَّر ويُخزَّن زمنيًا
    /// في القاعدة. الفارغ يعني null، وغير الصالح يُرفض برسالة تحمل اسم الحقل.
    /// </summary>
    private static DateTime? ParseDateTime(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!TryParseDate(value, out var date))
            throw new ArgumentException($"{fieldName} غير صالح — استخدم مثال: 1/8/2026");

        return date;
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
    }

    private static void FillDerivedFields(Document doc)
    {
        if (doc.AmountNumeric > 0 && string.IsNullOrWhiteSpace(doc.AmountWords))
            doc.AmountWords = FormatAmountWords(doc.AmountNumeric, doc.Currency);
        if (doc.Amount2Numeric > 0 && string.IsNullOrWhiteSpace(doc.Amount2Words))
            doc.Amount2Words = FormatAmountWords(doc.Amount2Numeric, doc.Currency2);
        if (doc.InclusionAmountNumeric > 0 && string.IsNullOrWhiteSpace(doc.InclusionAmountWords))
            doc.InclusionAmountWords = FormatAmountWords(doc.InclusionAmountNumeric, doc.InclusionCurrency);

        doc.IsDraft = string.IsNullOrWhiteSpace(doc.FileNumber) || string.IsNullOrWhiteSpace(doc.FileYear);
        var label = doc.IsDraft ? ExecutionStatusCatalog.DraftFilter : "متداول";
        var borrower = (doc.BorrowerName ?? string.Empty).Trim();
        doc.DocumentType = string.IsNullOrWhiteSpace(borrower) ? label : $"{label} - {borrower}";

        var parts = new[] { doc.BorrowerName, doc.BorrowerFamily, doc.Applicant, doc.Lawyer,
            doc.Court, doc.FileNumber, doc.ContractNumber, doc.BorrowerNationalId }
            .Where(v => !string.IsNullOrWhiteSpace(v));
        if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed)
        {
            // ملف «منفذ عليه»: مقيد دائمًا، والعنوان يعتمد على حالة الوضع،
            // واسم البحث يضم أسماء طلبات التنفيذ والجهات/الأشخاص المنفذ عليهم.
            doc.IsDraft = false;
            doc.DocumentType = $"{ExecutedStatusCatalog.ToLabel(doc.ExecutedStatus ?? ExecutedStatusCatalog.None)}";
            var applicantNames = doc.ExecutionApplicants
                .Select(a => string.Join(' ', a.Name, a.Father, a.Family))
                .Where(v => !string.IsNullOrWhiteSpace(v));
            var executedNames = doc.ExecutedPublicEntities
                .Select(e => e.EntityName)
                .Concat(doc.ExecutedNaturalPersons.Select(p => string.Join(' ', p.Name, p.Father, p.Family)))
                .Where(v => !string.IsNullOrWhiteSpace(v));
            var executedHeirNames = doc.ExecutedHeirs
                .Select(h => string.Join(' ', h.HeirName, h.HeirFather, h.HeirFamily))
                .Where(v => !string.IsNullOrWhiteSpace(v));
            parts = parts
                .Concat(applicantNames)
                .Concat(executedNames)
                .Concat(executedHeirNames);
        }
        doc.SearchText = string.Join(' ', parts);

        doc.FullData = JsonSerializer.Serialize(new
        {
            doc.BorrowerName, doc.BorrowerFamily, doc.AmountNumeric, doc.Currency,
            doc.ContractNumber, doc.Court, doc.Applicant, doc.Lawyer
        });
    }

    private static string FormatAmountWords(decimal amount, string? currency)
    {
        var words = NumberToWords.Convert((long)amount);
        return string.IsNullOrWhiteSpace(words)
            ? string.Empty
            : $"{words} {currency} فقط لا غير".Trim();
    }
}
