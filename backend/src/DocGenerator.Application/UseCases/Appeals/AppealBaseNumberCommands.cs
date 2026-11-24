using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>
/// تدوير رقم الأساس الاستئنافي للسنة الحالية — للمحامي المتابع المسند له وفي حالة
/// «منظور» فقط، بإلحاق صف تدوير جديد مع بقاء السجلات السابقة والتدقيق.
/// </summary>
public sealed record SaveAppealBaseNumbersCommand(
    int AppealId,
    SaveAppealBaseNumbersRequest Request,
    int UserId,
    string? ActorName) : ICommand<bool>;

public sealed class SaveAppealBaseNumbersCommandHandler
    : CommandHandlerBase<SaveAppealBaseNumbersCommand, bool>
{
    private readonly IAppealRepository _appeals;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public SaveAppealBaseNumbersCommandHandler(
        IAppealRepository appeals,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<SaveAppealBaseNumbersCommand, bool>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<bool> HandleCoreAsync(
        SaveAppealBaseNumbersCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        // التدوير للمسند له فقط، وللمنظور فقط — المنشئ غير المسند والمحسوم/المشطوب مرفوضان.
        if (appeal.AssignedLawyerId != command.UserId)
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
        var value = DocumentAppealService.Bounded(
                entry.BaseNumber, AppealBaseNumberPolicy.MaxNumberLength, "رقم الأساس الاستئنافي")
            ?? throw new ArgumentException("أدخل رقم الأساس الاستئنافي للسنة الحالية");

        await _tx.RunAsync(async token =>
        {
            // سجل جديد للسنة الحالية: الأحدث (Year ثم CreatedAt) هو المعتبر، وتبقى السابقة محفوظة.
            appeal.BaseNumbers.Add(new AppealBaseNumber
            {
                AppealId = appeal.Id,
                Year = year,
                BaseNumber = value,
                CreatedById = command.UserId,
                CreatedAt = DateTime.UtcNow,
            });
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "rotate_appeal_base_number",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"أضاف سجل رقم أساس استئنافي (رقم {appeal.Id}) لسنة {year} بالقيمة {value} — تُحفظ السجلات السابقة", token);
        }, ct);

        return true;
    }
}

/// <summary>
/// تصحيح رقم أساس استئنافي مسجّل مسبقًا لسنة محددة — تعديل في مكانه بلا إلحاق سجل
/// جديد، للمحامي المسند له. إن كانت السنة بلا صف تدوير تُصحَّح حقول القيد الأصلية
/// (رقم القيد) بدلًا من ذلك. متاح في أي حالة كـ«تعديل القيد» (تصحيح بيانات موضعي)،
/// بخلاف «التدوير» المحصور بالمنظور لأنه ينشئ هوية سنة جديدة.
/// </summary>
public sealed record CorrectAppealBaseNumberCommand(
    int AppealId,
    int Year,
    CorrectAppealBaseNumberRequest Request,
    int UserId,
    string? ActorName) : ICommand<bool>;

public sealed class CorrectAppealBaseNumberCommandHandler
    : CommandHandlerBase<CorrectAppealBaseNumberCommand, bool>
{
    private readonly IAppealRepository _appeals;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public CorrectAppealBaseNumberCommandHandler(
        IAppealRepository appeals,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<CorrectAppealBaseNumberCommand, bool>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<bool> HandleCoreAsync(
        CorrectAppealBaseNumberCommand command, CancellationToken ct)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");

        DocumentAppealService.EnsureAssignedFollower(appeal, command.UserId);

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        var year = command.Year;
        AppealBaseNumberPolicy.EnsureAllowedYear(year, currentYear);

        var value = DocumentAppealService.Bounded(
                command.Request.BaseNumber, AppealBaseNumberPolicy.MaxNumberLength, "رقم الأساس الاستئنافي")
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
            await _audit.LogAsync(command.ActorName, "correct_appeal_base_number",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"صحّح رقم الأساس الاستئنافي (رقم {appeal.Id}) لسنة {year} إلى {value}", token);
        }, ct);

        return true;
    }
}
