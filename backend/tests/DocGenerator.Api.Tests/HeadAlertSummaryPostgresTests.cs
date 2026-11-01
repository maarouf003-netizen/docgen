using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using System.Text;

namespace DocGenerator.Api.Tests;

/// <summary>
/// تغطية حيّة لإسقاط ملخصات التنبيهات على <c>PostgreSQL</c> (`P3-8`): كل اختبارات
/// الإسقاط الحالية تعمل على <c>SQLite</c> وحده، فتنحرف دلالات <c>length()</c>/
/// <c>substr()</c> أو تحويل <c>timestamptz</c> بلا اكتشاف.
///
/// <para>تُنشأ قاعدة معزولة، تُطبَّق الهجرات حتى الأحدث، تُبذر الصفوف نفسها في
/// <c>SQLite</c> و<c>PostgreSQL</c>، ثم يُقارن ناتج <see cref="HeadAlertRepository"/>
/// حقلًا بحقل — لا «نجاح تنفيذ» فقط.</para>
///
/// <para>البذور المقصودة: نص إيموجي بطول 600 نقطة (حدّ المعاينة 561)، تنبيه مؤرشف،
/// صف مستلم مقروء وآخر غير مقروء، وتنبيهان ترتيب <c>LastContentChangedAt</c> فيهما
/// معاكس لترتيب <c>Id</c> (يكشف الاعتماد على ترتيب المفتاح بدل عمود الترتيب).</para>
///
/// <para>في <c>CI</c> يعمل فعليًا عبر <c>DOCGEN_REQUIRE_POSTGRES=1</c>؛ محليًا
/// يُتخطّى عند غياب السلسلة (انظر <see cref="PostgresTestEnvironment"/>).</para>
/// </summary>
public sealed class HeadAlertSummaryPostgresTests
{
    private const int EmojiMessageLength = 600;
    private const string Emoji = "😀";

    /// <summary>معرّف إنابة اصطلاحي (لا FK على `HeadAlerts.DelegationId`) لاختبار فلتر الشريط.</summary>
    private const int DelegationId = 99;

    [Fact]
    public async Task Summary_Projection_MatchesSqlite_OnLivePostgres()
    {
        var baseConnectionString = PostgresTestEnvironment.RequireOrSkip();
        if (baseConnectionString is null)
            return;

        await using var sqlite = CreateSqliteContext();
        await sqlite.Database.OpenConnectionAsync();
        await sqlite.Database.EnsureCreatedAsync();
        var sqliteSeed = await SeedAsync(sqlite);

        var databaseName = "docgen_sum_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = await PostgresTestDatabase.CreateAsync(baseConnectionString, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var postgres = new DocGeneratorPostgresDbContext(options);
            await postgres.Database.MigrateAsync();

            var postgresSeed = await SeedAsync(postgres);
            Assert.Equal(sqliteSeed, postgresSeed);

            var sqliteRepository = new HeadAlertRepository(sqlite);
            var postgresRepository = new HeadAlertRepository(postgres);

            // النشط فقط: البذور الثلاث مرتّبة بعكس الـId، والصف المؤرشف مستبعد.
            var sqliteOversight = await sqliteRepository.ListSummariesForOversightAsync(
                AlertArchiveFilter.Active, delegationId: null, skip: 0, take: AlertPageRequest.MaxPageSize);
            var postgresOversight = await postgresRepository.ListSummariesForOversightAsync(
                AlertArchiveFilter.Active, delegationId: null, skip: 0, take: AlertPageRequest.MaxPageSize);

            Assert.Equal(sqliteOversight.TotalCount, postgresOversight.TotalCount);
            Assert.Equal(
                new[] { sqliteSeed.FirstAlertId, sqliteSeed.SecondAlertId },
                sqliteOversight.Items.Select(i => i.Id));
            Assert.Equal(
                sqliteOversight.Items.Select(i => i.Id),
                postgresOversight.Items.Select(i => i.Id));
            Assert.Equal(sqliteOversight.Items, postgresOversight.Items);
            Assert.DoesNotContain(postgresOversight.Items, i => i.Id == sqliteSeed.ArchivedAlertId);

            var first = Assert.Single(postgresOversight.Items, i => i.Id == sqliteSeed.FirstAlertId);
            Assert.True(first.PreviewTruncated);
            Assert.Equal(EmojiMessageLength, first.MessageLength);
            Assert.Equal(HeadAlertTextLimits.PreviewMaxElements, first.MessagePreview.EnumerateRunes().Count());
            Assert.EndsWith(HeadAlertTextLimits.Ellipsis, first.MessagePreview);
            Assert.False(string.IsNullOrEmpty(first.MessagePreview));
            Assert.Equal(1, first.RecipientCount);
            Assert.Equal(1, first.UnreadCount);
            Assert.Equal("أحمد محمد العلي", first.DocumentTitle);

            // الأرشيف: يُقارن أيضًا عند طلب الكل (حقول ArchivedAt وTotalCount).
            var sqliteAll = await sqliteRepository.ListSummariesForOversightAsync(
                AlertArchiveFilter.All, delegationId: null, skip: 0, take: AlertPageRequest.MaxPageSize);
            var postgresAll = await postgresRepository.ListSummariesForOversightAsync(
                AlertArchiveFilter.All, delegationId: null, skip: 0, take: AlertPageRequest.MaxPageSize);
            Assert.Equal(sqliteAll.TotalCount, postgresAll.TotalCount);
            Assert.Equal(sqliteAll.Items, postgresAll.Items);
            Assert.Contains(
                postgresAll.Items,
                i => i.Id == sqliteSeed.ArchivedAlertId && i.IsArchived && i.ArchivedAt is not null);

            // منظور المستلم: معاينة/طول/ترتيب/حالة قراءة، والعدّادات غير محسوبة.
            var sqliteRecipient = await sqliteRepository.ListSummariesForRecipientAsync(
                sqliteSeed.LawyerId, branchId: null, AlertArchiveFilter.Active,
                unreadOnly: false, delegationId: null, skip: 0,
                take: AlertPageRequest.MaxPageSize, includeCounters: false);
            var postgresRecipient = await postgresRepository.ListSummariesForRecipientAsync(
                sqliteSeed.LawyerId, branchId: null, AlertArchiveFilter.Active,
                unreadOnly: false, delegationId: null, skip: 0,
                take: AlertPageRequest.MaxPageSize, includeCounters: false);

            Assert.Equal(sqliteRecipient.Items, postgresRecipient.Items);
            Assert.Equal(sqliteRecipient.TotalCount, postgresRecipient.TotalCount);
            var unread = Assert.Single(postgresRecipient.Items, i => i.Id == sqliteSeed.FirstAlertId);
            Assert.False(unread.IsRead);
            Assert.Null(unread.RecipientCount);
            var read = Assert.Single(postgresRecipient.Items, i => i.Id == sqliteSeed.SecondAlertId);
            Assert.True(read.IsRead);
            Assert.Null(read.UnreadCount);

            // الإسقاط المفرد (جسم 201) — المسار الرابع المشترك في SummaryRows.
            var sqliteSummary = await sqliteRepository.GetSummaryAsync(
                sqliteSeed.SecondAlertId, sqliteSeed.LawyerId, includeCounters: true);
            var postgresSummary = await postgresRepository.GetSummaryAsync(
                sqliteSeed.SecondAlertId, sqliteSeed.LawyerId, includeCounters: true);
            Assert.Equal(sqliteSummary, postgresSummary);
            Assert.NotNull(postgresSummary);
            Assert.Equal(1, postgresSummary!.RecipientCount);
            Assert.Equal(0, postgresSummary.UnreadCount);

            // الصادر: مرشّح المنشئ + الفرع (كلا التنبيهين أنشأهما الرئيس).
            var sqliteSent = await sqliteRepository.ListSummariesSentAsync(
                sqliteSeed.HeadId, sqliteSeed.BranchId, AlertArchiveFilter.Active,
                skip: 0, take: AlertPageRequest.MaxPageSize);
            var postgresSent = await postgresRepository.ListSummariesSentAsync(
                sqliteSeed.HeadId, sqliteSeed.BranchId, AlertArchiveFilter.Active,
                skip: 0, take: AlertPageRequest.MaxPageSize);
            Assert.Equal(sqliteSent.TotalCount, postgresSent.TotalCount);
            Assert.Equal(sqliteSent.Items, postgresSent.Items);

            // شريط الإنابة: التنبيه الثاني مرتبط بإنابة (بلا FK) — الفلتر نفسه.
            var sqliteDelegation = await sqliteRepository.ListSummariesByDelegationAsync(
                DelegationId, sqliteSeed.LawyerId, skip: 0, take: AlertPageRequest.MaxPageSize);
            var postgresDelegation = await postgresRepository.ListSummariesByDelegationAsync(
                DelegationId, sqliteSeed.LawyerId, skip: 0, take: AlertPageRequest.MaxPageSize);
            Assert.Equal(sqliteDelegation.Items, postgresDelegation.Items);
            Assert.Equal(
                sqliteSeed.SecondAlertId,
                Assert.Single(postgresDelegation.Items).Id);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(baseConnectionString, databaseName);
        }
    }

    private static DocGeneratorDbContext CreateSqliteContext()
    {
        var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        return new DocGeneratorDbContext(options);
    }

    /// <summary>
    /// بذور متطابقة في القاعدتين: الفرع/المستخدمون/الملف أولًا، ثم التنبيهات
    /// بترتيب إدخال واحد كي تتطابق المعرّفات، فيصبح <c>Assert.Equal</c> على
    /// السجلات كاملًا ذا معنى.
    /// </summary>
    private static async Task<Seed> SeedAsync(DocGeneratorDbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var branch = new Branch { Name = "دمشق", Code = $"SUM-{suffix}" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var head = new User
        {
            Username = $"sum_head_{suffix}",
            FullName = "رئيس القسم",
            Role = UserRole.Head,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        var lawyer = new User
        {
            Username = $"sum_law_{suffix}",
            FullName = "محامي الملخصات",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        db.Users.AddRange(head, lawyer);
        await db.SaveChangesAsync();

        var document = new Document
        {
            BranchId = branch.Id,
            CreatedById = lawyer.Id,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "محمد",
            BorrowerFamily = "العلي",
            DocumentType = "عقد",
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var earlier = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var later = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc);
        var oldest = new DateTime(2025, 12, 31, 9, 0, 0, DateTimeKind.Utc);

        // الأول: Id أصغر لكن LastContentChangedAt أحدث ⇒ يجب أن يتصدّر.
        var first = new HeadAlert
        {
            BranchId = branch.Id,
            CreatedById = head.Id,
            TargetType = HeadAlertTargetType.Document,
            Kind = HeadAlertKindCatalog.ManualToDocument,
            DocumentId = document.Id,
            Message = string.Concat(Enumerable.Repeat(Emoji, EmojiMessageLength)),
            CreatedAt = earlier,
            LastContentChangedAt = later,
            Recipients = new List<HeadAlertRecipient>
            {
                new() { UserId = lawyer.Id, IsRead = false },
            },
        };
        // الثاني: Id أكبر وLastContentChangedAt أقدم ⇒ يليه رغم الـId الأعلى.
        var second = new HeadAlert
        {
            BranchId = branch.Id,
            CreatedById = head.Id,
            TargetType = HeadAlertTargetType.Lawyer,
            Kind = HeadAlertKindCatalog.ManualToLawyer,
            TargetLawyerId = lawyer.Id,
            DelegationId = DelegationId,
            Message = "رسالة قصيرة",
            CreatedAt = earlier,
            LastContentChangedAt = earlier,
            Recipients = new List<HeadAlertRecipient>
            {
                new() { UserId = lawyer.Id, IsRead = true, ReadAt = earlier },
            },
        };
        var archived = new HeadAlert
        {
            BranchId = branch.Id,
            CreatedById = head.Id,
            TargetType = HeadAlertTargetType.Branch,
            Kind = HeadAlertKindCatalog.ManualToBranch,
            Message = "تنبيه مؤرشف",
            CreatedAt = oldest,
            LastContentChangedAt = oldest,
            IsArchived = true,
            ArchivedAt = later,
        };

        db.HeadAlerts.AddRange(first, second, archived);
        await db.SaveChangesAsync();

        return new Seed(branch.Id, head.Id, lawyer.Id, first.Id, second.Id, archived.Id);
    }

    private sealed record Seed(
        int BranchId,
        int HeadId,
        int LawyerId,
        int FirstAlertId,
        int SecondAlertId,
        int ArchivedAlertId);
}
