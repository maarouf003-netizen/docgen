using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Users;

/// <summary>سجل تعاقب الرئاسة — الأحدث أولًا، مع أسماء الفرع/الشعبة/المستخدم.</summary>
public sealed record ListSuccessionQuery(int? BranchId) : IQuery<List<HeadSuccessionDto>>;

internal sealed class ListSuccessionQueryHandler
    : QueryHandlerBase<ListSuccessionQuery, List<HeadSuccessionDto>>
{
    private readonly IRepository<HeadSuccession> _successions;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<Section> _sections;
    private readonly IUserRepository _users;

    public ListSuccessionQueryHandler(
        IRepository<HeadSuccession> successions,
        IRepository<Branch> branches,
        IRepository<Section> sections,
        IUserRepository users,
        IEnumerable<IPipelineBehavior<ListSuccessionQuery, List<HeadSuccessionDto>>> behaviors)
        : base(behaviors)
    {
        _successions = successions;
        _branches = branches;
        _sections = sections;
        _users = users;
    }

    protected override async Task<List<HeadSuccessionDto>> HandleCoreAsync(
        ListSuccessionQuery query, CancellationToken ct)
    {
        var rows = await _successions.ListAsync(ct);
        var branches = await _branches.ListAsync(ct);
        var branchNames = branches.ToDictionary(b => b.Id, b => b.Name);
        var sections = await _sections.ListAsync(ct);
        var sectionNames = sections.ToDictionary(s => s.Id, s => s.Name);
        var users = await _users.ListAllUsersAsync(ct);
        var userNames = users.ToDictionary(u => u.Id, u => u.FullName);
        return rows
            .Where(h => query.BranchId == null || h.BranchId == query.BranchId.Value)
            .OrderByDescending(h => h.At)
            .ThenByDescending(h => h.Id)
            .Select(h => new HeadSuccessionDto(
                h.Id,
                h.BranchId,
                branchNames.GetValueOrDefault(h.BranchId),
                h.SectionId,
                h.SectionId.HasValue ? sectionNames.GetValueOrDefault(h.SectionId.Value) : null,
                h.UserId,
                userNames.GetValueOrDefault(h.UserId),
                h.Role.ToString().ToLowerInvariant(),
                h.Event,
                h.At,
                h.ActorName,
                h.Reason))
            .ToList();
    }
}
