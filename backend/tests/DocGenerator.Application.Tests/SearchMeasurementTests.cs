using System.Diagnostics;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace DocGenerator.Application.Tests;

/// <summary>
/// قياس أداء البحث (`RF-024` — `INT-009`/`INT-010`): يُزرع حجم يفوق الإنتاج الحالي (10k ملف ≈
/// 5 سنوات بمعدل 2000/سنة) مع كفيل لكل ملف لتمرين فروع `Any`، وتُثبَّت صحة العدّات حتميًا،
/// بينما الأزمنة وخطط التنفيذ تُسجَّل في مخرجات الاختبار للحكم عليها — لا يُفشَل عليها
/// (الأزمنة الجدارية هشّة ولا تصلح حدًّا — روح `M6`).
/// </summary>
public class SearchMeasurementTests : IDisposable
{
    private const int Count = 10_000;

    private readonly DocGeneratorDbContext _db;
    private readonly DocumentRepository _repo;
    private readonly ITestOutputHelper _output;

    public SearchMeasurementTests(ITestOutputHelper output)
    {
        _output = output;
        _db = TestDb.Create();
        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        _db.Branches.Add(branch);
        _db.SaveChanges();
        _db.Users.Add(new User
        {
            Username = "measurer",
            FullName = "مستخدم القياس",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
        });
        _db.SaveChanges();
        _repo = new DocumentRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task M24_SearchOverTenThousandDocs_CompletesWithCorrectCounts()
    {
        var seedSw = Stopwatch.StartNew();
        var docs = new List<Document>(Count);
        for (var i = 0; i < Count; i++)
        {
            var doc = new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                GeneralEntitySide = "applicant",
                BorrowerName = $"مقترض {i % 100}",
                BorrowerFamily = "عائلة",
                SearchText = $"نص {i}",
                AmountNumeric = 100 + i,
                ExecStatus = i % 5 == 1 ? ExecutionStatusCatalog.Deferred : string.Empty,
                CreatedAt = new DateTime(2024, 6, 15),
            };
            doc.Guarantors.Add(new Guarantor
            {
                GuarantorNumber = 1,
                GuarantorName = $"كفيل {i}",
                GuarantorFamily = "عائلة",
            });
            docs.Add(doc);
        }
        _db.Documents.AddRange(docs);
        await _db.SaveChangesAsync();
        seedSw.Stop();
        _output.WriteLine($"M24: seeded {Count} docs + {Count} guarantors in {seedSw.ElapsedMilliseconds} ms");

        // `INT-009`: بحث اسمي جزئي (`Contains` + `Any` على التوابع) — «مقترض 7» يطابق
        // `i%100` في {7, 70..79} أي 11% من الملفات، ولا شيء آخر يحمله (نص/كفيل متمايزان).
        var nameSw = Stopwatch.StartNew();
        var (nameTotal, _) = await _repo.SearchAsync(
            "مقترض 7", null, null, null, null, null, null, null, null, null, null, 1, 20);
        nameSw.Stop();
        Assert.Equal(1100, nameTotal);
        _output.WriteLine($"M24: person-name search over {Count} docs took {nameSw.ElapsedMilliseconds} ms (total={nameTotal})");

        // `INT-010`: فلتر الحالة الرئيسي (`ExecStatus` بلا فهرس) — التريث خُمس الملفات.
        var statusSw = Stopwatch.StartNew();
        var (deferredTotal, _) = await _repo.SearchAsync(
            null, ExecutionStatusCatalog.Deferred, null, null, null, null, null, null, null, null, null, 1, 20);
        statusSw.Stop();
        Assert.Equal(2000, deferredTotal);
        _output.WriteLine($"M24: status-filter search over {Count} docs took {statusSw.ElapsedMilliseconds} ms (total={deferredTotal})");

        // خطط التنفيذ للأشكال المكافئة (تُقرأ لا يُفشَل عليها — نص الخطة يتبدل بين إصدارات SQLite).
        var namePlan = Explain(
            "SELECT COUNT(*) FROM \"Documents\" AS \"d\" WHERE EXISTS " +
            "(SELECT 1 FROM \"Guarantors\" AS \"g\" WHERE \"g\".\"DocumentId\" = \"d\".\"Id\" " +
            "AND instr(\"g\".\"GuarantorName\", 'كفيل 5') > 0)");
        var statusPlan = Explain(
            $"SELECT COUNT(*) FROM \"Documents\" AS \"d\" WHERE \"d\".\"ExecStatus\" = '{ExecutionStatusCatalog.Deferred}'");
        Assert.NotEmpty(namePlan);
        Assert.NotEmpty(statusPlan);
        _output.WriteLine("M24: plan(name-search) = " + string.Join(" | ", namePlan));
        _output.WriteLine("M24: plan(status-filter) = " + string.Join(" | ", statusPlan));
        // تثبيت بنيوي فقط (نص الخطة يتبدل بين الإصدارات): بحث الأسماء مسبار `EXISTS`
        // مفهرس على التوابع + مسح خارجي؛ فلتر الحالة مسح كامل بلا فهرس.
        Assert.Contains(namePlan, d => d.Contains("EXISTS"));
        Assert.Contains(namePlan, d => d.Contains("SEARCH"));
        Assert.Contains(statusPlan, d => d.Contains("SCAN"));
    }

    /// <summary>تفاصيل `EXPLAIN QUERY PLAN` (عمود `detail`) لاستعلام خام على نفس الاتصال.</summary>
    private List<string> Explain(string sql)
    {
        using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "EXPLAIN QUERY PLAN " + sql;
        using var reader = cmd.ExecuteReader();
        var details = new List<string>();
        while (reader.Read())
            details.Add(reader.GetString(3));
        return details;
    }
}
