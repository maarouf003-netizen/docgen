using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// تنفيذ خدمة الاستئنافات: دورة حياة الاستئناف من التسطير حتى الحسم أو الشطب،
/// مع الإسناد والنقل والتدوير والإجراءات المستقلة. كل كتابة ضمن معاملة واحدة مع
/// سجل التدقيق، والتنبيهات إشعارات فرعية لا تُفشل العملية الأصلية عند تعذرها.
/// </summary>
public sealed class DocumentAppealService : IDocumentAppealService
{
    private readonly IAppealRepository _appeals;
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IHeadAlertService _alerts;

    public DocumentAppealService(
        IAppealRepository appeals,
        IDocumentRepository documents,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IHeadAlertService alerts)
    {
        _appeals = appeals;
        _documents = documents;
        _users = users;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _alerts = alerts;
    }

    // ── التسطير والتعديل قبل الإسناد ───────────────────────────────────────

    public async Task<AppealDto> CreateAsync(
        int documentId,
        UpsertAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var source = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new ArgumentException("الملف غير موجود");

        ValidateSourceForAppeal(source);
        if (source.CreatedById != userId)
            throw new ArgumentException("لا يمكنك تسطير استئناف على ملف لا تملكه");

        var direction = ParseDirection(request.Direction);
        var (appellantsJson, appelleesJson) = BuildSnapshots(source, direction, request.Appellants);

        var appeal = new DocumentAppeal
        {
            DocumentId = source.Id,
            Direction = direction,
            Status = AppealStatusCatalog.Pending,
            AppellantsJson = appellantsJson,
            AppelleesJson = appelleesJson,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        ApplyUpsertFields(appeal, request);

        await _tx.RunAsync(async token =>
        {
            await _appeals.AddAsync(appeal, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_appeal",
                source.Id, source.DocumentType,
                $"سطّر استئنافًا ({AppealDirectionCatalog.ToLabel(direction)}) على الملف (رقم {source.Id})", token);
        }, ct);

        await NotifyHeadPendingAsync(appeal.Id, source, userId, actorName, ct);
        return ToDto(appeal);
    }

    public async Task<AppealDto?> UpdateAsync(
        int appealId,
        UpsertAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");

        EnsurePendingByCreator(appeal, userId);
        var source = appeal.Document;

        var direction = ParseDirection(request.Direction);
        var (appellantsJson, appelleesJson) = BuildSnapshots(source, direction, request.Appellants);

        appeal.Direction = direction;
        appeal.AppellantsJson = appellantsJson;
        appeal.AppelleesJson = appelleesJson;
        ApplyUpsertFields(appeal, request);
        appeal.UpdatedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update_appeal",
                source.Id, source.DocumentType,
                $"عدّل الاستئناف (رقم {appeal.Id}) على الملف (رقم {source.Id})", token);
        }, ct);

        return ToDto(appeal);
    }

    public async Task<bool> DeleteAsync(int appealId, int userId, string? actorName, CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");

        EnsurePendingByCreator(appeal, userId);
        var source = appeal.Document;

        await _tx.RunAsync(async token =>
        {
            _appeals.Remove(appeal);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "delete_appeal",
                source.Id, source.DocumentType,
                $"حذف الاستئناف (رقم {appeal.Id}) على الملف (رقم {source.Id})", token);
        }, ct);

        try
        {
            await _alerts.DeleteByAppealAsync(appealId, ct);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(actorName, "head_alert_failed",
                source.Id, source.DocumentType,
                $"تعذّر حذف تنبيهات الاستئناف المحذوف (رقم {appealId}): {ex.Message}", ct);
        }

        return true;
    }

    // ── القراءة والقوائم ───────────────────────────────────────────────────

    public async Task<List<AppealDto>> ListForDocumentAsync(int documentId, CancellationToken ct = default)
    {
        // فحص وجود رخيص (بلا تحميل الشجرة الكاملة للملف).
        if (!await _documents.ExistsAsync(documentId, ct))
            throw new ArgumentException("الملف غير موجود");
        var items = await _appeals.ListByDocumentAsync(documentId, ct);
        return items.Select(ToDto).ToList();
    }

    public Task<PagedResult<AppealDto>> SearchAsync(
        string? query, string? status, int? visibleBranchId, int? visibleUserId,
        int page, int perPage, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);
        return SearchCoreAsync(query, status, visibleBranchId, visibleUserId, page, perPage, ct);
    }

    private async Task<PagedResult<AppealDto>> SearchCoreAsync(
        string? query, string? status, int? visibleBranchId, int? visibleUserId,
        int page, int perPage, CancellationToken ct)
    {
        var (total, items) = await _appeals.SearchAsync(
            query, status, visibleBranchId, visibleUserId, page, perPage, ct);

        return new PagedResult<AppealDto>
        {
            Items = items.Select(ToDto).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<AppealDto?> GetAsync(int appealId, CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct);
        return appeal is null ? null : ToDto(appeal);
    }

    /// <summary>كيان الاستئناف بروابطه — يُستخدم داخليًا للتحقق من الصلاحيات في المتحكم.</summary>
    public Task<DocumentAppeal?> GetEntityAsync(int appealId, CancellationToken ct = default)
        => _appeals.GetByIdWithDetailsAsync(appealId, ct);

    // ── القيد والحسم والشطب (المحامي المتابع) ─────────────────────────────

    public async Task<AppealDto?> UpdateRegistrationAsync(
        int appealId,
        UpdateAppealRegistrationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");

        EnsureAssignedFollower(appeal, userId);

        appeal.AppealTypeLabel = Bounded(request.AppealTypeLabel, 100, "نوع الاستئناف");
        appeal.AppellateCourt = Bounded(request.AppellateCourt, 300, "محكمة الاستئناف التنفيذية المختصة");
        appeal.AppealBaseNumber = Bounded(request.AppealBaseNumber, 100, "رقم الأساس الاستئنافي");
        appeal.AppealYear = Bounded(request.AppealYear, 50, "لعام");
        appeal.RegistrationDate = DocumentValidator.ParseDateTime(request.RegistrationDate, "تاريخ إقرار الاستئناف");
        appeal.UpdatedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update_appeal_registration",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"حدّث قيد الاستئناف (رقم {appeal.Id}) برقم أساس {appeal.AppealBaseNumber ?? "—"}", token);
        }, ct);

        return ToDto(appeal);
    }

    public async Task<AppealDto?> DecideAsync(
        int appealId,
        DecideAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");

        EnsureAssignedFollower(appeal, userId);
        if (appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن حسم استئناف لم يبق منظورًا");

        var decisionNumber = RequireText(request.DecisionNumber, "رقم قرار الحسم", 100);
        var decisionDate = DocumentValidator.ParseDateTime(request.DecisionDate, "تاريخ قرار الحسم")
            ?? throw new ArgumentException("تاريخ قرار الحسم مطلوب");
        var ruling = RequireText(request.DecisionRuling, "منطوق القرار", 2000);
        var outcome = Normalize(request.Outcome)
            ?? throw new ArgumentException("نتيجة الاستئناف مطلوبة");
        if (!AppealOutcomeCatalog.ValidOutcomes.Contains(outcome))
            throw new ArgumentException("نتيجة الاستئناف غير صالحة — اختر: للصالح أو للضد");

        appeal.Status = AppealStatusCatalog.Decided;
        appeal.DecisionNumber = decisionNumber;
        appeal.DecisionDate = decisionDate;
        appeal.DecisionRuling = ruling;
        appeal.Outcome = outcome;
        appeal.UpdatedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "decide_appeal",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"حسم الاستئناف (رقم {appeal.Id}) بالقرار {decisionNumber} — النتيجة: {AppealOutcomeCatalog.ToLabel(outcome)}", token);
        }, ct);

        await NotifyBaseLawyerFinalAsync(appeal, decided: true, actorName, ct);
        return ToDto(appeal);
    }

    public async Task<AppealDto?> StrikeAsync(
        int appealId,
        StrikeAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");

        EnsureAssignedFollower(appeal, userId);
        if (appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن شطب استئناف لم يبق منظورًا");

        var struckOffDate = DocumentValidator.ParseDateTime(request.StruckOffDate, "تاريخ الشطب")
            ?? throw new ArgumentException("تاريخ الشطب مطلوب");
        var decisionNumber = RequireText(request.StruckOffDecisionNumber, "رقم قرار الشطب", 100);

        appeal.Status = AppealStatusCatalog.StruckOff;
        appeal.StruckOffDate = struckOffDate;
        appeal.StruckOffDecisionNumber = decisionNumber;
        appeal.UpdatedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "strike_appeal",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"شطب الاستئناف (رقم {appeal.Id}) بقرار رقم {decisionNumber}", token);
        }, ct);

        await NotifyBaseLawyerFinalAsync(appeal, decided: false, actorName, ct);
        return ToDto(appeal);
    }

    // ── الإسناد والنقل (رئيس القسم) ───────────────────────────────────────

    public async Task<AppealDto?> AssignAsync(
        int appealId,
        AssignAppealRequest request,
        int userId,
        int? headBranchId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await LoadForHeadActionAsync(appealId, headBranchId, ct);
        if (appeal.AssignedLawyerId is not null)
            throw new ArgumentException("لا يمكن إسناد استئناف أُسند سلفًا");

        var lawyer = await ResolveTargetLawyerAsync(request.AssignedLawyerId, appeal.Document.BranchId, ct);

        appeal.AssignedLawyerId = lawyer.Id;
        appeal.AssignedAt = DateTime.UtcNow;
        appeal.UpdatedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "assign_appeal",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"أسند الاستئناف (رقم {appeal.Id}) إلى المحامي {lawyer.FullName}", token);
        }, ct);

        // تصفية تنبيه «اختيار محامٍ للاستئناف» بعد الإسناد (أنجز مهمّته).
        try
        {
            await _alerts.DeleteByAppealAsync(appeal.Id, ct);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(actorName, "head_alert_failed",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"تعذّر حذف تنبيه الاستئناف المعلّق (رقم {appeal.Id}) بعد إسناده: {ex.Message}", ct);
        }

        await NotifyFollowLawyerAssignedAsync(appeal, lawyer, userId, actorName, ct);
        return ToDto(appeal);
    }

    public async Task<AppealDto?> TransferAsync(
        int appealId,
        TransferAppealRequest request,
        int userId,
        int? headBranchId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await LoadForHeadActionAsync(appealId, headBranchId, ct);
        if (request.TargetLawyerId == appeal.AssignedLawyerId)
            throw new ArgumentException("الاستئناف مسند لهذا المحامي سلفًا");

        var lawyer = await ResolveTargetLawyerAsync(request.TargetLawyerId, appeal.Document.BranchId, ct);

        appeal.AssignedLawyerId = lawyer.Id;
        appeal.AssignedAt = DateTime.UtcNow;
        appeal.UpdatedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "transfer_appeal",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"نقل الاستئناف (رقم {appeal.Id}) إلى المحامي {lawyer.FullName}", token);
        }, ct);

        await NotifyFollowLawyerAssignedAsync(appeal, lawyer, userId, actorName, ct);
        return ToDto(appeal);
    }

    public async Task<int> TransferAllAsync(
        TransferAllAppealsRequest request,
        int? headBranchId,
        string? actorName,
        CancellationToken ct = default)
    {
        if (headBranchId is null)
            throw new ArgumentException("رئيس القسم دون فرع لا يمكنه نقل الاستئنافات");
        if (request.SourceLawyerId == request.TargetLawyerId)
            throw new ArgumentException("لا يمكن نقل الاستئنافات إلى المحامي نفسه");

        var target = await ResolveTargetLawyerAsync(request.TargetLawyerId, headBranchId.Value, ct);

        var movable = await _appeals.ListByAssigneeAsync(
            request.SourceLawyerId, headBranchId.Value, asNoTracking: false, ct);

        await _tx.RunAsync(async token =>
        {
            foreach (var appeal in movable)
            {
                appeal.AssignedLawyerId = target.Id;
                appeal.AssignedAt = DateTime.UtcNow;
                appeal.UpdatedAt = DateTime.UtcNow;
                _appeals.Update(appeal);
            }
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "transfer_all_appeals",
                details: $"نقل {movable.Count} استئنافًا من المحامي (رقم {request.SourceLawyerId}) إلى المحامي {target.FullName}", ct: token);
        }, ct);

        return movable.Count;
    }

    public Task<int> CountByAssigneeForHeadAsync(int assigneeId, int? headBranchId, CancellationToken ct = default)
    {
        if (headBranchId is null)
            throw new ArgumentException("رئيس القسم دون فرع لا يمكنه الاطلاع على الاستئنافات");
        return _appeals.CountByAssigneeAsync(assigneeId, headBranchId.Value, ct);
    }

    // ── تدوير رقم الأساس الاستئنافي ───────────────────────────────────────

    public async Task<List<AppealBaseNumberHistoryDto>> GetBaseNumberHistoryAsync(
        int appealId, CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        return BuildBaseNumberHistory(appeal);
    }

    /// <summary>
    /// تاريخ أرقام الأساس الاستئنافية: سجلات التدوير لكل سنة، مع الرقم الأصلي المسجّل
    /// عند القيد (AppealYear/AppealBaseNumber) إن لم يكن له سجل تدوير بنفس السنة —
    /// لتظهر النافذة «بكافة أرقام الأساس السابقة» كاملة.
    /// </summary>
    private static List<AppealBaseNumberHistoryDto> BuildBaseNumberHistory(DocumentAppeal appeal)
    {
        var history = appeal.BaseNumbers
            .Select(b => new AppealBaseNumberHistoryDto(b.Year, b.BaseNumber))
            .ToList();
        if (!string.IsNullOrWhiteSpace(appeal.AppealBaseNumber)
            && int.TryParse(appeal.AppealYear, out var registeredYear)
            && !history.Any(h => h.Year == registeredYear))
        {
            history.Add(new AppealBaseNumberHistoryDto(registeredYear, appeal.AppealBaseNumber.Trim()));
        }
        return history.OrderBy(h => h.Year).ToList();
    }

    public async Task SaveBaseNumbersAsync(
        int appealId,
        SaveAppealBaseNumbersRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        if (appeal.AssignedLawyerId != userId && appeal.CreatedById != userId)
            throw new ArgumentException("لا يمكنك تدوير رقم أساس استئناف لا تتابعه");
        if (request.Entries is null || request.Entries.Count == 0)
            throw new ArgumentException("أدخل رقم الأساس الاستئنافي للسنة الحالية");
        if (request.Entries.Count > 1)
            throw new ArgumentException("يُدخل رقم أساس واحد لسنة التدوير الحالية");

        var year = DateTime.Today.Year;
        var entry = request.Entries[0];
        var value = Normalize(entry.BaseNumber)
            ?? throw new ArgumentException("أدخل رقم الأساس الاستئنافي للسنة الحالية");

        await _tx.RunAsync(async token =>
        {
            var existing = appeal.BaseNumbers.FirstOrDefault(b => b.Year == year);
            if (existing is not null)
            {
                existing.BaseNumber = value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                appeal.BaseNumbers.Add(new AppealBaseNumber
                {
                    AppealId = appeal.Id,
                    Year = year,
                    BaseNumber = value,
                    CreatedById = userId,
                });
            }
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "rotate_appeal_base_number",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"حدّث رقم الأساس الاستئنافي (رقم {appeal.Id}) لسنة {year} بالقيمة {value}", token);
        }, ct);
    }

    public Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default)
        => _appeals.IsAssignedFollowerAsync(documentId, userId, ct);

    // ── الإجراءات والملاحظات المستقلة ─────────────────────────────────────

    public async Task<List<AppealActionDto>> GetActionsAsync(int appealId, CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        return appeal.Actions
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => ToActionDto(a))
            .ToList();
    }

    public async Task<AppealActionDto> AddActionAsync(
        int appealId,
        AddAppealActionRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        EnsureAssignedFollower(appeal, userId);

        var action = new AppealAction
        {
            AppealId = appeal.Id,
            Type = Bounded(request.Type, 20, "نوع الإجراء") ?? "action",
            Text = RequireText(request.Text, "نص الإجراء"),
            ActionDate = Bounded(request.ActionDate, 50, "تاريخ الإجراء"),
            ReminderDuration = Bounded(request.ReminderDuration, 20, "مدة التذكير"),
            ReminderColor = Bounded(request.ReminderColor, 20, "لون التذكير"),
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            appeal.Actions.Add(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "add_appeal_action",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"أضاف إجراءً على الاستئناف (رقم {appeal.Id})", token);
        }, ct);

        return ToActionDto(action);
    }

    public async Task<AppealActionDto?> UpdateActionAsync(
        int appealId,
        int actionId,
        UpdateAppealActionRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        EnsureAssignedFollower(appeal, userId);
        var action = appeal.Actions.FirstOrDefault(a => a.Id == actionId)
            ?? throw new ArgumentException("الإجراء غير موجود");

        action.Type = Bounded(request.Type, 20, "نوع الإجراء") ?? action.Type;
        action.Text = RequireText(request.Text, "نص الإجراء");
        action.ActionDate = Bounded(request.ActionDate, 50, "تاريخ الإجراء");
        action.ReminderDuration = Bounded(request.ReminderDuration, 20, "مدة التذكير");
        action.ReminderColor = Bounded(request.ReminderColor, 20, "لون التذكير");

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update_appeal_action",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"عدّل إجراءً على الاستئناف (رقم {appeal.Id})", token);
        }, ct);

        return ToActionDto(action);
    }

    public async Task<bool> DeleteActionAsync(
        int appealId,
        int actionId,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        EnsureAssignedFollower(appeal, userId);
        var action = appeal.Actions.FirstOrDefault(a => a.Id == actionId)
            ?? throw new ArgumentException("الإجراء غير موجود");

        await _tx.RunAsync(async token =>
        {
            appeal.Actions.Remove(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "delete_appeal_action",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"حذف إجراءً من الاستئناف (رقم {appeal.Id})", token);
        }, ct);

        return true;
    }

    public async Task<bool> ClearReminderAsync(
        int appealId,
        int actionId,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        EnsureAssignedFollower(appeal, userId);
        var action = appeal.Actions.FirstOrDefault(a => a.Id == actionId)
            ?? throw new ArgumentException("الإجراء غير موجود");

        action.ReminderDuration = null;
        action.ReminderColor = null;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "clear_appeal_reminder",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"ألغى تذكير إجراء على الاستئناف (رقم {appeal.Id})", token);
        }, ct);

        return true;
    }

    public async Task<List<AppealReminderDto>> GetRemindersAsync(int userId, CancellationToken ct = default)
    {
        var rows = await ListReminderRowsAsync(userId, ct);
        return rows
            .Select(r => new AppealReminderDto(
                r.ActionId,
                r.AppealId,
                r.DocumentId,
                r.AppealTitle,
                r.Text,
                r.ActionDate,
                r.ReminderDuration,
                r.ReminderColor,
                ActionReminderCalculator.ComputeDueDate(r.ActionDate, r.ReminderDuration, r.CreatedAt)))
            .OrderBy(r => r.DueDate)
            .ThenBy(r => r.AppealId)
            .ToList();
    }

    private async Task<List<(int ActionId, int AppealId, int DocumentId, string AppealTitle, string Text,
        string? ActionDate, string? ReminderDuration, string? ReminderColor, DateTime CreatedAt)>>
        ListReminderRowsAsync(int userId, CancellationToken ct)
    {
        var appeals = await _appeals.ListByAssigneeAsync(userId, null, asNoTracking: true, ct);
        var rows = new List<(int, int, int, string, string, string?, string?, string?, DateTime)>();
        foreach (var appeal in appeals)
        {
            foreach (var action in appeal.Actions.Where(a => a.ReminderDuration != null || a.ReminderColor != null))
            {
                rows.Add((action.Id, appeal.Id, appeal.DocumentId,
                    AppealTitleOf(appeal), action.Text, action.ActionDate,
                    action.ReminderDuration, action.ReminderColor, action.CreatedAt));
            }
        }
        return rows;
    }

    // ── أدوات التحقق ──────────────────────────────────────────────────────

    /// <summary>شروط الاستئناف على الملف: مقيد (ليس تحت الرفع). الشطب لا يمنع الاستئناف.</summary>
    private static void ValidateSourceForAppeal(Document source)
    {
        if (source.IsDraft)
            throw new ArgumentException("لا يمكن الاستئناف على ملف تحت الرفع");
    }

    private static void EnsurePendingByCreator(DocumentAppeal appeal, int userId)
    {
        if (appeal.CreatedById != userId)
            throw new ArgumentException("لا يمكنك تعديل أو حذف استئناف لم تسطّره");
        if (appeal.AssignedLawyerId is not null || appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن تعديل أو حذف استئناف بعد إسناده");
    }

    private static void EnsureAssignedFollower(DocumentAppeal appeal, int userId)
    {
        if (appeal.AssignedLawyerId != userId)
            throw new ArgumentException("لا يمكنك تعديل هذا الاستئناف — ليس مسندًا إليك للمتابعة");
    }

    private async Task<DocumentAppeal> LoadForHeadActionAsync(int appealId, int? headBranchId, CancellationToken ct)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        if (appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن التنفيذ على استئناف لم يبق منظورًا");
        if (appeal.Document.BranchId is null || appeal.Document.BranchId != headBranchId)
            throw new ArgumentException("لا يمكنك التنفيذ على هذا الاستئناف — ليس ضمن فرعك");
        return appeal;
    }

    private async Task<User> ResolveTargetLawyerAsync(int lawyerId, int? branchId, CancellationToken ct)
    {
        var lawyer = await _users.GetByIdAsync(lawyerId, ct);
        if (lawyer is null || lawyer.Role != UserRole.Lawyer)
            throw new ArgumentException("المحامي المختص غير موجود");
        if (!lawyer.IsActive)
            throw new ArgumentException("المحامي المختص غير مفعل");
        if (branchId is null || lawyer.BranchId != branchId)
            throw new ArgumentException("المحامي المختص ليس ضمن فرع الملف");
        return lawyer;
    }

    // ── بناء لقطات الأطراف ────────────────────────────────────────────────

    private static string ParseDirection(string? value)
    {
        var direction = value?.Trim() ?? string.Empty;
        if (!AppealDirectionCatalog.ValidDirections.Contains(direction))
            throw new ArgumentException("اتجاه الاستئناف غير صالح — اختر: مستأنِفين أو مستأنف علينا");
        return direction;
    }

    /// <summary>خيارات «المستأنف» بحسب الاتجاه وصفة الملف:
    /// مستأنِفين ← الجهات العامة طالبة التنفيذ فقط؛
    /// مستأنف علينا ← المنفذ عليهم (طبيعيون/جهات/ورثة) في وضع «منفذ عليه»،
    /// والمقترض والكفلاء والورثة في وضع «طالبة تنفيذ».</summary>
    private static List<PartyOption> BuildOptions(Document doc, string direction)
    {
        var options = new List<PartyOption>();
        var executedLike = GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide);

        if (direction == AppealDirectionCatalog.Appellants)
        {
            if (executedLike)
            {
                foreach (var e in doc.ExecutionApplicants)
                    options.Add(new PartyOption("execution-applicant", e.Id, TripleOr(e.Name, e.Father, e.Family, e.LegalRepresentative)));
            }
            else
            {
                foreach (var e in doc.ApplicantPublicEntities)
                    options.Add(new PartyOption("applicant-entity", e.Id, Single(e.Name)));
            }
            return options;
        }

        if (executedLike)
        {
            foreach (var p in doc.ExecutedNaturalPersons)
                options.Add(new PartyOption("executed-natural", p.Id, TripleOr(p.Name, p.Father, p.Family, null)));
            foreach (var e in doc.ExecutedPublicEntities)
                options.Add(new PartyOption("executed-public", e.Id, Single(e.EntityName)));
            foreach (var h in doc.ExecutedHeirs)
                options.Add(new PartyOption("executed-heir", h.Id, TripleOr(h.HeirName, h.HeirFather, h.HeirFamily, null)));
        }
        else
        {
            options.Add(new PartyOption("borrower", doc.Id, TripleOr(doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily, doc.BorrowerRepresentativeName)));
            foreach (var g in doc.Guarantors)
                options.Add(new PartyOption("guarantor", g.Id, TripleOr(g.GuarantorName, g.GuarantorFather, g.GuarantorFamily, null)));
            foreach (var h in doc.Heirs)
                options.Add(new PartyOption("heir", h.Id, TripleOr(h.HeirName, h.HeirFather, h.HeirFamily, null)));
        }
        return options;
    }

    /// <summary>سجل كامل لأطراف الملف من الجهتين — أساس خانة «المستأنف عليهم»:
    /// كل أطراف الملف ناقص المختارين ضمن المستأنف، لأن الاستئناف يكون بمواجهة الجميع حكمًا.</summary>
    private static List<PartyOption> BuildAllParties(Document doc)
    {
        var executedLike = GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide);
        var parties = new List<PartyOption>();

        if (executedLike)
        {
            foreach (var a in doc.ExecutionApplicants)
                parties.Add(new PartyOption("execution-applicant", a.Id, TripleOr(a.Name, a.Father, a.Family, a.LegalRepresentative)));
            foreach (var p in doc.ExecutedNaturalPersons)
                parties.Add(new PartyOption("executed-natural", p.Id, TripleOr(p.Name, p.Father, p.Family, null)));
            foreach (var h in doc.ExecutedHeirs)
                parties.Add(new PartyOption("executed-heir", h.Id, TripleOr(h.HeirName, h.HeirFather, h.HeirFamily, null)));
            foreach (var e in doc.ExecutedPublicEntities)
                parties.Add(new PartyOption("executed-public", e.Id, Single(e.EntityName)));
        }
        else
        {
            foreach (var e in doc.ApplicantPublicEntities)
                parties.Add(new PartyOption("applicant-entity", e.Id, Single(e.Name)));
            parties.Add(new PartyOption("borrower", doc.Id, TripleOr(doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily, doc.BorrowerRepresentativeName)));
            foreach (var g in doc.Guarantors)
                parties.Add(new PartyOption("guarantor", g.Id, TripleOr(g.GuarantorName, g.GuarantorFather, g.GuarantorFamily, null)));
            foreach (var h in doc.Heirs)
                parties.Add(new PartyOption("heir", h.Id, TripleOr(h.HeirName, h.HeirFather, h.HeirFamily, null)));
        }
        return parties;
    }

    private static (string AppellantsJson, string AppelleesJson) BuildSnapshots(
        Document doc, string direction, List<AppealPartySelectionDto>? selections)
    {
        var options = BuildOptions(doc, direction);
        if (options.Count == 0)
            throw new ArgumentException("لا توجد أطراف مؤهلة للاختيار كمستأنف على هذا الملف");

        var selected = (selections ?? new List<AppealPartySelectionDto>())
            .Select(s => $"{s.Kind.Trim()}:{s.PartyId}")
            .Distinct()
            .ToList();

        if (selected.Count == 0)
            throw new ArgumentException("يجب اختيار المستأنف");

        var byKey = options.ToDictionary(o => $"{o.Kind}:{o.PartyId}");
        var appellants = new List<AppealPartyDto>();
        foreach (var key in selected)
        {
            if (!byKey.TryGetValue(key, out var option))
                throw new ArgumentException("أحد المستأنفين المختارين لا يتبع أطراف الملف");
            appellants.Add(new AppealPartyDto(option.Kind, option.PartyId, option.Name));
        }

        // المستأنف عليهم = سجل أطراف الملف الكامل ناقص المختارين (مواجهة الجميع حكمًا).
        var selectedKeys = selected.ToHashSet();
        var appellees = BuildAllParties(doc)
            .Where(o => !selectedKeys.Contains($"{o.Kind}:{o.PartyId}"))
            .Select(o => new AppealPartyDto(o.Kind, o.PartyId, o.Name))
            .ToList();

        return (
            AppealSnapshotSerializer.SerializeParties(appellants),
            AppealSnapshotSerializer.SerializeParties(appellees));
    }

    private static void ApplyUpsertFields(DocumentAppeal appeal, UpsertAppealRequest request)
    {
        appeal.AppealTypeLabel = Bounded(request.AppealTypeLabel, 100, "نوع الاستئناف");
        appeal.AppealedDecisionText = RequireText(request.AppealedDecisionText, "نص القرار المستأنف", 2000);
        appeal.AppealedDecisionSummary = Bounded(request.AppealedDecisionSummary, 2000, "ملخص القرار المطلوب استئنافه");
        appeal.AppealedDecisionDate = DocumentValidator.ParseDateTime(request.AppealedDecisionDate, "تاريخ القرار المستأنف");
        appeal.InspectionBookNumber = Bounded(request.InspectionBookNumber, 200, "رقم كتاب المطالعة وإيداع الملف رئيس القسم");
        appeal.InspectionBookDate = DocumentValidator.ParseDateTime(request.InspectionBookDate, "تاريخ كتاب المطالعة وإيداع الملف رئيس القسم");
        appeal.GroundsSummary = Bounded(request.GroundsSummary, 2000, "ملخص كتاب المطالعة المتضمن موجبات الاستئناف");
        appeal.NoticeNumber = Bounded(request.NoticeNumber, 200, "رقم ورود سند تبليغ الاستئناف");
        appeal.NoticeDate = DocumentValidator.ParseDateTime(request.NoticeDate, "تاريخ ورود سند تبليغ الاستئناف");
        appeal.AppellateCourt = Bounded(request.AppellateCourt, 300, "محكمة الاستئناف التنفيذية المختصة");
        appeal.AppealBaseNumber = Bounded(request.AppealBaseNumber, 100, "رقم الأساس الاستئنافي");
        appeal.AppealYear = Bounded(request.AppealYear, 50, "لعام");
        appeal.DepositBookNumber = Bounded(request.DepositBookNumber, 200, "رقم كتاب إيداع الملف رئيس القسم");
        appeal.DepositBookDate = DocumentValidator.ParseDateTime(request.DepositBookDate, "تاريخ كتاب إيداع الملف رئيس القسم");
        appeal.DefenseOpinion = Bounded(request.DefenseOpinion, 2000, "رأي المحامي المتابع للملف بأسباب الاستئناف");
        appeal.Notes = Bounded(request.Notes, 2000, "الملاحظات");
        appeal.UpdatedAt = DateTime.UtcNow;
    }

    // ── التنبيهات (إشعارات فرعية لا تُفشل العملية) ────────────────────────

    private async Task NotifyHeadPendingAsync(int appealId, Document source, int userId, string? actorName, CancellationToken ct)
    {
        if (source.BranchId is null)
        {
            await _audit.LogAsync(actorName, "head_alert_failed",
                source.Id, source.DocumentType,
                $"تعذّر إشعار رئيس القسم بالاستئناف المعلّق (رقم {appealId}): الملف بلا فرع", ct);
            return;
        }
        try
        {
            await _alerts.CreateAsync(new CreateHeadAlertRequest(
                TargetType: "head",
                DocumentId: source.Id,
                TargetLawyerId: null,
                Message: $"وقع استئناف بملف {DocumentTitle(source)} رقم {source.FileNumber ?? "—"} نوع {source.FileType ?? "—"} دائرة تنفيذ {source.Court ?? "—"}، يرجى اختيار محامي لمتابعة الاستئناف",
                AppealId: appealId),
                userId, source.BranchId.Value, actorName, ct);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(actorName, "head_alert_failed",
                source.Id, source.DocumentType,
                $"تعذّر إشعار رئيس القسم بالاستئناف المعلّق (رقم {appealId}): {ex.Message}", ct);
        }
    }

    private async Task NotifyFollowLawyerAssignedAsync(DocumentAppeal appeal, User lawyer, int userId, string? actorName, CancellationToken ct)
    {
        var branchId = appeal.Document.BranchId;
        if (branchId is null)
            return;
        try
        {
            await _alerts.CreateAsync(new CreateHeadAlertRequest(
                TargetType: "lawyer",
                DocumentId: appeal.DocumentId,
                TargetLawyerId: lawyer.Id,
                Message: $"أحال إليك رئيس القسم استئناف لمتابعته أصولًا (ملف {DocumentTitle(appeal.Document)})",
                AppealId: appeal.Id),
                userId, branchId.Value, actorName, ct);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(actorName, "head_alert_failed",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"تعذّر إشعار المحامي {lawyer.FullName} بإسناد الاستئناف (رقم {appeal.Id}): {ex.Message}", ct);
        }
    }

    private async Task NotifyBaseLawyerFinalAsync(DocumentAppeal appeal, bool decided, string? actorName, CancellationToken ct)
    {
        var source = appeal.Document;
        var branchId = source.BranchId;
        if (branchId is null || source.CreatedById == appeal.AssignedLawyerId)
            return; // لا إشعار ذاتيًا عندما المتابع هو نفسه محامي الملف الأساس.
        try
        {
            var outcomePart = decided && appeal.Outcome is not null
                ? $" — النتيجة: {AppealOutcomeCatalog.ToLabel(appeal.Outcome)}"
                : string.Empty;
            var state = decided ? "محسومًا" : "مشطوبًا";
            await _alerts.CreateAsync(new CreateHeadAlertRequest(
                TargetType: "document",
                DocumentId: source.Id,
                TargetLawyerId: null,
                Message: $"أصبح استئناف قرار رئيس التنفيذ في الملف ({DocumentTitle(source)}) {state}{outcomePart}",
                AppealId: appeal.Id),
                appeal.AssignedLawyerId ?? source.CreatedById, branchId.Value, actorName, ct);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(actorName, "head_alert_failed",
                source.Id, source.DocumentType,
                $"تعذّر إشعار محامي الملف الأساس بحالة الاستئناف (رقم {appeal.Id}): {ex.Message}", ct);
        }
    }

    // ── أدوات مساعدة ──────────────────────────────────────────────────────

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>تطبيع نص اختياري مع رفض تجاوز الحد الأقصى برسالة عربية واضحة
    /// (بدل استثناء قاعدة البيانات الخام عند Postgres).</summary>
    private static string? Bounded(string? value, int maxLength, string fieldName)
    {
        var normalized = Normalize(value);
        if (normalized is not null && normalized.Length > maxLength)
            throw new ArgumentException($"طول {fieldName} يتجاوز الحد الأقصى ({maxLength} حرفًا)");
        return normalized;
    }

    private static string RequireText(string? value, string fieldName, int maxLength = 2000)
    {
        var normalized = Bounded(value, maxLength, fieldName)
            ?? throw new ArgumentException($"{fieldName} مطلوب");
        return normalized;
    }

    private static string Single(string? name) => string.IsNullOrWhiteSpace(name) ? "—" : name!.Trim();

    private static string TripleOr(string? first, string? second, string? third, string? fallback)
    {
        var parts = new[] { first, second, third }.Where(v => !string.IsNullOrWhiteSpace(v));
        var triple = string.Join(' ', parts);
        return !string.IsNullOrWhiteSpace(triple) ? triple : (string.IsNullOrWhiteSpace(fallback) ? "—" : fallback!.Trim());
    }

    /// <summary>اسم عرض للملف: الثلاثي إن توفر وإلا نوع المستند وإلا «ملف رقم».</summary>
    private static string DocumentTitle(Document doc)
    {
        var parts = new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }
            .Where(v => !string.IsNullOrWhiteSpace(v));
        var name = string.Join(' ', parts);
        if (!string.IsNullOrWhiteSpace(name)) return name;
        if (!string.IsNullOrWhiteSpace(doc.ExecutedNaturalPersons.FirstOrDefault(p => p.Name != null)?.Name))
            return doc.ExecutedNaturalPersons.First(p => p.Name != null).Name!.Trim();
        return string.IsNullOrWhiteSpace(doc.DocumentType) ? $"ملف رقم {doc.Id}" : doc.DocumentType!;
    }

    /// <summary>عنوان موجز للاستئناف في التذكيرات.</summary>
    private static string AppealTitleOf(DocumentAppeal appeal)
        => $"استئناف قرار رئيس التنفيذ — {AppealDirectionCatalog.ToLabel(appeal.Direction)}";

    private static AppealActionDto ToActionDto(AppealAction action) => new(
        action.Id,
        action.Type,
        action.Text,
        action.ActionDate,
        action.ReminderDuration,
        action.ReminderColor,
        action.CreatedBy?.FullName,
        action.CreatedAt);

    private static AppealDto ToDto(DocumentAppeal a)
    {
        var d = a.Document;
        var currentYear = DateTime.Today.Year;
        var currentRow = a.BaseNumbers.FirstOrDefault(b => b.Year == currentYear)?.BaseNumber;
        var latestRecorded = a.BaseNumbers.Count > 0
            ? a.BaseNumbers.Max(b => b.Year)
            : int.TryParse(a.AppealYear, out var year) ? year : 0;
        var needsRotation = a.Status == AppealStatusCatalog.Pending
            && latestRecorded > 0
            && latestRecorded < currentYear
            && currentRow is null;

        return new AppealDto(
            a.Id,
            a.DocumentId,
            DocumentTitle(d),
            d.FileNumber,
            d.FileType,
            d.FileYear,
            d.Court,
            a.Direction,
            AppealDirectionCatalog.ToLabel(a.Direction),
            a.Status,
            AppealStatusCatalog.ToLabel(a.Status),
            a.AppealTypeLabel,
            AppealSnapshotSerializer.DeserializeParties(a.AppellantsJson),
            AppealSnapshotSerializer.DeserializeParties(a.AppelleesJson),
            a.AppealedDecisionText,
            a.AppealedDecisionSummary,
            FreeDateParser.ToResponse(a.AppealedDecisionDate),
            a.InspectionBookNumber,
            FreeDateParser.ToResponse(a.InspectionBookDate),
            a.GroundsSummary,
            a.NoticeNumber,
            FreeDateParser.ToResponse(a.NoticeDate),
            a.AppellateCourt,
            a.AppealBaseNumber,
            a.AppealYear,
            a.DepositBookNumber,
            FreeDateParser.ToResponse(a.DepositBookDate),
            a.DefenseOpinion,
            FreeDateParser.ToResponse(a.RegistrationDate),
            a.DecisionNumber,
            FreeDateParser.ToResponse(a.DecisionDate),
            a.DecisionRuling,
            a.Outcome,
            a.Outcome is null ? null : AppealOutcomeCatalog.ToLabel(a.Outcome),
            FreeDateParser.ToResponse(a.StruckOffDate),
            a.StruckOffDecisionNumber,
            a.Notes,
            needsRotation,
            currentRow ?? a.AppealBaseNumber,
            a.AssignedLawyerId,
            a.AssignedLawyer?.FullName,
            a.CreatedAt,
            a.CreatedBy?.FullName,
            a.CreatedById);
    }

    /// <summary>خيار طرف داخل لقطات الاستئناف (بناء داخلي قبل التسلسل).</summary>
    private sealed record PartyOption(string Kind, int PartyId, string Name);
}
