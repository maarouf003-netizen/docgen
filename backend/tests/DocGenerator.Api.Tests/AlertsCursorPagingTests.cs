using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;

using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// تكامل مؤشر الترقيم على <c>GET /api/alerts</c> و<c>/api/alerts/sent</c>
/// (الخطة §9.1، الدليل §7 البند 4): المؤشر يثبّت نافذة الصفحة على آخر صف معروض،
/// فلا تكرار ولا فجوة عند إصدار تنبيه بين الجلبين، ويُرفض أي صيغة غير صالحة
/// بـ<c>400</c> عربي. مسار <c>page</c> القديم يبقى مدعومًا كما هو.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class AlertsCursorPagingTests
{
    private readonly ApiFactory _factory;

    public AlertsCursorPagingTests(ApiFactory factory) => _factory = factory;

    /// <summary>مرساة زمنية ثابتة: الترتيب حتمي بلا اعتماد على ساعة التشغيل.</summary>
    private static readonly DateTime Anchor = new(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc);

    private static async Task<PagedResult<HeadAlertSummaryDto>> ReadPageAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<HeadAlertSummaryDto>>();
        Assert.NotNull(page);
        return page!;
    }

    /// <summary>يقرأ رسالة الخطأ العربية من جسم الاستجابة (<c>ProblemDetails.message</c>).</summary>
    private static async Task<string> ReadProblemMessageAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("message", out var message)
            ? message.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    /// يبذر تنبيهات يدوية مباشرة لاستقبال محامٍ واحد بأزمنة صريحة متزايدة
    /// (الأحدث آخرًا) — لا يمرّ بالخدمة كي لا يتدخل منطق الإحلال في البذور.
    /// </summary>
    private async Task<IReadOnlyList<int>> SeedAlertsAsync(
        int branchId,
        int recipientUserId,
        int createdById,
        int count,
        int minuteOffset = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var ids = new List<int>(count);

        for (var i = 0; i < count; i++)
        {
            var alert = new HeadAlert
            {
                BranchId = branchId,
                CreatedById = createdById,
                TargetType = HeadAlertTargetType.Lawyer,
                Kind = HeadAlertKindCatalog.ManualToLawyer,
                TargetLawyerId = recipientUserId,
                Message = $"تنبيه مؤشر {minuteOffset + i}",
                CreatedAt = Anchor.AddMinutes(minuteOffset + i),
                LastContentChangedAt = Anchor.AddMinutes(minuteOffset + i),
                Recipients = new List<HeadAlertRecipient> { new() { UserId = recipientUserId } },
            };
            db.HeadAlerts.Add(alert);
            await db.SaveChangesAsync();
            ids.Add(alert.Id);
        }

        return ids;
    }

    private static string Wire(HeadAlertSummaryDto row) =>
        AlertCursor.Format(row.LastContentChangedAt, row.Id);

    [Fact]
    public async Task CursorPaging_WalksAllRowsOnce_WithoutGapsOrDuplicates()
    {
        var branchId = await _factory.GetBranchIdAsync("DAM");
        var (lawyerId, client) = await _factory.NewLawyerClientAsync(branchId);
        var createdById = await _factory.UserIdAsync("head1");
        var seeded = await SeedAlertsAsync(branchId, lawyerId, createdById, count: 5);

        // الأحدث أولًا: آخر بذرة ثم ما قبلها.
        var expected = Enumerable.Reverse(seeded).ToArray();

        var first = await ReadPageAsync(
            await client.GetAsync("/api/alerts?pageSize=2"));
        Assert.Equal(5, first.TotalCount);
        Assert.Equal(expected.Take(2), first.Items.Select(i => i.Id));

        var second = await ReadPageAsync(
            await client.GetAsync($"/api/alerts?pageSize=2&cursor={Uri.EscapeDataString(Wire(first.Items[^1]))}"));
        Assert.Equal(5, second.TotalCount);
        Assert.Equal(expected.Skip(2).Take(2), second.Items.Select(i => i.Id));

        var third = await ReadPageAsync(
            await client.GetAsync($"/api/alerts?pageSize=2&cursor={Uri.EscapeDataString(Wire(second.Items[^1]))}"));
        Assert.Equal(5, third.TotalCount);
        Assert.Equal(expected.Skip(4), third.Items.Select(i => i.Id));

        var collected = first.Items.Concat(second.Items).Concat(third.Items).Select(i => i.Id).ToArray();
        Assert.Equal(expected, collected);
        Assert.Equal(expected.Length, collected.Distinct().Count());

        // بعد آخر صف: صفحة فارغة لا تُعيد شيئًا، والإجمالي يبقى إجمالي القائمة.
        var exhausted = await ReadPageAsync(
            await client.GetAsync($"/api/alerts?pageSize=2&cursor={Uri.EscapeDataString(Wire(third.Items[^1]))}"));
        Assert.Empty(exhausted.Items);
        Assert.Equal(5, exhausted.TotalCount);
    }

    [Fact]
    public async Task CursorPaging_InsertingNewerRowBetweenFetches_DoesNotShiftWindow()
    {
        var branchId = await _factory.GetBranchIdAsync("DAM");
        var (lawyerId, client) = await _factory.NewLawyerClientAsync(branchId);
        var createdById = await _factory.UserIdAsync("head1");
        var seeded = await SeedAlertsAsync(branchId, lawyerId, createdById, count: 4);

        var first = await ReadPageAsync(await client.GetAsync("/api/alerts?pageSize=2"));
        Assert.Equal(Enumerable.Reverse(seeded).Take(2), first.Items.Select(i => i.Id));

        // صف أحدث من كل المعروض يُدرج بين الجلبين: الإزاحة كانت ستُعيد الصفوف نفسها.
        var inserted = await SeedAlertsAsync(
            branchId, lawyerId, createdById, count: 1, minuteOffset: 60);

        var second = await ReadPageAsync(
            await client.GetAsync(
                $"/api/alerts?pageSize=2&cursor={Uri.EscapeDataString(Wire(first.Items[^1]))}"));

        // الصفحة التالية هي بقيّة الصفوف القديمة بالضبط — لا تكرار ولا فقدان.
        Assert.Equal(Enumerable.Reverse(seeded).Skip(2), second.Items.Select(i => i.Id));
        Assert.DoesNotContain(inserted[0], second.Items.Select(i => i.Id));
        Assert.Equal(5, second.TotalCount);

        // والصف الجديد يُلتقط بإعادة التحميل من الصفحة الأولى (المسار الوحيد المقصود).
        var refreshed = await ReadPageAsync(await client.GetAsync("/api/alerts?pageSize=2"));
        Assert.Equal(new[] { inserted[0] }.Concat(Enumerable.Reverse(seeded).Take(1)), refreshed.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Cursor_TakesPrecedenceOverPageNumber_AndReportsPageOne()
    {
        var branchId = await _factory.GetBranchIdAsync("DAM");
        var (lawyerId, client) = await _factory.NewLawyerClientAsync(branchId);
        var createdById = await _factory.UserIdAsync("head1");
        await SeedAlertsAsync(branchId, lawyerId, createdById, count: 5);

        var first = await ReadPageAsync(await client.GetAsync("/api/alerts?pageSize=2"));
        var cursor = Uri.EscapeDataString(Wire(first.Items[^1]));

        var byCursor = await ReadPageAsync(
            await client.GetAsync($"/api/alerts?pageSize=2&cursor={cursor}"));
        // رقم صفحة مضلّل لا يغيّر النافذة: المؤشر هو المرجع.
        var byCursorWithPage = await ReadPageAsync(
            await client.GetAsync($"/api/alerts?pageSize=2&page=9&cursor={cursor}"));

        Assert.Equal(byCursor.Items.Select(i => i.Id), byCursorWithPage.Items.Select(i => i.Id));
        Assert.Equal(1, byCursorWithPage.Page);
        Assert.Equal(1, byCursor.Page);
        Assert.Equal(2, byCursorWithPage.PerPage);
    }

    [Fact]
    public async Task OffsetPaging_StillWorks_AndReportsRequestedPage()
    {
        var branchId = await _factory.GetBranchIdAsync("DAM");
        var (lawyerId, client) = await _factory.NewLawyerClientAsync(branchId);
        var createdById = await _factory.UserIdAsync("head1");
        var seeded = await SeedAlertsAsync(branchId, lawyerId, createdById, count: 5);

        var page2 = await ReadPageAsync(await client.GetAsync("/api/alerts?pageSize=2&page=2"));

        Assert.Equal(2, page2.Page);
        Assert.Equal(Enumerable.Reverse(seeded).Skip(2).Take(2), page2.Items.Select(i => i.Id));
        Assert.Equal(5, page2.TotalCount);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("2026-11-02T09:30:15Z")] // بلا معرّف.
    [InlineData("2026-11-02T09:30:15Z|0")] // المعرّف يبدأ من 1.
    [InlineData("2026-11-02T09:30:15|5")] // وقت بلا منطقة UTC.
    [InlineData("2026-11-02T09:30:15+03:00|5")] // منطقة غير UTC.
    [InlineData("not-a-date|5")]
    public async Task InvalidCursor_Returns400WithArabicMessage(string cursor)
    {
        var branchId = await _factory.GetBranchIdAsync("DAM");
        var (_, client) = await _factory.NewLawyerClientAsync(branchId);

        var response = await client.GetAsync(
            $"/api/alerts?pageSize=2&cursor={Uri.EscapeDataString(cursor)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = await ReadProblemMessageAsync(response);
        // الرسالة عربية كاملة بلا ذيل إطار العمل الإنجليزي (Parameter 'cursor').
        Assert.Equal(
            "قيمة cursor غير صالحة — أعِد تحميل القائمة ثم أعد المحاولة",
            message);
        Assert.DoesNotContain("Parameter", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task PageNumber_IsStillValidated_EvenThoughCursorOverridesWindow(int page)
    {
        // §9.1: المؤشر يلغي **أثر** `page` في النافذة، لا التحقق منه — فيبقى
        // الطلب المشوّه مرفوضًا، ورسالته عربية نظيفة.
        var branchId = await _factory.GetBranchIdAsync("DAM");
        var (lawyerId, client) = await _factory.NewLawyerClientAsync(branchId);
        var createdById = await _factory.UserIdAsync("head1");
        await SeedAlertsAsync(branchId, lawyerId, createdById, count: 3);

        var first = await ReadPageAsync(await client.GetAsync("/api/alerts?pageSize=2"));
        var cursor = Uri.EscapeDataString(Wire(first.Items[^1]));

        var response = await client.GetAsync($"/api/alerts?cursor={cursor}&page={page}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = await ReadProblemMessageAsync(response);
        Assert.Equal("رقم الصفحة يبدأ من 1", message);
        Assert.DoesNotContain("Parameter", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CursorPaging_WithArchivedAllAndOnly_WalksTheFilteredSet()
    {
        var branchId = await _factory.GetBranchIdAsync("DAM");
        var (lawyerId, client) = await _factory.NewLawyerClientAsync(branchId);
        var createdById = await _factory.UserIdAsync("head1");
        var seeded = await SeedAlertsAsync(branchId, lawyerId, createdById, count: 3);

        // أرشفة الأحدث: يخرج من «النشط» ويبقى في «الكل» و«المؤرشف فقط».
        var archivedId = seeded[^1];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var alert = await db.HeadAlerts.SingleAsync(a => a.Id == archivedId);
            alert.IsArchived = true;
            alert.ArchivedAt = Anchor.AddHours(1);
            await db.SaveChangesAsync();
        }

        var allFirst = await ReadPageAsync(
            await client.GetAsync("/api/alerts?archived=all&pageSize=2"));
        Assert.Equal(3, allFirst.TotalCount);
        Assert.Equal(new[] { archivedId, seeded[1] }, allFirst.Items.Select(i => i.Id));

        var allSecond = await ReadPageAsync(
            await client.GetAsync(
                $"/api/alerts?archived=all&pageSize=2&cursor={Uri.EscapeDataString(Wire(allFirst.Items[^1]))}"));
        Assert.Equal(3, allSecond.TotalCount);
        Assert.Equal(new[] { seeded[0] }, allSecond.Items.Select(i => i.Id));

        var onlyArchived = await ReadPageAsync(
            await client.GetAsync("/api/alerts?archived=only&pageSize=2"));
        Assert.Equal(archivedId, Assert.Single(onlyArchived.Items).Id);

        // بعد الصف المؤرشف الوحيد: نافذة فارغة (المؤشر لا يتجاوز الفلتر).
        var onlyArchivedNext = await ReadPageAsync(
            await client.GetAsync(
                $"/api/alerts?archived=only&pageSize=2&cursor={Uri.EscapeDataString(Wire(onlyArchived.Items[^1]))}"));
        Assert.Empty(onlyArchivedNext.Items);
        Assert.Equal(1, onlyArchivedNext.TotalCount);
    }

    /// <summary>
    /// السيناريو الذي كان يُنتج قصًّا صامتًا في الواجهة: **أرشفة صفوف بين الجلبين**
    /// تُنقص `totalCount` حتى يصبح المحمّل مساويًا/متجاوزًا له. العقد الخادمي الذي
    /// تبني عليه الواجهة قرارها «صفحة ممتلئة ⇒ قد يوجد مزيد»: المؤشر يواصل بعد آخر
    /// صف معروض ويعيد بقية القائمة **دائمًا** حتى النهاية، ولا يعتمد على `totalCount`.
    /// </summary>
    [Fact]
    public async Task CursorPaging_AfterArchiveBetweenFetches_StillDeliversRemainingRows()
    {
        var branchId = await _factory.GetBranchIdAsync("DAM");
        var (lawyerId, client) = await _factory.NewLawyerClientAsync(branchId);
        var createdById = await _factory.UserIdAsync("head1");
        var seeded = await SeedAlertsAsync(branchId, lawyerId, createdById, count: 5);

        var first = await ReadPageAsync(await client.GetAsync("/api/alerts?pageSize=2"));
        Assert.Equal(5, first.TotalCount);
        Assert.Equal(Enumerable.Reverse(seeded).Take(2), first.Items.Select(i => i.Id));

        // أرشفة الصفوف المعروضة أثناء التصفح: تخرج من «النشط» ⇒ الإجمالي 3.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var shown = first.Items.Select(i => i.Id).ToArray();
            foreach (var alert in await db.HeadAlerts.Where(a => shown.Contains(a.Id)).ToListAsync())
            {
                alert.IsArchived = true;
                alert.ArchivedAt = Anchor.AddHours(1);
            }
            await db.SaveChangesAsync();
        }

        var second = await ReadPageAsync(await client.GetAsync(
            $"/api/alerts?pageSize=2&cursor={Uri.EscapeDataString(Wire(first.Items[^1]))}"));

        // صفحة ممتلئة والإجمالي 3 (أصغر من مجموع ما حُمّل 4) — وهذا بالضبط ما كان
        // يُقنع الواجهة القديمة بالتوقف. الخادم يواصل: [3,2].
        Assert.Equal(3, second.TotalCount);
        Assert.Equal(Enumerable.Reverse(seeded).Skip(2).Take(2), second.Items.Select(i => i.Id));

        var third = await ReadPageAsync(await client.GetAsync(
            $"/api/alerts?pageSize=2&cursor={Uri.EscapeDataString(Wire(second.Items[^1]))}"));

        // الصف الأخير الذي كان سيُفقد في الإصلاح الأول: يصل عبر صفحة ثالثة.
        Assert.Equal(new[] { seeded[0] }, third.Items.Select(i => i.Id));
        Assert.Equal(3, third.TotalCount);

        var walked = first.Items.Concat(second.Items).Concat(third.Items)
            .Select(i => i.Id).ToArray();
        Assert.Equal(seeded.Count, walked.Distinct().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(AlertPageRequest.MaxPageSize + 1)]
    public async Task InvalidPageSize_WithCursor_Returns400(int pageSize)
    {
        var branchId = await _factory.GetBranchIdAsync("DAM");
        var (lawyerId, client) = await _factory.NewLawyerClientAsync(branchId);
        var createdById = await _factory.UserIdAsync("head1");
        await SeedAlertsAsync(branchId, lawyerId, createdById, count: 3);

        var first = await ReadPageAsync(await client.GetAsync("/api/alerts?pageSize=2"));
        var cursor = Uri.EscapeDataString(Wire(first.Items[^1]));

        var response = await client.GetAsync($"/api/alerts?cursor={cursor}&pageSize={pageSize}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SentCursorPaging_WalksCreatorRows_AndRejectsInvalidCursor()
    {
        // فرع جديد: القيد الفريد يسمح برئيس نشط واحد لكل فرع (UserConfiguration).
        var branchId = await _factory.CreateBranchAsync(UniqueName("فرع"));
        var head = await _factory.CreateUserAsync(
            UniqueName("head_cursor"), UserRole.Head, branchId);
        var recipientId = await _factory.UserIdAsync("lawyer1");
        var seeded = await SeedAlertsAsync(branchId, recipientId, head.Id, count: 3);
        var client = _factory.ClientForUser(head);

        var first = await ReadPageAsync(
            await client.GetAsync("/api/alerts/sent?pageSize=2"));
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(Enumerable.Reverse(seeded).Take(2), first.Items.Select(i => i.Id));
        Assert.Equal(1, first.Page);

        var second = await ReadPageAsync(
            await client.GetAsync(
                $"/api/alerts/sent?pageSize=2&cursor={Uri.EscapeDataString(Wire(first.Items[^1]))}"));
        Assert.Equal(3, second.TotalCount);
        Assert.Equal(Enumerable.Reverse(seeded).Skip(2), second.Items.Select(i => i.Id));
        Assert.Equal(1, second.Page);

        var invalid = await client.GetAsync("/api/alerts/sent?cursor=garbage");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains(
            "cursor غير صالحة",
            await ReadProblemMessageAsync(invalid),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// المؤشر يعمل مع بقية الفلاتر (غير المقروء): شرط keyset يُطبَّق على النتيجة
    /// المفلترة نفسها أصلًا، و<b>قبل</b> نافذة الصفحة.
    /// </summary>
    [Fact]
    public async Task CursorPaging_AppliesToFilteredSetOnly()
    {
        var branchId = await _factory.GetBranchIdAsync("DAM");
        var (lawyerId, client) = await _factory.NewLawyerClientAsync(branchId);
        var createdById = await _factory.UserIdAsync("head1");
        var seeded = await SeedAlertsAsync(branchId, lawyerId, createdById, count: 4);

        // تعليم الأحدث مقروءًا: يخرج من «غير المقروء» فيبقى ثلاثة.
        var readAlertId = seeded[^1];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var recipient = await db.Set<HeadAlertRecipient>()
                .SingleAsync(r => r.HeadAlertId == readAlertId && r.UserId == lawyerId);
            recipient.IsRead = true;
            recipient.ReadAt = Anchor.AddHours(1);
            await db.SaveChangesAsync();
        }

        var first = await ReadPageAsync(
            await client.GetAsync("/api/alerts?pageSize=2&unreadOnly=true"));
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(seeded.Take(3).Reverse().Take(2), first.Items.Select(i => i.Id));

        var second = await ReadPageAsync(
            await client.GetAsync(
                $"/api/alerts?pageSize=2&unreadOnly=true&cursor={Uri.EscapeDataString(Wire(first.Items[^1]))}"));
        Assert.Equal(3, second.TotalCount);
        Assert.Equal(new[] { seeded[0] }, second.Items.Select(i => i.Id));
    }

    /// <summary>
    /// وضع الإشراف (مدير/مشرف): القائمة عبر الفروع بحالة قراءة `null` وعدّادات
    /// مستلمين محسوبة قاعديًا، وتعمل مع المؤشر، والعدّاد الشخصي مرفوض `403`.
    /// (كان الترقيم الإشرافي بالمؤشر غير مُختبر عبر `HTTP` — كشفته مراجعة وضع الإشراف.)
    /// </summary>
    [Fact]
    public async Task OversightRole_ListIsSupervisionShaped_AndPagesWithCursor()
    {
        var branchId = await _factory.GetBranchIdAsync("DAM");
        var createdById = await _factory.UserIdAsync("head1");
        var recipientId = await _factory.UserIdAsync("lawyer1");
        await SeedAlertsAsync(branchId, recipientId, createdById, count: 3);

        var manager = _factory.AuthorizedClient("manager");
        var first = await ReadPageAsync(await manager.GetAsync("/api/alerts?pageSize=2"));

        Assert.Equal(1, first.Page);
        Assert.True(first.TotalCount >= 3);
        Assert.All(first.Items, item => Assert.Null(item.IsRead));
        Assert.All(first.Items, item => Assert.NotNull(item.RecipientCount));

        if (first.Items.Count == 0)
            return;

        var second = await ReadPageAsync(
            await manager.GetAsync(
                $"/api/alerts?pageSize=2&cursor={Uri.EscapeDataString(Wire(first.Items[^1]))}"));

        Assert.Equal(1, second.Page);
        Assert.Empty(second.Items.Select(i => i.Id).Intersect(first.Items.Select(i => i.Id)));

        // العدّاد الشخصي «غير المقروء لي» لا معنى له للإشراف (مستلم-محدد خلفيًا).
        var unread = await manager.GetAsync("/api/alerts/unread-count?scope=user");
        Assert.Equal(HttpStatusCode.Forbidden, unread.StatusCode);
    }

    /// <summary>
    /// ترتيب القرار (§9.4): الدور يسبق شكل الطلب، فلا ينقلب <c>403</c> إلى <c>400</c>
    /// بسبب مؤشر غير صالح. تحليل المؤشر في الخدمة بعد فحص الدور/الفرع هو ما يحفظ هذا
    /// (والحالة الكاملة لرئيس بلا فرع في <c>AlertsIntegrationTests</c>).
    /// </summary>
    [Fact]
    public async Task AuthorizationPrecedesCursorShape_SoForbiddenWins()
    {
        var manager = _factory.AuthorizedClient("manager");

        var sent = await manager.GetAsync("/api/alerts/sent?cursor=garbage");
        Assert.Equal(HttpStatusCode.Forbidden, sent.StatusCode);

        var list = await manager.GetAsync("/api/alerts/sent?pageSize=1&cursor=garbage");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }
}
