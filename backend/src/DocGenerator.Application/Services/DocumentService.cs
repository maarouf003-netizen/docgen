using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
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
    Task<DocumentResponse?> UpdateAsync(int documentId, DocumentUpsertRequest request, string? actorName, CancellationToken ct = default);
    Task<bool> DeleteAsync(int documentId, string? actorName, CancellationToken ct = default);
    Task<bool> RestoreAsync(int documentId, string? actorName, CancellationToken ct = default);
    Task<DocumentResponse> TransferAsync(int documentId, int targetLawyerId, string? actorName, CancellationToken ct = default);
    Task<PagedResult<DocumentResponse>> SearchDeletedAsync(string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default);
    Task<PagedResult<DocumentResponse>> SearchAsync(string? query, string? status, string? applicant, string? court, string? lawyer, int? branchId, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default);
    Task<(List<string> Applicants, List<string> Courts, List<string> Lawyers)> GetFilterOptionsAsync(int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(int documentId, string status, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default);
    Task<bool> CancelStatusAsync(int documentId, string? actorName, CancellationToken ct = default);
    Task IncrementViewCountAsync(int documentId, CancellationToken ct = default);
    Task<List<ExecutionActionDto>> GetExecutionActionsAsync(int documentId, CancellationToken ct = default);
    Task<ExecutionActionDto> AddExecutionActionAsync(int documentId, AddExecutionActionRequest request, int userId, string? actorName, CancellationToken ct = default);
    Task<ExecutionActionDto?> UpdateExecutionActionAsync(int documentId, int actionId, UpdateExecutionActionRequest request, string? actorName, CancellationToken ct = default);
    Task<bool> DeleteExecutionActionAsync(int documentId, int actionId, string? actorName, CancellationToken ct = default);
    Task<bool> ClearReminderAsync(int documentId, int actionId, string? actorName, CancellationToken ct = default);
}

public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IRepository<Guarantor> _guarantors;
    private readonly IRepository<RealEstate> _realEstates;
    private readonly IRepository<ExecutionAction> _actions;
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
        if (string.IsNullOrWhiteSpace(request.BorrowerName))
            throw new ArgumentException("اسم المقترض مطلوب");

        var doc = new Document
        {
            BranchId = branchId,
            CreatedById = userId,
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
            await _audit.LogAsync(actorName, "create", doc.Id, doc.DocumentType, $"أنشأ المستند (رقم {doc.Id})", token);
            return DocumentResponse.FromEntity(doc);
        }, ct);
    }

    public async Task<DocumentResponse?> UpdateAsync(int documentId, DocumentUpsertRequest request, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return null;

        ApplyRequest(doc, request);
        FillDerivedFields(doc);
        ApplyRegistrationDate(doc, request.FileRegistrationDate);
        doc.UpdatedAt = DateTime.UtcNow;

        return await _tx.RunAsync(async token =>
        {
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update", doc.Id, doc.DocumentType, $"عدّل المستند (رقم {doc.Id})", token);
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
            await _audit.LogAsync(actorName, "delete", documentId, doc.DocumentType, $"حذف المستند (رقم {documentId})", token);
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
            await _audit.LogAsync(actorName, "restore", documentId, doc.DocumentType, $"استعادة المستند (رقم {documentId})", token);
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
                $"نقل الملف إلى المحامي: {target.FullName}", token);

            return DocumentResponse.FromEntity(transferred);
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

    public async Task<PagedResult<DocumentResponse>> SearchAsync(
        string? query, string? status, string? applicant, string? court, string? lawyer, int? branchId, int page, int perPage,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchAsync(
            query, status, applicant, court, lawyer, branchId, visibleBranchId, visibleUserId, page, perPage, ct);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(DocumentResponse.FromEntity).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<(List<string> Applicants, List<string> Courts, List<string> Lawyers)> GetFilterOptionsAsync(
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
        => await _documents.GetFilterOptionsAsync(visibleBranchId, visibleUserId, ct);

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
            await _audit.LogAsync(actorName, "status", doc.Id, doc.DocumentType, auditDetail, token);
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
            await _audit.LogAsync(actorName, "status", doc.Id, doc.DocumentType, "إلغاء الحالة", token);
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
                $"أضاف {TypeLabel(type)}: {action.Text}", token);
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

        action.Type = type;
        action.Text = text;
        action.ActionDate = actionDate;
        action.ReminderDuration = reminderDuration;
        action.ReminderColor = reminderColor;

        await _tx.RunAsync(async token =>
        {
            _actions.Update(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", documentId, null,
                $"عدّل {TypeLabel(type)}: {action.Text}", token);
        }, ct);
        return new ExecutionActionDto(action.Id, action.Type, action.Text, action.ActionDate,
            action.ReminderDuration, action.ReminderColor, actorName, action.CreatedAt);
    }

    public async Task<bool> DeleteExecutionActionAsync(int documentId, int actionId, string? actorName, CancellationToken ct = default)
    {
        var action = await _actions.GetByIdAsync(actionId, ct);
        if (action is null || action.DocumentId != documentId)
            return false;

        return await _tx.RunAsync(async token =>
        {
            _actions.Remove(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", documentId, null,
                $"حذف {TypeLabel(action.Type)}: {action.Text}", token);
            return true;
        }, ct);
    }

    public async Task<bool> ClearReminderAsync(int documentId, int actionId, string? actorName, CancellationToken ct = default)
    {
        var action = await _actions.GetByIdAsync(actionId, ct);
        if (action is null || action.DocumentId != documentId)
            return false;

        action.ReminderDuration = null;
        action.ReminderColor = null;

        return await _tx.RunAsync(async token =>
        {
            _actions.Update(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", documentId, null,
                $"ألغى التذكير عن {TypeLabel(action.Type)}: {action.Text}", token);
            return true;
        }, ct);
    }

    private static (string Type, string Text, string? ActionDate) NormalizeAction(string type, string text, string? actionDate)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("نص الإجراء أو الملاحظة مطلوب");

        type = (type ?? "action").Trim();
        if (type is not ("action" or "note"))
            throw new ArgumentException("نوع غير صالح");

        var trimmedText = text.Trim();
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

        return (type, trimmedText, trimmedDate);
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
        doc.Lawyer = r.Lawyer;
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

        doc.RealEstates.Clear();
        foreach (var re in r.RealEstates)
        {
            doc.RealEstates.Add(new RealEstate
            {
                Owner = re.Owner,
                Property = re.Property,
                PropertyNumber = re.PropertyNumber,
                PropertyDistrict = re.PropertyDistrict,
                LandRegistry = re.LandRegistry,
                ShareType = re.ShareType,
            });
        }
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
