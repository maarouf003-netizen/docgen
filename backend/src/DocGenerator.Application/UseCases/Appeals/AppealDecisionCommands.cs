using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>
/// حسم الاستئناف (رقم القرار/تاريخه/منطوقه/النتيجة) — المحامي المتابع فقط، مع تنبيه
/// صاحب الملف الأساس والتدقيق.
/// </summary>
public sealed record DecideDocumentAppealCommand(
    int AppealId,
    DecideAppealRequest Request,
    int UserId,
    string? ActorName) : ICommand<AppealDto?>;

public sealed class DecideDocumentAppealCommandHandler
    : CommandHandlerBase<DecideDocumentAppealCommand, AppealDto?>
{
    private readonly IAppealRepository _appeals;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public DecideDocumentAppealCommandHandler(
        IAppealRepository appeals,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<DecideDocumentAppealCommand, AppealDto?>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<AppealDto?> HandleCoreAsync(
        DecideDocumentAppealCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");

        DocumentAppealService.EnsureAssignedFollower(appeal, command.UserId);
        if (appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن حسم استئناف لم يبق منظورًا");

        var decisionNumber = DocumentAppealService.RequireText(request.DecisionNumber, "رقم قرار الحسم", 100);
        var decisionDate = DocumentValidator.ParseDateTime(request.DecisionDate, "تاريخ قرار الحسم")
            ?? throw new ArgumentException("تاريخ قرار الحسم مطلوب");
        var ruling = DocumentAppealService.RequireText(request.DecisionRuling, "منطوق القرار", 2000);
        var outcome = DocumentAppealService.Normalize(request.Outcome)
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
            await DocumentAppealRules.EnqueueAppealFinalAsync(
                appeal, decided: true, appeal.AssignedLawyerId ?? appeal.Document.CreatedById,
                DateTime.UtcNow, ServerClock.CurrentYear(_clock, _timeZone), _alertOutbox, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "decide_appeal",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"حسم الاستئناف (رقم {appeal.Id}) بالقرار {decisionNumber} — النتيجة: {AppealOutcomeCatalog.ToLabel(outcome)}", token);
        }, ct);

        return DocumentAppealService.ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
    }
}

/// <summary>
/// شطب الاستئناف (تاريخ الشطب/رقم قرار الشطب) — المحامي المتابع فقط، مع تنبيه
/// صاحب الملف الأساس والتدقيق.
/// </summary>
public sealed record StrikeDocumentAppealCommand(
    int AppealId,
    StrikeAppealRequest Request,
    int UserId,
    string? ActorName) : ICommand<AppealDto?>;

public sealed class StrikeDocumentAppealCommandHandler
    : CommandHandlerBase<StrikeDocumentAppealCommand, AppealDto?>
{
    private readonly IAppealRepository _appeals;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public StrikeDocumentAppealCommandHandler(
        IAppealRepository appeals,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<StrikeDocumentAppealCommand, AppealDto?>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<AppealDto?> HandleCoreAsync(
        StrikeDocumentAppealCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");

        DocumentAppealService.EnsureAssignedFollower(appeal, command.UserId);
        if (appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن شطب استئناف لم يبق منظورًا");

        var struckOffDate = DocumentValidator.ParseDateTime(request.StruckOffDate, "تاريخ الشطب")
            ?? throw new ArgumentException("تاريخ الشطب مطلوب");
        var decisionNumber = DocumentAppealService.RequireText(request.StruckOffDecisionNumber, "رقم قرار الشطب", 100);

        appeal.Status = AppealStatusCatalog.StruckOff;
        appeal.StruckOffDate = struckOffDate;
        appeal.StruckOffDecisionNumber = decisionNumber;
        appeal.UpdatedAt = DateTime.UtcNow;
        // كالحسم: خروج الحالة من «منظور» يرفع الرمز لإبطال الكتابات المتزامنة القديمة.
        appeal.Version++;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await DocumentAppealRules.EnqueueAppealFinalAsync(
                appeal, decided: false, appeal.AssignedLawyerId ?? appeal.Document.CreatedById,
                DateTime.UtcNow, ServerClock.CurrentYear(_clock, _timeZone), _alertOutbox, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "strike_appeal",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"شطب الاستئناف (رقم {appeal.Id}) بقرار رقم {decisionNumber}", token);
        }, ct);

        return DocumentAppealService.ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
    }
}
