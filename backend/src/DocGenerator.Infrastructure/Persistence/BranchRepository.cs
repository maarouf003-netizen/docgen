using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// استعلامات الفروع على مستوى قاعدة البيانات: التفرّد وفحص الاستخدام وعدّادات العرض.
/// </summary>
public class BranchRepository : Repository<Branch>, IBranchRepository
{
    public BranchRepository(DocGeneratorDbContext db) : base(db) { }

    public async Task<bool> NameExistsAsync(string name, int? excludeBranchId, CancellationToken ct = default)
    {
        var normalized = name.Trim();
        return await Db.Branches.AnyAsync(b =>
            b.Name.Trim() == normalized
            && (excludeBranchId == null || b.Id != excludeBranchId), ct);
    }

    public async Task<bool> CodeExistsAsync(string code, int? excludeBranchId, CancellationToken ct = default)
    {
        var normalized = code.Trim();
        return await Db.Branches.AnyAsync(b =>
            b.Code.Trim() == normalized
            && (excludeBranchId == null || b.Id != excludeBranchId), ct);
    }

    public Task<bool> HasUsersAsync(int branchId, CancellationToken ct = default)
        => Db.Users.AnyAsync(u => u.BranchId == branchId, ct);

    public Task<bool> HasDocumentsAsync(int branchId, CancellationToken ct = default)
        => Db.Documents.AnyAsync(d => d.BranchId == branchId, ct);

    public async Task<Dictionary<int, int>> CountUsersByBranchAsync(CancellationToken ct = default)
        => await Db.Users
            .Where(u => u.BranchId != null)
            .GroupBy(u => u.BranchId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

    public async Task<Dictionary<int, int>> CountDocumentsByBranchAsync(CancellationToken ct = default)
        => await Db.Documents
            .Where(d => d.BranchId != null)
            .GroupBy(d => d.BranchId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
}
