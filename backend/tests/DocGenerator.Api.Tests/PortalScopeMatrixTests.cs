using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P2-02c — مصفوفة عزل نطاق المندوبين في البوابة (مورد البوابة).
/// <para>
/// العقد المقيس: المندوب يرى فقط الملفات المرتبطة بمدخل جهته (`User.PortalEntryId`) عبر
/// طرفَي المستند (طالب/منفذ عليه) وبشرط الجهة نهائية وغير قيد المراجعة؛ الملف خارج
/// النطاق يُعاد <c>404</c> لا <c>403</c> (البوابة تخفي الوجود خلافًا لـAPI الداخلي)،
/// والقوائم تُرشّح بصمت، والمندوب بلا ربط يرى قائمة فارغة، والمحامي/الإداري يُرفض بـ<code>403</code>،
/// والزائر بـ<code>401</code>.
/// </para>
/// </summary>
[Collection(PostgresApiTestCollection.Name)]
public class PortalScopeMatrixTests : IAsyncLifetime
{
    private const string EntryABranch = "مديرية ألف";
    private const string EntryBBranch = "مديرية باء";

    private readonly PostgresApiFactory _factory;

    public PortalScopeMatrixTests(PostgresApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [PostgresFact]
    public async Task PortalScope_IsolationMatrix_MatchesPolicy()
    {
        var factory = _factory;
        var lawyerToken = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;

        int entryAId;
        int entryBId;
        string delegateA;
        string delegateB;
        string delegateWithoutEntry;
        int documentA;
        int documentB;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var creator = db.Users.Single(u => u.Username == "lawyer1");

            var groupA = CreateGroup("جهة ألف", "دمشق", EntryABranch, creator.Id);
            var groupB = CreateGroup("جهة باء", "حلب", EntryBBranch, creator.Id);
            db.PublicEntityGroups.AddRange(groupA, groupB);
            await db.SaveChangesAsync();

            entryAId = groupA.Entries.First().Id;
            entryBId = groupB.Entries.First().Id;

            var a = await factory.CreateUserAsync(UniqueName("del_a"), UserRole.EntityManager);
            var b = await factory.CreateUserAsync(UniqueName("del_b"), UserRole.EntityManager);
            var none = await factory.CreateUserAsync(UniqueName("del_none"), UserRole.EntityManager);
            delegateA = a.Username;
            delegateB = b.Username;
            delegateWithoutEntry = none.Username;

            db.Users.Single(u => u.Id == a.Id).PortalEntryId = entryAId;
            db.Users.Single(u => u.Id == b.Id).PortalEntryId = entryBId;
            await db.SaveChangesAsync();

            documentA = await factory.CreateDocumentAsync(lawyerToken, borrowerName: UniqueName("doc_a"));
            documentB = await factory.CreateDocumentAsync(lawyerToken, borrowerName: UniqueName("doc_b"));

            db.ApplicantPublicEntities.AddRange(
                new ApplicantPublicEntity
                {
                    DocumentId = documentA,
                    RegistryId = entryAId,
                    Name = EntryABranch,
                    Branch = EntryABranch,
                    Governorate = "دمشق",
                },
                new ApplicantPublicEntity
                {
                    DocumentId = documentB,
                    RegistryId = entryBId,
                    Name = EntryBBranch,
                    Branch = EntryBBranch,
                    Governorate = "حلب",
                });
            await db.SaveChangesAsync();
        }

        var clientA = factory.AuthorizedClient(delegateA);
        var clientB = factory.AuthorizedClient(delegateB);
        var failures = new List<string>();

        // 1) النطاق المعلن: مدخل واحد فقط لجهة المندوب.
        var myScope = await clientA.GetAsync("/api/portal/my-scope");
        if (myScope.StatusCode != HttpStatusCode.OK)
        {
            failures.Add($"A my-scope: {myScope.StatusCode} والمتوقع OK");
        }
        else
        {
            using var scopeJson = JsonDocument.Parse(await myScope.Content.ReadAsStringAsync());
            var entries = scopeJson.RootElement.GetProperty("entries");
            if (entries.GetArrayLength() != 1 || entries[0].GetProperty("id").GetInt32() != entryAId)
            {
                failures.Add("A my-scope: النطاق ليس مدخل جهة A وحده");
            }

            var branchName = entries[0].GetProperty("branchName").GetString();
            if (branchName != EntryABranch)
            {
                failures.Add($"A my-scope: branchName={branchName} والمتوقع {EntryABranch}");
            }
        }

        // 2) القوائم تُرشّح بالنطاق: عنصر واحد لكل مندوب وبلا تسرّب.
        await ExpectSingleFileAsync(clientA, documentA, "A", failures);
        await ExpectSingleFileAsync(clientB, documentB, "B", failures);

        // 3) التفاصيل: ملف الغير = 404 (البوابة تخفي الوجود)، والملف المملوك = 200.
        await ExpectStatusAsync(clientA, $"/api/portal/files/{documentA}", HttpStatusCode.OK, "A detail own", failures);
        await ExpectStatusAsync(clientA, $"/api/portal/files/{documentB}", HttpStatusCode.NotFound, "A detail foreign", failures);
        await ExpectStatusAsync(clientB, $"/api/portal/files/{documentB}", HttpStatusCode.OK, "B detail own", failures);
        await ExpectStatusAsync(clientB, $"/api/portal/files/{documentA}", HttpStatusCode.NotFound, "B detail foreign", failures);

        // 4) entryId خارج النطاق يُرفض صراحةً (عقد قائم: 403 برسالة).
        await ExpectStatusAsync(
            clientA, $"/api/portal/files?entryId={entryBId}", HttpStatusCode.Forbidden, "A foreign entryId", failures);

        // 5) مندوب بلا ربط جهة: قائمة فارغة لا خطأ.
        var noEntry = factory.AuthorizedClient(delegateWithoutEntry);
        var noEntryList = await noEntry.GetAsync("/api/portal/files?perPage=20");
        if (noEntryList.StatusCode != HttpStatusCode.OK)
        {
            failures.Add($"no-entry files: {noEntryList.StatusCode} والمتوقع OK");
        }
        else
        {
            using var json = JsonDocument.Parse(await noEntryList.Content.ReadAsStringAsync());
            if (json.RootElement.GetProperty("totalCount").GetInt32() != 0)
            {
                failures.Add("no-entry files: totalCount != 0");
            }
        }

        // 6) الأدوار غير البوابة والزوار مرفوضون.
        await ExpectStatusAsync(
            factory.ClientWithToken(lawyerToken), "/api/portal/files", HttpStatusCode.Forbidden, "lawyer", failures);
        await ExpectStatusAsync(
            factory.CreateClient(), "/api/portal/files", HttpStatusCode.Unauthorized, "anonymous", failures);

        Assert.True(
            failures.Count == 0,
            "انحرافات مصفوفة نطاق البوابة:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static PublicEntityGroup CreateGroup(
        string canonicalName, string governorate, string branchName, int createdById)
    {
        var group = new PublicEntityGroup
        {
            CanonicalName = canonicalName,
            EntityType = PublicEntityTypeCatalog.Ministry,
        };
        group.Entries.Add(new PublicEntity
        {
            Governorate = governorate,
            BranchName = branchName,
            Status = EntityStatusCatalog.Final,
            CreatedById = createdById,
        });
        return group;
    }

    private static async Task ExpectSingleFileAsync(
        HttpClient client, int expectedDocumentId, string actor, List<string> failures)
    {
        var response = await client.GetAsync("/api/portal/files?perPage=20");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            failures.Add($"{actor} files: {response.StatusCode} والمتوقع OK");
            return;
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (body.GetProperty("totalCount").GetInt32() != 1)
        {
            failures.Add($"{actor} files: totalCount != 1");
            return;
        }

        var onlyId = body.GetProperty("items")[0].GetProperty("id").GetInt32();
        if (onlyId != expectedDocumentId)
        {
            failures.Add($"{actor} files: id={onlyId} والمتوقع {expectedDocumentId}");
        }
    }

    private static async Task ExpectStatusAsync(
        HttpClient client, string path, HttpStatusCode expected, string actor, List<string> failures)
    {
        var response = await client.GetAsync(path);
        if (response.StatusCode != expected)
        {
            failures.Add($"{actor}: {path} → {response.StatusCode} والمتوقع {expected}");
        }
    }
}
