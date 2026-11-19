using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// P0-06 — تزامن تفاؤلي على كل كيان قابل للتعديل:
/// (1) كل كيان محمي يحمل <c>Version</c> مُعرَّفًا كـ<c>IsConcurrencyToken</c>؛
/// (2) زيادة مركزية في <c>SaveChanges</c> لا تفقد الحماية إن نسي مسار الكتابة الزيادة؛
/// (3) تحديث انطلاقًا من نسخة قديمة يُرفض بـ<c>DbUpdateConcurrencyException</c> (409)؛
/// (4) الحارس المبكر يرفض النسخة القديمة برسالة ودية، ويقبل الغياب (توافق خلفي).
/// </summary>
public class OptimisticConcurrencyTests : IAsyncLifetime
{
    private PostgresTestDb _pg = null!;
    private DocGeneratorPostgresDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _pg = await PostgresTestDb.CreateAsync();
        _db = _pg.CreateContext();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _pg.DisposeAsync();
    }

    private Branch SeedBranch(string code = "DAM", string name = "دمشق")
    {
        var branch = new Branch { Name = name, Code = code, Governorate = "دمشق" };
        _db.Branches.Add(branch);
        _db.SaveChanges();
        return branch;
    }

    [PostgresFact]
    public async Task ModifiedVersionedRow_GetsVersionIncremented_BySaveChanges()
    {
        var branch = SeedBranch();
        Assert.Equal(0, branch.Version);

        branch.Name = "دمشق المحدثة";
        await _db.SaveChangesAsync();

        Assert.Equal(1, branch.Version);
        _db.Entry(branch).State = EntityState.Detached;
        var stored = await _db.Branches.AsNoTracking().SingleAsync(b => b.Id == branch.Id);
        Assert.Equal(1, stored.Version);
    }

    [PostgresFact]
    public async Task ConsecutiveWrites_KeepIncrementingVersion()
    {
        var branch = SeedBranch();

        branch.Phone = "011";
        await _db.SaveChangesAsync();
        branch.Phone = "012";
        await _db.SaveChangesAsync();

        Assert.Equal(2, branch.Version);
    }

    [PostgresFact]
    public async Task ExplicitVersionByWriter_IsNotDoubled()
    {
        var branch = SeedBranch();

        // مسار خدمي يزيد النسخة صراحةً (نمط DocumentWriteGuard): يحترم الاعتراض قيمته.
        branch.Version = 7;
        branch.Name = "دمشق 7";
        await _db.SaveChangesAsync();

        Assert.Equal(7, branch.Version);
    }

    [PostgresFact]
    public async Task StaleUpdate_ThrowsConcurrencyException()
    {
        var options = _pg.Options;

        await using (var seed = new DocGeneratorPostgresDbContext(options))
        {
            seed.Branches.Add(new Branch { Name = "حمص", Code = "HMS", Governorate = "حمص" });
            await seed.SaveChangesAsync();
        }

        await using var first = new DocGeneratorPostgresDbContext(options);
        await using var second = new DocGeneratorPostgresDbContext(options);

        var winner = await first.Branches.SingleAsync(b => b.Code == "HMS");
        var loser = await second.Branches.SingleAsync(b => b.Code == "HMS");

        winner.Name = "حمص (الفائز)";
        await first.SaveChangesAsync();
        Assert.Equal(1, winner.Version);

        // الخاسر يحمل النسخة القديمة (0) في WHERE — لا يمحو تحديث الفائز.
        loser.Name = "حمص (الخاسر)";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [PostgresFact]
    public void Guard_RejectsStaleVersion_WithDocumentConflict()
    {
        var branch = new Branch { Id = 1, Version = 3 };

        var ex = Assert.Throws<DocumentConflictException>(
            () => OptimisticConcurrencyGuard.EnsureCurrent(branch, 2, "الفرع"));
        Assert.Contains("الفرع", ex.Message, StringComparison.Ordinal);
        Assert.Contains("أعد التحميل", ex.Message, StringComparison.Ordinal);
    }

    [PostgresFact]
    public void Guard_AcceptsMatchingOrAbsentVersion()
    {
        var branch = new Branch { Id = 1, Version = 3 };

        OptimisticConcurrencyGuard.EnsureCurrent(branch, 3, "الفرع");
        OptimisticConcurrencyGuard.EnsureCurrent(branch, null, "الفرع");
    }

    [PostgresTheory]
    [InlineData(typeof(Document))]
    [InlineData(typeof(DocumentAppeal))]
    [InlineData(typeof(DocumentDelegation))]
    [InlineData(typeof(ExecutionCircuit))]
    [InlineData(typeof(User))]
    [InlineData(typeof(PublicEntity))]
    [InlineData(typeof(PublicEntityGroup))]
    [InlineData(typeof(Correspondence))]
    [InlineData(typeof(Section))]
    [InlineData(typeof(Branch))]
    [InlineData(typeof(ReviewLetter))]
    [InlineData(typeof(ReviewLetterMessage))]
    [InlineData(typeof(PersonalReminder))]
    [InlineData(typeof(ForumMessage))]
    [InlineData(typeof(ExecutionAction))]
    [InlineData(typeof(DocumentIntervention))]
    public void EveryEditableAggregate_MarksVersionAsConcurrencyToken(Type entityType)
    {
        var property = _db.Model.FindEntityType(entityType)?.FindProperty(nameof(IVersionedEntity.Version));

        Assert.NotNull(property);
        Assert.True(property.IsConcurrencyToken);
        Assert.False(property.IsNullable);
    }

    [PostgresFact]
    public void VersionedEntities_ImplementTheSharedContract()
    {
        Assert.True(typeof(IVersionedEntity).IsAssignableFrom(typeof(Branch)));
        Assert.True(typeof(IVersionedEntity).IsAssignableFrom(typeof(Document)));
        Assert.True(typeof(IVersionedEntity).IsAssignableFrom(typeof(User)));
    }
}
