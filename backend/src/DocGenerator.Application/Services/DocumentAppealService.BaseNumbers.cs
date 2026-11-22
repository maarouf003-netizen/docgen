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
    // ── تدوير رقم الأساس الاستئنافي ───────────────────────────────────────

    public async Task<List<AppealBaseNumberHistoryDto>> GetBaseNumberHistoryAsync(
        int appealId, CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        return AppealBaseNumberPolicy.BuildHistory(appeal, currentYear)
            .Select(h => new AppealBaseNumberHistoryDto(h.Year, h.Number, h.IsCurrent))
            .ToList();
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
        // التدوير للمسند له فقط، وللمنظور فقط — المنشئ غير المسند والمحسوم/المشطوب مرفوضان.
        if (appeal.AssignedLawyerId != userId)
            throw new ArgumentException("لا يمكنك تدوير رقم أساس استئناف لا تتابعه");
        if (appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن تدوير رقم أساس استئناف لم يبق منظورًا");
        if (request.Entries is null || request.Entries.Count == 0)
            throw new ArgumentException("أدخل رقم الأساس الاستئنافي للسنة الحالية");
        if (request.Entries.Count > 1)
            throw new ArgumentException("يُدخل رقم أساس واحد لسنة التدوير الحالية");

        var year = ServerClock.CurrentYear(_clock, _timeZone);
        var currentIdentity = AppealBaseNumberPolicy.ResolveCurrent(appeal, year);
        if (currentIdentity?.Year == year)
            throw new ArgumentException("لا حاجة لتدوير رقم الأساس — يوجد رقم مسجّل للسنة الحالية؛ استخدم التصحيح");

        var entry = request.Entries[0];
        var value = Bounded(entry.BaseNumber, AppealBaseNumberPolicy.MaxNumberLength, "رقم الأساس الاستئنافي")
            ?? throw new ArgumentException("أدخل رقم الأساس الاستئنافي للسنة الحالية");

        await _tx.RunAsync(async token =>
        {
            // سجل جديد للسنة الحالية: الأحدث (Year ثم CreatedAt) هو المعتبر، وتبقى السابقة محفوظة.
            appeal.BaseNumbers.Add(new AppealBaseNumber
            {
                AppealId = appeal.Id,
                Year = year,
                BaseNumber = value,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow,
            });
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "rotate_appeal_base_number",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"أضاف سجل رقم أساس استئنافي (رقم {appeal.Id}) لسنة {year} بالقيمة {value} — تُحفظ السجلات السابقة", token);
        }, ct);
    }

    /// <summary>
    /// تصحيح رقم أساس استئنافي مسجّل مسبقًا لسنة محددة — تعديل في مكانه بلا إلحاق سجل
    /// جديد، للمحامي المسند له. إن كانت السنة بلا صف تدوير تُصحَّح حقول القيد الأصلية
    /// (رقم القيد) بدلًا من ذلك. متاح في أي حالة كـ«تعديل القيد» (تصحيح بيانات موضعي)،
    /// بخلاف «التدوير» المحصور بالمنظور لأنه ينشئ هوية سنة جديدة.
    /// </summary>
    public async Task CorrectBaseNumberAsync(
        int appealId,
        int year,
        CorrectAppealBaseNumberRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");

        EnsureAssignedFollower(appeal, userId);

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        AppealBaseNumberPolicy.EnsureAllowedYear(year, currentYear);

        var value = Bounded(request.BaseNumber, AppealBaseNumberPolicy.MaxNumberLength, "رقم الأساس الاستئنافي")
            ?? throw new ArgumentException("أدخل رقم الأساس الاستئنافي");

        var row = AppealBaseNumberPolicy.LatestRowForYear(appeal, year);

        if (row is not null)
        {
            row.BaseNumber = value;
            row.UpdatedAt = DateTime.UtcNow;
        }
        else if (AppealBaseNumberPolicy.TryParseYear(appeal.AppealYear) == year
                 && !string.IsNullOrWhiteSpace(appeal.AppealBaseNumber))
        {
            appeal.AppealBaseNumber = value;
        }
        else
        {
            throw new ArgumentException($"لا يوجد رقم أساس مسجّل لسنة {year}");
        }

        appeal.UpdatedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "correct_appeal_base_number",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"صحّح رقم الأساس الاستئنافي (رقم {appeal.Id}) لسنة {year} إلى {value}", token);
        }, ct);
    }

    public Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default)
        => _appeals.IsAssignedFollowerAsync(documentId, userId, ct);

    public Task<bool> HasForwardedAppealAsync(int documentId, CancellationToken ct = default)
        => _appeals.HasForwardedAppealAsync(documentId, ct);
}
