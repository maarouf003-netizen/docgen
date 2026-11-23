using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Forum;

/// <summary>حذف رسالة منتدى (المؤلف أو المشرف) — يملك معاملته.</summary>
public sealed record DeleteForumMessageCommand(int MessageId, int ActorUserId)
    : ICommand<bool>, ITransactionalRequest;

public sealed class DeleteForumMessageCommandHandler
    : CommandHandlerBase<DeleteForumMessageCommand, bool>
{
    private readonly IForumRepository _forum;
    private readonly IUserRepository _users;

    public DeleteForumMessageCommandHandler(
        IForumRepository forum,
        IUserRepository users,
        IEnumerable<IPipelineBehavior<DeleteForumMessageCommand, bool>> behaviors)
        : base(behaviors)
    {
        _forum = forum;
        _users = users;
    }

    protected override async Task<bool> HandleCoreAsync(DeleteForumMessageCommand command, CancellationToken ct)
    {
        var message = await _forum.GetTrackedByIdAsync(command.MessageId, ct)
            ?? throw new KeyNotFoundException("الرسالة غير موجودة");

        var actor = await ForumMembership.RequireAsync(_users, command.ActorUserId, ct);

        if (message.AuthorId != command.ActorUserId && actor.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("حذف الرسالة متاح لكاتبها أو للمشرف فقط");

        // الحذف في قاعدة البيانات (الردود/القراءات) — داخل معاملة الوسم.
        return await _forum.DeleteMessageAsync(command.MessageId, ct);
    }
}
