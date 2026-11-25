using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Forum;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حزمة معالجات قراءات المنتدى (`P4-01cu`) على قاعدة اختبارية واحدة — بدل تكرار
/// ربط السلوكيات الفارغة في كل اختبار.
/// </summary>
public sealed record ForumReadHandlers(
    IQueryHandler<ListForumMessagesQuery, ForumMessagesPageDto> Messages,
    IQueryHandler<GetForumMessageQuery, ForumMessageDto?> Message,
    IQueryHandler<GetPinnedForumMessageQuery, ForumMessageDto?> Pinned,
    IQueryHandler<GetForumMessageReadersQuery, List<ForumReaderDto>> Readers,
    IQueryHandler<SearchForumUsersQuery, List<ForumUserLookupDto>> Users,
    IQueryHandler<CountUnreadForumMessagesQuery, int> Unread);

public static class ForumReadHandlerFactory
{
    public static ForumReadHandlers Create(DocGeneratorDbContext db)
        => new(
            new ListForumMessagesQueryHandler(
                new ForumRepository(db),
                Array.Empty<IPipelineBehavior<ListForumMessagesQuery, ForumMessagesPageDto>>()),
            new GetForumMessageQueryHandler(
                new ForumRepository(db),
                Array.Empty<IPipelineBehavior<GetForumMessageQuery, ForumMessageDto?>>()),
            new GetPinnedForumMessageQueryHandler(
                new ForumRepository(db),
                Array.Empty<IPipelineBehavior<GetPinnedForumMessageQuery, ForumMessageDto?>>()),
            new GetForumMessageReadersQueryHandler(
                new ForumRepository(db),
                Array.Empty<IPipelineBehavior<GetForumMessageReadersQuery, List<ForumReaderDto>>>()),
            new SearchForumUsersQueryHandler(
                new UserRepository(db),
                Array.Empty<IPipelineBehavior<SearchForumUsersQuery, List<ForumUserLookupDto>>>()),
            new CountUnreadForumMessagesQueryHandler(
                new ForumRepository(db),
                new UserRepository(db),
                Array.Empty<IPipelineBehavior<CountUnreadForumMessagesQuery, int>>()));
}
