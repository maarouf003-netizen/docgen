using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حتمية حدود الصفحات **الإزاحية**: كل استعلام مُصفَّح يحتاج فاصلًا ثانيًا (`Id`)
/// بعد مفتاح الترتيب. بلا فاصل، ترتيب الصفوف المتساوية في المفتاح غير محدد في SQL،
/// فتُكرَّر صفوف أو تُفقد أخرى بين الصفحات — وهو عيب الإزاحة الجوهري حتى بلا أي
/// تزامن (والأثر أخطر في سجل التدقيق: `DateTime.UtcNow` على Windows دقّته ~15ms
/// فالتساوي شائع في العمليات الدفعية).
///
/// <para>تُثبت الاختبارات هنا المستودعات التي كانت بلا فاصل: المستندات (النشط
/// والمحذوف) وسجل التدقيق. المقارنة على **تسلسل المعرّفات** لا على المحتوى، لأن
/// الفاصل المطلوب هو `Id` تنازليًا.</para>
/// </summary>
public class PagedOrderingTests : IDisposable
{
    /// <summary>مرساة زمنية موحّدة: كل الصفوف متساوية في مفتاح الترتيب عن قصد.</summary>
    private static readonly DateTime Anchor = new(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc);

    private const int SeededRows = 6;
    private const int PageSize = 2;

    private readonly DocGeneratorDbContext _db;
    private readonly int _branchId;
    private readonly int _userId;

    public PagedOrderingTests()
    {
        _db = TestDb.Create();

        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        _db.Branches.Add(branch);
        _db.SaveChanges();

        var user = new User
        {
            Username = "ordering_user",
            FullName = "مستخدم الترتيب",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        _db.SaveChanges();

        _branchId = branch.Id;
        _userId = user.Id;
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task DocumentSearch_EqualCreatedAt_PagesAreDisjointAndComplete_ByDescendingId()
    {
        var seeded = new List<Document>(SeededRows);
        for (var i = 0; i < SeededRows; i++)
            seeded.Add(AddDocument($"ملف ترتيب {i}", deleted: false));

        var repository = new DocumentRepository(_db);

        var collected = new List<int>();
        for (var page = 1; page <= SeededRows / PageSize; page++)
        {
            var (total, items) = await repository.SearchAsync(
                null, null, null, null, null, null, null, null, null,
                visibleBranchId: null, visibleUserId: null, page, PageSize);

            Assert.Equal(SeededRows, total);
            collected.AddRange(items.Select(d => d.Id));
        }

        Assert.Equal(DescendingIds(seeded), collected);
        // لا تكرار ولا فقدان: المجموع يساوي عدد الصفوف وكل معرّف مرة واحدة.
        Assert.Equal(SeededRows, collected.Distinct().Count());
    }

    [Fact]
    public async Task DocumentSearchDeleted_EqualDeletedAt_PagesAreDisjointAndComplete_ByDescendingId()
    {
        var seeded = new List<Document>(SeededRows);
        for (var i = 0; i < SeededRows; i++)
            seeded.Add(AddDocument($"ملف محذوف {i}", deleted: true));

        var repository = new DocumentRepository(_db);

        var collected = new List<int>();
        for (var page = 1; page <= SeededRows / PageSize; page++)
        {
            var (total, items) = await repository.SearchDeletedAsync(
                query: null, visibleBranchId: null, visibleUserId: null, page, PageSize);

            Assert.Equal(SeededRows, total);
            collected.AddRange(items.Select(d => d.Id));
        }

        Assert.Equal(DescendingIds(seeded), collected);
        Assert.Equal(SeededRows, collected.Distinct().Count());
    }

    [Fact]
    public async Task AuditLogSearch_EqualTimestamp_PagesAreDisjointAndComplete_ByDescendingId()
    {
        for (var i = 0; i < SeededRows; i++)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Timestamp = Anchor,
                UserName = "ordering_user",
                ActionType = "view_document",
                Details = $"حدث ترتيب {i}",
            });
        }
        await _db.SaveChangesAsync();

        var seeded = _db.AuditLogs.AsEnumerable().ToList();
        var repository = new AuditLogRepository(_db);

        var collected = new List<int>();
        for (var page = 1; page <= SeededRows / PageSize; page++)
        {
            var (total, items) = await repository.SearchAsync(
                userName: null, actionType: null, page, PageSize);

            Assert.Equal(SeededRows, total);
            collected.AddRange(items.Select(a => a.Id));
        }

        Assert.Equal(DescendingIds(seeded), collected);
        Assert.Equal(SeededRows, collected.Distinct().Count());
    }

    private Document AddDocument(string searchText, bool deleted)
    {
        var document = new Document
        {
            BranchId = _branchId,
            CreatedById = _userId,
            IsDraft = false,
            BorrowerName = searchText,
            BorrowerFamily = "عائلة",
            SearchText = searchText,
            CreatedAt = Anchor,
            UpdatedAt = Anchor,
            IsDeleted = deleted,
            DeletedAt = deleted ? Anchor : null,
        };
        _db.Documents.Add(document);
        _db.SaveChanges();
        return document;
    }

    /// <summary>التسلسل المتوقع: تنازليًا بالمعرّف لأن مفتاح الترتيب متساوٍ عند الجميع.</summary>
    private static List<int> DescendingIds(IEnumerable<Document> rows) =>
        rows.Select(d => d.Id).OrderByDescending(id => id).ToList();

    private static List<int> DescendingIds(IEnumerable<AuditLog> rows) =>
        rows.Select(a => a.Id).OrderByDescending(id => id).ToList();
}
