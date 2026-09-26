using System.Diagnostics;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace DocGenerator.Application.Tests;

/// <summary>
/// قياس أداء إحصاءات البوابة (M6): يُزرع حجم واقعي (1000 ملف) وتُثبَّت صحة
/// النتائج حتميًا، بينما الزمن يُسجَّل في مخرجات الاختبار للحكم عليه —
/// لا يُفشَل عليه (الأزمنة الجدارية هشّة تحت الحمل ولا تصلح حدًّا).
/// </summary>
public class PortalStatsMeasurementTests : IDisposable
{
    private static readonly DateTime FixedDate = new(2020, 1, 15);

    private readonly DocGeneratorDbContext _db;
    private readonly IPortalService _portal;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _delegateGroupId;
    private readonly int _entryAId;
    private readonly int _entryBId;
    private readonly ITestOutputHelper _output;

    public PortalStatsMeasurementTests(ITestOutputHelper output)
    {
        _output = output;
        _db = TestDb.Create();

        _db.Users.Add(new User { Username = "creator", FullName = "منشئ", Role = UserRole.Admin, PasswordHash = "x" });
        var group = new PublicEntityGroup { CanonicalName = "مصرف القياس", EntityType = PublicEntityTypeCatalog.Company };
        group.Entries.Add(new PublicEntity { Governorate = "دمشق", BranchName = "الفرع الرئيسي", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        group.Entries.Add(new PublicEntity { Governorate = "اللاذقية", BranchName = "فرع 1", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        _db.PublicEntityGroups.Add(group);
        _db.SaveChanges();
        _entryAId = group.Entries.First(e => e.Governorate == "دمشق").Id;
        _entryBId = group.Entries.First(e => e.Governorate == "اللاذقية").Id;

        var del = new User { Username = "delegate_m6", FullName = "مندوب القياس", Role = UserRole.EntityManager, PortalGroupId = group.Id, PasswordHash = "x" };
        _db.Users.Add(del);
        _db.SaveChanges();
        _delegateGroupId = del.Id;

        _portal = PortalServiceFactory.Create(_db, _audit);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task M6_StatsOverScopeVolume_CompletesWithCorrectTotals()
    {
        const int count = 1000;
        // مفردات الحالة من الكتالوج وحده (روح L2) — العملات بيانات اختبار حرة لا كتالوج لها.
        var statuses = new string?[]
        {
            null,
            ExecutionStatusCatalog.ExecutedBySettlement,
            ExecutionStatusCatalog.Deferred,
            ExecutionStatusCatalog.ExecutedForcibly,
            ExecutionStatusCatalog.ExecutedForcibly,
            ExecutionStatusCatalog.ReferredToStart,
        };
        var docs = new List<Document>(count);
        for (var i = 0; i < count; i++)
        {
            var registryId = i % 2 == 0 ? _entryAId : _entryBId;
            var doc = new Document
            {
                CreatedById = 1,
                IsDraft = false,
                BorrowerName = $"مقترض {i}",
                BorrowerFamily = "عائلة",
                AmountNumeric = 100 + i,
                Currency = i % 3 == 0 ? "دولار أمريكي" : "ليرة سورية",
                ExecStatus = statuses[i % statuses.Length] ?? string.Empty,
                ExecSubStatus = statuses[i % statuses.Length] == ExecutionStatusCatalog.ExecutedForcibly && i % 2 == 1
                    ? ExecutionStatusCatalog.SubPartiallyExecuted
                    : null,
                GeneralEntitySide = "applicant",
                CreatedAt = FixedDate,
                UpdatedAt = FixedDate,
            };
            doc.ApplicantPublicEntities.Add(new ApplicantPublicEntity { Name = $"مقترض {i}", Governorate = "دمشق", RegistryId = registryId });
            doc.ApplicantRegistryId = registryId;
            doc.SearchText = DocumentSearchTextBuilder.Build(doc);
            docs.Add(doc);
        }
        _db.Documents.AddRange(docs);
        await _db.SaveChangesAsync();

        var sw = Stopwatch.StartNew();
        var stats = await _portal.GetStatsAsync(_delegateGroupId);
        sw.Stop();

        Assert.Equal(count, stats.TotalFiles);
        Assert.Equal(
            stats.CirculatingFiles + stats.ExecutedFiles + stats.DeferredFiles
            + stats.ReferredToStartFiles + stats.DraftFiles,
            stats.TotalFiles);
        _output.WriteLine($"M6: GetStatsAsync over {count} in-scope docs took {sw.ElapsedMilliseconds} ms");
    }
}
