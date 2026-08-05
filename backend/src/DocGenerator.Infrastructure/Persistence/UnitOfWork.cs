using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly DocGeneratorDbContext _db;

    public UnitOfWork(DocGeneratorDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
