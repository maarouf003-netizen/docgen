using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Forum;

/// <summary>
/// تبديل تثبيت رسالة منتدى — المشرف فقط، والجديد يُسقط السابق. يملك معاملته،
/// ويحوّل سباق التثبيت المزدوج (انتهاك القيد الفريد) إلى `DocumentConflictException`
/// كما الأصل. بلا تدقيق (المنتدى خارج نطاقه بقرار المالك).
/// </summary>
public sealed record ToggleForumPinCommand(int MessageId, int ActorUserId)
    : ICommand<ForumMessageDto>, ITransactionalRequest;

public sealed class ToggleForumPinCommandHandler
    : CommandHandlerBase<ToggleForumPinCommand, ForumMessageDto>
{
    private readonly IForumRepository _forum;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IDbExceptionClassifier _errors;

    public ToggleForumPinCommandHandler(
        IForumRepository forum,
        IUserRepository users,
        IUnitOfWork uow,
        IDbExceptionClassifier errors,
        IEnumerable<IPipelineBehavior<ToggleForumPinCommand, ForumMessageDto>> behaviors)
        : base(behaviors)
    {
        _forum = forum;
        _users = users;
        _uow = uow;
        _errors = errors;
    }

    protected override async Task<ForumMessageDto> HandleCoreAsync(
        ToggleForumPinCommand command, CancellationToken ct)
    {
        var actor = await ForumMembership.RequireAsync(_users, command.ActorUserId, ct);
        if (actor.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("التثبيت متاح للمشرف فقط");

        var message = await _forum.GetTrackedByIdAsync(command.MessageId, ct)
            ?? throw new KeyNotFoundException("الرسالة غير موجودة");

        try
        {
            if (message.IsPinned)
            {
                message.IsPinned = false;
            }
            else
            {
                var current = await _forum.GetTrackedPinnedAsync(ct);
                if (current is not null && current.Id != message.Id)
                    current.IsPinned = false;
                message.IsPinned = true;
            }
            await _uow.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (_errors.IsUniqueViolation(ex))
        {
            // سباق تثبيت مزدوج: القيد الفريد هو الحارس الأخير — خطأ ودود لا 500.
            throw new DocumentConflictException("تزامن تثبيت مع مستخدم آخر — حُدّثت الحالة، أعد المحاولة", ex);
        }

        // العدّاد لرسائل الكاتب نفسه فقط (كبقية المسارات — لا كشف لغيره).
        var pinCounts = message.AuthorId == command.ActorUserId
            ? await _forum.CountReadsAsync(new[] { message.Id }, ct)
            : new Dictionary<int, int>();
        return ForumService.ToDto(message, pinCounts.GetValueOrDefault(message.Id));
    }
}
