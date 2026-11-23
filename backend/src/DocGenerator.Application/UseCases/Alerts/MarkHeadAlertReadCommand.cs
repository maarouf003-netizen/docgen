using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Alerts;

/// <summary>تعليم تنبيه مقروءًا لصاحب صف الاستلام وحده (تحديث ذرّي في المستودع).</summary>
public sealed record MarkHeadAlertReadCommand(int AlertId, HeadAlertActor Actor) : ICommand<bool>;

public sealed class MarkHeadAlertReadCommandHandler : CommandHandlerBase<MarkHeadAlertReadCommand, bool>
{
    private readonly IHeadAlertRepository _alerts;

    public MarkHeadAlertReadCommandHandler(
        IHeadAlertRepository alerts,
        IEnumerable<IPipelineBehavior<MarkHeadAlertReadCommand, bool>> behaviors)
        : base(behaviors)
        => _alerts = alerts;

    protected override Task<bool> HandleCoreAsync(MarkHeadAlertReadCommand command, CancellationToken ct)
    {
        if (!RolePermissions.CanReceiveAlerts(command.Actor.Role))
            throw new UnauthorizedAccessException("تعليم القراءة مخصص للمستلمين (محامٍ أو رئيس نطاق)");

        return _alerts.TryMarkReadAsync(command.AlertId, command.Actor.UserId, DateTime.UtcNow, ct);
    }
}
