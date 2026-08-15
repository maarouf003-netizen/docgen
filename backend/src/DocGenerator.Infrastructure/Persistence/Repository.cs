using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly DocGeneratorDbContext Db;
    protected readonly DbSet<T> Set;

    public Repository(DocGeneratorDbContext db)
    {
        Db = db;
        Set = db.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await WithIncludes().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id, ct);
    }

    public async Task<List<T>> ListAsync(CancellationToken ct = default)
    {
        return await WithIncludes().ToListAsync(ct);
    }

    public async Task AddAsync(T entity, CancellationToken ct = default)
    {
        await Set.AddAsync(entity, ct);
    }

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity) => Set.Remove(entity);

    private IQueryable<T> WithIncludes()
    {
        IQueryable<T> query = Set;

        if (typeof(T) == typeof(Document))
        {
            query = query
                .Include(d => ((Document)(object)d).Guarantors)
                .Include(d => ((Document)(object)d).RealEstates)
                .ThenInclude(r => r.Owners)
                .Include(d => ((Document)(object)d).Heirs)
                .Include(d => ((Document)(object)d).ExecutionActions)
                .Include(d => ((Document)(object)d).RegistrationDate)
                .Include(d => ((Document)(object)d).CreatedBy)
                .Include(d => ((Document)(object)d).Branch)
                .Include(d => ((Document)(object)d).BaseNumbers)
                .Include(d => ((Document)(object)d).ExecutionApplicants)
                .ThenInclude(a => a.Heirs)
                .Include(d => ((Document)(object)d).ExecutedPublicEntities)
                .Include(d => ((Document)(object)d).ExecutedNaturalPersons)
                .ThenInclude(p => p.Heirs)
                .Include(d => ((Document)(object)d).ExecutedHeirs)
                .Include(d => ((Document)(object)d).Occurrences)
                .ThenInclude(o => o.CreatedBy)
                .Include(d => ((Document)(object)d).ApplicantPublicEntities)
                .Include(d => ((Document)(object)d).Assignments);
        }
        else if (typeof(T) == typeof(User))
        {
            query = query.Include(u => ((User)(object)u).Branch);
        }

        return query;
    }
}
