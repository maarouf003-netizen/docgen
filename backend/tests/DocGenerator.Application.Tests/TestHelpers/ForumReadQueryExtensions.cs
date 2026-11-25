using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Forum;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري لمعالجات قراءات المنتدى (`P4-01cu`): يحافظ على توقيعات خدمة
/// المنتدى القديمة فوق سجل المعالجات — منطق الاختبارات كما هو.
/// </summary>
public static class ForumReadQueryExtensions
{
    public static Task<ForumMessagesPageDto> ListAsync(
        this ForumReadHandlers handlers,
        int actorUserId,
        int limit,
        int? before,
        int? after,
        CancellationToken ct = default)
        => handlers.Messages.HandleAsync(
            new ListForumMessagesQuery(actorUserId, limit, before, after), ct);

    public static Task<ForumMessageDto?> GetByIdAsync(
        this ForumReadHandlers handlers,
        int id,
        int actorUserId,
        CancellationToken ct = default)
        => handlers.Message.HandleAsync(new GetForumMessageQuery(id, actorUserId), ct);

    public static Task<ForumMessageDto?> GetPinnedAsync(
        this ForumReadHandlers handlers,
        int actorUserId,
        CancellationToken ct = default)
        => handlers.Pinned.HandleAsync(new GetPinnedForumMessageQuery(actorUserId), ct);

    public static Task<List<ForumReaderDto>> GetReadersAsync(
        this ForumReadHandlers handlers,
        int id,
        int actorUserId,
        CancellationToken ct = default)
        => handlers.Readers.HandleAsync(new GetForumMessageReadersQuery(id, actorUserId), ct);

    public static Task<List<ForumUserLookupDto>> SearchUsersAsync(
        this ForumReadHandlers handlers,
        string? q,
        int actorUserId,
        CancellationToken ct = default)
        => handlers.Users.HandleAsync(new SearchForumUsersQuery(q, actorUserId), ct);

    public static Task<int> CountUnreadAsync(
        this ForumReadHandlers handlers,
        int actorUserId,
        CancellationToken ct = default)
        => handlers.Unread.HandleAsync(new CountUnreadForumMessagesQuery(actorUserId), ct);
}
