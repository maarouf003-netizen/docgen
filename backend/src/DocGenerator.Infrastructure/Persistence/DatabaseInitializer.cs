using DocGenerator.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// تنفيذ تهيئة قاعدة البيانات عبر كيان EF الفعلي: يطبّق المهاجرات، ثم يسند البذر
/// إلى <see cref="DbSeeder"/> حسب البيئة (تطوير/إنتاج).
/// </summary>
public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly DocGeneratorDbContext _db;
    private readonly IPasswordHasher _hasher;

    public DatabaseInitializer(DocGeneratorDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task InitializeAsync(
        bool development,
        string? bootstrapAdminPassword,
        CancellationToken ct = default)
    {
        await _db.Database.MigrateAsync(ct);

        if (development)
            await DbSeeder.SeedAsync(_db, _hasher, ct);
        else
            await DbSeeder.BootstrapAsync(_db, _hasher, bootstrapAdminPassword, ct);
    }
}
