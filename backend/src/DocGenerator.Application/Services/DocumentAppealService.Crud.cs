using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentAppealService : IDocumentAppealService
{
    // ── التسطير والتعديل قبل الإسناد ───────────────────────────────────────

    public async Task<AppealDto> CreateAsync(
        int documentId,
        UpsertAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var source = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new KeyNotFoundException("الملف غير موجود");

        ValidateSourceForAppeal(source);
        // P2-02f: فشل الصلاحية = 403 لا 400 (نفس تصحيح مسارات الإنابة).
        if (source.CreatedById != userId)
            throw new UnauthorizedAccessException("لا يمكنك تسطير استئناف على ملف لا تملكه");

        var direction = ParseDirection(request.Direction);
        var (appellantsJson, appelleesJson) = BuildSnapshots(source, direction, request.Appellants);
        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);

        var appeal = new DocumentAppeal
        {
            DocumentId = source.Id,
            Direction = direction,
            Status = AppealStatusCatalog.Pending,
            AppellantsJson = appellantsJson,
            AppelleesJson = appelleesJson,
            ForwardState = AppealForwardCatalog.Owned,
            Version = 1,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        ApplyUpsertFields(appeal, request, currentYear);

        await _tx.RunAsync(async token =>
        {
            await _appeals.AddAsync(appeal, token);
            await _uow.SaveChangesAsync(token);
            await EnqueueAppealPendingAsync(appeal, source, userId, DateTime.UtcNow, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_appeal",
                source.Id, source.DocumentType,
                $"تسطير الاستئناف ({AppealDirectionCatalog.ToLabel(direction)}) على الملف (رقم {source.Id})", token);
        }, ct);

        return ToDto(appeal, currentYear);
    }

    public async Task<AppealDto?> UpdateAsync(
        int appealId,
        UpsertAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new KeyNotFoundException("الاستئناف غير موجود");

        EnsurePendingByCreator(appeal, userId);
        var source = appeal.Document;

        var direction = ParseDirection(request.Direction);
        var (appellantsJson, appelleesJson) = BuildSnapshots(source, direction, request.Appellants);

        appeal.Direction = direction;
        appeal.AppellantsJson = appellantsJson;
        appeal.AppelleesJson = appelleesJson;
        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        ApplyUpsertFields(appeal, request, currentYear);
        appeal.UpdatedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update_appeal",
                source.Id, source.DocumentType,
                $"عدّل الاستئناف (رقم {appeal.Id}) على الملف (رقم {source.Id})", token);
        }, ct);

        return ToDto(appeal, currentYear);
    }

    public async Task<bool> DeleteAsync(int appealId, int userId, string? actorName, CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new KeyNotFoundException("الاستئناف غير موجود");

        EnsurePendingByCreator(appeal, userId);
        var source = appeal.Document;

        await _tx.RunAsync(async token =>
        {
            await _alerts.ArchiveByAppealAsync(
                appeal.Id,
                HeadAlertKindCatalog.AppealKinds,
                "appeal_deleted",
                token);
            _appeals.Remove(appeal);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "delete_appeal",
                source.Id, source.DocumentType,
                $"حذف الاستئناف (رقم {appeal.Id}) من الملف (رقم {source.Id})", token);
        }, ct);

        return true;
    }

    // ── القراءة والقوائم ───────────────────────────────────────────────────

    public async Task<List<AppealDto>> ListForDocumentAsync(int documentId, CancellationToken ct = default)
    {
        // فحص وجود رخيص (بلا تحميل الشجرة الكاملة للملف).
        if (!await _documents.ExistsAsync(documentId, ct))
            throw new ArgumentException("الملف غير موجود");
        var items = await _appeals.ListByDocumentAsync(documentId, ct);
        return items.Select(a => ToDto(a, ServerClock.CurrentYear(_clock, _timeZone))).ToList();
    }

    public Task<PagedResult<AppealDto>> SearchAsync(
        string? query, string? status, int? visibleBranchId, int? visibleUserId,
        int? ownerSectionId, int page, int perPage, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);
        return SearchCoreAsync(query, status, visibleBranchId, visibleUserId, ownerSectionId, page, perPage, ct);
    }

    private async Task<PagedResult<AppealDto>> SearchCoreAsync(
        string? query, string? status, int? visibleBranchId, int? visibleUserId,
        int? ownerSectionId, int page, int perPage, CancellationToken ct)
    {
        var (total, items) = await _appeals.SearchAsync(
            query, status, visibleBranchId, visibleUserId, ownerSectionId, page, perPage, ct);

        return new PagedResult<AppealDto>
        {
            Items = items.Select(a => ToDto(a, ServerClock.CurrentYear(_clock, _timeZone))).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<AppealDto?> GetAsync(int appealId, CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct);
        return appeal is null ? null : ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
    }

    /// <summary>كيان الاستئناف بروابطه — يُستخدم داخليًا للتحقق من الصلاحيات في المتحكم.</summary>
    public Task<DocumentAppeal?> GetEntityAsync(int appealId, CancellationToken ct = default)
        => _appeals.GetByIdWithDetailsAsync(appealId, ct);

    private static void ApplyUpsertFields(DocumentAppeal appeal, UpsertAppealRequest request, int currentYear)
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
        // رقم الأساس يخضع لسياسة واحدة في كل المسارات: الطول والحدود الزمنية.
        // السنة بلا رقم لا معنى لها فلا تُحفظ (بيانات نظيفة).
        var appealBaseNumber = Bounded(
            request.AppealBaseNumber, AppealBaseNumberPolicy.MaxNumberLength, "رقم الأساس الاستئنافي");
        var appealBaseYear = AppealBaseNumberPolicy.ValidateYear(request.AppealYear, currentYear, "لعام");
        appeal.AppealBaseNumber = appealBaseNumber;
        appeal.AppealYear = appealBaseNumber is null
            ? null
            : appealBaseYear?.ToString(CultureInfo.InvariantCulture);
        // كتاب إيداع الملف رئيس القسم: مسار «مستأنف علينا» فقط — يُصفَّر لمسار «مستأنِفين»
        // (appeal.Direction مضبوط قبل الاستدعاء في CreateAsync وUpdateAsync).
        if (appeal.Direction == AppealDirectionCatalog.Appellants)
        {
            appeal.DepositBookNumber = null;
            appeal.DepositBookDate = null;
        }
        else
        {
            appeal.DepositBookNumber = Bounded(request.DepositBookNumber, 200, "رقم كتاب إيداع الملف رئيس القسم");
            appeal.DepositBookDate = DocumentValidator.ParseDateTime(request.DepositBookDate, "تاريخ كتاب إيداع الملف رئيس القسم");
        }
        appeal.DefenseOpinion = Bounded(request.DefenseOpinion, 2000, "رأي المحامي المتابع للملف بأسباب الاستئناف");
        appeal.Notes = Bounded(request.Notes, 2000, "الملاحظات");
        appeal.UpdatedAt = DateTime.UtcNow;
    }
}
