using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>
/// تحديث حقول قيد الاستئناف (المحكمة/رقم الأساس/السنة/تاريخ الإقرار/النوع) —
/// المحامي المتابع فقط، مع سياسة رقم الأساس الواحدة والتدقيق.
/// <para>
/// **حدود المعاملة:** التحميل والتحقق والسياسة خارج المعاملة، ثم معاملة واحدة
/// للتحديث والحفظ والتدقيق — بلا وسم `ITransactionalRequest`.
/// </para>
/// </summary>
public sealed record UpdateAppealRegistrationCommand(
    int AppealId,
    UpdateAppealRegistrationRequest Request,
    int UserId,
    string? ActorName) : ICommand<AppealDto?>;

public sealed class UpdateAppealRegistrationCommandHandler
    : CommandHandlerBase<UpdateAppealRegistrationCommand, AppealDto?>
{
    private readonly IAppealRepository _appeals;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public UpdateAppealRegistrationCommandHandler(
        IAppealRepository appeals,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<UpdateAppealRegistrationCommand, AppealDto?>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<AppealDto?> HandleCoreAsync(
        UpdateAppealRegistrationCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");

        DocumentAppealService.EnsureAssignedFollower(appeal, command.UserId);

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        var currentIdentity = AppealBaseNumberPolicy.ResolveCurrent(appeal, currentYear);

        var requestedNumber = DocumentAppealService.Bounded(
            request.AppealBaseNumber, AppealBaseNumberPolicy.MaxNumberLength, "رقم الأساس الاستئنافي");
        var requestedYear = AppealBaseNumberPolicy.ValidateYear(request.AppealYear, currentYear, "لعام");

        appeal.AppealTypeLabel = DocumentAppealService.Bounded(request.AppealTypeLabel, 100, "نوع الاستئناف");
        appeal.AppellateCourt = DocumentAppealService.Bounded(request.AppellateCourt, 300, "محكمة الاستئناف التنفيذية المختصة");
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
                appeal.AppealYear = targetYear?.ToString(System.Globalization.CultureInfo.InvariantCulture);
                effectiveYear = targetYear;
            }
        }

        appeal.UpdatedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await _uow.SaveChangesAsync(token);
            var loggedNumber = requestedNumber ?? currentIdentity?.Number ?? "—";
            var loggedYear = effectiveYear?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—";
            await _audit.LogAsync(command.ActorName, "update_appeal_registration",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"حدّث قيد الاستئناف (رقم {appeal.Id}) برقم أساس {loggedNumber} لعام {loggedYear}", token);
        }, ct);

        return DocumentAppealService.ToDto(appeal, currentYear);
    }
}
