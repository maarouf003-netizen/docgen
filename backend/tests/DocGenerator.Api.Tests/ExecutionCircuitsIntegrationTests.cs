using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;
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

    // مستثنى من R1-1 عمدًا: `HeadBranchIdAsync` يُثبّت `Governorate` كأثر جانبي
    // تعتمد عليه `NewSectionHeadAsync` (تستدعيه للتحضير لا للقيمة)، و`BranchIdOfHeadAsync`
    // قراءة صرفة لفرع `head1` — توحيدهما كان سيُخفي هذا التقسيم المقصود.
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
            borrowerName = UniqueName("مقترض"),
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
        var sourceName = UniqueName("دائرة مصدر");
        var targetName = UniqueName("دائرة هدف");
        var source = await CreateCircuitAsync(head, sourceName);
        var target = await CreateCircuitAsync(head, targetName);
        var number = $"8{Guid.NewGuid():N}"[..7];
        var docId = await CreateDocumentAsync(lawyer, source, number);

        var (lawyer2Id, lawyer2Client) = await _factory.NewLawyerClientAsync(branchId);

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
        var source = await CreateCircuitAsync(head, UniqueName("دائرة مصدر"));
        var target = await CreateCircuitAsync(head, UniqueName("دائرة هدف"));
        var number = $"7{Guid.NewGuid():N}"[..7];
        var docId = await CreateDocumentAsync(lawyer, source, number);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var lawyer1 = await db.Users.FirstAsync(u => u.Username == "lawyer1");
        var (otherId, _) = await _factory.NewLawyerClientAsync(lawyer1.BranchId!.Value);

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
        var targetName = UniqueName("دائرة هدف");
        var source = await CreateCircuitAsync(head, UniqueName("دائرة مصدر"));
        var target = await CreateCircuitAsync(head, targetName);
        var number = $"6{Guid.NewGuid():N}"[..7];
        var docId = await CreateDocumentAsync(lawyer1, source, number);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var l1 = await db.Users.SingleAsync(u => u.Username == "lawyer1");
        var (otherId, otherClient) = await _factory.NewLawyerClientAsync(l1.BranchId!.Value);

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
        var source = await CreateCircuitAsync(head, UniqueName("دائرة مصدر"));
        var target = await CreateCircuitAsync(head, UniqueName("دائرة هدف"));
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

    [Fact]
    public async Task Transfer_ByManager_MovesOwnership()
    {
        await HeadBranchIdAsync();
        var head = _factory.AuthorizedClient("head1");
        var circuitId = await CreateCircuitAsync(head, UniqueName("دائرة للنقل"));
        var manager = _factory.AuthorizedClient("manager");
        var section = await manager.PostAsJsonAsync("/api/sections", new
        {
            branchId = await BranchIdOfHeadAsync(),
            name = UniqueName("شعبة النقل"),
        });
        Assert.Equal(HttpStatusCode.Created, section.StatusCode);
        using var sectionDoc = await section.Content.ReadFromJsonAsync<JsonDocument>();
        var sectionId = sectionDoc!.RootElement.GetProperty("id").GetInt32();

        var transfer = await manager.PostAsJsonAsync($"/api/execution-circuits/{circuitId}/transfer",
            new { targetSectionId = sectionId });
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.Equal(sectionId, (await db.ExecutionCircuits.FindAsync(circuitId))!.SectionId);
    }

    [Fact]
    public async Task Transfer_ByHead_Forbidden()
    {
        await HeadBranchIdAsync();
        var head = _factory.AuthorizedClient("head1");
        var circuitId = await CreateCircuitAsync(head, UniqueName("دائرة للنقل"));

        var transfer = await head.PostAsJsonAsync($"/api/execution-circuits/{circuitId}/transfer",
            new { targetSectionId = (int?)null });

        Assert.Equal(HttpStatusCode.Forbidden, transfer.StatusCode);
    }

    [Fact]
    public async Task Transfer_MissingCircuit_BadRequest()
    {
        var manager = _factory.AuthorizedClient("manager");
        var transfer = await manager.PostAsJsonAsync("/api/execution-circuits/999999/transfer",
            new { targetSectionId = (int?)null });

        Assert.Equal(HttpStatusCode.BadRequest, transfer.StatusCode);
    }

    private async Task<int> BranchIdOfHeadAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return (await db.Users.SingleAsync(u => u.Username == "head1")).BranchId!.Value;
    }

    private async Task<(int SectionId, HttpClient SubHead)> NewSectionHeadAsync(string sectionName)
    {
        await HeadBranchIdAsync();
        var manager = _factory.AuthorizedClient("manager");
        var branchId = await BranchIdOfHeadAsync();
        var section = await manager.PostAsJsonAsync("/api/sections", new { branchId, name = sectionName });
        Assert.Equal(HttpStatusCode.Created, section.StatusCode);
        using var sectionDoc = await section.Content.ReadFromJsonAsync<JsonDocument>();
        var sectionId = sectionDoc!.RootElement.GetProperty("id").GetInt32();

        var admin = _factory.AuthorizedClient("admin");
        var username = UniqueName("subh");
        var created = await admin.PostAsJsonAsync("/api/users", new
        {
            username,
            fullName = username,
            role = "subhead",
            branchId,
            password = "TestPass1234",
            sectionId,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var token = (await _factory.LoginAsync(username, "TestPass1234"))!.Token!;
        return (sectionId, _factory.ClientWithToken(token));
    }

    [Fact]
    public async Task Mine_SubHeadSeesOwnSectionOnly_AndCreatesAutoTagged()
    {
        await HeadBranchIdAsync();
        var head = _factory.AuthorizedClient("head1");
        var divisionCircuit = await CreateCircuitAsync(head, UniqueName("قسم-عزل"));
        var (_, sub) = await NewSectionHeadAsync(UniqueName("شعبة-عزل"));

        // إنشاء رئيس الشعبة يلتحق بشعبته تلقائيًا (§8.4).
        var created = await sub.PostAsJsonAsync("/api/execution-circuits", new { name = UniqueName("شعبة-دائرة") });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdDoc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var subCircuit = createdDoc!.RootElement.GetProperty("id").GetInt32();

        var mine = await sub.GetAsync("/api/execution-circuits/mine");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        using var mineDoc = await mine.Content.ReadFromJsonAsync<JsonDocument>();
        var ids = mineDoc!.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();
        Assert.DoesNotContain(divisionCircuit, ids);
        Assert.Contains(subCircuit, ids);

        // رئيس القسم لا يرى دائرة الشعبة (عزل كامل — قرار §1.11).
        var headMine = await head.GetAsync("/api/execution-circuits/mine");
        using var headDoc = await headMine.Content.ReadFromJsonAsync<JsonDocument>();
        var headIds = headDoc!.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(divisionCircuit, headIds);
        Assert.DoesNotContain(subCircuit, headIds);
    }

    [Fact]
    public async Task Rename_ForeignScope_BadRequest()
    {
        await HeadBranchIdAsync();
        var (_, sub) = await NewSectionHeadAsync(UniqueName("شعبة-دخيلة"));
        var created = await sub.PostAsJsonAsync("/api/execution-circuits", new { name = UniqueName("دائرة-دخيلة") });
        using var createdDoc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var circuitId = createdDoc!.RootElement.GetProperty("id").GetInt32();

        var head = _factory.AuthorizedClient("head1");
        var rename = await head.PutAsJsonAsync($"/api/execution-circuits/{circuitId}/rename", new { name = "اسم دخيل" });
        Assert.Equal(HttpStatusCode.BadRequest, rename.StatusCode);
        Assert.Contains("ليست ضمن نطاقك", await rename.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rename_OwnScope_SubHead_Ok()
    {
        // المسار الموجب عبر السياسة والنطاق: رئيس الشعبة يسمّي دائرته (200).
        await HeadBranchIdAsync();
        var (_, sub) = await NewSectionHeadAsync(UniqueName("شعبة-تسمية"));
        var created = await sub.PostAsJsonAsync("/api/execution-circuits", new { name = UniqueName("دائرة-تسمية") });
        using var createdDoc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var circuitId = createdDoc!.RootElement.GetProperty("id").GetInt32();

        var rename = await sub.PutAsJsonAsync($"/api/execution-circuits/{circuitId}/rename", new { name = UniqueName("دائرة-مسماة") });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
    }

    [Fact]
    public async Task Delete_ForeignScope_BadRequest()
    {
        await HeadBranchIdAsync();
        var (_, sub) = await NewSectionHeadAsync(UniqueName("شعبة-حذف"));
        var created = await sub.PostAsJsonAsync("/api/execution-circuits", new { name = UniqueName("دائرة-حذف") });
        using var createdDoc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var circuitId = createdDoc!.RootElement.GetProperty("id").GetInt32();

        // دائرة فارغة بلا ملفات: الرفض سببه النطاق لا الإشغال.
        var head = _factory.AuthorizedClient("head1");
        var delete = await head.DeleteAsync($"/api/execution-circuits/{circuitId}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.Contains("ليست ضمن نطاقك", await delete.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Stats_HeadSeesDivisionOnly()
    {
        await HeadBranchIdAsync();
        var head = _factory.AuthorizedClient("head1");
        var divisionCircuit = await CreateCircuitAsync(head, UniqueName("قسم-إحصاء"));
        var (_, sub) = await NewSectionHeadAsync(UniqueName("شعبة-إحصاء"));
        var created = await sub.PostAsJsonAsync("/api/execution-circuits", new { name = UniqueName("شعبة-إحصاء-دائرة") });
        using var createdDoc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var subCircuit = createdDoc!.RootElement.GetProperty("id").GetInt32();

        var stats = await head.GetAsync("/api/execution-circuits/stats");
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
        using var statsDoc = await stats.Content.ReadFromJsonAsync<JsonDocument>();
        var ids = statsDoc!.RootElement.EnumerateArray().Select(e => e.GetProperty("circuitId").GetInt32()).ToList();
        Assert.Contains(divisionCircuit, ids);
        Assert.DoesNotContain(subCircuit, ids);
    }

    [Fact]
    public async Task ForLawyer_LawyerSeesAll_HeadSeesOwn()
    {
        await HeadBranchIdAsync();
        var head = _factory.AuthorizedClient("head1");
        var divisionCircuit = await CreateCircuitAsync(head, UniqueName("قسم-قائمة"));
        var (_, sub) = await NewSectionHeadAsync(UniqueName("شعبة-قائمة"));
        var created = await sub.PostAsJsonAsync("/api/execution-circuits", new { name = UniqueName("شعبة-قائمة-دائرة") });
        using var createdDoc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var subCircuit = createdDoc!.RootElement.GetProperty("id").GetInt32();

        var lawyer = _factory.AuthorizedClient("lawyer1");
        var all = await lawyer.GetAsync("/api/execution-circuits/for-lawyer");
        using var allDoc = await all.Content.ReadFromJsonAsync<JsonDocument>();
        var allIds = allDoc!.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(divisionCircuit, allIds);
        Assert.Contains(subCircuit, allIds);

        var own = await head.GetAsync("/api/execution-circuits/for-lawyer");
        using var ownDoc = await own.Content.ReadFromJsonAsync<JsonDocument>();
        var ownIds = ownDoc!.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(divisionCircuit, ownIds);
        Assert.DoesNotContain(subCircuit, ownIds);
    }

    [Fact]
    public async Task GetFiles_ReturnsMirrorHiddenBySearch()
    {
        // العطل A (معالج الإفراغ كان يمرّ بفلتر البحث العام): ملف بحالة «منفذ إنابة»
        // يُخفى من /api/documents?court=.. — ولا بد أن يظهر في /files أيًا كانت حالته.
        await HeadBranchIdAsync();
        var head = _factory.AuthorizedClient("head1");
        var lawyer = _factory.AuthorizedClient("lawyer1");
        var sourceName = UniqueName("دائرة فعفراغة");
        var source = await CreateCircuitAsync(head, sourceName);
        var docId = await CreateDocumentAsync(lawyer, source, $"6{Guid.NewGuid():N}"[..7]);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var entity = await db.Documents.SingleAsync(d => d.Id == docId);
            entity.ExecStatus = ExecutionStatusCatalog.DelegationExecuted; // «منفذ إنابة»
            entity.IsDraft = false;
            await db.SaveChangesAsync();
        }

        var files = await head.GetAsync($"/api/execution-circuits/{source}/files");
        Assert.Equal(HttpStatusCode.OK, files.StatusCode);
        using (var filesDoc = await files.Content.ReadFromJsonAsync<JsonDocument>())
        {
            var row = filesDoc!.RootElement.EnumerateArray()
                .Single(e => e.GetProperty("id").GetInt32() == docId);
            Assert.Contains("صلح", row.GetProperty("fileType").GetString());
        }

        var search = await head.GetAsync("/api/documents?court=" + Uri.EscapeDataString(sourceName) + "&perPage=100");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        using (var searchDoc = await search.Content.ReadFromJsonAsync<JsonDocument>())
        {
            // يثبت أن المسار القديم كان يخفيه فعلًا: مصفوفة items بلا معه.
            var searchIds = searchDoc!.RootElement.GetProperty("items").EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32()).ToList();
            Assert.DoesNotContain(docId, searchIds);
        }

        // نطاق دخيل: رئيس الشعبة بشعبة أخرى لا يملك الدائرة → ليس ضمن نطاقك.
        var (_, sub) = await NewSectionHeadAsync(UniqueName("شعبة-مخل-بملفات"));
        var intruder = await sub.GetAsync($"/api/execution-circuits/{source}/files");
        Assert.Equal(HttpStatusCode.BadRequest, intruder.StatusCode);
        Assert.Contains("ليست ضمن نطاقك", await intruder.Content.ReadAsStringAsync());
    }
}
