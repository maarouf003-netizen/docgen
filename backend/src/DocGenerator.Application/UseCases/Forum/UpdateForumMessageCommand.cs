using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Forum;

/// <summary>
/// تعديل رسالة منتدى — الكاتب فقط (يختم «عُدّل»)، ويملك معاملته.
/// بلا تدقيق (المنتدى خارج نطاقه بقرار المالك).
/// </summary>
public sealed record UpdateForumMessageCommand(
    int MessageId,
    UpdateForumMessageRequest Request,
    int ActorUserId) : ICommand<ForumMessageDto>, ITransactionalRequest;

public sealed class UpdateForumMessageCommandHandler
    : CommandHandlerBase<UpdateForumMessageCommand, ForumMessageDto>
{
    private readonly IForumRepository _forum;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public UpdateForumMessageCommandHandler(
        IForumRepository forum,
        IUserRepository users,
        IUnitOfWork uow,
        TimeProvider clock,
        IEnumerable<IPipelineBehavior<UpdateForumMessageCommand, ForumMessageDto>> behaviors)
        : base(behaviors)
    {
        _forum = forum;
        _users = users;
        _uow = uow;
        _clock = clock;
    }

    protected override async Task<ForumMessageDto> HandleCoreAsync(
        UpdateForumMessageCommand command, CancellationToken ct)
    {
        var body = await ForumMessageContent.ResolveMentionsAsync(
            ForumMessageContent.RequireBody(command.Request.Body), _users, ct);
        var message = await _forum.GetTrackedByIdAsync(command.MessageId, ct)
            ?? throw new KeyNotFoundException("الرسالة غير موجودة");
        if (message.AuthorId != command.ActorUserId)
            throw new UnauthorizedAccessException("تعديل الرسالة متاح لكاتبها فقط");

        OptimisticConcurrencyGuard.EnsureCurrent(message, command.Request.Version, "رسالة المنتدى");

        var now = _clock.GetUtcNow().UtcDateTime;
        message.Body = body;
        message.EditedById = command.ActorUserId;
        message.EditedAtUtc = now;
        await _uow.SaveChangesAsync(ct);

        var counts = await _forum.CountReadsAsync(new[] { message.Id }, ct);
        return ForumService.ToDto(message, counts.GetValueOrDefault(message.Id));
    }
}
