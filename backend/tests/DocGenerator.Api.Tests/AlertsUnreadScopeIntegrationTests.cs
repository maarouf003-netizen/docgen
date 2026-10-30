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
/// نطاقا عدّاد غير المقروء (`D6`/`D7`، الخطة §5/المرحلة 4) على **قاعدة معزولة لكل اختبار**،
/// فالأعداد المطلقة حاسمة: <c>user</c> = صفوف الفاعل نفسه (الرئيس مقيّد بفرعه)،
/// و<c>received</c> = كل غير المقروء على التنبيهات التي استلمها الرئيس في فرعه.
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
    public async Task Scopes_UserIsBranchBound_ReceivedCoversReceivedAlerts()
    {
        var damId = await Factory.GetBranchIdAsync("DAM");
        var aleppoId = await Factory.GetBranchIdAsync("ALP");
        var headId = await Factory.UserIdAsync("head1");
        var lawyerId = await Factory.UserIdAsync("lawyer1");
        var extra = await Factory.CreateUserAsync(UniqueName("lawyer_scope"), UserRole.Lawyer, damId);

        // A في فرعه ومستلم فيه مع محاميين ⇒ 3 صفوف غير مقروءة.
        var collectiveId = await SeedAlertAsync(
            damId, headId, "جماعي", archived: false, headId, lawyerId, extra.Id);
        // B في فرعه لكنه ليس مستلمًا ⇒ لا يُحتسب في أي نطاق.
        await SeedAlertAsync(damId, headId, "لمحامٍ فقط", archived: false, lawyerId);
        // C استلمه في فرع آخر ⇒ خارج نطاق الرئيس.
        await SeedAlertAsync(aleppoId, headId, "فرع آخر", archived: false, headId);
        // D مؤرشف في فرعه ومستلم فيه ⇒ مستبعد من النطاقين.
        await SeedAlertAsync(damId, headId, "مؤرشف", archived: true, headId, lawyerId);

        var head = Factory.AuthorizedClient("head1");

        // user: صفّه وحده في A ⇒ 1. received: كل غير المقروء على تنبيهاته في فرعه ⇒ 3.
        Assert.Equal(1, await CountAsync(head, "user"));
        Assert.Equal(3, await CountAsync(head, "received"));

        // الافتراضي (بلا scope) هو user — عقد ما قبل المرحلة 4 محفوظ.
        Assert.Equal(1, await CountAsync(head, null));

        // D7: العدّاد الشخصي = عدد صفوف قائمته (لا عدّاد أكبر من القائمة).
        var listResponse = await head.GetAsync("/api/alerts?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResult<HeadAlertSummaryDto>>();
        Assert.NotNull(page);
        Assert.Equal(page!.Items.Count, await CountAsync(head, "user"));

        // D6: `received` = مجموع `unreadCount` لتنبيهات قائمته (السلوك القديم حرفيًا).
        Assert.Equal(page.Items.Sum(a => a.UnreadCount ?? 0), await CountAsync(head, "received"));

        // قراءة صف محامٍ من A تخفض «المستلَم» وحده، ولا تمس صف الرئيس.
        var lawyer = Factory.AuthorizedClient("lawyer1");
        Assert.Equal(HttpStatusCode.NoContent,
            (await lawyer.PatchAsJsonAsync($"/api/alerts/{collectiveId}/read", new { })).StatusCode);
        Assert.Equal(2, await CountAsync(head, "received"));
        Assert.Equal(1, await CountAsync(head, "user"));
    }

    [Fact]
    public async Task ReceivedScope_IsRejectedForLawyerAndOversight()
    {
        var lawyer = Factory.AuthorizedClient("lawyer1");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await lawyer.GetAsync("/api/alerts/unread-count?scope=received")).StatusCode);
        // المحامي يملك النطاق الشخصي (وإن كان صفرًا هنا).
        Assert.Equal(HttpStatusCode.OK,
            (await lawyer.GetAsync("/api/alerts/unread-count?scope=user")).StatusCode);

        foreach (var username in new[] { "manager", "admin" })
        {
            foreach (var scope in new[] { "user", "received" })
            {
                Assert.Equal(HttpStatusCode.Forbidden,
                    (await Factory.AuthorizedClient(username)
                        .GetAsync($"/api/alerts/unread-count?scope={scope}")).StatusCode);
            }
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
        Assert.Equal("قيمة scope غير صالحة — استخدم user أو received", message);
    }
}
