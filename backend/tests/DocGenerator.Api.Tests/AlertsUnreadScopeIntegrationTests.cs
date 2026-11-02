using System.Net;
using System.Net.Http.Json;

using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;

using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// نطاق عدّاد غير المقروء (`D6`/`D7`، الخطة §5/المرحلة 4) على **قاعدة معزولة لكل اختبار**،
/// فالأعداد المطلقة حاسمة: <c>user</c> = صفوف الفاعل نفسه (الرئيس مقيّد بفرعه).
/// حُذف نطاق <c>received</c> في <c>PR-6a</c>، فصار <c>?scope=received</c> <c>400</c> عربيًا صريحًا.
/// </summary>
public sealed class AlertsUnreadScopeIntegrationTests : IsolatedApiTestBase
{
    private sealed record UnreadCountDto(int Count);

    /// <summary>تنبيه مُبذور مباشرة بمستلمين صريحين — لضبط صفوف القراءة بدقة.</summary>
    private async Task<int> SeedAlertAsync(
        int branchId, int createdById, string message, bool archived, params int[] recipientIds)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var alert = new HeadAlert
        {
            BranchId = branchId,
            CreatedById = createdById,
            TargetType = HeadAlertTargetType.Lawyer,
            Kind = HeadAlertKindCatalog.ManualToLawyer,
            TargetLawyerId = recipientIds.FirstOrDefault(),
            Message = message,
            IsArchived = archived,
            CreatedAt = DateTime.UtcNow,
            LastContentChangedAt = DateTime.UtcNow,
            Recipients = recipientIds.Select(id => new HeadAlertRecipient { UserId = id }).ToList(),
        };
        db.HeadAlerts.Add(alert);
        await db.SaveChangesAsync();
        return alert.Id;
    }

    private static async Task<int> CountAsync(HttpClient client, string? scope)
    {
        var url = scope is null
            ? "/api/alerts/unread-count"
            : $"/api/alerts/unread-count?scope={scope}";
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UnreadCountDto>();
        Assert.NotNull(dto);
        return dto!.Count;
    }

    [Fact]
    public async Task UserScope_IsBranchBound_AndEqualsUnreadOnlyListTotal()
    {
        var damId = await Factory.GetBranchIdAsync("DAM");
        var aleppoId = await Factory.GetBranchIdAsync("ALP");
        var headId = await Factory.UserIdAsync("head1");
        var lawyerId = await Factory.UserIdAsync("lawyer1");
        var extra = await Factory.CreateUserAsync(UniqueName("lawyer_scope"), UserRole.Lawyer, damId);

        // A في فرعه ومستلم فيه مع محاميين ⇒ صفّ غير مقروء واحد لصاحب الحساب.
        var collectiveId = await SeedAlertAsync(
            damId, headId, "جماعي", archived: false, headId, lawyerId, extra.Id);
        // B في فرعه لكنه ليس مستلمًا ⇒ لا يُحتسب.
        await SeedAlertAsync(damId, headId, "لمحامٍ فقط", archived: false, lawyerId);
        // C استلمه في فرع آخر ⇒ خارج نطاق الرئيس (قيد الفرع).
        await SeedAlertAsync(aleppoId, headId, "فرع آخر", archived: false, headId);
        // D مؤرشف في فرعه ومستلم فيه ⇒ مستبعد.
        await SeedAlertAsync(damId, headId, "مؤرشف", archived: true, headId, lawyerId);

        var head = Factory.AuthorizedClient("head1");

        // user: صفّه وحده في A ⇒ 1.
        Assert.Equal(1, await CountAsync(head, "user"));

        // الافتراضي (بلا scope) هو user — التنبيه C في فرع آخر مستبعد، فالقيمة 1 لا 2.
        Assert.Equal(1, await CountAsync(head, null));

        // D7 + ح‑19: الثبات هو «العدّاد = TotalCount لقائمة غير المقروء» (يصمد أمام
        // الترقيم)، لا طول الصفحة. القائمة العامة تبقى أطول لأنها تشمل المقروء.
        var unreadListResponse = await head.GetAsync("/api/alerts?unreadOnly=true&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, unreadListResponse.StatusCode);
        var unreadPage = await unreadListResponse.Content
            .ReadFromJsonAsync<PagedResult<HeadAlertSummaryDto>>();
        Assert.NotNull(unreadPage);
        Assert.Equal(unreadPage!.TotalCount, await CountAsync(head, "user"));

        var listResponse = await head.GetAsync("/api/alerts?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResult<HeadAlertSummaryDto>>();
        Assert.NotNull(page);
        Assert.True(page!.Items.Count >= unreadPage.TotalCount);

        // قراءة صف محامٍ من A لا تمس صف الرئيس (العدّاد «لي» لا مجموع المستلمين).
        var lawyer = Factory.AuthorizedClient("lawyer1");
        Assert.Equal(HttpStatusCode.NoContent,
            (await lawyer.PatchAsJsonAsync($"/api/alerts/{collectiveId}/read", new { })).StatusCode);
        Assert.Equal(1, await CountAsync(head, "user"));

        // ح‑21: قراءة الرئيس صفّه تخفض `user` إلى صفر.
        Assert.Equal(HttpStatusCode.NoContent,
            (await head.PatchAsJsonAsync($"/api/alerts/{collectiveId}/read", new { })).StatusCode);
        Assert.Equal(0, await CountAsync(head, "user"));
    }

    [Fact]
    public async Task ReceivedScope_IsRejectedAsBadRequest_ForAllRoles_WithCleanArabicMessage()
    {
        // PR-6a: `received` محذوف — 400 عربي صريح لكل دور، لا 500 ولا سقوط صامت إلى user.
        foreach (var username in new[] { "head1", "lawyer1", "manager", "admin" })
        {
            var response = await Factory.AuthorizedClient(username)
                .GetAsync("/api/alerts/unread-count?scope=received");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var json = System.Text.Json.JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());
            Assert.Equal(
                "قيمة scope غير صالحة — استخدم user",
                json.RootElement.GetProperty("message").GetString());
        }

        // النطاق المعتمد `user` بلا تغيير: 200 للمستلمين و403 للإشراف.
        Assert.Equal(HttpStatusCode.OK, (await Factory.AuthorizedClient("head1")
            .GetAsync("/api/alerts/unread-count?scope=user")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Factory.AuthorizedClient("lawyer1")
            .GetAsync("/api/alerts/unread-count?scope=user")).StatusCode);
        foreach (var username in new[] { "manager", "admin" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await Factory.AuthorizedClient(username)
                .GetAsync("/api/alerts/unread-count?scope=user")).StatusCode);
        }
    }

    [Theory]
    [InlineData("xxx")]
    [InlineData("branch")]
    [InlineData("1")]
    public async Task UnknownScope_IsBadRequest_WithCleanMessage(string scope)
    {
        var response = await Factory.AuthorizedClient("lawyer1")
            .GetAsync($"/api/alerts/unread-count?scope={scope}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var message = json.RootElement.GetProperty("message").GetString();
        Assert.Equal("قيمة scope غير صالحة — استخدم user", message);
    }
}
