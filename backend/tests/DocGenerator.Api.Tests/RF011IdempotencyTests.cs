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
/// اختبارات RF-011 (INT-007): مفتاح عدم التكرار — الإرسال المزدوج بنفس المفتاح
/// أثر واحد ونتيجة مخزنة. تفشل قبل الإصلاح (أثر مكرر/نتائج مختلفة) وتخضر بعده.
/// عزل الاسماء: مستخدمون بأسماء `GUID` فريدة (عرف `ApiTestCollection`) فلا تتداخل
/// صفوف التدقيق مع فلاتر الفئات الأخرى (فاعل مشترك = تلوث Count).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RF011IdempotencyTests
{
    private readonly ApiFactory _factory;

    public RF011IdempotencyTests(ApiFactory factory) => _factory = factory;

    private static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..20];

    /// <summary>محامٍ جديد بكلمة `123456` وتوكنه — فرع دمشق ما لم يُحدَّد غيره.</summary>
    private async Task<(int Id, string Token)> NewLawyerAsync(int? branchId = null)
    {
        branchId ??= await BranchIdAsync("DAM");
        var username = Unique("idemlaw");
        await _factory.CreateUserAsync(username, UserRole.Lawyer, branchId, "123456");
        var token = (await _factory.LoginAsync(username, "123456"))!.Token!;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var id = (await db.Users.SingleAsync(u => u.Username == username)).Id;
        return (id, token);
    }

    private async Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return (await db.Branches.SingleAsync(b => b.Code == code)).Id;
    }

    /// <summary>
    /// فرع معزول برئيسه الوحيد (قرار §2.26 + عزل صفوف التدقيق عن `head1` المشترك —
    /// عرف الملف: فاعل مشترك = تلوّث `Count`).
    /// </summary>
    private async Task<int> CreateBranchAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var branch = new Branch { Name = "فرع عدم التكرار", Code = $"IB_{Guid.NewGuid():N}"[..12].ToUpperInvariant() };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        return branch.Id;
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

    private static object CreatePayload(string borrower) => new
    {
        documentType = "بيان دعوى",
        borrowerName = borrower,
        applicant = "المدعي",
        court = "دمشق",
        contractType = "تعهد",
        amountNumeric = 100,
    };

    [Fact]
    public async Task DoubleCreate_SameKey_SingleDocument()
    {
        // نقرة مزدوجة بنفس المفتاح: اليوم ملفّان؛ بعد RF-011 ملف واحد والثانية `200` بنفس الجسم.
        var (_, token) = await NewLawyerAsync();
        var client = _factory.WithToken(token);
        var key = Guid.NewGuid().ToString();
        var name = Unique("توأم");

        var first = await PostWithKeyAsync(client, "/api/documents", CreatePayload(name), key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var body1 = await first.Content.ReadFromJsonAsync<JsonDocument>();
        var id1 = body1!.RootElement.GetProperty("id").GetInt32();

        var second = await PostWithKeyAsync(client, "/api/documents", CreatePayload(name), key);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var text1 = await first.Content.ReadAsStringAsync();
        var text2 = await second.Content.ReadAsStringAsync();
        Assert.Equal(text1, text2);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.Equal(1, await db.Documents.CountAsync(d => d.Id == id1));
        Assert.Equal(1, await db.Documents.CountAsync(d => d.BorrowerName == name));
    }

    [Fact]
    public async Task SameKey_DifferentPayload_ExecutesFresh()
    {
        // مفتاح مستعمل لحمولة مختلفة (تصحيح-ثم-إعادة-إرسال): نية جديدة تُنفَّذ
        // طازجة — لا تُعاد نتيجة الأولى ولا يُرفَض التصحيح (يخضر قبل/بعد).
        var (_, token) = await NewLawyerAsync();
        var client = _factory.WithToken(token);
        var key = Guid.NewGuid().ToString();

        var first = await PostWithKeyAsync(client, "/api/documents", CreatePayload(Unique("أول")), key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await PostWithKeyAsync(client, "/api/documents", CreatePayload(Unique("مختلف")), key);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var text1 = await first.Content.ReadAsStringAsync();
        var text2 = await second.Content.ReadAsStringAsync();
        Assert.NotEqual(text1, text2);
    }

    [Fact]
    public async Task MissingKey_LegacyDoubleCreate()
    {
        // توصيف (يخضر قبل/بعد): بلا مفتاح يبقى السلوك القديم — ملفّان.
        var (_, token) = await NewLawyerAsync();
        var client = _factory.WithToken(token);
        var name = Unique("قديم");

        var first = await PostWithKeyAsync(client, "/api/documents", CreatePayload(name), null);
        var second = await PostWithKeyAsync(client, "/api/documents", CreatePayload(name), null);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.Equal(2, await db.Documents.CountAsync(d => d.BorrowerName == name));
    }

    [Fact]
    public async Task DoubleTransferAll_SameKey_SameCount()
    {
        // النقل الثاني اليوم يعيد `0`؛ بعد RF-011 يعيد العدد المخزن نفسه.
        // فرع معزول برئيسه (لا `head1` المشترك — عزل التدقيق).
        var branchId = await CreateBranchAsync();
        var freshHead = await _factory.CreateUserAsync(Unique("idemhead"), UserRole.Head, branchId, "123456");
        var headToken = (await _factory.LoginAsync(freshHead.Username, "123456"))!.Token!;
        var (sourceId, sourceToken) = await NewLawyerAsync(branchId);
        var (targetId, _) = await NewLawyerAsync(branchId);
        await _factory.CreateDocumentAsync(sourceToken, borrowerName: Unique("منقول"));

        var client = _factory.WithToken(headToken);
        var key = Guid.NewGuid().ToString();
        var body = new { sourceLawyerId = sourceId, targetLawyerId = targetId };

        var first = await PostWithKeyAsync(client, "/api/documents/transfer-all", body, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var json1 = await first.Content.ReadFromJsonAsync<JsonDocument>();
        var count1 = json1!.RootElement.GetProperty("transferredCount").GetInt32();
        Assert.True(count1 >= 1);

        var second = await PostWithKeyAsync(client, "/api/documents/transfer-all", body, key);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var text1 = await first.Content.ReadAsStringAsync();
        var text2 = await second.Content.ReadAsStringAsync();
        Assert.Equal(text1, text2);
    }

    [Fact]
    public async Task DoubleMerge_SameKey_SameResult()
    {
        // الدمج الثاني اليوم `400` (غير نشطة)؛ بعد RF-011 يعيد نتيجة الدمج المخزنة.
        var adminName = Unique("idemadm");
        await _factory.CreateUserAsync(adminName, UserRole.Admin, null, "123456");
        var adminToken = (await _factory.LoginAsync(adminName, "123456"))!.Token!;
        int survivor, absorbed, adminId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            adminId = (await db.Users.SingleAsync(u => u.Username == adminName)).Id;
            var g1 = new PublicEntityGroup { CanonicalName = Unique("ناجية"), EntityType = PublicEntityTypeCatalog.Ministry, IsActive = true, CreatedAt = DateTime.UtcNow };
            var g2 = new PublicEntityGroup { CanonicalName = Unique("ممتصة"), EntityType = PublicEntityTypeCatalog.Ministry, IsActive = true, CreatedAt = DateTime.UtcNow };
            db.PublicEntityGroups.AddRange(g1, g2);
            await db.SaveChangesAsync();
            db.PublicEntities.AddRange(
                new PublicEntity { GroupId = g1.Id, Governorate = "دمشق", BranchName = "الفرع الرئيسي", Status = EntityStatusCatalog.Final, CreatedById = adminId, IsActive = true, CreatedAt = DateTime.UtcNow },
                new PublicEntity { GroupId = g2.Id, Governorate = "حلب", BranchName = "فرع حلب", Status = EntityStatusCatalog.Final, CreatedById = adminId, IsActive = true, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            survivor = g1.Id;
            absorbed = g2.Id;
        }

        var client = _factory.WithToken(adminToken);
        var key = Guid.NewGuid().ToString();
        var body = new
        {
            survivorGroupId = survivor,
            absorbedGroupIds = new[] { absorbed },
            decreeKind = "قرار",
            decreeNumber = "7",
            decreeDate = "1/8/2026",
        };

        var first = await PostWithKeyAsync(client, "/api/entity-registry/merge-commit", body, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostWithKeyAsync(client, "/api/entity-registry/merge-commit", body, key);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var text1 = await first.Content.ReadAsStringAsync();
        var text2 = await second.Content.ReadAsStringAsync();
        Assert.Equal(text1, text2);
    }
}
