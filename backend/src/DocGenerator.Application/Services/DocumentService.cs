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
    /// «اعتبار الملف منفذًا كاملًا بهذا البيع» (نظام «طالبة تنفيذ»): إغلاق «منفذ جبريا (منفذ
    /// جزئيا)» الذي فُعّل تلقائيًا بإتمام إنابة إلى «منفذ كاملا» — يُخصم من ملفٍ منفذ جزئيًا
    /// وفيه إنابة منفذة، ويُلزم إدخال «تاريخ تحويل بدل المبيع للجهة العامة» (و«رقم الإشعار»
    /// اختياريًا)، ويُسجَّل وقعة «منفذ جبريا» في وقوعات الملف. حينها فقط يدخل بدل الإنابة
    /// ضمن «إحصاءات منفذ جبريا» مرة واحدة (عبر مسار DelegationSalesAmount القائم).
    /// </summary>
    Task<bool> ConsiderExecutedByDelegationAsync(int documentId, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default);
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

public sealed partial class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IRepository<Guarantor> _guarantors;
    private readonly IRepository<Asset> _assets;
    private readonly IRepository<ExecutionAction> _actions;
    private readonly IRepository<DocumentBaseNumber> _baseNumbers;
    private readonly IRepository<DocumentRegistrationDate> _registrationDates;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IDelegationRepository _delegations;
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
        IDelegationRepository delegations,
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
        _delegations = delegations;
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

        DocumentValidator.ValidateSide(request);
        DocumentValidator.ValidateExecutedRequest(request);
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

        DocumentValidator.ValidateSide(request);
        DocumentValidator.ValidateExecutedRequest(request);
        DocumentValidator.ValidateRegistrationDate(request);

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
            await SyncDelegationSnapshotsForDocumentAsync(doc, token);
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

    public async Task IncrementViewCountAsync(int documentId, CancellationToken ct = default)
    {
        await _documents.IncrementViewCountAsync(documentId, ct);
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

}
