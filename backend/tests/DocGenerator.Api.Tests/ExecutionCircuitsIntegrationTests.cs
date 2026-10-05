using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// مجموعة عزل خاصة: سجل الدوائر يغيّر حالة المطابقة العامة (القفل من السجل) —
/// واختبارات المجموعة المشتركة تفترض سجلًا فارغًا، لذا تُقلع هذه الفئة قاعدتها الخاصة.
/// </summary>
[CollectionDefinition("execution-circuits-integration")]
public sealed class ExecutionCircuitsCollection : ICollectionFixture<ApiFactory>
{
}

/// <summary>
/// اختبارات تكامل سجل دوائر التنفيذ (BQ-004) عبر HTTP الحقيقي:
/// التدفق الكامل (إدخال→اختيار→إفراغ→إعادة قيد→حذف)، إعادة تشغيل الإحالة بنفس
/// المفتاح (H3)، انتقال التنبيه مع الملكية (H2)، وحذف دائرة ذات إنابة مكتملة (H1).
/// عزل الأسماء/الأرقام بـ GUID فريد احتياطًا رغم العزل.
/// </summary>
[Collection("execution-circuits-integration")]
public class ExecutionCircuitsIntegrationTests
{
    private readonly ApiFactory _factory;

    public ExecutionCircuitsIntegrationTests(ApiFactory factory) => _factory = factory;

    private static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..24];

    /// <summary>محامٍ جديد في فرع الرئيس مع عميل مسجَّل دخوله فعليًا (Cookie + CSRF).</summary>
    private async Task<(int Id, HttpClient Client)> NewLawyerClientAsync(int branchId)
    {
        var username = Unique("law2");
        var user = await _factory.CreateUserAsync(username, UserRole.Lawyer, branchId);
        var token = (await _factory.LoginAsync(username, "123456"))!.Token!;
        return (user.Id, _factory.WithToken(token));
    }

    private async Task<int> HeadBranchIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var head = await db.Users.SingleAsync(u => u.Username == "head1");
        if (string.IsNullOrWhiteSpace((await db.Branches.FindAsync(head.BranchId))?.Governorate))
        {
            var branch = await db.Branches.FindAsync(head.BranchId);
            branch!.Governorate = "دمشق";
            await db.SaveChangesAsync();
        }
        return head.BranchId!.Value;
    }

    private async Task<int> CreateCircuitAsync(HttpClient head, string name)
    {
        var response = await head.PostAsJsonAsync("/api/execution-circuits", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("id").GetInt32();
    }

    private async Task<int> CreateDocumentAsync(HttpClient lawyer, int circuitId, string number)
    {
        var response = await lawyer.PostAsJsonAsync("/api/documents", new
        {
            documentType = "بيان دعوى",
            borrowerName = Unique("مقترض"),
            applicant = "المدعي",
            contractType = "تعهد",
            amountNumeric = 500,
            fileNumber = number,
            fileType = "صلح",
            fileYear = "2026",
            fileRegistrationDate = "1/8/2026",
            executionCircuitId = circuitId,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<HttpResponseMessage> PostWithKeyAsync(
        HttpClient client, string url, object body, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        if (key is not null)
            request.Headers.Add("X-Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task FullFlow_CreateReferCompleteDelete_Succeeds()
    {
        var branchId = await HeadBranchIdAsync();
        var head = _factory.AuthorizedClient("head1");
        var lawyer = _factory.AuthorizedClient("lawyer1");
        var sourceName = Unique("دائرة مصدر");
        var targetName = Unique("دائرة هدف");
        var source = await CreateCircuitAsync(head, sourceName);
        var target = await CreateCircuitAsync(head, targetName);
        var number = $"8{Guid.NewGuid():N}"[..7];
        var docId = await CreateDocumentAsync(lawyer, source, number);

        var (lawyer2Id, lawyer2Client) = await NewLawyerClientAsync(branchId);

        var refer = await head.PostAsJsonAsync($"/api/execution-circuits/{source}/refer-files", new
        {
            fileIds = new[] { docId },
            targetLawyerId = lawyer2Id,
            targetCircuitId = target,
        });
        Assert.Equal(HttpStatusCode.OK, refer.StatusCode);

        var pending = await lawyer2Client.GetAsync("/api/documents/my-pending-registrations");
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        var pendingText = await pending.Content.ReadAsStringAsync();
        Assert.Contains(docId.ToString(), pendingText);

        var complete = await lawyer2Client.PostAsJsonAsync("/api/documents/complete-registrations", new
        {
            entries = new[] { new { documentId = docId, fileNumber = $"9{Guid.NewGuid():N}"[..7], fileType = "صلح", fileYear = "2026" } },
        });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        var del = await head.DeleteAsync($"/api/execution-circuits/{source}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var stats = await head.GetAsync("/api/execution-circuits/stats");
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
        Assert.Contains(targetName, await stats.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Refer_IdempotentReplay_ReturnsStoredBody()
    {
        await HeadBranchIdAsync();
        var head = _factory.AuthorizedClient("head1");
        var lawyer = _factory.AuthorizedClient("lawyer1");
        var source = await CreateCircuitAsync(head, Unique("دائرة مصدر"));
        var target = await CreateCircuitAsync(head, Unique("دائرة هدف"));
        var number = $"7{Guid.NewGuid():N}"[..7];
        var docId = await CreateDocumentAsync(lawyer, source, number);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var lawyer1 = await db.Users.FirstAsync(u => u.Username == "lawyer1");
        var (otherId, _) = await NewLawyerClientAsync(lawyer1.BranchId!.Value);

        var key = Guid.NewGuid().ToString();
        var body = new { fileIds = new[] { docId }, targetLawyerId = otherId, targetCircuitId = target };
        var first = await PostWithKeyAsync(head, $"/api/execution-circuits/{source}/refer-files", body, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var text1 = await first.Content.ReadAsStringAsync();

        var second = await PostWithKeyAsync(head, $"/api/execution-circuits/{source}/refer-files", body, key);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(text1, await second.Content.ReadAsStringAsync());

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.Equal(1, await db2.DocumentOccurrences
            .CountAsync(o => o.DocumentId == docId && o.OccurrenceType == OccurrenceTypeCatalog.CircuitReferred));
    }

    [Fact]
    public async Task Transfer_PendingAlertFollowsNewOwner()
    {
        await HeadBranchIdAsync();
        var head = _factory.AuthorizedClient("head1");
        var lawyer1 = _factory.AuthorizedClient("lawyer1");
        var targetName = Unique("دائرة هدف");
        var source = await CreateCircuitAsync(head, Unique("دائرة مصدر"));
        var target = await CreateCircuitAsync(head, targetName);
        var number = $"6{Guid.NewGuid():N}"[..7];
        var docId = await CreateDocumentAsync(lawyer1, source, number);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var l1 = await db.Users.SingleAsync(u => u.Username == "lawyer1");
        var (otherId, otherClient) = await NewLawyerClientAsync(l1.BranchId!.Value);

        // إحالة لمالكه نفسه في الدائرة الجديدة → معلق عند المحامي الأول بتنبيه.
        var refer = await head.PostAsJsonAsync($"/api/execution-circuits/{source}/refer-files", new
        {
            fileIds = new[] { docId },
            targetLawyerId = l1.Id,
            targetCircuitId = target,
        });
        Assert.Equal(HttpStatusCode.OK, refer.StatusCode);

        // نقل فردي للمالك الجديد → التنبيه يتبعه ويُصفَّى عن القديم.
        var transfer = await head.PostAsJsonAsync($"/api/documents/{docId}/transfer",
            new { targetLawyerId = otherId });
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        var newAlerts = await otherClient.GetAsync("/api/alerts");
        Assert.Contains(targetName, await newAlerts.Content.ReadAsStringAsync());

        var oldAlerts = await lawyer1.GetAsync("/api/alerts");
        Assert.DoesNotContain(targetName, await oldAlerts.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delete_WithCompletedDelegation_DetachesAndSucceeds()
    {
        await HeadBranchIdAsync();
        var head = _factory.AuthorizedClient("head1");
        var lawyer = _factory.AuthorizedClient("lawyer1");
        var source = await CreateCircuitAsync(head, Unique("دائرة مصدر"));
        var target = await CreateCircuitAsync(head, Unique("دائرة هدف"));
        var number = $"5{Guid.NewGuid():N}"[..7];
        var docId = await CreateDocumentAsync(lawyer, source, number);

        string courtName;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            courtName = (await db.ExecutionCircuits.FindAsync(target))!.Name;
            var headUser = await db.Users.SingleAsync(u => u.Username == "head1");
            db.DocumentDelegations.Add(new DocumentDelegation
            {
                SourceDocumentId = docId,
                DelegatedCourt = courtName,
                DelegatedCircuitId = target,
                IsExternal = false,
                Status = DelegationStatusCatalog.Executed,
                CreatedById = headUser.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // قبل الإصلاح: 500 (انتهاك FK). بعد الإصلاح: 204 مع تجميد الاسم.
        var del = await head.DeleteAsync($"/api/execution-circuits/{target}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var delegation = await db2.DocumentDelegations
            .IgnoreQueryFilters()
            .FirstAsync(d => d.SourceDocumentId == docId);
        Assert.Null(delegation.DelegatedCircuitId);
        Assert.Equal(courtName, delegation.DelegatedCourt);
    }
}
