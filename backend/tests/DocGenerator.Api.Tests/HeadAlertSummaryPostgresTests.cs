using DocGenerator.Api.Tests.TestServices;
using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Api.Tests;

/// <summary>
/// تغطية حيّة لإسقاط ملخصات التنبيهات على <c>PostgreSQL</c> (`P3-8`): دلالات
/// <c>length()</c> على نقاط Unicode، وتحويل <c>timestamptz</c>، وترتيب
/// <c>LastContentChangedAt</c> المعاكس لترتيب <c>Id</c>، وkeyset cursor.
///
/// <para>كان الاختبار يقارن <c>SQLite</c> بـ<c>PostgreSQL</c> حقلًا بحقل؛ بعد `P1-08`
/// (‏`ADR-0009`) صار المزوّد واحدًا، فصارت التوقعات **صريحة** على `PostgreSQL` بدل
/// مقارنة مزوّدين (‏والمقارنة القديمة كانت تُرجع صامتة عند غياب السلسلة).</para>
/// </summary>
public sealed class HeadAlertSummaryPostgresTests
{
    private const int EmojiMessageLength = 600;
    private const string Emoji = "😀";

    /// <summary>معرّف إنابة اصطلاحي (لا FK على `HeadAlerts.DelegationId`) لاختبار فلتر الشريط.</summary>
    private const int DelegationId = 99;

    [PostgresFact]
    public async Task Summary_Projection_OnLivePostgres()
    {
        using var hostDb = PostgresHostDatabase.Create();
        var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
            .UseNpgsql(hostDb.ConnectionString, npgsql => npgsql.CommandTimeout(180))
            .Options;
        await using var postgres = new DocGeneratorPostgresDbContext(options);

        var seed = await SeedAsync(postgres);
        var repository = new HeadAlertRepository(postgres);

        // النشط فقط: البذور مرتبة بعكس الـId، والصف المؤرشف مستبعد.
        var oversight = await repository.ListSummariesForOversightAsync(
            AlertArchiveFilter.Active, delegationId: null, cursor: null, skip: 0, take: AlertPageRequest.MaxPageSize);

        Assert.Equal(2, oversight.TotalCount);
        Assert.Equal(
            new[] { seed.FirstAlertId, seed.SecondAlertId },
            oversight.Items.Select(i => i.Id));
        Assert.DoesNotContain(oversight.Items, i => i.Id == seed.ArchivedAlertId);

        var first = Assert.Single(oversight.Items, i => i.Id == seed.FirstAlertId);
        Assert.True(first.PreviewTruncated);
        Assert.Equal(EmojiMessageLength, first.MessageLength);
        Assert.Equal(HeadAlertTextLimits.PreviewMaxElements, first.MessagePreview.EnumerateRunes().Count());
        Assert.EndsWith(HeadAlertTextLimits.Ellipsis, first.MessagePreview);
        Assert.False(string.IsNullOrEmpty(first.MessagePreview));
        Assert.Equal(1, first.RecipientCount);
        Assert.Equal(1, first.UnreadCount);
        Assert.Equal("أحمد محمد العلي", first.DocumentTitle);

        // الأرشيف: يُطلب الكل فتظهر حقول ArchivedAt وTotalCount.
        var all = await repository.ListSummariesForOversightAsync(
            AlertArchiveFilter.All, delegationId: null, cursor: null, skip: 0, take: AlertPageRequest.MaxPageSize);
        Assert.Equal(3, all.TotalCount);
        Assert.Contains(
            all.Items,
            i => i.Id == seed.ArchivedAlertId && i.IsArchived && i.ArchivedAt is not null);

        // منظور المستلم: معاينة/طول/ترتيب/حالة قراءة، والعدّادات غير محسوبة.
        var recipient = await repository.ListSummariesForRecipientAsync(
            seed.LawyerId, branchId: null, AlertArchiveFilter.Active,
            unreadOnly: false, delegationId: null, cursor: null, skip: 0,
            take: AlertPageRequest.MaxPageSize, includeCounters: false);

        Assert.Equal(2, recipient.TotalCount);
        var unread = Assert.Single(recipient.Items, i => i.Id == seed.FirstAlertId);
        Assert.False(unread.IsRead);
        Assert.Null(unread.RecipientCount);
        var read = Assert.Single(recipient.Items, i => i.Id == seed.SecondAlertId);
        Assert.True(read.IsRead);
        Assert.Null(read.UnreadCount);

        // الإسقاط المفرد (جسم 201) — المسار الرابع المشترك في SummaryRows.
        var summary = await repository.GetSummaryAsync(
            seed.SecondAlertId, seed.LawyerId, includeCounters: true);
        Assert.NotNull(summary);
        Assert.Equal(1, summary!.RecipientCount);
        Assert.Equal(0, summary.UnreadCount);

        // الصادر: مرشّح المنشئ + الفرع (كلا التنبيهين أنشأهما الرئيس).
        var sent = await repository.ListSummariesSentAsync(
            seed.HeadId, seed.BranchId, AlertArchiveFilter.Active,
            cursor: null, skip: 0, take: AlertPageRequest.MaxPageSize);
        Assert.Equal(2, sent.TotalCount);
        Assert.Equal(
            new[] { seed.FirstAlertId, seed.SecondAlertId },
            sent.Items.Select(i => i.Id));

        // المؤشر: نفس شرط keyset ونفس الترتيب بعد ترجمة `OR` على `timestamptz`.
        var firstPage = await repository.ListSummariesForOversightAsync(
            AlertArchiveFilter.Active, delegationId: null, cursor: null, skip: 0, take: 1);
        var tail = Assert.Single(firstPage.Items);
        Assert.Equal(seed.FirstAlertId, tail.Id);

        var cursor = new AlertCursor(tail.LastContentChangedAt, tail.Id);
        var next = await repository.ListSummariesForOversightAsync(
            AlertArchiveFilter.Active, delegationId: null, cursor, skip: 0, take: 1);

        Assert.NotEmpty(next.Items);
        Assert.DoesNotContain(next.Items, i => i.Id == tail.Id);
        var nextItem = Assert.Single(next.Items);
        Assert.Equal(seed.SecondAlertId, nextItem.Id);

        // شريط الإنابة: التنبيه الثاني مرتبط بإنابة (بلا FK) — الفلتر نفسه.
        var delegation = await repository.ListSummariesByDelegationAsync(
            DelegationId, seed.LawyerId, skip: 0, take: AlertPageRequest.MaxPageSize);
        Assert.Equal(seed.SecondAlertId, Assert.Single(delegation.Items).Id);
    }

    /// <summary>
    /// بذور ثابتة على `PostgreSQL` فقط: الفرع/المستخدمون/الملف أولًا، ثم التنبيهات
    /// بترتيب إدخال واحد كي تكون المعرّفات المتوقعة صريحة.
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
