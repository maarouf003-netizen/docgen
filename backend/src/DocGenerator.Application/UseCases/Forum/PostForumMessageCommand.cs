using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Forum;

/// <summary>
/// نشر رسالة منتدى: تحقق النص/الذِكر + لقط هوية الكاتب خلفيًا + اقتباس اختياري —
/// يملك معاملته. بلا تدقيق (المنتدى خارج نطاقه بقرار المالك).
/// </summary>
public sealed record PostForumMessageCommand(
    PostForumMessageRequest Request,
    int ActorUserId) : ICommand<ForumMessageDto>, ITransactionalRequest;

public sealed class PostForumMessageCommandHandler
    : CommandHandlerBase<PostForumMessageCommand, ForumMessageDto>
{
    private readonly IForumRepository _forum;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public PostForumMessageCommandHandler(
        IForumRepository forum,
        IUserRepository users,
        IUnitOfWork uow,
        TimeProvider clock,
        IEnumerable<IPipelineBehavior<PostForumMessageCommand, ForumMessageDto>> behaviors)
        : base(behaviors)
    {
        _forum = forum;
        _users = users;
        _uow = uow;
        _clock = clock;
    }

    protected override async Task<ForumMessageDto> HandleCoreAsync(
        PostForumMessageCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var body = await ForumMessageContent.ResolveMentionsAsync(
            ForumMessageContent.RequireBody(request.Body), _users, ct);
        var author = await ForumMembership.RequireAsync(_users, command.ActorUserId, ct);

        string? quotedAuthor = null;
        string? quotedExcerpt = null;
        if (request.QuotedMessageId.HasValue)
        {
            var quoted = await _forum.GetByIdAsync(request.QuotedMessageId.Value, ct)
                ?? throw new ArgumentException("الرسالة المقتبسة غير موجودة");
            quotedAuthor = ForumMessageContent.Truncate(quoted.AuthorName, ForumService.MaxQuotedAuthorLength);
            quotedExcerpt = ForumMessageContent.Truncate(quoted.Body, ForumService.MaxQuotedExcerptLength);
        }

        // مصدر الوقت الوحيد: الساعة المحقونة (قابلة للتجميد في الاختبارات).
        var now = _clock.GetUtcNow().UtcDateTime;
        var message = new ForumMessage
        {
            Body = body,
            AuthorId = author.Id,
            AuthorName = ForumIdentityParts.DisplayName(author),
            AuthorRole = author.Role.ToString().ToLowerInvariant(),
            AuthorLocation = ForumIdentityParts.ResolveLocation(author),
            AuthorSection = ForumIdentityParts.ResolveSection(author),
            QuotedMessageId = request.QuotedMessageId,
            QuotedAuthorName = quotedAuthor,
            QuotedExcerpt = quotedExcerpt,
            CreatedAt = now,
        };

        await _forum.AddAsync(message, ct);
        await _uow.SaveChangesAsync(ct);

        return ForumService.ToDto(message, 0);
    }
}
