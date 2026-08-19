using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

public class DocumentsIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DocumentsIntegrationTests(ApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("manager")]
    [InlineData("admin")]
    [InlineData("head1")]
    public async Task CreateDocument_AsNonLawyer_Forbidden(string username)
    {
        var client = _factory.AuthorizedClient(username);
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            documentType = "بيان دعوى",
            borrowerName = "مقترض",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateDocument_AsLawyer_ReturnsCreated()
    {
        var client = _factory.AuthorizedClient("lawyer1");
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            documentType = "بيان دعوى",
            borrowerName = "مقترض اختبار",
            contractType = "تعهد",
            amountNumeric = 100,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsManagerOrHead_Forbidden_AsOwnerLawyer_NoContent()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var lawyerClient = _factory.WithToken(token);
        var createResponse = await lawyerClient.PostAsJsonAsync("/api/documents", new
        {
            documentType = "بيان دعوى",
            borrowerName = "للحذف",
            contractType = "تعهد",
            amountNumeric = 50,
        });
        var doc = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var id = doc!.RootElement.GetProperty("id").GetInt32();

        var managerClient = _factory.AuthorizedClient("manager");
        var managerForbidden = await managerClient.DeleteAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, managerForbidden.StatusCode);

        var headClient = _factory.AuthorizedClient("head1");
        var headForbidden = await headClient.DeleteAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, headForbidden.StatusCode);

        var noContent = await lawyerClient.DeleteAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.NoContent, noContent.StatusCode);

        doc.Dispose();
    }

    [Fact]
    public async Task Delete_SoftDeletesDocument_ThenRestore_Works()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);

        var delete = await client.DeleteAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // مخفي من القراءة المباشرة بعد الحذف المنطقي
        var get = await client.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        // الصف باقٍ في القاعدة موسوماً كمحذوف منطقياً (لا حذف فيزيائي)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var row = db.Documents.IgnoreQueryFilters().Single(d => d.Id == id);
            Assert.True(row.IsDeleted);
            Assert.NotNull(row.DeletedAt);
        }

        // الاستعادة تعيد ظهور المستند
        var restore = await client.PostAsync($"/api/documents/{id}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        var getAfter = await client.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.OK, getAfter.StatusCode);
    }

    [Fact]
    public async Task Restore_AsManager_Forbidden()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var lawyerClient = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);
        await lawyerClient.DeleteAsync($"/api/documents/{id}");

        var managerClient = _factory.AuthorizedClient("manager");
        var response = await managerClient.PostAsync($"/api/documents/{id}/restore", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDeleted_AsHead_ReturnsOnlyDeletedDocuments()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var lawyerClient = _factory.WithToken(token);
        var activeId = await _factory.CreateDocumentAsync(token, "محذوفة - نشطة");
        var deletedId = await _factory.CreateDocumentAsync(token, "محذوفة - ستُحذف");
        await lawyerClient.DeleteAsync($"/api/documents/{deletedId}");

        var headClient = _factory.AuthorizedClient("head1");
        var response = await headClient.GetAsync("/api/documents/deleted?perPage=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var ids = body!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(deletedId, ids);
        Assert.DoesNotContain(activeId, ids);
    }

    [Fact]
    public async Task GetDeleted_AsManager_Forbidden()
    {
        var managerClient = _factory.AuthorizedClient("manager");
        var response = await managerClient.GetAsync("/api/documents/deleted");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Restore_AsLawyerOfAnotherBranch_Forbidden()
    {
        var damascusToken = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var damascusClient = _factory.WithToken(damascusToken);
        var id = await _factory.CreateDocumentAsync(damascusToken, "مستند محامي دمشق");
        await damascusClient.DeleteAsync($"/api/documents/{id}");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var aleppo = db.Branches.Single(b => b.Code == "ALP");
            await _factory.CreateUserAsync("lawyer_aleppo", UserRole.Lawyer, aleppo.Id);
        }

        var aleppoClient = _factory.AuthorizedClient("lawyer_aleppo");
        var response = await aleppoClient.PostAsync($"/api/documents/{id}/restore", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // يبقى المستند محذوفاً (لم تُستعد)
        var restored = await damascusClient.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.NotFound, restored.StatusCode);
    }

    [Fact]
    public async Task GetDeleted_Search_ByQuery_FiltersDeleted()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var lawyerClient = _factory.WithToken(token);
        var id1 = await _factory.CreateDocumentAsync(token, "محذوفة - فحص");
        var id2 = await _factory.CreateDocumentAsync(token, "محذوفة - أخرى");
        await lawyerClient.DeleteAsync($"/api/documents/{id1}");
        await lawyerClient.DeleteAsync($"/api/documents/{id2}");

        var headClient = _factory.AuthorizedClient("head1");
        var response = await headClient.GetAsync("/api/documents/deleted?q=" + Uri.EscapeDataString("فحص") + "&perPage=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var ids = body!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(id1, ids);
        Assert.DoesNotContain(id2, ids);
    }

    [Fact]
    public async Task SetStatus_ValidStatus_ReturnsOk()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/status",
            new { status = "تريث", fields = new { tarithNumber = "5", tarithDate = "1/1/2024" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SetStatus_InvalidStatus_ReturnsBadRequest()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/status",
            new { status = "حالة مزيفة" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetStatus_NegativeCollectedAmount_ReturnsBadRequest()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/status",
            new { status = "منفذ بالتسوية", fields = new { baraetNumber = "77", baraetDate = "1/1/2024", collectedAmount = "-5" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetStatus_InvalidExecSubStatus_ReturnsBadRequest()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/status",
            new { status = "منفذ جبريا", fields = new { execSubStatus = "غير معروف" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetStatus_Deferred_MissingTarithNumberDate_ReturnsBadRequest()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/status",
            new { status = "تريث", fields = new { tarithNumber = "5" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetStatus_Settlement_MissingBaraetNumberDate_ReturnsBadRequest()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/status",
            new { status = "منفذ بالتسوية", fields = new { baraetNumber = "77" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetStatus_ForcedComplete_PersistsSubStatusWithoutBaraet()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await CreateCirculatingDocumentAsync(token);
        var estateId = await FirstEstateIdAsync(token, id);

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/status",
            new { status = "منفذ جبريا", fields = new { execSubStatus = "منفذ كاملا", collectedAmount = "1000", soldAssetIds = estateId.ToString(), forcedExecutionDate = "1/1/2024" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var docResponse = await client.GetAsync($"/api/documents/{id}");
        var doc = await docResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("منفذ كاملا", doc!.RootElement.GetProperty("execSubStatus").GetString());
        Assert.Null(doc.RootElement.GetProperty("baraetNumber").GetString());
        Assert.Equal(1000m, doc.RootElement.GetProperty("collectedAmount").GetDecimal());
        Assert.Equal("1/1/2024", doc.RootElement.GetProperty("forcedExecutionDate").GetString());
        doc.Dispose();
    }

    [Fact]
    public async Task SetStatus_ForcedExecution_PersistsSubStatusAndCollectedAmount()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await CreateCirculatingDocumentAsync(token);
        var estateId = await FirstEstateIdAsync(token, id);

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/status",
            new { status = "منفذ جبريا", fields = new { execSubStatus = "منفذ جزئيا", collectedAmount = "750", soldAssetIds = estateId.ToString(), forcedExecutionDate = "1/2/2024" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var docResponse = await client.GetAsync($"/api/documents/{id}");
        var doc = await docResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("منفذ جبريا", doc!.RootElement.GetProperty("execStatus").GetString());
        Assert.Equal("منفذ جزئيا", doc.RootElement.GetProperty("execSubStatus").GetString());
        Assert.Equal(750m, doc.RootElement.GetProperty("collectedAmount").GetDecimal());
        Assert.Equal("1/2/2024", doc.RootElement.GetProperty("forcedExecutionDate").GetString());
        doc.Dispose();
    }

    [Fact]
    public async Task SetStatus_FromDraft_ForbidsForcibleExecution_ReturnsBadRequest()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        // ملف تحت رفع (بلا رقم ملف): «منفذ جبريا» ممنوع من هذه الحالة.
        var draftResponse = await client.PostAsJsonAsync("/api/documents", new
        {
            generalEntitySide = "applicant",
            borrowerName = "مقترض",
            applicant = "المدعي",
            contractType = "تعهد",
            amountNumeric = 500,
        });
        using var draftBody = await draftResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var draftId = draftBody!.RootElement.GetProperty("id").GetInt32();

        var response = await client.PostAsJsonAsync($"/api/documents/{draftId}/status",
            new { status = "منفذ جبريا", fields = new { execSubStatus = "منفذ كاملا" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RevertStatus_WithSayerFields_ReturnsToCirculating()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);

        var deferred = await client.PostAsJsonAsync($"/api/documents/{id}/status",
            new { status = "تريث", fields = new { tarithNumber = "5", tarithDate = "1/1/2024" } });
        Assert.Equal(HttpStatusCode.OK, deferred.StatusCode);

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/revert-status",
            new { fields = new { sayerNumber = "8", sayerDate = "2/2/2024", sayerRegNumber = "9", sayerRegDate = "3/3/2024" } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await (await client.GetAsync($"/api/documents/{id}")).Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(string.Empty, doc!.RootElement.GetProperty("execStatus").GetString());
    }

    [Fact]
    public async Task SetStatus_ToStruckOff_ThenRestore_ApplicantSide()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await CreateCirculatingDocumentAsync(token);

        // متداول → مشطوب: يتطلب تاريخ الشطب، ويختفي الملف من القوائم العامة ويظهر في المشطوبة.
        var strike = await client.PostAsJsonAsync($"/api/documents/{id}/status",
            new { status = "مشطوب", fields = new { struckOffDate = "1/2/2024" } });
        Assert.Equal(HttpStatusCode.OK, strike.StatusCode);

        var after = await client.GetAsync("/api/documents?q=مقترض");
        using var afterBody = await after.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.DoesNotContain(id, afterBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));

        var struckOff = await client.GetAsync("/api/documents/struck-off?q=مقترض");
        using var struckOffBody = await struckOff.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Contains(id, struckOffBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));

        // الإعادة من صفحة المشطوبة برقم وسنة إلزاميين.
        var restore = await client.PostAsJsonAsync($"/api/documents/{id}/restore-struck-off",
            new { renewalFileNumber = "999", renewalYear = 2024, renewalFileType = "س" });
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        using var restored = await (await client.GetAsync($"/api/documents/{id}")).Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(string.Empty, restored!.RootElement.GetProperty("execStatus").GetString());
    }

    [Fact]
    public async Task Search_ReturnsPagedResults()
    {
        var client = _factory.AuthorizedClient("manager");
        var response = await client.GetAsync("/api/documents?page=1&perPage=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(body!.RootElement.GetProperty("totalCount").GetInt32() >= 0);
        body.Dispose();
    }

    [Fact]
    public async Task Lawyer_CannotAccessOtherBranchesDocuments()
    {
        var damascusToken = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await _factory.CreateDocumentAsync(damascusToken, "مستند محامي دمشق");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var aleppo = db.Branches.Single(b => b.Code == "ALP");
            await _factory.CreateUserAsync("lawyer_halab", UserRole.Lawyer, aleppo.Id);
        }

        var aleppoClient = _factory.AuthorizedClient("lawyer_halab");
        var response = await aleppoClient.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndGet_PersistsIncomingAndUnderFilingFields()
    {
        var client = _factory.AuthorizedClient("lawyer1");
        const string incoming = "قيد/2026/120";
        const string incomingDate = "05/02/2026";
        const string underFiling = "رفع-88";

        var createResponse = await client.PostAsJsonAsync("/api/documents", new
        {
            documentType = "بيان دعوى",
            borrowerName = "مقترض فحص",
            contractType = "تعهد",
            fileNumber = "220",
            fileYear = "2026",
            fileRegistrationDate = "01/03/2026",
            fileIncoming = incoming,
            fileIncomingDate = incomingDate,
            underFilingNumber = underFiling,
            branchName = "الفرع الرئيسي - دمشق",
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var id = created!.RootElement.GetProperty("id").GetInt32();

        var getResponse = await client.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var doc = await getResponse.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(incoming, doc!.RootElement.GetProperty("fileIncoming").GetString());
        Assert.Equal(incomingDate, doc.RootElement.GetProperty("fileIncomingDate").GetString());
        Assert.Equal(underFiling, doc.RootElement.GetProperty("underFilingNumber").GetString());
        Assert.Equal("الفرع الرئيسي - دمشق", doc.RootElement.GetProperty("branchName").GetString());

        created.Dispose();
        doc.Dispose();
    }

    [Fact]
    public async Task Search_FiltersByMutadawalAndTahRafaa()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);

        var draftResponse = await client.PostAsJsonAsync("/api/documents", new
        {
            borrowerName = "مرشح تحت رفع",
            contractType = "تعهد",
            amountNumeric = 10,
        });
        var draft = await draftResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var draftId = draft!.RootElement.GetProperty("id").GetInt32();

        var activeResponse = await client.PostAsJsonAsync("/api/documents", new
        {
            borrowerName = "مرشح متداول",
            contractType = "تعهد",
            amountNumeric = 20,
            fileNumber = "991",
            fileYear = "2026",
            fileRegistrationDate = "01/06/2026",
        });
        var active = await activeResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var activeId = active!.RootElement.GetProperty("id").GetInt32();

        var draftList = await client.GetAsync("/api/documents?status=تحت رفع&perPage=50");
        var draftBody = await draftList.Content.ReadFromJsonAsync<JsonDocument>();
        var draftIds = draftBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(draftId, draftIds);
        Assert.DoesNotContain(activeId, draftIds);

        var activeList = await client.GetAsync("/api/documents?status=متداول&perPage=50");
        var activeBody = await activeList.Content.ReadFromJsonAsync<JsonDocument>();
        var activeIds = activeBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(activeId, activeIds);
        Assert.DoesNotContain(draftId, activeIds);

        draft.Dispose();
        active.Dispose();
        draftBody.Dispose();
        activeBody.Dispose();
    }

    [Fact]
    public async Task Update_AddingFileNumberAndYear_BecomesMutadawal()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);

        var draftResponse = await client.PostAsJsonAsync("/api/documents", new
        {
            borrowerName = "مسودة متداول",
            contractType = "تعهد",
            amountNumeric = 200,
        });
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var id = draft!.RootElement.GetProperty("id").GetInt32();
        Assert.True(draft.RootElement.GetProperty("isDraft").GetBoolean());
        Assert.Contains("تحت رفع", draft.RootElement.GetProperty("documentType").GetString());

        var updateResponse = await client.PutAsJsonAsync($"/api/documents/{id}", new
        {
            borrowerName = "مسودة متداول",
            contractType = "تعهد",
            amountNumeric = 200,
            fileNumber = "777",
            fileYear = "2026",
            fileRegistrationDate = "01/05/2026",
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var doc = await updateResponse.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.False(doc!.RootElement.GetProperty("isDraft").GetBoolean());
        var type = doc.RootElement.GetProperty("documentType").GetString();
        Assert.StartsWith("متداول", type);
        Assert.DoesNotContain("تحت رفع", type);

        draft.Dispose();
        doc.Dispose();
    }

    [Fact]
    public async Task Get_AsManager_ShowsViewCounters()
    {
        var client = _factory.AuthorizedClient("manager");
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await _factory.CreateDocumentAsync(token);

        await client.PostAsync($"/api/documents/{id}/view", null);

        var response = await client.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(1, doc!.RootElement.GetProperty("viewCount").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("printCount").GetInt32());
    }

    [Fact]
    public async Task Get_AsLawyer_HidesViewCounters()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var lawyerClient = _factory.WithToken(token);
        var managerClient = _factory.AuthorizedClient("manager");

        var id = await _factory.CreateDocumentAsync(token, "مستند عدادات محامي");
        await managerClient.PostAsync($"/api/documents/{id}/view", null);

        var response = await lawyerClient.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(0, doc!.RootElement.GetProperty("viewCount").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("printCount").GetInt32());
    }

    [Fact]
    public async Task Search_AsLawyer_HidesViewCounters()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var lawyerClient = _factory.WithToken(token);
        var managerClient = _factory.AuthorizedClient("manager");

        var id = await _factory.CreateDocumentAsync(token, "بحث عدادات محامي");
        await managerClient.PostAsync($"/api/documents/{id}/view", null);

        var url = "/api/documents?q=" + Uri.EscapeDataString("بحث عدادات محامي") + "&perPage=50";
        var response = await lawyerClient.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var item = body!.RootElement.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("id").GetInt32() == id);
        Assert.Equal(0, item.GetProperty("viewCount").GetInt32());
    }

    [Fact]
    public async Task AddAction_AsLawyer_ReturnsActionAndPersists()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token, "مستند إجراءات");

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/actions",
            new { text = "تم إشعار المنفذ عليه", actionDate = "1/8/2026" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var action = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("تم إشعار المنفذ عليه", action!.RootElement.GetProperty("text").GetString());

        var listResponse = await client.GetAsync($"/api/documents/{id}/actions");
        var list = await listResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(1, list!.RootElement.GetArrayLength());

        action.Dispose();
        list.Dispose();
    }

    [Fact]
    public async Task AddAction_AsManager_Forbidden()
    {
        var client = _factory.AuthorizedClient("manager");
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await _factory.CreateDocumentAsync(token);

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/actions",
            new { text = "إجراء من مدير", actionDate = "1/8/2026" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddAction_EmptyText_ReturnsBadRequest()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/actions",
            new { text = "  ", actionDate = "1/8/2026" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddAction_AsLawyerOnOthersDocument_Forbidden()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var damascus = db.Branches.Single(b => b.Code == "DAM");
            await _factory.CreateUserAsync("lawyer_other_damascus", UserRole.Lawyer, damascus.Id);
        }
        var lawyer2Token = (await _factory.LoginAsync("lawyer_other_damascus", "123456"))!.Token!;
        var otherId = await _factory.CreateDocumentAsync(lawyer2Token, "مستند غيره");

        var response = await client.PostAsJsonAsync($"/api/documents/{otherId}/actions",
            new { text = "إجراء", actionDate = "1/8/2026" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetActions_IsEmptyInitially()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);

        var response = await client.GetAsync($"/api/documents/{id}/actions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var list = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(0, list!.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Search_IncludesExecutionActions()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token, "مستند قائمة إجراءات");
        await client.PostAsJsonAsync($"/api/documents/{id}/actions",
            new { text = "إجراء في القائمة", actionDate = "1/8/2026" });

        var url = "/api/documents?q=" + Uri.EscapeDataString("مستند قائمة إجراءات") + "&perPage=50";
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var item = body!.RootElement.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("id").GetInt32() == id);
        var actions = item.GetProperty("executionActions");
        Assert.Equal(1, actions.GetArrayLength());
        Assert.Equal("إجراء في القائمة", actions[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task AddNote_WithoutDate_DefaultsToToday()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/actions",
            new { type = "note", text = "ملاحظة بلا تاريخ" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var action = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("note", action!.RootElement.GetProperty("type").GetString());
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), action.RootElement.GetProperty("actionDate").GetString());
    }

    [Fact]
    public async Task AddAction_WithoutDate_ReturnsBadRequest()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);

        var response = await client.PostAsJsonAsync($"/api/documents/{id}/actions",
            new { type = "action", text = "إجراء بلا تاريخ" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAction_AsLawyer_ReturnsUpdatedAction()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);
        var createResponse = await client.PostAsJsonAsync($"/api/documents/{id}/actions",
            new { type = "action", text = "إجراء قديم", actionDate = "1/1/2026" });
        using var created = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var actionId = created!.RootElement.GetProperty("id").GetInt32();

        var response = await client.PutAsJsonAsync($"/api/documents/{id}/actions/{actionId}",
            new { type = "note", text = "ملاحظة محدثة" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var updated = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("note", updated!.RootElement.GetProperty("type").GetString());
        Assert.Equal("ملاحظة محدثة", updated.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task UpdateAction_AsManager_Forbidden()
    {
        var client = _factory.AuthorizedClient("manager");
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await _factory.CreateDocumentAsync(token);
        var postClient = _factory.WithToken(token);
        var createResponse = await postClient.PostAsJsonAsync($"/api/documents/{id}/actions",
            new { type = "action", text = "إجراء", actionDate = "1/1/2026" });
        using var created = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var actionId = created!.RootElement.GetProperty("id").GetInt32();

        var response = await client.PutAsJsonAsync($"/api/documents/{id}/actions/{actionId}",
            new { type = "note", text = "تعديل مدير" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAction_AsLawyer_RemovesAction()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);
        var createResponse = await client.PostAsJsonAsync($"/api/documents/{id}/actions",
            new { type = "action", text = "إجراء للحذف", actionDate = "1/1/2026" });
        using var created = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var actionId = created!.RootElement.GetProperty("id").GetInt32();

        var response = await client.DeleteAsync($"/api/documents/{id}/actions/{actionId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var listResponse = await client.GetAsync($"/api/documents/{id}/actions");
        using var list = await listResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(0, list!.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task DeleteAction_AsManager_Forbidden()
    {
        var client = _factory.AuthorizedClient("manager");
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await _factory.CreateDocumentAsync(token);
        var postClient = _factory.WithToken(token);
        var createResponse = await postClient.PostAsJsonAsync($"/api/documents/{id}/actions",
            new { type = "action", text = "إجراء", actionDate = "1/1/2026" });
        using var created = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var actionId = created!.RootElement.GetProperty("id").GetInt32();

        var response = await client.DeleteAsync($"/api/documents/{id}/actions/{actionId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ClearReminder_AsLawyer_RemovesReminderKeepsAction()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);
        var createResponse = await client.PostAsJsonAsync($"/api/documents/{id}/actions",
            new { type = "action", text = "إجراء بموعد", actionDate = "1/8/2026", reminderDuration = "أسبوع", reminderColor = "أحمر" });
        using var created = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var actionId = created!.RootElement.GetProperty("id").GetInt32();

        var response = await client.DeleteAsync($"/api/documents/{id}/actions/{actionId}/reminder");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var listResponse = await client.GetAsync($"/api/documents/{id}/actions");
        using var list = await listResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var action = Assert.Single(list!.RootElement.EnumerateArray());
        Assert.Equal("إجراء بموعد", action.GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, action.GetProperty("reminderDuration").ValueKind);
        Assert.Equal(JsonValueKind.Null, action.GetProperty("reminderColor").ValueKind);
    }

    [Fact]
    public async Task ClearReminder_AsManager_Forbidden()
    {
        var client = _factory.AuthorizedClient("manager");
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await _factory.CreateDocumentAsync(token);
        var postClient = _factory.WithToken(token);
        var createResponse = await postClient.PostAsJsonAsync($"/api/documents/{id}/actions",
            new { type = "action", text = "إجراء", actionDate = "1/1/2026", reminderDuration = "أسبوع", reminderColor = "أصفر" });
        using var created = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var actionId = created!.RootElement.GetProperty("id").GetInt32();

        var response = await client.DeleteAsync($"/api/documents/{id}/actions/{actionId}/reminder");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Search_ByApplicantFilter_FiltersResults()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id1 = await _factory.CreateDocumentAsync(token, "مستند فلتر طالب", applicant: "المدعي");
        var id2 = await _factory.CreateDocumentAsync(token, "مستند آخر", applicant: "مدعي آخر");

        var response = await client.GetAsync("/api/documents?applicant=" + Uri.EscapeDataString("المدعي") + "&perPage=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var ids = body!.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32());
        Assert.Contains(id1, ids);
        Assert.DoesNotContain(id2, ids);
    }

    [Fact]
    public async Task Search_ByCourtFilter_FiltersResults()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id1 = await _factory.CreateDocumentAsync(token, "مستند دائرة دمشق", court: "دمشق");
        var id2 = await _factory.CreateDocumentAsync(token, "مستند دائرة حلب", court: "حلب");

        var response = await client.GetAsync("/api/documents?court=" + Uri.EscapeDataString("حلب") + "&perPage=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var ids = body!.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32());
        Assert.Contains(id2, ids);
        Assert.DoesNotContain(id1, ids);
    }

    [Fact]
    public async Task GetFilterOptions_ReturnsApplicantsAndCourts()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        await _factory.CreateDocumentAsync(token, "مستند خيارات فلترة", applicant: "المدعي", court: "دمشق");

        var response = await client.GetAsync("/api/documents/filter-options");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var applicants = body!.RootElement.GetProperty("applicants").EnumerateArray().Select(a => a.GetString());
        var courts = body.RootElement.GetProperty("courts").EnumerateArray().Select(c => c.GetString());
        Assert.Contains("المدعي", applicants);
        Assert.Contains("دمشق", courts);
    }

    [Fact]
    public async Task Search_ByPublicEntityBranch_FiltersResults()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var executedId = await CreateExecutedDocumentAsync(token);
        var plainId = await _factory.CreateDocumentAsync(token, "مستند بلا جهة عامة");

        var response = await client.GetAsync(
            "/api/documents?publicEntityBranch=" + Uri.EscapeDataString("فرع المزة") + "&perPage=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var ids = body!.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32());
        Assert.Contains(executedId, ids);
        Assert.DoesNotContain(plainId, ids);
    }

    [Fact]
    public async Task GetFilterOptions_ReturnsPublicEntityBranches()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        await CreateExecutedDocumentAsync(token);

        var response = await client.GetAsync("/api/documents/filter-options");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var branches = body!.RootElement.GetProperty("publicEntityBranches").EnumerateArray().Select(b => b.GetString());
        Assert.Contains("فرع المزة", branches);
    }

    [Fact]
    public async Task Search_ByLawyerFilter_AsLawyer_Forbidden()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);

        var response = await client.GetAsync("/api/documents?lawyer=" + Uri.EscapeDataString("محامي"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Search_ByLawyerFilter_AsHead_FiltersResults()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id1 = await _factory.CreateDocumentAsync(token, "مستند محامي دمشق");

        var damId = await BranchIdAsync("DAM");
        await _factory.CreateUserAsync("lawyer_filter_other", UserRole.Lawyer, damId);
        var otherToken = (await _factory.LoginAsync("lawyer_filter_other", "123456"))!.Token!;
        var id2 = await _factory.CreateDocumentAsync(otherToken, "مستند محامي آخر");

        var headClient = _factory.AuthorizedClient("head1");
        var response = await headClient.GetAsync("/api/documents?lawyer=" + Uri.EscapeDataString("محامي دمشق") + "&perPage=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var ids = body!.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32());
        Assert.Contains(id1, ids);
        Assert.DoesNotContain(id2, ids);
    }

    [Fact]
    public async Task GetFilterOptions_AsManager_ReturnsLawyers()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await _factory.CreateDocumentAsync(token, "مستند خيارات محام");

        var managerClient = _factory.AuthorizedClient("manager");
        var response = await managerClient.GetAsync("/api/documents/filter-options");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var lawyers = body!.RootElement.GetProperty("lawyers").EnumerateArray().Select(l => l.GetString());
        Assert.Contains("محامي دمشق", lawyers);
    }

    [Fact]
    public async Task Search_ReturnsAdministrativeBranchName_AsManager()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await _factory.CreateDocumentAsync(token, "مستند فرع الإدارة");

        var managerClient = _factory.AuthorizedClient("manager");
        var response = await managerClient.GetAsync($"/api/documents?perPage=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var items = body!.RootElement.GetProperty("items").EnumerateArray();
        var doc = Assert.Single(items, i => i.GetProperty("id").GetInt32() == id);
        Assert.Equal("الفرع الرئيسي - دمشق", doc.GetProperty("administrativeBranchName").GetString());
    }

    [Fact]
    public async Task Search_ByTripleName_FiltersResults()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token, "أحمد", borrowerFather: "خالد", borrowerFamily: "الخطيب");

        var response = await client.GetAsync("/api/documents?q=" + Uri.EscapeDataString("أحمد خالد الخطيب") + "&perPage=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var items = body!.RootElement.GetProperty("items").EnumerateArray();
        Assert.Contains(items, i => i.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task Search_ByBorrowerHeirName_FiltersResults()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var createResponse = await client.PostAsJsonAsync("/api/documents", new
        {
            borrowerName = "أحمد",
            borrowerFather = "خالد",
            borrowerFamily = "الخطيب",
            court = "دمشق",
            contractType = "تعهد",
            amountNumeric = 500,
            borrowerHeirs = new[]
            {
                new { name = "محمود", father = "خالد", family = "الحلبي", addressType = "عنوان", address = "المزة" },
            },
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createBody = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var id = createBody!.RootElement.GetProperty("id").GetInt32();

        var response = await client.GetAsync("/api/documents?q=" + Uri.EscapeDataString("محمود الحلبي") + "&perPage=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var items = body!.RootElement.GetProperty("items").EnumerateArray();
        Assert.Contains(items, i => i.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task Create_WithRepresentativesAndHeirCapacity_RoundTrips()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            borrowerName = "أحمد",
            borrowerFather = "خالد",
            borrowerFamily = "الخطيب",
            court = "دمشق",
            contractType = "تعهد",
            amountNumeric = 500,
            borrowerRepresentativeName = "الولي",
            borrowerRepresentativeFather = "أب",
            borrowerRepresentativeFamily = "نسبة",
            borrowerRepresentativeCapacity = "ولي",
            borrowerRepresentativeAddressType = "وكيل قانوني",
            borrowerRepresentativeAddress = "المحامي سامر",
            guarantors = new[]
            {
                new
                {
                    guarantorNumber = 1,
                    name = "سمير",
                    father = "حسن",
                    family = "علي",
                    representativeName = "الوصي",
                    representativeCapacity = "وصي",
                    representativeAddressType = "موطن مختار",
                    representativeAddress = "دمشق",
                },
            },
            borrowerHeirs = new[]
            {
                new { name = "محمود", father = "خالد", family = "الحلبي", capacity = "إضافة لتركة", addressType = "موطن مختار", address = "المزة" },
            },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = body!.RootElement;

        Assert.Equal("الولي", root.GetProperty("borrowerRepresentativeName").GetString());
        Assert.Equal("ولي", root.GetProperty("borrowerRepresentativeCapacity").GetString());
        Assert.Equal("وكيل قانوني", root.GetProperty("borrowerRepresentativeAddressType").GetString());
        Assert.Equal("المحامي سامر", root.GetProperty("borrowerRepresentativeAddress").GetString());

        var guarantor = root.GetProperty("guarantors")[0];
        Assert.Equal("الوصي", guarantor.GetProperty("representativeName").GetString());
        Assert.Equal("وصي", guarantor.GetProperty("representativeCapacity").GetString());
        Assert.Equal("موطن مختار", guarantor.GetProperty("representativeAddressType").GetString());
        Assert.Equal("دمشق", guarantor.GetProperty("representativeAddress").GetString());

        var heir = root.GetProperty("borrowerHeirs")[0];
        Assert.Equal("إضافة لتركة", heir.GetProperty("capacity").GetString());
        Assert.Equal("موطن مختار", heir.GetProperty("addressType").GetString());
    }

    [Fact]
    public async Task Create_ExecutedSide_WithRepresentativeAndCombinedCapacity_RoundTrips()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            generalEntitySide = "executed",
            documentType = "الجهة العامة منفذ عليها",
            fileNumber = "998",
            fileYear = "2024",
            contractTypeSelector = "عادي",
            court = "دمشق",
            applicant = "المدعي",
            executionApplicants = new[]
            {
                new
                {
                    name = "أحمد",
                    father = "خالد",
                    family = "الخطيب",
                    representationType = "أصالة وإضافة",
                    deceasedName = "المورث",
                    deceasedFather = "أب المورث",
                    deceasedFamily = "نسبة المورث",
                    representativeName = "القيم",
                    representativeCapacity = "قيم",
                    representativeLegalRepresentative = "المحامي القانوني",
                },
            },
            executedNaturalPersons = new[]
            {
                new
                {
                    name = "سامر",
                    father = "حسن",
                    family = "علي",
                    addressType = "عنوان",
                    addressOrRepresentative = "دمشق",
                    representationType = "أصالة",
                    representativeName = "ولي الشخص",
                    representativeCapacity = "ولي",
                    representativeAddressType = "عنوان",
                    representativeAddress = "حلب",
                },
            },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = body!.RootElement;

        var applicant = root.GetProperty("executionApplicants")[0];
        Assert.Equal("أصالة وإضافة", applicant.GetProperty("representationType").GetString());
        Assert.Equal("المورث", applicant.GetProperty("deceasedName").GetString());
        Assert.Equal("القيم", applicant.GetProperty("representativeName").GetString());
        Assert.Equal("قيم", applicant.GetProperty("representativeCapacity").GetString());
        Assert.Equal("المحامي القانوني", applicant.GetProperty("representativeLegalRepresentative").GetString());

        var person = root.GetProperty("executedNaturalPersons")[0];
        Assert.Equal("ولي الشخص", person.GetProperty("representativeName").GetString());
        Assert.Equal("ولي", person.GetProperty("representativeCapacity").GetString());
        Assert.Equal("عنوان", person.GetProperty("representativeAddressType").GetString());
        Assert.Equal("حلب", person.GetProperty("representativeAddress").GetString());
    }

    [Fact]
    public async Task SetStatus_AsHeadOrManager_Forbidden()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await _factory.CreateDocumentAsync(token);

        var headClient = _factory.AuthorizedClient("head1");
        var headForbidden = await headClient.PostAsJsonAsync($"/api/documents/{id}/status",
            new { status = "متداول" });
        Assert.Equal(HttpStatusCode.Forbidden, headForbidden.StatusCode);

        var managerClient = _factory.AuthorizedClient("manager");
        var managerForbidden = await managerClient.PostAsJsonAsync($"/api/documents/{id}/status",
            new { status = "متداول" });
        Assert.Equal(HttpStatusCode.Forbidden, managerForbidden.StatusCode);
    }

    [Fact]
    public async Task Update_AsHeadOrManager_Forbidden()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await _factory.CreateDocumentAsync(token);

        var headClient = _factory.AuthorizedClient("head1");
        var headForbidden = await headClient.PutAsJsonAsync($"/api/documents/{id}",
            new { borrowerName = "تعديل رئيس" });
        Assert.Equal(HttpStatusCode.Forbidden, headForbidden.StatusCode);

        var managerClient = _factory.AuthorizedClient("manager");
        var managerForbidden = await managerClient.PutAsJsonAsync($"/api/documents/{id}",
            new { borrowerName = "تعديل مدير" });
        Assert.Equal(HttpStatusCode.Forbidden, managerForbidden.StatusCode);
    }

    [Fact]
    public async Task GetDeleted_AsLawyer_ReturnsOwnDeletedDocuments()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var lawyerClient = _factory.WithToken(token);
        var ownId = await _factory.CreateDocumentAsync(token, "محذوفة - خاصتي");
        await lawyerClient.DeleteAsync($"/api/documents/{ownId}");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var damascus = db.Branches.Single(b => b.Code == "DAM");
            await _factory.CreateUserAsync("lawyer_other_damascus2", UserRole.Lawyer, damascus.Id);
        }
        var lawyer2Token = (await _factory.LoginAsync("lawyer_other_damascus2", "123456"))!.Token!;
        var otherId = await _factory.CreateDocumentAsync(lawyer2Token, "محذوفة - محامٍ آخر");
        await _factory.WithToken(lawyer2Token).DeleteAsync($"/api/documents/{otherId}");

        var response = await lawyerClient.GetAsync("/api/documents/deleted?perPage=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var ids = body!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(ownId, ids);
        Assert.DoesNotContain(otherId, ids);
    }

    [Fact]
    public async Task BaseNumbersHistory_AfterRotation_ReturnsYearsAndDisplayFileNumber()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token);

        // قيد الملف (رقم + سنة + تاريخ قيد) في سنة سابقة حتى يخرج من «تحت رفع» ويصبح مؤهلًا
        // للتدوير في السنة الحالية (الملف المقيد في السنة الحالية لا يُدوَّر لأن رقمه هو رقم أساسه).
        var previousYear = DateTime.Today.Year - 1;
        var update = await client.PutAsJsonAsync($"/api/documents/{id}", new
        {
            documentType = "بيان دعوى",
            borrowerName = "مقترض",
            contractType = "تعهد",
            amountNumeric = 500,
            fileNumber = "520",
            fileYear = previousYear.ToString(),
            fileRegistrationDate = "1/8/" + previousYear,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        // تدوير: إدخال رقم أساس للسنة الحالية.
        var rotate = await client.PutAsJsonAsync("/api/documents/rotate", new
        {
            entries = new[] { new { documentId = id, baseNumber = "1500" } },
        });
        Assert.Equal(HttpStatusCode.NoContent, rotate.StatusCode);

        // تاريخ أرقام الأساس.
        var historyResponse = await client.GetAsync($"/api/documents/{id}/base-numbers");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<List<BaseNumberYearDto>>();
        var entry = Assert.Single(history!);
        Assert.Equal(DateTime.Today.Year, entry.Year);
        Assert.Equal("1500", entry.BaseNumber);

        // الرقم الفعّال في العرض التفصيلي يحل محل رقم الملف، والأصلي يبقى للتاريخ.
        var detail = await client.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var detailJson = await detail.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("1500", detailJson!.RootElement.GetProperty("displayFileNumber").GetString());
        Assert.Equal("520", detailJson.RootElement.GetProperty("fileNumber").GetString());
    }

    [Fact]
    public async Task BaseNumbersHistory_AnotherLawyer_Forbidden()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await _factory.CreateDocumentAsync(token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var damascus = db.Branches.Single(b => b.Code == "DAM");
            await _factory.CreateUserAsync("lawyer_other_bn", UserRole.Lawyer, damascus.Id);
        }
        var otherToken = (await _factory.LoginAsync("lawyer_other_bn", "123456"))!.Token!;
        var otherClient = _factory.WithToken(otherToken);

        var response = await otherClient.GetAsync($"/api/documents/{id}/base-numbers");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRotationList_ExcludesFilesRegisteredInCurrentYear()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var currentYear = DateTime.Today.Year;

        // ملف مقيد في السنة الحالية → لا يظهر في جدول التدوير (رقمه الأصلي هو رقم أساس سنته).
        var currentRegistered = await _factory.CreateDocumentAsync(token);
        var updateCurrent = await client.PutAsJsonAsync($"/api/documents/{currentRegistered}", new
        {
            documentType = "بيان دعوى",
            borrowerName = "مقترض حال",
            contractType = "تعهد",
            amountNumeric = 500,
            fileNumber = "60",
            fileYear = currentYear.ToString(),
            fileRegistrationDate = "1/8/" + currentYear,
        });
        Assert.Equal(HttpStatusCode.OK, updateCurrent.StatusCode);

        // ملف مقيد في سنة سابقة → يظهر ويستحق التدوير.
        var previousRegistered = await _factory.CreateDocumentAsync(token);
        var updatePrevious = await client.PutAsJsonAsync($"/api/documents/{previousRegistered}", new
        {
            documentType = "بيان دعوى",
            borrowerName = "مقترض سابق",
            contractType = "تعهد",
            amountNumeric = 500,
            fileNumber = "61",
            fileYear = (currentYear - 1).ToString(),
            fileRegistrationDate = "1/8/" + (currentYear - 1),
        });
        Assert.Equal(HttpStatusCode.OK, updatePrevious.StatusCode);

        var list = await client.GetAsync("/api/documents/rotate?perPage=50");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var body = await list.Content.ReadFromJsonAsync<JsonDocument>();
        var ids = body!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("documentId").GetInt32()).ToList();
        Assert.DoesNotContain(currentRegistered, ids);
        Assert.Contains(previousRegistered, ids);
    }

    [Fact]
    public async Task SaveBaseNumbers_FileRegisteredInCurrentYear_Rejected()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var currentYear = DateTime.Today.Year;

        // قيد ملف في السنة الحالية ثم محاولة تدويره → تُرفض.
        var id = await _factory.CreateDocumentAsync(token);
        var update = await client.PutAsJsonAsync($"/api/documents/{id}", new
        {
            documentType = "بيان دعوى",
            borrowerName = "مقترض",
            contractType = "تعهد",
            amountNumeric = 500,
            fileNumber = "70",
            fileYear = currentYear.ToString(),
            fileRegistrationDate = "1/8/" + currentYear,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var rotate = await client.PutAsJsonAsync("/api/documents/rotate", new
        {
            entries = new[] { new { documentId = id, baseNumber = "1500" } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, rotate.StatusCode);

        // لا يُحفظ أي رقم أساس للملف.
        var historyResponse = await client.GetAsync($"/api/documents/{id}/base-numbers");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<List<BaseNumberYearDto>>();
        Assert.Empty(history!);
    }

    private async Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var branch = await db.Branches.AsNoTracking().SingleAsync(b => b.Code == code);
        return branch.Id;
    }

    /// إنشاء ملف «طالبة تنفيذ» مقيد (متداول) مع عقار واحد — للاختبارات التي تحتاج انتقالات
    /// الحالة المتاحة من المتداول فقط (مثل «منفذ جبريا» الذي يتطلب عقارات مباعة بالمزاد).
    private async Task<int> CreateCirculatingDocumentAsync(string token)
    {
        var client = _factory.WithToken(token);
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            generalEntitySide = "applicant",
            borrowerName = "مقترض",
            applicant = "المدعي",
            court = "دمشق",
            contractType = "تعهد",
            amountNumeric = 500,
            fileNumber = "520",
            fileYear = "2024",
            fileRegistrationDate = "1/1/2024",
            branchName = "الفرع الرئيسي - دمشق",
            assets = new[]
            {
                new { assetKind = "عقار", property = "بيت", propertyNumber = "12345", propertyDistrict = "المزة", landRegistry = "الصالحية", shareType = "تمام العقار", owners = new[] { "المدعى عليه" } },
            },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return body!.RootElement.GetProperty("id").GetInt32();
    }

    private async Task<int> FirstEstateIdAsync(string token, int documentId)
    {
        var client = _factory.WithToken(token);
        using var doc = await (await client.GetAsync($"/api/documents/{documentId}")).Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("assets")[0].GetProperty("id").GetInt32();
    }

    private async Task<int> CreateExecutedDocumentAsync(string token)
    {
        var client = _factory.WithToken(token);
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            generalEntitySide = "executed",
            documentType = "الجهة العامة منفذ عليها",
            fileNumber = "999",
            fileYear = "2024",
            fileRegistrationDate = (string?)null,
            fileReceiptDate = "2024-01-05",
            executedRequiredAmount = 1000,
            contractTypeSelector = "عادي",
            court = "دمشق",
            applicant = "المدعي",
            executionApplicants = new[]
            {
                new { name = "أحمد", father = "خالد", family = "الخطيب", representationType = "أصالة" },
            },
            executedPublicEntities = new[]
            {
                new { entityName = "المصرف العقاري", entityBranch = "فرع المزة" },
            },
            executedNaturalPersons = new[]
            {
                new { name = "سامر", father = "حسن", family = "علي", addressType = "عنوان", addressOrRepresentative = "دمشق", representationType = "أصالة" },
            },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return body!.RootElement.GetProperty("id").GetInt32();
    }

    private async Task<int> CreateDepositDocumentAsync(string token)
    {
        var client = _factory.WithToken(token);
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            generalEntitySide = "deposit",
            documentType = "عرض وايداع",
            fileNumber = "888",
            fileYear = "2024",
            fileRegistrationDate = (string?)null,
            fileReceiptDate = "2024-01-05",
            executedRequiredAmount = 1500,
            contractTypeSelector = "عادي",
            court = "دمشق",
            applicant = "معروض",
            executionApplicants = new[]
            {
                new { name = "هاني", father = "سامر", family = "النجار", representationType = "أصالة" },
            },
            executedPublicEntities = new[]
            {
                new { entityName = "المصرف التجاري", entityBranch = "فرع دمشق" },
            },
            executedNaturalPersons = new[]
            {
                new { name = "رامي", father = "سالم", family = "عبد", addressType = "عنوان", addressOrRepresentative = "دمشق", representationType = "أصالة" },
            },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return body!.RootElement.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task ExecutedSide_FromExecuted_IsTerminal()
    {
        // صفة «الجهة العامة منفذ عليها»: حالة «منفذ» نهائية؛ لا إرجاع إلى متداول ولا شطب.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await CreateExecutedDocumentAsync(token);
        await client.PostAsJsonAsync($"/api/documents/{id}/executed-status", new { status = "منفذ" });

        var toTrading = await client.PostAsJsonAsync($"/api/documents/{id}/executed-status", new { status = "" });
        Assert.Equal(HttpStatusCode.BadRequest, toTrading.StatusCode);
        var toStruckOff = await client.PostAsJsonAsync($"/api/documents/{id}/executed-status", new { status = "مشطوب" });
        Assert.Equal(HttpStatusCode.BadRequest, toStruckOff.StatusCode);

        using var doc = await (await client.GetAsync($"/api/documents/{id}")).Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("منفذ", doc!.RootElement.GetProperty("executedStatus").GetString());
    }

    [Fact]
    public async Task Deposit_FromExecutedToStruckOff_Forbidden()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await CreateDepositDocumentAsync(token);
        await client.PostAsJsonAsync($"/api/documents/{id}/executed-status", new { status = "منفذ" });

        var toStruckOff = await client.PostAsJsonAsync($"/api/documents/{id}/executed-status", new { status = "مشطوب" });
        Assert.Equal(HttpStatusCode.BadRequest, toStruckOff.StatusCode);
    }

    [Fact]
    public async Task Deposit_FromExecutedToCirculating_RequiresSayerAndKeepsAmount()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await CreateDepositDocumentAsync(token);
        await client.PostAsJsonAsync($"/api/documents/{id}/executed-status", new
        {
            status = "منفذ",
            executedPaidAmount = 1250,
            executedDepositDate = "10/6/2024",
        });

        // دون كتاب السير بالملف يُرفض.
        var without = await client.PostAsJsonAsync($"/api/documents/{id}/executed-status", new { status = "" });
        Assert.Equal(HttpStatusCode.BadRequest, without.StatusCode);

        // بالكتاب يُقبل: يعود إلى متداول مع بقاء المبلغ المودع وتاريخه، وتُضبط العلامة الدائمة.
        var with = await client.PostAsJsonAsync($"/api/documents/{id}/executed-status", new
        {
            status = "",
            sayerNumber = "44",
            sayerDate = "1/8/2026",
            sayerRegNumber = "55",
            sayerRegDate = "2/8/2026",
        });
        Assert.Equal(HttpStatusCode.OK, with.StatusCode);

        using var doc = await (await client.GetAsync($"/api/documents/{id}")).Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("", doc!.RootElement.GetProperty("executedStatus").GetString());
        Assert.Equal(1250m, doc.RootElement.GetProperty("executedPaidAmount").GetDecimal());
        Assert.Equal("44", doc.RootElement.GetProperty("sayerNumber").GetString());
        Assert.Equal("1/8/2026", doc.RootElement.GetProperty("sayerDate").GetString());
        Assert.Equal("55", doc.RootElement.GetProperty("sayerRegNumber").GetString());
        Assert.Equal("2/8/2026", doc.RootElement.GetProperty("sayerRegDate").GetString());
    }

    [Fact]
    public async Task ExecutedSide_Create_ThenStrikeOff_ThenRestore_AndSearch()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);

        // إنشاء ملف وضع «الجهة العامة منفذ عليها» يحمل الصفة والكيانات الفرعية.
        var id = await CreateExecutedDocumentAsync(token);
        var created = await client.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var createdBody = await created.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("الجهة العامة منفذ عليها", createdBody!.RootElement.GetProperty("generalEntitySideLabel").GetString());
        Assert.Equal("المصرف العقاري", createdBody.RootElement.GetProperty("executedPublicEntities")[0].GetProperty("entityName").GetString());
        Assert.Equal(1000, createdBody.RootElement.GetProperty("executedRequiredAmount").GetInt32());

        // ظاهر في البحث العام قبل الشطب.
        var before = await client.GetAsync("/api/documents?q=المصرف العقاري");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        using var beforeBody = await before.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Contains(id, beforeBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));

        // شطب الملف → يختفي من البحث العام ويظهر في صفحة المشطوبة.
        var strike = await client.PostAsJsonAsync($"/api/documents/{id}/executed-status", new { status = "مشطوب" });
        Assert.Equal(HttpStatusCode.OK, strike.StatusCode);

        var after = await client.GetAsync("/api/documents?q=المصرف العقاري");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        using var afterBody = await after.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.DoesNotContain(id, afterBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));

        var struckOff = await client.GetAsync("/api/documents/struck-off?q=المصرف العقاري");
        Assert.Equal(HttpStatusCode.OK, struckOff.StatusCode);
        using var struckOffBody = await struckOff.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Contains(id, struckOffBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));

        // إعادة الشطب: يعود إلى البحث العام، ويبقى تاريخ الشطب محفوظًا، ويُطبق التجديد
        // برقم ملف جديد إلزامي لسنة الإعادة.
        var restore = await client.PostAsJsonAsync($"/api/documents/{id}/restore-struck-off", new
        {
            renewalFileNumber = "2026/555",
            renewalFileType = "قضية تنفيذ",
            renewalFileReceiptNumber = "33",
            renewalFileReceiptDate = "1/3/2026",
            renewalDate = "10/3/2026",
        });
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        var restored = await client.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        using var restoredBody = await restored.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("", restoredBody!.RootElement.GetProperty("executedStatus").GetString());
        Assert.NotNull(restoredBody.RootElement.GetProperty("struckOffDate").GetString());
        Assert.Equal("2026/555", restoredBody.RootElement.GetProperty("renewalFileNumber").GetString());
        Assert.Equal("قضية تنفيذ", restoredBody.RootElement.GetProperty("renewalFileType").GetString());
        Assert.Equal("2026/555", restoredBody.RootElement.GetProperty("displayFileNumber").GetString());
    }

    [Fact]
    public async Task RestoreStruckOff_WithoutRenewalFileNumber_BadRequest()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await CreateExecutedDocumentAsync(token);

        await client.PostAsJsonAsync($"/api/documents/{id}/executed-status", new { status = "مشطوب" });
        var restore = await client.PostAsJsonAsync($"/api/documents/{id}/restore-struck-off", new { });

        Assert.Equal(HttpStatusCode.BadRequest, restore.StatusCode);
    }

    [Fact]
    public async Task ExecutedPage_ListExecutedLike_FullyExecuted_WithExecutionDate()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await CreateExecutedDocumentAsync(token);

        // تنفيذ الملف بوضع «منفذ» مع تاريخ التنفيذ → يظهر في صفحة «الملفات المنفذة».
        var run = await client.PostAsJsonAsync($"/api/documents/{id}/executed-status", new
        {
            status = "منفذ",
            executedExecutionDate = "2026-06-01",
            executedDescription = "دفع كامل",
            executedPaidAmount = 1000,
        });
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);

        var page = await client.GetAsync("/api/documents/executed");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using var pageBody = await page.Content.ReadFromJsonAsync<JsonDocument>();
        var items = pageBody!.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("id").GetInt32() == id);
        var mine = items.Single(i => i.GetProperty("id").GetInt32() == id);
        Assert.Equal("منفذ", mine.GetProperty("executedStatus").GetString());
        Assert.NotNull(mine.GetProperty("executedExecutionDate").GetString());
    }

    [Fact]
    public async Task ExecutedPage_ExcludesTradingAndStruckOff()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await CreateExecutedDocumentAsync(token);

        // قبل التنفيذ (متداول) لا يظهر في صفحة «الملفات المنفذة».
        var before = await client.GetAsync("/api/documents/executed");
        using var beforeBody = await before.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.DoesNotContain(id, beforeBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));

        // بعد الشطب لا يظهر أيضًا (لا منفذ ولا مشطوب).
        await client.PostAsJsonAsync($"/api/documents/{id}/executed-status", new { status = "مشطوب" });
        var after = await client.GetAsync("/api/documents/executed");
        using var afterBody = await after.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.DoesNotContain(id, afterBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task MainList_HidesExecutedFamily_UnlessTextSearch()
    {
        // عائلة «منفذ عليها/عرض وايداع» بحالة «منفذ» تُخفى من القائمة العامة، وتظهر فقط عند البحث النصي.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await CreateExecutedDocumentAsync(token);

        var run = await client.PostAsJsonAsync($"/api/documents/{id}/executed-status", new { status = "منفذ" });
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);

        var plain = await client.GetAsync("/api/documents?perPage=50");
        using var plainBody = await plain.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.DoesNotContain(id, plainBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));

        var searched = await client.GetAsync("/api/documents?q=" + Uri.EscapeDataString("المصرف العقاري") + "&perPage=50");
        using var searchedBody = await searched.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Contains(id, searchedBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task MainList_HidesApplicantSideExecuted_UnlessTextSearch()
    {
        // «طالبة تنفيذ» منفذة (بالتسوية) تُخفى من القائمة العامة، وتظهر فقط عند البحث النصي.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await CreateCirculatingDocumentAsync(token);

        var run = await client.PostAsJsonAsync($"/api/documents/{id}/status", new
        {
            status = "منفذ بالتسوية",
            fields = new { baraetNumber = "1/1", baraetDate = "1/3/2026" },
        });
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);

        var plain = await client.GetAsync("/api/documents?perPage=50");
        using var plainBody = await plain.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.DoesNotContain(id, plainBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));

        var searched = await client.GetAsync("/api/documents?q=" + Uri.EscapeDataString("مقترض") + "&perPage=50");
        using var searchedBody = await searched.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Contains(id, searchedBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task Annex_ForBankingDocument_RoundTripsAndIsSearchable()
    {
        // ملحق العقد (للعقد المصرفي): يُحفظ ويُعاد عرضه، ويمكن البحث برقمه (مُضمّن في SearchText).
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);

        var create = await client.PostAsJsonAsync("/api/documents", new
        {
            generalEntitySide = "applicant",
            borrowerName = "مقترض الملحق",
            applicant = "المصرف",
            court = "دمشق",
            contractType = "تعهد",
            contractTypeSelector = "مصرفي",
            contractNumber = "C-777",
            annexType = "تعديل",
            annexNumber = "A-888",
            annexDate = "15/3/2026",
            amountNumeric = 500,
            branchName = "الفرع الرئيسي - دمشق",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var createdBody = await create.Content.ReadFromJsonAsync<JsonDocument>();
        var id = createdBody!.RootElement.GetProperty("id").GetInt32();

        using var fetched = await (await client.GetAsync($"/api/documents/{id}")).Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("تعديل", fetched!.RootElement.GetProperty("annexType").GetString());
        Assert.Equal("A-888", fetched.RootElement.GetProperty("annexNumber").GetString());
        Assert.Equal("15/3/2026", fetched.RootElement.GetProperty("annexDate").GetString());

        // البحث النصي برقم الملحق يجد الملف (AnnexNumber ضمن SearchText).
        var searched = await client.GetAsync("/api/documents?q=" + Uri.EscapeDataString("A-888") + "&perPage=50");
        using var searchedBody = await searched.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Contains(id, searchedBody!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task Generate_ForExecutedSideDocument_Rejected()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await CreateExecutedDocumentAsync(token);

        var response = await client.GetAsync($"/api/documents/{id}/generate?template=basic_docs");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StruckOff_Page_AsNonPrivilegedRole_Forbidden()
    {
        // الملفات المشطوبة بنفس صلاحيات المحذوفات: المدير لا يرى هذه الصفحة.
        var managerClient = _factory.AuthorizedClient("manager");
        var response = await managerClient.GetAsync("/api/documents/struck-off");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Occurrences_ManualAddUpdateDelete_RoundTrip()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await CreateExecutedDocumentAsync(token);

        // إضافة وقعة شطب يدويًا.
        var add = await client.PostAsJsonAsync($"/api/documents/{id}/occurrences", new
        {
            occurrenceType = "struck-off",
            eventDate = "1/8/2026",
            fileNumber = "999",
            year = 2026,
        });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        using var addBody = await add.Content.ReadFromJsonAsync<JsonDocument>();
        var occurrenceId = addBody!.RootElement.GetProperty("id").GetInt32();

        var afterAdd = await client.GetAsync($"/api/documents/{id}");
        using var afterAddBody = await afterAdd.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("struck-off", afterAddBody!.RootElement.GetProperty("occurrences")[0]
            .GetProperty("occurrenceType").GetString());
        Assert.Equal("999", afterAddBody.RootElement.GetProperty("occurrences")[0]
            .GetProperty("fileNumber").GetString());

        // تعديلها إلى تجديد ببيانات وورود اخطار.
        var update = await client.PutAsJsonAsync($"/api/documents/{id}/occurrences/{occurrenceId}", new
        {
            occurrenceType = "renewal",
            eventDate = "5/9/2026",
            fileNumber = "2026/555",
            fileType = "قضية تنفيذ",
            year = 2026,
            receiptNumber = "45",
            receiptDate = "2/9/2026",
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var afterUpdate = await client.GetAsync($"/api/documents/{id}");
        using var afterUpdateBody = await afterUpdate.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("renewal", afterUpdateBody!.RootElement.GetProperty("occurrences")[0]
            .GetProperty("occurrenceType").GetString());
        Assert.Equal("2026/555", afterUpdateBody.RootElement.GetProperty("occurrences")[0]
            .GetProperty("fileNumber").GetString());
        Assert.Equal("45", afterUpdateBody.RootElement.GetProperty("occurrences")[0]
            .GetProperty("receiptNumber").GetString());

        // الحذف → تخلو الوقوعات.
        var delete = await client.DeleteAsync($"/api/documents/{id}/occurrences/{occurrenceId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await client.GetAsync($"/api/documents/{id}");
        using var afterDeleteBody = await afterDelete.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(0, afterDeleteBody!.RootElement.GetProperty("occurrences").GetArrayLength());
    }

    [Fact]
    public async Task Occurrences_Add_AsNonEditableRole_Forbidden()
    {
        // إدارة الوقوعات للمحامي صاحب الملف فقط — المدير يقرأ ولا يعدّل.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await CreateExecutedDocumentAsync(token);

        var managerClient = _factory.AuthorizedClient("manager");
        var response = await managerClient.PostAsJsonAsync($"/api/documents/{id}/occurrences", new
        {
            occurrenceType = "struck-off",
            eventDate = "1/8/2026",
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class BaseNumberYearDto
    {
        public int Year { get; set; }
        public string BaseNumber { get; set; } = string.Empty;
    }
}
