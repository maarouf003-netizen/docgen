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

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        var currentIdentity = AppealBaseNumberPolicy.ResolveCurrent(appeal, currentYear);

        var requestedNumber = Bounded(
            request.AppealBaseNumber, AppealBaseNumberPolicy.MaxNumberLength, "رقم الأساس الاستئنافي");
        var requestedYear = AppealBaseNumberPolicy.ValidateYear(request.AppealYear, currentYear, "لعام");

        appeal.AppealTypeLabel = Bounded(request.AppealTypeLabel, 100, "نوع الاستئناف");
        appeal.AppellateCourt = Bounded(request.AppellateCourt, 300, "محكمة الاستئناف التنفيذية المختصة");
        appeal.RegistrationDate = DocumentValidator.ParseDateTime(request.RegistrationDate, "تاريخ إقرار الاستئناف");

        // الرقم الحالي واحد: «تعديل القيد» يصحّح مصدر الهوية الحالية في مكانه
        // (أحدث صف سنة ≤ الحالية، أو حقول القيد إن لم يوجد صف)، ولا يُلحق سجلًا جديدًا.
        // الإفراغ لا يمسّ الرقم المسجَّل (لا مسار لحذفه)؛ التصحيح هو مسار التعديل.
        var effectiveYear = currentIdentity?.Year;
        if (requestedNumber is not null)
        {
            if (currentIdentity?.Row is not null)
            {
                // سنة صف التدوير مُولَّدة من الخادم؛ إعادة كتابتها تطمس سنة مؤرشفة.
                if (requestedYear is int rowYear && rowYear != currentIdentity.Row.Year)
                    throw new ArgumentException(
                        "سنة رقم الأساس الحالية مُدارة آليًا — استخدم «تدوير» لسنة جديدة أو «تصحيح» لتعديل سجل سابق");

                currentIdentity.Row.BaseNumber = requestedNumber;
                currentIdentity.Row.UpdatedAt = DateTime.UtcNow;
                effectiveYear = currentIdentity.Row.Year;
            }
            else
            {
                // لا هوية: تُنسب الكتابة الجديدة للسنة الحالية افتراضيًا.
                // رقم قديم بسنة غير رقمية: تبقى سنته فارغة (لا تُختلق سنة للسنة الحالية).
                var targetYear = requestedYear
                    ?? currentIdentity?.Year
                    ?? (currentIdentity is null ? currentYear : (int?)null);

                // نقل الرقم الحالي إلى سنة لها هوية أخرى يطمس تلك الهوية في السجل؛
                // تصحيح رقم سنة سابقة مساره المستقل.
                if (requestedYear is int yearToApply
                    && yearToApply != currentIdentity?.Year
                    && AppealBaseNumberPolicy.HasIdentityForYear(appeal, yearToApply))
                {
                    throw new ArgumentException(
                        $"يوجد رقم أساس مسجّل لسنة {yearToApply} — استخدم التصحيح لتعديله");
                }

                appeal.AppealBaseNumber = requestedNumber;
                appeal.AppealYear = targetYear?.ToString(CultureInfo.InvariantCulture);
                effectiveYear = targetYear;
            }
        }

        appeal.UpdatedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await _uow.SaveChangesAsync(token);
            var loggedNumber = requestedNumber ?? currentIdentity?.Number ?? "—";
            var loggedYear = effectiveYear?.ToString(CultureInfo.InvariantCulture) ?? "—";
            await _audit.LogAsync(actorName, "update_appeal_registration",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"حدّث قيد الاستئناف (رقم {appeal.Id}) برقم أساس {loggedNumber} لعام {loggedYear}", token);
        }, ct);

        return ToDto(appeal, currentYear);
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
        // خروج الحالة من «منظور» يُبطل شروط الإسناد/النقل/الإحالة المقروءة
        // خارج المعاملة — الرفع يجعل أي كتابة متزامنة قديمة تفشل برمز التزامن.
        appeal.Version++;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await EnqueueAppealFinalAsync(appeal, decided: true, appeal.AssignedLawyerId ?? appeal.Document.CreatedById,
                DateTime.UtcNow, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "decide_appeal",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"حسم الاستئناف (رقم {appeal.Id}) بالقرار {decisionNumber} — النتيجة: {AppealOutcomeCatalog.ToLabel(outcome)}", token);
        }, ct);

        return ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
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
        // كالحسم: خروج الحالة من «منظور» يرفع الرمز لإبطال الكتابات المتزامنة القديمة.
        appeal.Version++;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await EnqueueAppealFinalAsync(appeal, decided: false, appeal.AssignedLawyerId ?? appeal.Document.CreatedById,
                DateTime.UtcNow, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "strike_appeal",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"شطب الاستئناف (رقم {appeal.Id}) بقرار رقم {decisionNumber}", token);
        }, ct);

        return ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
    }
}
