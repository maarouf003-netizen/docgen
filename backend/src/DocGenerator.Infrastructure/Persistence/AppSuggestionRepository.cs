using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>مستودع اقتراحات التطوير — القراءة الإدارية شاملة، وقراءة المرسل مقيدة به.</summary>
public class AppSuggestionRepository : Repository<AppSuggestion>, IAppSuggestionRepository
{
    public AppSuggestionRepository(DocGeneratorDbContext db) : base(db) { }

    public async Task<PagedResult<AppSuggestion>> PagedForAdminAsync(int page, int perPage, CancellationToken ct = default)
    {
        var query = Db.AppSuggestions.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Include(s => s.Sender)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(ct);
        return new PagedResult<AppSuggestion> { Items = items, Page = page, PerPage = perPage, TotalCount = total };
    }
    public async Task<List<AppSuggestion>> ListBySenderAsync(int senderId, CancellationToken ct = default)
        => await Db.AppSuggestions
            .AsNoTracking()
            .Where(s => s.SenderId == senderId)
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .ToListAsync(ct);

    public async Task<AppSuggestion?> GetByIdWithSenderAsync(int id, CancellationToken ct = default)
        => await Db.AppSuggestions
            .AsNoTracking()
            .Include(s => s.Sender)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
}
