using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
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

        var actor = await _users.GetByIdAsync(command.ActorUserId, ct)
            ?? throw new UnauthorizedAccessException("المستخدم غير موجود");
        if (!actor.IsActive || actor.Role == UserRole.EntityManager)
            throw new UnauthorizedAccessException("لا تملك صلاحية الوصول للمنتدى");
        if (actor.Role == UserRole.SubHead && actor.SectionId is null)
            throw new UnauthorizedAccessException("حسابك بلا شعبة — أعد الدخول");

        if (message.AuthorId != command.ActorUserId && actor.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("ليس لديك صلاحية حذف هذه الرسالة");

        // الحذف في قاعدة البيانات (الردود/القراءات) — داخل معاملة الوسم.
        return await _forum.DeleteMessageAsync(command.MessageId, ct);
    }
}
