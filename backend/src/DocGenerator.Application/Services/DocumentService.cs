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
    Task<PagedResult<DocumentResponse>> SearchAsync(string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default);
    /// <summary>
    /// بحث ترحّلي عن ملفات وضع «منفذ عليه» المشطوبة فقط — صفحة «الملفات المشطوبة».
    /// بنفس صلاحيات المحذوفات: محامٍ (ملفاته) / رئيس قسم (فرعه) / مشرف (الكل).
    /// </summary>
    Task<PagedResult<DocumentResponse>> SearchStruckOffAsync(string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default);
    /// <summary>
    /// بحث ترحّلي عن الملفات المنفذة (منفذ عليها/عرض وايداع بحالة «منفذ»، وطالبة تنفيذ
    /// بالتسوية أو الجبري الكامل) — صفحة «الملفات المنفذة»، ظاهرة لجميع الأدوار.
    /// </summary>
    Task<PagedResult<DocumentResponse>> SearchExecutedAsync(string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default);
    Task<List<DocumentResponse>> ExportAsync(string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default);
    Task<DocumentFilterOptions> GetFilterOptionsAsync(string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(int documentId, string status, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default);
    /// <summary>
    /// «التراجع» في نظام «طالبة تنفيذ»: إعادة الملف إلى المتداول من تريث أو منفذ بالتسوية أو منفذ
    /// جبريا بموجب كتاب الجهة العامة بالسير بالملف (رقم وتاريخ الكتاب وورودهما إلزاميان)،
    /// ويُسجَّل وقعة «تراجع» بحقولها في وقوعات الملف.
    /// </summary>
    Task<bool> RevertStatusAsync(int documentId, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default);
    /// <summary>
    /// تعيين حالة وضع «منفذ عليه» (ExecutedStatusCatalog): منفذ/مشطوب. عند الشطب يُثبَّت
    /// StruckOffDate (لحظة الشطب UTC) فيُخفى الملف من القوائم والتصدير ويظهر في صفحة المشطوبة.
    /// إعادة الحالة إلى متداول (سلسلة فارغة) تُبقي StruckOffDate محفوظًا لعرضه بعد الإعادة.
    /// </summary>
    Task<bool> UpdateExecutedStatusAsync(int documentId, string status, string? actorName, CancellationToken ct = default);
    /// <summary>
    /// تعيين حالة وضع «منفذ عليه» مع حقولها (ExecutedStatusRequest): عند الانتقال إلى «منفذ»
    /// تُحفظ حقوله (المبلغ/كيفية التنفيذ/تاريخ الإيداع)، وعند الانتقال إلى «مشطوب» يُحفظ
    /// تاريخ الشطب المُرسَل إن وُجد وإلا توقيت الانتقال. عند العودة من مشطوب إلى متداول
    /// يُطبَّق بيان التجديد (رقم الملف الجديد إلزامي). إعادة الحالة إلى متداول (سلسلة فارغة)
    /// تُبقي تاريخ الشطب محفوظًا لعرضه بعد الإعادة.
    /// </summary>
    Task<bool> UpdateExecutedStatusAsync(int documentId, string status, ExecutedStatusRequest? request, string? actorName, CancellationToken ct = default);
    /// <summary>
    /// إعادة ملف مشطوب إلى المتداول في وضع «منفذ عليه» (فك الشطب): تُصفَّر ExecutedStatus
    /// وتبقى StruckOffDate محفوظة (تُعرض في تفاصيل الملف بعد الإعادة).
    /// </summary>
    Task<bool> RestoreStruckOffAsync(int documentId, string? actorName, CancellationToken ct = default);
    /// <summary>
    /// إعادة ملف مشطوب إلى المتداول مع تجديد الملف برقم ملف جديد لسنة الإعادة (إلزامي):
    /// يُطبَّق بيان التجديد ويُسجَّل رقم أساس لسنة الإعادة الحالية فيعود الملف بالرقم والنوع الجديدين.
    /// </summary>
    Task<bool> RestoreStruckOffAsync(int documentId, RenewalRequest renewal, string? actorName, CancellationToken ct = default);
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
    /// <summary>
    /// إضافة وقعة «منفذ عليه» (شطب أو تجديد) يدويًا إلى سجل وقوعات الملف.
    /// </summary>
    Task<DocumentOccurrenceDto> AddOccurrenceAsync(int documentId, UpsertOccurrenceRequest request, int userId, string? actorName, CancellationToken ct = default);
    /// <summary>
    /// تعديل وقعة «منفذ عليه» قائمة (شطب أو تجديد) في سجل وقوعات الملف.
    /// </summary>
    Task<DocumentOccurrenceDto?> UpdateOccurrenceAsync(int documentId, int occurrenceId, UpsertOccurrenceRequest request, string? actorName, CancellationToken ct = default);
    /// <summary>
    /// حذف وقعة «منفذ عليه» (شطب أو تجديد) من سجل وقوعات الملف.
    /// </summary>
    Task<bool> DeleteOccurrenceAsync(int documentId, int occurrenceId, string? actorName, CancellationToken ct = default);
}

public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IRepository<Guarantor> _guarantors;
    private readonly IRepository<Asset> _assets;
    private readonly IRepository<ExecutionAction> _actions;
    private readonly IRepository<DocumentBaseNumber> _baseNumbers;
    private readonly IRepository<DocumentRegistrationDate> _registrationDates;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public DocumentService(
        IDocumentRepository documents,
        IUserRepository users,
        IRepository<Guarantor> guarantors,
        IRepository<Asset> assets,
        IRepository<ExecutionAction> actions,
        IRepository<DocumentBaseNumber> baseNumbers,
        IRepository<DocumentRegistrationDate> registrationDates,
        IRepository<DocumentOccurrence> occurrences,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _documents = documents;
        _users = users;
        _guarantors = guarantors;
        _assets = assets;
        _actions = actions;
        _baseNumbers = baseNumbers;
        _registrationDates = registrationDates;
        _occurrences = occurrences;
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
            // أول سجل تعاقب: منشئ الملف (Lawyer هو اسم المنشئ عند الإنشاء).
            await _documents.AddAssignmentAsync(doc.Id, AssignmentKindCatalog.Create,
                doc.Lawyer ?? actor.Username, null, DateTime.UtcNow, token);
            await _audit.LogAsync(actorName, "create", doc.Id, doc.DocumentType,
                AuditWithActor($"أنشأ المستند (رقم {doc.Id})", doc), token);
            // ملف أُنشئ مشطوبًا من البداية: يُسجَّل وقعة شطب تلقائيًا ليتسق سجل الوقوعات
            // مع البيانات المُرحَّلة (بعد حفظ المستند فيُعرف رقمه داخل المعاملة نفسها).
            if (doc.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
                await AddStruckOffOccurrenceAsync(doc, userId, token);
            await _uow.SaveChangesAsync(token);
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

        // التجديد عند تعديل ملف مشطوب إلى متداول: يُطبَّق بيان التجديد (رقم الملف الجديد إلزامي)
        // فيعود الملف برقمه ونوعه الجديدين لسنة الإعادة. التعديل دون تغيير الحالة لا يُجدَّد.
        var wasStruckOff = ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus);

        ApplyRequest(doc, request);
        FillDerivedFields(doc);
        ApplyRegistrationDate(doc, request.FileRegistrationDate);
        doc.UpdatedAt = DateTime.UtcNow;

        return await _tx.RunAsync(async token =>
        {
            if (wasStruckOff && doc.ExecutedStatus == ExecutedStatusCatalog.None)
                await ApplyRenewalAsync(doc, request, true, userId, token);
            else if (!wasStruckOff && ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus))
                await AddStruckOffOccurrenceAsync(doc, userId, token);
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
                documentId, doc.CreatedById, target.Id, target.FullName,
                doc.Lawyer ?? doc.CreatedBy?.FullName ?? string.Empty, token);
            if (transferred is null)
                throw new DocumentConflictException("تغيّر المحامي المختص للملف أثناء النقل — أعد المحاولة");

            // سجل تعاقب: الملف أُحيل إلى المحامي المستهدف بتاريخ النقل.
            await _documents.AddAssignmentAsync(documentId, AssignmentKindCatalog.Transfer,
                target.FullName, actorName, DateTime.UtcNow, token);

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

            var transferred = await _documents.TransferAllOwnerAsync(sourceLawyerId, target.Id, target.FullName, source.FullName, token);
            if (transferred != files.Count)
                throw new DocumentConflictException("تغيّرت بيانات الملفات أثناء النقل الجماعي — أعد المحاولة");

            var now = DateTime.Now;
            foreach (var file in files)
            {
                // سجل تعاقب لكل ملف أُحيل إلى المحامي المستهدف.
                await _documents.AddAssignmentAsync(file.Id, AssignmentKindCatalog.Transfer,
                    target.FullName, actorName, DateTime.UtcNow, token);
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

    public async Task<PagedResult<DocumentResponse>> SearchExecutedAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchExecutedAsync(query, visibleBranchId, visibleUserId, page, perPage, ct);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(DocumentResponse.FromEntity).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<PagedResult<DocumentResponse>> SearchAsync(
        string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch, int page, int perPage,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchAsync(
            query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, page, perPage, ct);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(DocumentResponse.FromEntity).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<DocumentFilterOptions> GetFilterOptionsAsync(
        string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
        => await _documents.GetFilterOptionsAsync(status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, ct);

    public async Task<List<DocumentResponse>> ExportAsync(
        string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
    {
        var items = await _documents.ExportAsync(
            query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, ct);
        return items.Select(DocumentResponse.FromEntity).ToList();
    }

    public async Task<bool> UpdateStatusAsync(int documentId, string status, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            throw new ArgumentException("حالة نظام «طالبة تنفيذ» تخص ملفات «الجهة العامة طالبة التنفيذ» فقط");

        status = (status ?? string.Empty).Trim();
        var valid = ExecutionStatusCatalog.ValidStatuses.Contains(status)
            || status == ExecutionStatusCatalog.StateStruckOff;
        if (!valid)
            throw new ArgumentException("حالة غير صالحة");

        // آلة الحالات: تُمنع الانتقالات غير المسموحة من الحالة الحالية صراحةً.
        var current = ExecutionStatusCatalog.CurrentState(doc.IsDraft, doc.ExecStatus, doc.ExecutedStatus);
        if (!ExecutionStatusCatalog.IsAllowedStatusChange(current, status))
            throw new ArgumentException(
                $"لا يمكن الانتقال من الحالة «{ExecutionStatusCatalog.ToStateLabel(current)}» إلى «{ExecutionStatusCatalog.ToStatusLabel(status)}»");

        var details = new Dictionary<string, string>();
        switch (status)
        {
            case ExecutionStatusCatalog.ExecutedForcibly:
                var sub = fields.GetValueOrDefault("execSubStatus");
                if (sub is null || !ExecutionStatusCatalog.ValidSubStatuses.Contains(sub))
                    throw new ArgumentException("نوع التنفيذ الفرعي غير صالح");
                doc.ExecSubStatus = sub;
                details["execSubStatus"] = sub;
                ApplyCollectedAmounts(doc, fields, details);
                ApplySoldAssets(doc, fields, details);
                ClearBaraetFields(doc);
                ClearTarithFields(doc);
                ClearSayerFields(doc);
                RequireField(fields, "forcedExecutionDate", "تاريخ قرار الإحالة القطعية");
                doc.ForcedExecutionDate = fields.GetValueOrDefault("forcedExecutionDate");
                CopyDetail(details, "forcedExecutionDate", doc.ForcedExecutionDate);
                break;
            case ExecutionStatusCatalog.ExecutedBySettlement:
                RequireField(fields, "baraetNumber", "رقم كتاب براءة الذمة");
                RequireField(fields, "baraetDate", "تاريخ كتاب براءة الذمة");
                doc.BaraetNumber = fields.GetValueOrDefault("baraetNumber");
                doc.BaraetDate = fields.GetValueOrDefault("baraetDate");
                doc.BaraetRegNumber = fields.GetValueOrDefault("baraetRegNumber");
                doc.BaraetRegDate = fields.GetValueOrDefault("baraetRegDate");
                CopyDetail(details, "baraetNumber", doc.BaraetNumber);
                CopyDetail(details, "baraetDate", doc.BaraetDate);
                CopyDetail(details, "baraetRegNumber", doc.BaraetRegNumber);
                CopyDetail(details, "baraetRegDate", doc.BaraetRegDate);
                ApplyCollectedAmounts(doc, fields, details);
                ClearTarithFields(doc);
                ClearSayerFields(doc);
                ClearForcedExecutionField(doc);
                doc.ExecSubStatus = null;
                doc.SoldAssetIds = null;
                break;
            case ExecutionStatusCatalog.Deferred:
                RequireField(fields, "tarithNumber", "رقم كتاب التريث");
                RequireField(fields, "tarithDate", "تاريخ كتاب التريث");
                doc.TarithNumber = fields.GetValueOrDefault("tarithNumber");
                doc.TarithDate = fields.GetValueOrDefault("tarithDate");
                doc.TarithRegNumber = fields.GetValueOrDefault("tarithRegNumber");
                doc.TarithRegDate = fields.GetValueOrDefault("tarithRegDate");
                CopyDetail(details, "tarithNumber", doc.TarithNumber);
                CopyDetail(details, "tarithDate", doc.TarithDate);
                CopyDetail(details, "tarithRegNumber", doc.TarithRegNumber);
                CopyDetail(details, "tarithRegDate", doc.TarithRegDate);
                ClearBaraetFields(doc);
                ClearSayerFields(doc);
                ClearForcedExecutionField(doc);
                doc.ExecSubStatus = null;
                ClearCollectedFields(doc);
                doc.SoldAssetIds = null;
                break;
            default: // مشطوب (نظام «طالبة تنفيذ»): يُخفى من القوائم ويظهر في صفحة «الملفات المشطوبة».
                var struckOffDateRaw = fields.GetValueOrDefault("struckOffDate");
                if (string.IsNullOrWhiteSpace(struckOffDateRaw))
                    throw new ArgumentException("يجب إدخال تاريخ الشطب");
                doc.StruckOffDate = ParseDateTime(struckOffDateRaw, "تاريخ الشطب");
                details["struckOffDate"] = struckOffDateRaw;
                ClearBaraetFields(doc);
                ClearTarithFields(doc);
                ClearSayerFields(doc);
                ClearForcedExecutionField(doc);
                doc.ExecSubStatus = null;
                ClearCollectedFields(doc);
                doc.SoldAssetIds = null;
                break;
        }

        doc.ExecStatus = status;
        var occurrenceType = status == ExecutionStatusCatalog.StateStruckOff
            ? OccurrenceTypeCatalog.StruckOff
            : ExecutionStatusCatalog.Classify(status) switch
            {
                ExecutionStatus.ExecutedForcibly => OccurrenceTypeCatalog.Forcible,
                ExecutionStatus.ExecutedBySettlement => OccurrenceTypeCatalog.Settled,
                ExecutionStatus.Deferred => OccurrenceTypeCatalog.Deferred,
                _ => throw new ArgumentException("حالة غير صالحة"),
            };

        return await _tx.RunAsync(async token =>
        {
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            // تسجيل وقعة تغيير الحالة بحقولها الكاملة ضمن المعاملة نفسها — سجل زمني مستقل
            // يبقى ظاهرًا في «وقوعات الملف» بعد أي تراجع أو تعديل لاحق للحالة.
            await _occurrences.AddAsync(new DocumentOccurrence
            {
                DocumentId = doc.Id,
                OccurrenceType = occurrenceType,
                EventDate = status == ExecutionStatusCatalog.StateStruckOff ? doc.StruckOffDate : DateTime.UtcNow,
                Details = details.Count > 0 ? SerializeDetails(details) : null,
                CreatedById = doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, token);
            await _uow.SaveChangesAsync(token);
            var auditDetail = status == ExecutionStatusCatalog.StateStruckOff
                ? $"حالة {ExecutionStatusCatalog.StateStruckOff}"
                : $"حالة {ExecutionStatusCatalog.ToLabel(ExecutionStatusCatalog.Classify(status))}";
            await _audit.LogAsync(actorName, "status", doc.Id, doc.DocumentType,
                AuditWithActor(auditDetail, doc), token);
            return true;
        }, ct);
    }

    public async Task<bool> RevertStatusAsync(int documentId, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            throw new ArgumentException("التراجع عن الحالة يخص ملفات «الجهة العامة طالبة التنفيذ» فقط");

        var current = ExecutionStatusCatalog.CurrentState(doc.IsDraft, doc.ExecStatus, doc.ExecutedStatus);
        if (!ExecutionStatusCatalog.CanRevert(current))
            throw new ArgumentException(
                $"لا يمكن التراجع عن الحالة الحالية «{ExecutionStatusCatalog.ToStateLabel(current)}»");

        // حقول كتاب الجهة العامة بالسير بالملف: رقم وتاريخ الكتاب وورودهما إلزامية.
        RequireField(fields, "sayerNumber", "رقم كتاب الجهة العامة بالسير بالملف");
        RequireField(fields, "sayerDate", "تاريخ كتاب الجهة العامة بالسير بالملف");
        RequireField(fields, "sayerRegNumber", "رقم ورود كتاب بالسير بالملف");
        RequireField(fields, "sayerRegDate", "تاريخ ورود كتاب بالسير بالملف");
        doc.SayerNumber = fields.GetValueOrDefault("sayerNumber");
        doc.SayerDate = fields.GetValueOrDefault("sayerDate");
        doc.SayerRegNumber = fields.GetValueOrDefault("sayerRegNumber");
        doc.SayerRegDate = fields.GetValueOrDefault("sayerRegDate");

        var details = new Dictionary<string, string>();
        CopyDetail(details, "sayerNumber", doc.SayerNumber);
        CopyDetail(details, "sayerDate", doc.SayerDate);
        CopyDetail(details, "sayerRegNumber", doc.SayerRegNumber);
        CopyDetail(details, "sayerRegDate", doc.SayerRegDate);

        // العودة إلى المتداول: تُصفَّر حالة التنفيذ وحقولها مع الإبقاء على حقول «السير بالملف»
        // محفوظةً لتبقى ظاهرة في «وقوعات الملف» (لقطة الحقوق في الوقعة أسفل).
        doc.ExecStatus = ExecutionStatusCatalog.None;
        doc.ExecSubStatus = null;
        ClearCollectedFields(doc);
        ClearBaraetFields(doc);
        ClearTarithFields(doc);
        ClearForcedExecutionField(doc);
        doc.SoldAssetIds = null;

        return await _tx.RunAsync(async token =>
        {
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _occurrences.AddAsync(new DocumentOccurrence
            {
                DocumentId = doc.Id,
                OccurrenceType = OccurrenceTypeCatalog.Revert,
                EventDate = DateTime.UtcNow,
                Details = details.Count > 0 ? SerializeDetails(details) : null,
                CreatedById = doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "status", doc.Id, doc.DocumentType,
                AuditWithActor("تراجع عن الحالة وعاد الملف إلى المتداول", doc), token);
            return true;
        }, ct);
    }

    public async Task<bool> UpdateExecutedStatusAsync(int documentId, string status, string? actorName, CancellationToken ct = default)
        => await UpdateExecutedStatusAsync(documentId, status, null, actorName, ct);

    public async Task<bool> UpdateExecutedStatusAsync(int documentId, string status, ExecutedStatusRequest? request, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;
        if (!GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            throw new ArgumentException("حالة وضع (متداول/منفذ/مشطوب) تخص ملفات «الجهة العامة منفذ عليها» و«عرض وايداع» فقط");

        status = (status ?? string.Empty).Trim();
        if (!ExecutedStatusCatalog.ValidStatuses.Contains(status))
            throw new ArgumentException("حالة غير صالحة");

        var current = doc.ExecutedStatus;
        // حالة «منفذ» في صفة «الجهة العامة منفذ عليها» نهائية: لا تُغيَّر إلى متداول ولا إلى مشطوب
        // (ويبقى الدخول مجددًا إلى «منفذ» ذاتها مسموحًا لتحديث حقول الحالة).
        if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed
            && current == ExecutedStatusCatalog.Executed
            && status != ExecutedStatusCatalog.Executed)
            throw new ArgumentException("حالة «منفذ» في صفة «الجهة العامة منفذ عليها» نهائية لا يمكن تغييرها");
        // «عرض وايداع» يُشطب من متداوله فقط؛ أما المنفذ فلا يُشطب بل يُعاد إلى متداول بكتاب السير بالملف.
        if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Deposit
            && current == ExecutedStatusCatalog.Executed
            && status == ExecutedStatusCatalog.StruckOff)
            throw new ArgumentException("«عرض وايداع» المنفذ لا يُشطب؛ يمكن إرجاعه إلى متداول بكتاب الجهة العامة بالسير بالملف");

        // الإرجاع من «منفذ» إلى «متداول» في «عرض وايداع»: كتاب الجهة العامة بالسير بالملف إلزامي
        // (رقم وتاريخ الكتاب وورودهما)، ويُحفظ مع بقاء المبالغ المودعة، ويُسجَّل وقعة تراجع.
        // يُتحقق هنا قبل أي تعديل على حالة الملف كي لا تترك حالةُ فشلٍ أثرًا على السجل.
        var depositRevert = doc.GeneralEntitySide == GeneralEntitySideCatalog.Deposit
            && current == ExecutedStatusCatalog.Executed
            && status == ExecutedStatusCatalog.None;
        Dictionary<string, string>? revertDetails = null;
        if (depositRevert)
        {
            var sayerFields = new Dictionary<string, string?>
            {
                ["sayerNumber"] = request?.SayerNumber,
                ["sayerDate"] = request?.SayerDate,
                ["sayerRegNumber"] = request?.SayerRegNumber,
                ["sayerRegDate"] = request?.SayerRegDate,
            };
            RequireField(sayerFields, "sayerNumber", "رقم كتاب الجهة العامة بالسير بالملف");
            RequireField(sayerFields, "sayerDate", "تاريخ كتاب الجهة العامة بالسير بالملف");
            RequireField(sayerFields, "sayerRegNumber", "رقم ورود كتاب بالسير بالملف");
            RequireField(sayerFields, "sayerRegDate", "تاريخ ورود كتاب بالسير بالملف");
            doc.SayerNumber = sayerFields["sayerNumber"];
            doc.SayerDate = sayerFields["sayerDate"];
            doc.SayerRegNumber = sayerFields["sayerRegNumber"];
            doc.SayerRegDate = sayerFields["sayerRegDate"];
            revertDetails = new Dictionary<string, string>();
            CopyDetail(revertDetails, "sayerNumber", doc.SayerNumber);
            CopyDetail(revertDetails, "sayerDate", doc.SayerDate);
            CopyDetail(revertDetails, "sayerRegNumber", doc.SayerRegNumber);
            CopyDetail(revertDetails, "sayerRegDate", doc.SayerRegDate);
        }

        var wasStruckOff = ExecutedStatusCatalog.IsStruckOff(current);
        doc.ExecutedStatus = ExecutedStatusCatalog.IsStored(status) ? status : ExecutedStatusCatalog.None;
        // عند الدخول إلى «مشطوب» يُحدَّث تاريخ الشطب: بتاريخه المُرسَل إن وُجد وإلا للآن.
        // فلو عاد الملف إلى المتداول (مع إبقاء تاريخ الشطب السابق لعرضه بعد الإعادة) ثم شُطب
        // من جديد، فيجب أن يحمل الشطبُ الجديد تاريخَه الخاص لا تاريخ شطبه الأول.
        if (!wasStruckOff && doc.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
        {
            var submitted = ParseDateTime(request?.StruckOffDate, "تاريخ الشطب");
            doc.StruckOffDate = submitted ?? DateTime.UtcNow;
        }
        // عند الدخول إلى «منفذ» تُحفظ حقول الحالة المقدَّمة فقط ولا تُمسّ المحفوظة سابقًا:
        // المبلغ وهو خاص بالصفين (تنفيذ/ايداع)، والوصف خاص بصفة «منفذ عليها»، وتاريخ الإيداع
        // خاص بصفة «عرض وايداع». الإعادة إلى منفذ بحقول فارغة تُبقي ما سبق تسجيله.
        if (doc.ExecutedStatus == ExecutedStatusCatalog.Executed)
        {
            // المبلغ المدفوع (حتى ثلاثة بعملاتها) خاص بالصفين (تنفيذ/ايداع): تُحفظ الخانة
            // المقدَّمة فقط بعملتها، ولا تُمسّ المحفوظة سابقًا في سواها. وعملة الخانة عائدة
            // لمنهج «كل مبلغ له عملة»: المقدَّمة، وإلا المحفوظة سابقًا، وإلا الافتراضية.
            if (request?.ExecutedPaidAmount is { } paidAmount)
            {
                doc.ExecutedPaidAmount = paidAmount;
                doc.ExecutedPaidCurrency = request.ExecutedPaidCurrency ?? doc.ExecutedPaidCurrency ?? "ليرة سورية";
            }
            if (request?.ExecutedPaidAmount2 is { } paidAmount2)
            {
                doc.ExecutedPaidAmount2 = paidAmount2;
                doc.ExecutedPaidCurrency2 = request.ExecutedPaidCurrency2 ?? doc.ExecutedPaidCurrency2 ?? "ليرة سورية";
            }
            if (request?.ExecutedPaidAmount3 is { } paidAmount3)
            {
                doc.ExecutedPaidAmount3 = paidAmount3;
                doc.ExecutedPaidCurrency3 = request.ExecutedPaidCurrency3 ?? doc.ExecutedPaidCurrency3 ?? "ليرة سورية";
            }
            if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed)
            {
                var description = (request?.ExecutedDescription ?? string.Empty).Trim();
                if (description.Length > 0)
                    doc.ExecutedDescription = description;
                var executionDate = ParseDateTime(request?.ExecutedExecutionDate, "تاريخ التنفيذ");
                if (executionDate is not null)
                    doc.ExecutedExecutionDate = executionDate;
            }
            else
            {
                doc.ExecutedDescription = null;
                doc.ExecutedExecutionDate = null;
            }
            if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
            {
                // عند دخول «عرض وايداع» إلى «منفذ» تُضبط العلامة الدائمة «سبق تنفيذه» فلا يخرج
                // مبلغه المودع من الإحصاءات (عددًا ومبلغًا) حتى بعد عودته إلى المتداول.
                doc.WasDepositExecuted = true;
                var depositDate = ParseDateTime(request?.ExecutedDepositDate, "تاريخ ايداعه حساب الجهة العامة");
                if (depositDate is not null)
                    doc.ExecutedDepositDate = depositDate;
            }
        }

        return await _tx.RunAsync(async token =>
        {
            // العودة من مشطوب إلى متداول تستلزم تجديد الملف برقم ملف جديد لسنة الإعادة.
            if (wasStruckOff && doc.ExecutedStatus == ExecutedStatusCatalog.None)
                await ApplyRenewalAsync(doc, request ?? new RenewalRequest(), true, doc.CreatedById, token);
            // الانتقال إلى مشطوب يُسجَّل وقعة شطب في سجل وقوعات الملف.
            else if (!wasStruckOff && ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus))
                await AddStruckOffOccurrenceAsync(doc, doc.CreatedById, token);
            // الإرجاع من «منفذ» إلى «متداول» (عرض وايداع) يُسجَّل وقعة تراجع بحقول كتاب السير.
            else if (depositRevert)
                await _occurrences.AddAsync(new DocumentOccurrence
                {
                    DocumentId = doc.Id,
                    OccurrenceType = OccurrenceTypeCatalog.Revert,
                    EventDate = DateTime.UtcNow,
                    Details = revertDetails?.Count > 0 ? SerializeDetails(revertDetails) : null,
                    CreatedById = doc.CreatedById,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                }, token);
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            var label = ExecutedStatusCatalog.ToLabel(doc.ExecutedStatus);
            var sideLabel = GeneralEntitySideCatalog.ToLabel(doc.GeneralEntitySide);
            var auditDetail = depositRevert
                ? $"أعاد «{sideLabel}» إلى المتداول بكتاب الجهة العامة بالسير بالملف"
                : $"حالة وضع «{sideLabel}»: {label}";
            await _audit.LogAsync(actorName, "executed-status", doc.Id, doc.DocumentType,
                AuditWithActor(auditDetail, doc), token);
            return true;
        }, ct);
    }

    public async Task<bool> RestoreStruckOffAsync(int documentId, string? actorName, CancellationToken ct = default)
        => await RestoreStruckOffAsync(documentId, new RenewalRequest(), actorName, ct);

    public async Task<bool> RestoreStruckOffAsync(int documentId, RenewalRequest renewal, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;

        var executedLike = GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide);
        var struckOff = executedLike
            ? ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus)
            : doc.ExecStatus == ExecutionStatusCatalog.StateStruckOff;
        if (executedLike && !struckOff)
            return false;
        if (!executedLike && !struckOff)
            throw new ArgumentException("فك الشطب يخص ملفًا مشطوبًا");

        // فك الشطب: العودة إلى متداول مع الإبقاء على تاريخ الشطب محفوظًا لعرضه بعد الإعادة.
        if (executedLike)
            doc.ExecutedStatus = ExecutedStatusCatalog.None;
        else
            doc.ExecStatus = ExecutionStatusCatalog.None;

        return await _tx.RunAsync(async token =>
        {
            // إعادة الملف المشطوب من صفحة «الملفات المشطوبة» تُعد تجديدًا: رقم الملف الجديد
            // إلزامي (ومعه سنة الإعادة في نظام «طالبة تنفيذ»)، ويُسجَّل رقم أساس لسنة الإعادة
            // فيعود الملف بالرقم والنوع الجديدين.
            await ApplyRenewalAsync(doc, renewal, executedLike, doc.CreatedById, token);
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "restore-struck-off", doc.Id, doc.DocumentType,
                AuditWithActor("أعاد ملفًا مشطوبًا إلى المتداول مع تجديد رقم الملف", doc), token);
            return true;
        }, ct);
    }

    /// <summary>
    /// تطبيق بيان تجديد الملف المشطوب: رقم الملف الجديد إلزامي (ومعه سنة الإعادة في نظام
    /// «طالبة تنفيذ»)، وتُفسَّر التواريخ النصية الحرة، ويُسجَّل رقم أساس لسنة الإعادة فيعود
    /// الملف بالرقم والنوع الجديدين.
    /// </summary>
    private async Task ApplyRenewalAsync(Document doc, RenewalRequest? renewal, bool executedLike, int? userId, CancellationToken ct)
    {
        var number = renewal?.RenewalFileNumber?.Trim();
        if (string.IsNullOrEmpty(number))
            throw new ArgumentException("رقم الملف الجديد مطلوب عند إعادة الملف المشطوب");
        if (number.Length > 100)
            throw new ArgumentException("رقم الملف الجديد يتجاوز الطول المسموح");

        // سنة الإعادة: يحددها المستخدم في نظام «طالبة تنفيذ» (إلزامية)، وافتراضية للعام
        // الحالي في صفة «منفذ عليها» للاتساق مع السلوك القائم.
        int year;
        if (executedLike)
        {
            year = DateTime.Today.Year;
        }
        else
        {
            if (renewal?.RenewalYear is not { } enteredYear)
                throw new ArgumentException("سنة الإعادة مطلوبة عند إعادة الملف المشطوب");
            if (enteredYear < 1900 || enteredYear > 2100)
                throw new ArgumentException("سنة الإعادة غير صالحة");
            year = enteredYear;
        }

        var type = renewal?.RenewalFileType?.Trim();
        if (!string.IsNullOrEmpty(type) && type.Length > 100)
            throw new ArgumentException("نوع الملف الجديد يتجاوز الطول المسموح");

        var receiptNumber = renewal?.RenewalFileReceiptNumber?.Trim();
        if (!string.IsNullOrEmpty(receiptNumber) && receiptNumber.Length > 200)
            throw new ArgumentException("رقم ورود اخطار التجديد يتجاوز الطول المسموح");

        doc.RenewalFileNumber = number;
        doc.RenewalFileReceiptNumber = string.IsNullOrEmpty(receiptNumber) ? null : receiptNumber;
        doc.RenewalFileReceiptDate = ParseDateTime(renewal?.RenewalFileReceiptDate, "تاريخ ورود اخطار التجديد");
        doc.RenewalDate = ParseDateTime(renewal?.RenewalDate, "تاريخ التجديد");
        doc.RenewalFileType = string.IsNullOrEmpty(type) ? doc.FileType : type;
        // النوع الجديد إن وُجد يُطبَّق على نوع الملف الظاهر.
        if (!string.IsNullOrEmpty(type))
            doc.FileType = type;

        // يعود الملف برقم سنة الإعادة: سجل رقم أساس لسنة الإعادة بالرقم الجديد فيظهر عبر
        // DisplayFileNumber (رقم أساس السنة الحالية ?? رقم الملف الأصلي).
        var existing = doc.BaseNumbers.FirstOrDefault(b => b.Year == year);
        if (existing is null)
        {
            await _baseNumbers.AddAsync(new DocumentBaseNumber
            {
                DocumentId = doc.Id,
                Year = year,
                BaseNumber = number,
                CreatedById = userId ?? doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, ct);
        }
        else
        {
            existing.BaseNumber = number;
            existing.UpdatedAt = DateTime.UtcNow;
            _baseNumbers.Update(existing);
        }

        // سجل وقعة التجديد في «وقوعات الملف»: الرقم الجديد والنوع وسنة الإعادة
        // وورود اخطار التجديد — ضمن المعاملة نفسها فلا يضيع السجل عند فشل الحفظ.
        await _occurrences.AddAsync(new DocumentOccurrence
        {
            DocumentId = doc.Id,
            OccurrenceType = OccurrenceTypeCatalog.Renewal,
            EventDate = doc.RenewalDate,
            FileNumber = number,
            FileType = string.IsNullOrEmpty(type) ? null : type,
            Year = year,
            ReceiptNumber = doc.RenewalFileReceiptNumber,
            ReceiptDate = doc.RenewalFileReceiptDate,
            CreatedById = userId ?? doc.CreatedById,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        }, ct);
    }

    /// <summary>
    /// تسجيل وقعة الشطب في «وقوعات الملف» عند انتقال ملف «منفذ عليه»/«عرض وايداع»
    /// إلى الحالة «مشطوب»: تاريخ الشطب المحفوظ في المستند والرقم الأصلي للملف (الرقم
    /// الذي حُمّل عليه) وسنة الشطب — ضمن المعاملة نفسها فلا يضيع السجل عند فشل الحفظ.
    /// </summary>
    private async Task AddStruckOffOccurrenceAsync(Document doc, int? userId, CancellationToken ct)
    {
        string? oldNumber = (doc.FileNumber ?? string.Empty).Trim();
        await _occurrences.AddAsync(new DocumentOccurrence
        {
            DocumentId = doc.Id,
            OccurrenceType = OccurrenceTypeCatalog.StruckOff,
            EventDate = doc.StruckOffDate,
            FileNumber = string.IsNullOrEmpty(oldNumber) ? null : oldNumber,
            Year = doc.StruckOffDate?.Year,
            CreatedById = userId ?? doc.CreatedById,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        }, ct);
    }

    public async Task IncrementViewCountAsync(int documentId, CancellationToken ct = default)
    {
        await _documents.IncrementViewCountAsync(documentId, ct);
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
                d.BaseNumbers.FirstOrDefault(b => b.Year == currentYear)?.BaseNumber,
                RotationDisplayName(d)))
            .ToList();

        return new PagedResult<RotationDocumentDto>
        {
            Items = items,
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    /// <summary>
    /// اسم العرض للملف في صفحة التدوير. لعائلتي وضع «منفذ عليه» (Executed + Deposit) لا يوجد
    /// مقترض، فأُعرض اسم طالب العرض (الجهة العامة أو الشخص الطبيعي) كما تُعرض في ملفات الإكسل
    /// والطباعة، ولو تعذّر توافره فلا أسمًا. ولنظام «طالبة تنفيذ» تُستخدم أسماء المقترض المعتادة.
    /// </summary>
    private static string RotationDisplayName(Document d)
    {
        if (!GeneralEntitySideCatalog.IsExecutedLike(d.GeneralEntitySide))
            return FormatName(d.BorrowerName, d.BorrowerFather, d.BorrowerFamily);

        var applicant = d.ExecutionApplicants
            .Select(a => FormatName(a.Name, a.Father, a.Family))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (!string.IsNullOrWhiteSpace(applicant))
            return applicant;

        var entity = d.ExecutedPublicEntities
            .Select(e => e.EntityName)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (!string.IsNullOrWhiteSpace(entity))
            return entity;

        return d.ExecutedNaturalPersons
            .Select(p => FormatName(p.Name, p.Father, p.Family))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }

    private static string FormatName(string? first, string? father, string? family) =>
        string.Join(' ', new[] { first, father, family }.Where(v => !string.IsNullOrWhiteSpace(v)));

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

                // الأهلية: مقيد برقم ملف وغير مقيد في السنة الحالية — حتى لا يُدوَّر محتوى
                // الملف تحت رفع أو ملفٌ حديث عهد قيده (رقم ملفه الأصلي هو نفسه رقم أساس سنته،
                // فلا يحتاج رقم أساس جديدًا لهذه السنة).
                // الفحص العائلي قطعي ومطابق حرفيًا لعبارة القائمة في المستودع: عائلتا وضع
                // «منفذ عليه» (Executed + Deposit) تُحكمان حصريًا بحالة الوضع (متداولٌ فقط = لا
                // منفذ ولا مشطوب) وبشرط وجود رقم أساس من سنة سابقة — التدوير فيهما استمرار لدوران
                // سبق أن بدأ، ولا تُعدُّ حالة نظام «طالبة تنفيذ» (ExecStatus/ExecSubStatus) ذات
                // فائدة فيهما. أما نظام «طالبة تنفيذ» فشرطه القديم نفسه.
                if (doc.IsDraft || doc.FileYear == year.ToString())
                    throw new ArgumentException($"الملف (رقم {doc.Id}) غير مؤهل للتدوير");

                var eligible = GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide)
                    ? doc.ExecutedStatus == ExecutedStatusCatalog.None
                        && doc.BaseNumbers.Any(b => b.Year < year)
                    : !ExecutionStatusCatalog.IsExecuted(doc.ExecStatus, doc.ExecSubStatus);
                if (!eligible)
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

    public async Task<DocumentOccurrenceDto> AddOccurrenceAsync(int documentId, UpsertOccurrenceRequest request, int userId, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            throw new KeyNotFoundException();

        var occurrence = CreateOccurrence(documentId, request, userId);
        await _tx.RunAsync(async token =>
        {
            await _occurrences.AddAsync(occurrence, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "occurrence", doc.Id, doc.DocumentType,
                AuditWithActor($"أضاف وقعة {ToOccurrenceLabel(occurrence)}: {ToOccurrenceSummary(occurrence)}", doc), token);
        }, ct);
        return ToDto(occurrence, actorName);
    }

    public async Task<DocumentOccurrenceDto?> UpdateOccurrenceAsync(int documentId, int occurrenceId, UpsertOccurrenceRequest request, string? actorName, CancellationToken ct = default)
    {
        var occurrence = await _occurrences.GetByIdAsync(occurrenceId, ct);
        if (occurrence is null || occurrence.DocumentId != documentId)
            return null;

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return null;

        ApplyOccurrence(occurrence, request);
        occurrence.UpdatedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            _occurrences.Update(occurrence);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "occurrence", documentId, doc.DocumentType,
                AuditWithActor($"عدّل وقعة {ToOccurrenceLabel(occurrence)}: {ToOccurrenceSummary(occurrence)}", doc), token);
        }, ct);
        return ToDto(occurrence, actorName);
    }

    public async Task<bool> DeleteOccurrenceAsync(int documentId, int occurrenceId, string? actorName, CancellationToken ct = default)
    {
        var occurrence = await _occurrences.GetByIdAsync(occurrenceId, ct);
        if (occurrence is null || occurrence.DocumentId != documentId)
            return false;

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;

        return await _tx.RunAsync(async token =>
        {
            _occurrences.Remove(occurrence);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "occurrence", documentId, doc.DocumentType,
                AuditWithActor($"حذف وقعة {ToOccurrenceLabel(occurrence)}: {ToOccurrenceSummary(occurrence)}", doc), token);
            return true;
        }, ct);
    }

    /// <summary>
    /// إنشاء كيان الوقعة مع التحقق الكامل من النوع والحقول: النوع يجب أن يكون ضمن
    /// OccurrenceTypeCatalog، والتواريخ نصوص حرة تُفسَّر زمنيًا، ورقم الملف الجديد إلزامي
    /// لوقعة التجديد، وجميع الحقول مقيدة بأطوالها القصوى.
    /// </summary>
    private static DocumentOccurrence CreateOccurrence(int documentId, UpsertOccurrenceRequest request, int userId)
    {
        var occurrence = new DocumentOccurrence
        {
            DocumentId = documentId,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        ApplyOccurrence(occurrence, request);
        return occurrence;
    }

    /// <summary>
    /// تطبيق حقول الطلب على كيان الوقعة مع التحقق (يُعارض ApplyOccurrence لكل من
    /// الإضافة والتعديل فيتوحد سلوك التحقق ويمنع التكرار).
    /// </summary>
    private static void ApplyOccurrence(DocumentOccurrence occurrence, UpsertOccurrenceRequest request)
    {
        var type = (request.OccurrenceType ?? string.Empty).Trim();
        if (!OccurrenceTypeCatalog.ValidTypes.Contains(type))
            throw new ArgumentException("نوع وقعة غير صالح");

        // وقوعات تغيير الحالة (نظام «طالبة تنفيذ»): تُحفظ حقولها التفصيلية كما وردت مع
        // التحقق من الحقول الإلزامية لكل نوع (تريث/منفذ بالتسوية/منفذ جبريا/تراجع).
        if (OccurrenceTypeCatalog.IsStatusChange(type))
        {
            var details = NormalizeDetails(request.Details);
            switch (type)
            {
                case OccurrenceTypeCatalog.Deferred:
                    RequireDetail(details, "tarithNumber", "رقم كتاب التريث");
                    RequireDetail(details, "tarithDate", "تاريخ كتاب التريث");
                    break;
                case OccurrenceTypeCatalog.Settled:
                    RequireDetail(details, "baraetNumber", "رقم كتاب براءة الذمة");
                    RequireDetail(details, "baraetDate", "تاريخ كتاب براءة الذمة");
                    break;
                case OccurrenceTypeCatalog.Forcible:
                    RequireDetail(details, "execSubStatus", "نوع التنفيذ الفرعي");
                    break;
                case OccurrenceTypeCatalog.Revert:
                    RequireDetail(details, "sayerNumber", "رقم كتاب الجهة العامة بالسير بالملف");
                    RequireDetail(details, "sayerDate", "تاريخ كتاب الجهة العامة بالسير بالملف");
                    RequireDetail(details, "sayerRegNumber", "رقم ورود كتاب بالسير بالملف");
                    RequireDetail(details, "sayerRegDate", "تاريخ ورود كتاب بالسير بالملف");
                    break;
            }
            occurrence.OccurrenceType = type;
            occurrence.EventDate = ParseDateTime(request.EventDate, "تاريخ الوقعة");
            occurrence.FileNumber = null;
            occurrence.FileType = null;
            occurrence.Year = null;
            occurrence.ReceiptNumber = null;
            occurrence.ReceiptDate = null;
            occurrence.Details = details.Count > 0 ? SerializeDetails(details) : null;
            return;
        }

        var number = (request.FileNumber ?? string.Empty).Trim();
        if (number.Length > 100)
            throw new ArgumentException("رقم الملف يتجاوز الطول المسموح");
        if (OccurrenceTypeCatalog.IsRenewal(type) && string.IsNullOrEmpty(number))
            throw new ArgumentException("رقم الملف الجديد مطلوب لوقعة التجديد");

        var fileType = (request.FileType ?? string.Empty).Trim();
        if (fileType.Length > 100)
            throw new ArgumentException("نوع الملف يتجاوز الطول المسموح");

        var receiptNumber = (request.ReceiptNumber ?? string.Empty).Trim();
        if (receiptNumber.Length > 200)
            throw new ArgumentException("رقم ورود اخطار التجديد يتجاوز الطول المسموح");

        if (request.Year is not null && (request.Year < 1900 || request.Year > 2100))
            throw new ArgumentException("سنة الوقعة غير صالحة");

        occurrence.OccurrenceType = type;
        occurrence.EventDate = ParseDateTime(request.EventDate,
            type == OccurrenceTypeCatalog.Renewal ? "تاريخ التجديد" : "تاريخ الشطب");
        occurrence.FileNumber = string.IsNullOrEmpty(number) ? null : number;
        occurrence.FileType = string.IsNullOrEmpty(fileType) ? null : fileType;
        occurrence.Year = request.Year;
        occurrence.ReceiptNumber = string.IsNullOrEmpty(receiptNumber) ? null : receiptNumber;
        occurrence.ReceiptDate = ParseDateTime(request.ReceiptDate, "تاريخ ورود اخطار التجديد");
        occurrence.Details = null;
    }

    /// <summary>تطبيع حقول الوقعة التفصيلية: تجاهل الفارغ وضبط القيم المخزنة.</summary>
    private static Dictionary<string, string> NormalizeDetails(Dictionary<string, string?>? raw)
    {
        var result = new Dictionary<string, string>();
        if (raw is null)
            return result;
        foreach (var (key, value) in raw)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            result[key] = value.Trim();
        }
        return result;
    }

    private static void RequireDetail(Dictionary<string, string> details, string key, string label)
    {
        if (!details.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"يجب إدخال {label} على الأقل");
    }

    private static string ToOccurrenceLabel(DocumentOccurrence occurrence) =>
        OccurrenceTypeCatalog.ToLabel(occurrence.OccurrenceType);

    /// <summary>
    /// ملخص مختصر للوقعة في سجل التدقيق: تاريخ الشطب/التجديد والرقم المعني بها.
    /// </summary>
    private static string ToOccurrenceSummary(DocumentOccurrence occurrence)
    {
        var date = occurrence.EventDate?.ToString("d/M/yyyy");
        return string.Concat(date, string.IsNullOrWhiteSpace(occurrence.FileNumber) ? string.Empty : $" — رقم: {occurrence.FileNumber}");
    }

    private static DocumentOccurrenceDto ToDto(DocumentOccurrence o, string? createdByName = null) =>
        new(o.Id, o.OccurrenceType, OccurrenceTypeCatalog.ToLabel(o.OccurrenceType), o.EventDate,
            o.FileNumber, o.FileType, o.Year, o.ReceiptNumber, o.ReceiptDate,
            ParseOccurrenceDetails(o.Details), createdByName);

    /// <summary>فكّ حقول الوقعة التفصيلية من JSON المخزن (أو null عند غيابها/عطبها).</summary>
    private static IReadOnlyDictionary<string, string>? ParseOccurrenceDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
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

    private static void ClearForcedExecutionField(Document doc)
    {
        doc.ForcedExecutionDate = null;
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
        // يوحّد الأرقام العربية/الفارسية ثم فواصل الأرقام العربية (فاصل عشري ٫ وألوف ٬)
        // إلى ما يقبله التحليل؛ فلا يكسر ما يكتب بالأرقام ASCII (يمر كما هو).
        raw = ArabicDigitNormalizer.Normalize(raw)
            .Replace('\u066B', '.')   // ٫ الفاصل العشري العربي
            .Replace('\u066C', ',');  // ٬ فاصل الألوف العربي
        if (decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            || decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out parsed))
        {
            if (parsed < 0)
                throw new ArgumentException("المبلغ المحصل لا يمكن أن يكون سالباً");
            return parsed;
        }
        throw new ArgumentException("المبلغ المحصل غير صالح");
    }

    /// <summary>تطبيق المبالغ المحصلة (حتى ثلاثة بعملاتها) من حقول الطلب على المستند وسجل الوقعة.</summary>
    private static void ApplyCollectedAmounts(Document doc, Dictionary<string, string?> fields, Dictionary<string, string> details)
    {
        doc.CollectedAmount = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount"));
        doc.CollectedAmount2 = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount2"));
        doc.CollectedAmount3 = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount3"));
        var currency = fields.GetValueOrDefault("collectedCurrency");
        var currency2 = fields.GetValueOrDefault("collectedCurrency2");
        var currency3 = fields.GetValueOrDefault("collectedCurrency3");
        doc.CollectedCurrency = string.IsNullOrWhiteSpace(currency) ? "ليرة سورية" : currency.Trim();
        doc.CollectedCurrency2 = string.IsNullOrWhiteSpace(currency2) ? "دولار أمريكي" : currency2.Trim();
        doc.CollectedCurrency3 = string.IsNullOrWhiteSpace(currency3) ? "يورو" : currency3.Trim();
        if (doc.CollectedAmount.HasValue)
        {
            details["collectedAmount"] = doc.CollectedAmount.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            details["collectedCurrency"] = doc.CollectedCurrency;
        }
        if (doc.CollectedAmount2.HasValue)
        {
            details["collectedAmount2"] = doc.CollectedAmount2.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            details["collectedCurrency2"] = doc.CollectedCurrency2;
        }
        if (doc.CollectedAmount3.HasValue)
        {
            details["collectedAmount3"] = doc.CollectedAmount3.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            details["collectedCurrency3"] = doc.CollectedCurrency3;
        }
    }

    /// <summary>
    /// تطبيق الأموال المباعة بالمزاد العلني (إلزامية في «منفذ جبريا»): تُتحقق المعرّفات
    /// من أموال الملف نفسه (عدا كفالة الرواتب)، وتُخزَّن JSON، وتُضمَّن أسماؤها في سجل الوقعة للعرض.
    /// </summary>
    private static void ApplySoldAssets(Document doc, Dictionary<string, string?> fields, Dictionary<string, string> details)
    {
        var raw = (fields.GetValueOrDefault("soldAssetIds") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("اختر الأموال التي جرى بيعها بالمزاد العلني على الأقل");

        var ids = new List<int>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var id))
                throw new ArgumentException("معرّف مال مباع غير صالح");
            ids.Add(id);
        }
        var ownedIds = new HashSet<int>(doc.Assets
            .Where(a => AssetKindCatalog.IsAuctionable(a.AssetKind))
            .Select(a => a.Id));
        if (ids.Any(id => !ownedIds.Contains(id)))
            throw new ArgumentException("الأموال المختارة ليست من أموال الملف");

        doc.SoldAssetIds = SerializeJson(ids);
        details["soldAssetIds"] = string.Join(",", ids);
        var soldNames = doc.Assets
            .Where(a => ids.Contains(a.Id))
            .Select(AssetDisplayName)
            .Where(v => !string.IsNullOrWhiteSpace(v));
        details["soldAssetNames"] = string.Join("، ", soldNames);
    }

    /// <summary>تسمية قراءة للأصل (تُستخدم في «منفذ جبريا» وفي قوائم العرض).</summary>
    private static string AssetDisplayName(Asset a) => AssetDisplay.Label(a);

    private static void ClearSayerFields(Document doc)
    {
        doc.SayerNumber = null;
        doc.SayerDate = null;
        doc.SayerRegNumber = null;
        doc.SayerRegDate = null;
    }

    private static void ClearCollectedFields(Document doc)
    {
        doc.CollectedAmount = null;
        doc.CollectedAmount2 = null;
        doc.CollectedAmount3 = null;
    }

    private static void CopyDetail(Dictionary<string, string> details, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            details[key] = value;
    }

    private static string SerializeDetails(Dictionary<string, string> details) =>
        JsonSerializer.Serialize(details);

    private static string SerializeJson<T>(T value) => JsonSerializer.Serialize(value);

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

        var borrowerHasRep = !IsEmptyRepresentative(r.BorrowerRepresentativeName, r.BorrowerRepresentativeFather, r.BorrowerRepresentativeFamily);
        doc.BorrowerRepresentativeName = borrowerHasRep ? (r.BorrowerRepresentativeName ?? string.Empty).Trim() : null;
        doc.BorrowerRepresentativeFather = borrowerHasRep ? (r.BorrowerRepresentativeFather ?? string.Empty).Trim() : null;
        doc.BorrowerRepresentativeFamily = borrowerHasRep ? (r.BorrowerRepresentativeFamily ?? string.Empty).Trim() : null;
        doc.BorrowerRepresentativeCapacity = borrowerHasRep ? NormalizeRepresentativeCapacity(r.BorrowerRepresentativeCapacity) : null;
        doc.BorrowerRepresentativeAddressType = borrowerHasRep ? NormalizeRepresentativeAddressType(r.BorrowerRepresentativeAddressType) : null;
        doc.BorrowerRepresentativeAddress = borrowerHasRep ? (r.BorrowerRepresentativeAddress ?? string.Empty).Trim() : null;

        // طبيعة المقترض: الاعتباري يحمل اسم الشخص الاعتباري في BorrowerName وتُصفَّر حقول الهوية
        // الطبيعية والممثل الشرعي والورثة (مفاهيم تخص الشخص الطبيعي)، ويُحتفظ برقم التسجيل ومن يمثله.
        // الطبيعي يُصفِّر الحقول الاعتبارية.
        doc.BorrowerNature = NormalizePartyNature(r.BorrowerNature);
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
        doc.Court = r.Court;
        // «طالب التنفيذ» في وضع «طالبة تنفيذ» يُشتق من قائمة الجهات (ApplicantPublicEntities)
        // في FillDerivedFields؛ ولا يُؤخذ نصيًا من الطلب بعد الآن.
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

            doc.ExecutedStatus = ExecutedStatusCatalog.IsStored(executedStatus) ? executedStatus : ExecutedStatusCatalog.None;
            if (doc.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
            {
                var submitted = ParseDateTime(r.StruckOffDate, "تاريخ الشطب");
                doc.StruckOffDate = submitted ?? doc.StruckOffDate ?? DateTime.UtcNow;
            }
            doc.ExecutedDescription = doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                ? (r.ExecutedDescription ?? string.Empty).Trim()
                : null;
            doc.FileReceiptDate = ParseDateTime(r.FileReceiptDate, "تاريخ ورود الاخطار");
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
                ? ParseDateTime(r.ExecutedDepositDate, "تاريخ ايداعه حساب الجهة العامة")
                : null;
            doc.ExecutedExecutionDate = doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                ? ParseDateTime(r.ExecutedExecutionDate, "تاريخ التنفيذ")
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
            foreach (var a in NormalizeApplicantPublicEntities(applicantList))
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
        {
            var nature = NormalizePartyNature(g.Nature);
            var isLegalGuarantor = PartyNatureCatalog.IsLegal(nature);
            var hasRep = !isLegalGuarantor
                && !IsEmptyRepresentative(g.RepresentativeName, g.RepresentativeFather, g.RepresentativeFamily);
            doc.Guarantors.Add(new Guarantor
            {
                GuarantorNumber = g.GuarantorNumber,
                GuarantorName = g.Name,
                GuarantorFather = isLegalGuarantor ? null : g.Father,
                GuarantorFamily = isLegalGuarantor ? null : g.Family,
                GuarantorMother = isLegalGuarantor ? null : g.Mother,
                GuarantorBirth = isLegalGuarantor ? null : g.Birth,
                GuarantorRegister = isLegalGuarantor ? null : g.Register,
                GuarantorNationalId = isLegalGuarantor ? null : g.NationalId,
                GuarantorAddress = g.Address,
                AddressType = g.AddressType,
                GuarantorNature = nature,
                GuarantorRegistrationNumber = isLegalGuarantor ? (g.RegistrationNumber ?? string.Empty).Trim() : null,
                GuarantorRepresentedBy = isLegalGuarantor ? (g.RepresentedBy ?? string.Empty).Trim() : null,
                RepresentativeName = hasRep ? (g.RepresentativeName ?? string.Empty).Trim() : null,
                RepresentativeFather = hasRep ? (g.RepresentativeFather ?? string.Empty).Trim() : null,
                RepresentativeFamily = hasRep ? (g.RepresentativeFamily ?? string.Empty).Trim() : null,
                RepresentativeCapacity = hasRep ? NormalizeRepresentativeCapacity(g.RepresentativeCapacity) : null,
                RepresentativeAddressType = hasRep ? NormalizeRepresentativeAddressType(g.RepresentativeAddressType) : null,
                RepresentativeAddress = hasRep ? (g.RepresentativeAddress ?? string.Empty).Trim() : null,
            });
        }

        // الورثة: صفوف بلا اسم ثلاثي تُتجاهل، ونوع العنوان غير الصالح يُعيَّر إلى «عنوان».
        // لا ورثة لشخص اعتباري (ورثة تخص الشخص الطبيعي المتوفى فقط).
        doc.Heirs.Clear();
        if (!PartyNatureCatalog.IsLegal(doc.BorrowerNature))
            foreach (var h in NormalizeHeirs(r.BorrowerHeirs, null))
                doc.Heirs.Add(h);
        foreach (var g in r.Guarantors)
            if (!PartyNatureCatalog.IsLegal(NormalizePartyNature(g.Nature)))
                foreach (var h in NormalizeHeirs(g.Heirs, g.GuarantorNumber))
                    doc.Heirs.Add(h);

        doc.Assets.Clear();
        foreach (var re in r.Assets)
        {
            var kind = (re.AssetKind ?? string.Empty).Trim();
            if (!AssetKindCatalog.IsValid(kind))
                throw new ArgumentException($"نوع الأصل غير صالح: {kind}");

            var asset = new Asset
            {
                AssetKind = kind,
                ShareType = re.ShareType,
                Property = re.Property,
                PropertyNumber = re.PropertyNumber,
                PropertyDistrict = re.PropertyDistrict,
                LandRegistry = re.LandRegistry,
                VehicleType = re.VehicleType,
                VehicleClass = re.VehicleClass,
                PlateNumber = re.PlateNumber,
                VehicleGovernorate = re.VehicleGovernorate,
                RegisterNumber = re.RegisterNumber,
                RegistrationDate = ParseDateTime(re.RegistrationDate, "تاريخ تسجيل المتجر"),
                ShopGovernorate = re.ShopGovernorate,
                ShopDescription = re.ShopDescription,
                ShopLocation = re.ShopLocation,
                PublicEntity = re.PublicEntity,
                LicenseNumber = re.LicenseNumber,
                LicenseDate = ParseDateTime(re.LicenseDate, "تاريخ الترخيص"),
                LicenseIssuer = re.LicenseIssuer,
                Notes = re.Notes,
            };
            asset.Owners = NormalizeOwners(re.Owners);
            // تمام الأصل لا يكون إلا لمالك واحد؛ عند تعدد الملاك تُفرض الحصة السهمية
            // حتى لو أُرسل نوع حصة آخر (حماية البيانات على مستوى الخدمة).
            // الأنواع غير الحصصية (كفالة الرواتب والمتجر غير المسجل) لا تحمل مقدار حصة.
            if (AssetKindCatalog.HasShare(kind))
            {
                if (asset.Owners.Count > 1)
                    asset.ShareType = "حصة سهمية";
                else if (string.IsNullOrWhiteSpace(asset.ShareType))
                    asset.ShareType = AssetKindCatalog.FullShareLabel(kind);
            }
            else
            {
                asset.ShareType = null;
            }
            doc.Assets.Add(asset);
        }
    }

    /// <summary>
    /// تطبيع قائمة ملاك الأصل: يُتجاهل الاسم الفارغ، ويُقصّ الاسم من الطرفين،
    /// وتُلغى التكرارات مع الحفاظ على ترتيب الاختيار الأصلي.
    /// </summary>
    private static List<AssetOwner> NormalizeOwners(IEnumerable<string>? owners)
    {
        var result = new List<AssetOwner>();
        if (owners is null)
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var order = 0;
        foreach (var owner in owners)
        {
            var name = (owner ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                continue;

            result.Add(new AssetOwner { Name = name, Order = order++ });
        }

        return result;
    }

    /// <summary>
    /// تصفية صفوف الورثة الصالحة فقط: يُتجاهل الوريث الخالي من الاسم الثلاثي كاملًا
    /// (الاسم واسم الأب والنسبة جميعًا)، ويُقيَّد نوع العنوان بالقيم المسموح بها
    /// («عنوان»/«موطن مختار»/«وكيل») مع معاملة أي قيمة أخرى أو فارغة كـ«عنوان»،
    /// وصفة الوريث بالقيم المسموح بها («أصالة»/«إضافة لتركة»/«أصالة وإضافة»)
    /// مع معاملة أي قيمة أخرى أو فارغة كـ«أصالة».
    /// </summary>
    private static List<Heir> NormalizeHeirs(IEnumerable<HeirDto>? heirs, int? guarantorNumber)
    {
        var result = new List<Heir>();
        if (heirs is null)
            return result;

        foreach (var h in heirs)
        {
            var name = (h.Name ?? string.Empty).Trim();
            var father = (h.Father ?? string.Empty).Trim();
            var family = (h.Family ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(father) && string.IsNullOrWhiteSpace(family))
                continue;

            var addressType = (h.AddressType ?? string.Empty).Trim();
            if (addressType != "عنوان" && addressType != "وكيل" && addressType != "موطن مختار")
                addressType = "عنوان";

            var capacity = (h.Capacity ?? string.Empty).Trim();
            if (capacity != "إضافة لتركة" && capacity != "أصالة وإضافة")
                capacity = "أصالة";

            result.Add(new Heir
            {
                GuarantorNumber = guarantorNumber,
                HeirName = name,
                HeirFather = father,
                HeirFamily = family,
                HeirCapacity = capacity,
                AddressType = addressType,
                HeirAddress = (h.Address ?? string.Empty).Trim(),
            });
        }

        return result;
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

            var nature = NormalizePartyNature(a.Nature);
            var isLegal = PartyNatureCatalog.IsLegal(nature);

            // الشخص الاعتباري بلا تمثيل بالتركة ولا ممثل شرعي: تُصفَّر حقول الهوية الطبيعية
            // ويُحتفظ برقم التسجيل ومن يمثلها ونوع العنوان وعنوانه.
            var representationType = isLegal
                ? "أصالة"
                : NormalizeApplicantRepresentation(a.RepresentationType);
            var hasEstate = !isLegal && representationType is "إضافة لتركة" or "أصالة وإضافة";
            var hasRep = !isLegal && !IsEmptyRepresentative(a.RepresentativeName, a.RepresentativeFather, a.RepresentativeFamily);

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
                RepresentativeCapacity = hasRep ? NormalizeRepresentativeCapacity(a.RepresentativeCapacity) : null,
                RepresentativeLegalRepresentative = hasRep ? (a.RepresentativeLegalRepresentative ?? string.Empty).Trim() : null,
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
    /// تطبيع قائمة الجهات طالبة التنفيذ في وضع «طالبة تنفيذ»: يُتجاهل ما بلا اسم جهة،
    /// ويُقصّ اسم الجهة وفرعها ومحافظتها.
    /// </summary>
    private static List<ApplicantPublicEntity> NormalizeApplicantPublicEntities(IEnumerable<ApplicantPublicEntityDto>? entities)
    {
        var result = new List<ApplicantPublicEntity>();
        if (entities is null)
            return result;

        foreach (var e in entities)
        {
            var name = (e.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            result.Add(new ApplicantPublicEntity
            {
                Name = name,
                Branch = (e.Branch ?? string.Empty).Trim(),
                Governorate = (e.Governorate ?? string.Empty).Trim(),
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
            var hasRep = !IsEmptyRepresentative(p.RepresentativeName, p.RepresentativeFather, p.RepresentativeFamily);

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
                RepresentativeCapacity = hasRep ? NormalizeRepresentativeCapacity(p.RepresentativeCapacity) : null,
                RepresentativeAddressType = hasRep ? NormalizeRepresentativeAddressType(p.RepresentativeAddressType) : null,
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
    /// هل الممثل الشرعي غائب (اسمه الثلاثي فارغ كاملًا)؟ تُعدّ الحقول فارغة فلا يُخزَّن ممثل.
    /// </summary>
    private static bool IsEmptyRepresentative(string? name, string? father, string? family) =>
        string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(father) && string.IsNullOrWhiteSpace(family);

    /// <summary>
    /// صفة الممثل الشرعي المقبولة: ولي / وصي / قيم؛ أي قيمة أخرى أو فارغة تُعاد فارغة.
    /// </summary>
    private static string NormalizeRepresentativeCapacity(string? capacity)
    {
        var value = (capacity ?? string.Empty).Trim();
        return value is "ولي" or "وصي" or "قيم" ? value : string.Empty;
    }

    /// <summary>
    /// نوع عنوان الممثل الشرعي المقبول: موطن مختار / عنوان / وكيل قانوني؛ أي قيمة أخرى أو فارغة
    /// تُعيَّر إلى «عنوان».
    /// </summary>
    private static string NormalizeRepresentativeAddressType(string? addressType)
    {
        var value = (addressType ?? string.Empty).Trim();
        return value is "موطن مختار" or "عنوان" or "وكيل قانوني" ? value : "عنوان";
    }

    /// <summary>
    /// طبيعة الطرف المقبولة (مقترض/كفيل/طالب تنفيذ): شخص طبيعي (natural) أو شخص اعتباري (legal)؛
    /// أي قيمة أخرى أو فارغة تُعيَّر إلى «شخص طبيعي».
    /// </summary>
    private static string NormalizePartyNature(string? nature)
    {
        var value = (nature ?? string.Empty).Trim();
        return PartyNatureCatalog.ValidNatures.Contains(value) ? value : PartyNatureCatalog.Natural;
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
    /// قيود عائلة وضع «الجهة العامة منفذ عليها» (Executed + Deposit): عادي فقط (لا مصرفي)،
    /// مقيد (لا مسودة)، وبلا مقترض/كفلاء/أموال. وتُطبق أيضًا على الملفات الحالية التي
    /// تُحرَّر بوضعها الجديد.
    /// </summary>
    private static void ValidateExecutedRequest(DocumentUpsertRequest request)
    {
        if (!GeneralEntitySideCatalog.IsExecutedLike(request.GeneralEntitySide))
            return;

        var sideLabel = GeneralEntitySideCatalog.ToLabel(request.GeneralEntitySide!);

        if (string.IsNullOrWhiteSpace(request.FileNumber) || string.IsNullOrWhiteSpace(request.FileYear))
            throw new ArgumentException($"ملف «{sideLabel}» يجب أن يكون مقيدًا برقم وسنة الملف");

        var selector = string.IsNullOrWhiteSpace(request.ContractTypeSelector)
            ? "عادي"
            : request.ContractTypeSelector.Trim();
        if (selector == "مصرفي")
            throw new ArgumentException($"ملف «{sideLabel}» يكون بعقد عادي فقط (لا مصرفي)");

        if (!string.IsNullOrWhiteSpace(request.BorrowerName)
            || request.Guarantors.Count > 0
            || request.Assets.Count > 0
            || request.BorrowerHeirs.Count > 0)
            throw new ArgumentException($"ملف «{sideLabel}» لا يتضمن مقترضًا أو كفلاء أو أموالًا");
    }

    /// <summary>
    /// الملف المقيّد (بعد إدخال رقم الملف وسنة الملف) لا بد أن يحمل تاريخ قيد صالحًا،
    /// لأنه المعيار الوحيد في إحصاءات المتداول. وتُستثنى عائلة وضع «الجهة العامة منفذ عليها»
    /// لأن ملفها يقيده الخصم لا محامي الدولة، فتاريخ ورود الاخطار يغني عن تاريخ القيد.
    /// </summary>
    private static void ValidateRegistrationDate(DocumentUpsertRequest request)
    {
        if (GeneralEntitySideCatalog.IsExecutedLike(request.GeneralEntitySide))
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
        var parsed = ActionDateParser.TryParse(value);
        if (parsed is { } result)
        {
            date = result;
            return true;
        }
        date = default;
        return false;
    }

    /// <summary>
    /// التاريخ في وضع «منفذ عليه» يُرسَل نصًا حرًا (مثال: 1/8/2026) فيُفسَّر ويُخزَّن زمنيًا
    /// في القاعدة. الفارغ يعني null، وغير الصالح يُرفض برسالة تحمل اسم الحقل.
    /// </summary>
    private static DateTime? ParseDateTime(string? value, string fieldName)
        => FreeDateParser.Parse(value, fieldName);

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

    private static void FillDerivedFields(Document doc)
    {
        if (doc.AmountNumeric > 0 && string.IsNullOrWhiteSpace(doc.AmountWords))
            doc.AmountWords = FormatAmountWords(doc.AmountNumeric, doc.Currency);
        if (doc.Amount2Numeric > 0 && string.IsNullOrWhiteSpace(doc.Amount2Words))
            doc.Amount2Words = FormatAmountWords(doc.Amount2Numeric, doc.Currency2);
        if (doc.Amount3Numeric > 0 && string.IsNullOrWhiteSpace(doc.Amount3Words))
            doc.Amount3Words = FormatAmountWords(doc.Amount3Numeric, doc.Currency3);
        if (doc.InclusionAmountNumeric > 0 && string.IsNullOrWhiteSpace(doc.InclusionAmountWords))
            doc.InclusionAmountWords = FormatAmountWords(doc.InclusionAmountNumeric, doc.InclusionCurrency);
        if (doc.InclusionAmount2Numeric > 0 && string.IsNullOrWhiteSpace(doc.InclusionAmount2Words))
            doc.InclusionAmount2Words = FormatAmountWords(doc.InclusionAmount2Numeric, doc.InclusionCurrency2);
        if (doc.InclusionAmount3Numeric > 0 && string.IsNullOrWhiteSpace(doc.InclusionAmount3Words))
            doc.InclusionAmount3Words = FormatAmountWords(doc.InclusionAmount3Numeric, doc.InclusionCurrency3);

        doc.IsDraft = string.IsNullOrWhiteSpace(doc.FileNumber) || string.IsNullOrWhiteSpace(doc.FileYear);
        var label = doc.IsDraft ? ExecutionStatusCatalog.DraftFilter : "متداول";
        var borrower = (doc.BorrowerName ?? string.Empty).Trim();
        doc.DocumentType = string.IsNullOrWhiteSpace(borrower) ? label : $"{label} - {borrower}";

        // «طالب التنفيذ» في وضع «طالبة تنفيذ» يُشتق من قائمة الجهات (اسم + فرع بين قوسين)،
        // فتُوحَّد طريقة التخزين ويبقى النص متوافقًا مع البحث والتصدير والتوليد. وإن كانت
        // القائمة فارغة مع وجود نص قديم محفوظ يُحافظ عليه (توافق مع الطلبات القديمة).
        var applicantText = BuildApplicantText(doc.ApplicantPublicEntities);
        if (!string.IsNullOrWhiteSpace(applicantText) || string.IsNullOrWhiteSpace(doc.Applicant))
            doc.Applicant = applicantText;

        var parts = new[] { doc.BorrowerName, doc.BorrowerFamily, doc.Applicant, doc.Lawyer,
            doc.Court, doc.FileNumber, doc.ContractNumber, doc.AnnexNumber, doc.BorrowerNationalId,
            doc.BorrowerRegistrationNumber, doc.BorrowerRepresentedBy,
            doc.FileArrivalNumber, doc.FileArrivalDate }
            .Where(v => !string.IsNullOrWhiteSpace(v));
        // أسماء ورثة المتوفين (المقترض/الكفلاء) تنضم إلى نص البحث ليكون البحث بأسماء الورثة
        // متسقًا عبر SearchText وفلتر الورثة المباشر في المستودع.
        var applicantHeirNames = doc.Heirs
            .Select(h => string.Join(' ', h.HeirName, h.HeirFather, h.HeirFamily))
            .Where(v => !string.IsNullOrWhiteSpace(v));
        parts = parts.Concat(applicantHeirNames);
        // أسماء الكفلاء الاعتباريين وأرقام تسجيلهم تنضم إلى نص البحث.
        var guarantorLegalNames = doc.Guarantors
            .SelectMany(g => new[] { g.GuarantorName, g.GuarantorRegistrationNumber, g.GuarantorRepresentedBy })
            .Where(v => !string.IsNullOrWhiteSpace(v));
        parts = parts.Concat(guarantorLegalNames);
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            // ملف «منفذ عليه»/«عرض وايداع»: مقيد دائمًا، والعنوان يعتمد على حالة الوضع،
            // واسم البحث يضم أسماء طلبات التنفيذ/العرض والجهات/الأشخاص المنفذ عليهم.
            doc.IsDraft = false;
            doc.DocumentType = $"{ExecutedStatusCatalog.ToLabel(doc.ExecutedStatus ?? ExecutedStatusCatalog.None)}";
            var applicantNames = doc.ExecutionApplicants
                .Select(a => string.Join(' ', a.Name, a.Father, a.Family))
                .Where(v => !string.IsNullOrWhiteSpace(v));
            var applicantLegalFields = doc.ExecutionApplicants
                .SelectMany(a => new[] { a.ApplicantRegistrationNumber, a.ApplicantRepresentedBy })
                .Where(v => !string.IsNullOrWhiteSpace(v));
            var executedNames = doc.ExecutedPublicEntities
                .Select(e => string.Join(' ', e.EntityName, e.Governorate))
                .Concat(doc.ExecutedNaturalPersons.Select(p => string.Join(' ', p.Name, p.Father, p.Family)))
                .Where(v => !string.IsNullOrWhiteSpace(v));
            var entityLegalFields = doc.ExecutedPublicEntities
                .SelectMany(e => new[] { e.RegistrationNumber, e.RepresentedBy })
                .Where(v => !string.IsNullOrWhiteSpace(v));
            var executedHeirNames = doc.ExecutedHeirs
                .Select(h => string.Join(' ', h.HeirName, h.HeirFather, h.HeirFamily))
                .Where(v => !string.IsNullOrWhiteSpace(v));
            parts = parts
                .Concat(applicantNames)
                .Concat(applicantLegalFields)
                .Concat(executedNames)
                .Concat(entityLegalFields)
                .Concat(executedHeirNames);
        }
        // SearchText معرف بحد طول 1000 (HasMaxLength)؛ PostgreSQL يرفض القيم الأطول عند
        // الإدراج/التحديث بخلاف SQLite. يُقتطع إلى الحد الأقصى ليبقى عمود البحث متسقًا.
        doc.SearchText = TruncateSearchText(string.Join(' ', parts));

        doc.FullData = JsonSerializer.Serialize(new
        {
            doc.BorrowerName, doc.BorrowerFamily, doc.AmountNumeric, doc.Currency,
            doc.ContractNumber, doc.Court, doc.Applicant, doc.Lawyer
        });
    }

    private const int SearchTextMaxLength = 1000;

    private static string TruncateSearchText(string value)
    {
        if (value.Length <= SearchTextMaxLength)
            return value;

        // تجنب قصّ بداية زوج بديل UTF-16 (surrogate pair) في النهاية.
        var end = SearchTextMaxLength;
        if (end > 0 && char.IsHighSurrogate(value[end - 1]) && end < value.Length && char.IsLowSurrogate(value[end]))
            end--;
        return value[..end];
    }

    private static string FormatAmountWords(decimal amount, string? currency)
    {
        var words = NumberToWords.Convert((long)amount);
        return string.IsNullOrWhiteSpace(words)
            ? string.Empty
            : $"{words} {currency} فقط لا غير".Trim();
    }

    /// <summary>
    /// النص الموحّد لطالب التنفيذ في وضع «طالبة تنفيذ» من قائمة الجهات:
    /// «الجهة - محافظة X و الجهة - محافظة Y» — يُشتق ليغذي البحث والتصدير والتوليد.
    /// الفرع لا يُضمّن هنا؛ يُعرض ويُفلتر عبر حقل الفرع المستقل في ApplicantPublicEntities.Branch.
    /// </summary>
    private static string BuildApplicantText(IEnumerable<ApplicantPublicEntity> entities) =>
        string.Join(" و ", entities
            .Select(e =>
            {
                var name = (e.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                    return string.Empty;
                var governorate = (e.Governorate ?? string.Empty).Trim();
                return string.IsNullOrWhiteSpace(governorate) ? name : $"{name} - محافظة {governorate}";
            })
            .Where(v => v.Length > 0));
}
