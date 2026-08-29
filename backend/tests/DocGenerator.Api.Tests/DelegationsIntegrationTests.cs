using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Api.Tests;

[Collection(ApiTestCollection.Name)]
public class DelegationsIntegrationTests
{
    private readonly ApiFactory _factory;

    public DelegationsIntegrationTests(ApiFactory factory) => _factory = factory;

    private static string NewName(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 16, 40)];

    private async Task<(int DocId, int AssetId)> CreateSourceWithAssetAsync()
    {
        var login = await _factory.LoginAsync("lawyer1", "123456");
        var docId = await _factory.CreateDocumentAsync(login!.Token!, borrowerName: "مقترض",
            borrowerFather: "أب", borrowerFamily: "العائلة", withEstate: true);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var assetId = db.Assets.Single(a => a.DocumentId == docId).Id;
        return (docId, assetId);
    }

    private sealed record DelegationRequestBody(
        string? DelegatedCourt,
        bool IsExternal,
        int? ExternalBranchId,
        string? DelegationDate,
        string? DelegationText,
        string? DepositBookNumber,
        string? DepositBookDate,
        int[] AssetIds);

    private static DelegationRequestBody SampleBody(int assetId) => new(
        "دائرة تنفيذ حلب",
        false,
        null,
        "1/8/2026",
        "الإنابة على العقار المذكور",
        "كتاب-1",
        "2/8/2026",
        new[] { assetId });

    [Fact]
    public async Task FullFlow_LawyerToHeadToTargetLawyer_ExecutesDelegation()
    {
        var (docId, assetId) = await CreateSourceWithAssetAsync();

        // تسطير من محامي الملف المنيب.
        var lawyer1 = _factory.AuthorizedClient("lawyer1");
        var created = await lawyer1.PostAsJsonAsync($"/api/documents/{docId}/delegations", SampleBody(assetId));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<DelegationDto>();
        Assert.NotNull(dto);
        Assert.Equal(DelegationStatusCatalog.PendingHead, dto!.Status);

        // طلبات الإنابة تظهر لرئيس قسم الفرع المنيب.
        var head = _factory.AuthorizedClient("head1");
        var pending = await (await head.GetAsync("/api/delegations/pending")).Content.ReadFromJsonAsync<List<DelegationDto>>();
        Assert.NotNull(pending);
        Assert.Contains(pending, d => d.Id == dto.Id);

        // محامٍ آخر في الفرع يتابع الإنابة (يُنشأ الملف المناب تلقائيًا).
        var targetLawyer = await _factory.CreateUserAsync(NewName("lawyer_deleg"), UserRole.Lawyer, branchId: await BranchIdAsync("DAM"));
        var assign = await head.PostAsJsonAsync($"/api/delegations/{dto.Id}/assign",
            new { assignedLawyerId = targetLawyer.Id });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var assigned = await assign.Content.ReadFromJsonAsync<DelegationDto>();
        Assert.NotNull(assigned);
        Assert.Equal(DelegationStatusCatalog.Assigned, assigned!.Status);
        Assert.True(assigned.TargetDocumentId.HasValue);

        // محامي الملف المناب يسجله أصولًا (رقم أساس + تاريخ قيد).
        var targetClient = _factory.AuthorizedClient(targetLawyer.Username);
        var register = await targetClient.PostAsJsonAsync($"/api/delegations/{dto.Id}/register",
            new { fileNumber = "890", fileYear = "2026", fileRegistrationDate = "5/8/2026" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var registered = await register.Content.ReadFromJsonAsync<DelegationDto>();
        Assert.NotNull(registered);
        Assert.Equal(DelegationStatusCatalog.Registered, registered!.Status);

        // إتمام الإنابة بالبيع وإعادة الملف.
        var assetDto = registered!.Assets.Single();
        var complete = await targetClient.PostAsJsonAsync($"/api/delegations/{dto.Id}/complete",
            new
            {
                returnDate = "10/8/2026",
                sales = new[] { new { delegationAssetId = assetDto.Id, salePrice = 750_000m } },
                forcedExecutionDate = "12/8/2026",
                saleCoversFullDebt = true,
            });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completed = await complete.Content.ReadFromJsonAsync<DelegationDto>();
        Assert.NotNull(completed);
        Assert.Equal(DelegationStatusCatalog.Executed, completed!.Status);
        Assert.Equal(750_000m, completed.Assets.Single().SalePrice);
        Assert.Equal(true, completed.SaleCoversFullDebt);

        // بطاقة «تشعبات الملف» في المنيب تعرض الإنابة المكتملة.
        var list = await (await lawyer1.GetAsync($"/api/documents/{docId}/delegations")).Content.ReadFromJsonAsync<List<DelegationDto>>();
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(DelegationStatusCatalog.Executed, list![0].Status);

        // الملف المناب «منفذ إنابة» يُعامل منفذًا: يظهر في صفحة «الملفات المنفذة»
        // ويُخفى من القائمة العامة (لا يبقى متداولًا إلى الأبد).
        var targetId = assigned.TargetDocumentId!.Value;
        var executedPage = await (await targetClient.GetAsync("/api/documents/executed")).Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        Assert.NotNull(executedPage);
        Assert.Contains(targetId, executedPage!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));
        var mainList = await (await targetClient.GetAsync("/api/documents?perPage=50")).Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        Assert.NotNull(mainList);
        Assert.DoesNotContain(targetId, mainList!.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task Complete_WithoutSaleCoversFullDebt_BadRequest()
    {
        // حقل تغطية البدل إلزامي عند الإتمام: غيابه يُرفض برسالة واضحة رغم صحة باقي المدخلات.
        var (docId, assetId) = await CreateSourceWithAssetAsync();
        var lawyer1 = _factory.AuthorizedClient("lawyer1");
        var created = await (await lawyer1.PostAsJsonAsync($"/api/documents/{docId}/delegations", SampleBody(assetId)))
            .Content.ReadFromJsonAsync<DelegationDto>();
        Assert.NotNull(created);

        var head = _factory.AuthorizedClient("head1");
        var targetLawyer = await _factory.CreateUserAsync(NewName("lawyer_missingcov"), UserRole.Lawyer, branchId: await BranchIdAsync("DAM"));
        await head.PostAsJsonAsync($"/api/delegations/{created!.Id}/assign", new { assignedLawyerId = targetLawyer.Id });
        var targetClient = _factory.AuthorizedClient(targetLawyer.Username);
        var regResponse = await targetClient.PostAsJsonAsync($"/api/delegations/{created.Id}/register",
            new { fileNumber = "895", fileYear = "2026", fileRegistrationDate = "5/8/2026" });
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);
        var registered = await regResponse.Content.ReadFromJsonAsync<DelegationDto>();

        var response = await targetClient.PostAsJsonAsync($"/api/delegations/{created.Id}/complete",
            new
            {
                returnDate = "10/8/2026",
                sales = new[] { new { delegationAssetId = registered!.Assets.Single().Id, salePrice = 750_000m } },
                forcedExecutionDate = "12/8/2026",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("غطى كامل المديونية", content);
    }

    [Fact]
    public async Task Create_ByNonLawyerOrOtherOwner_ForbiddenOrBadRequest()
    {
        var (docId, assetId) = await CreateSourceWithAssetAsync();

        foreach (var username in new[] { "manager", "admin", "head1" })
        {
            var client = _factory.AuthorizedClient(username);
            var response = await client.PostAsJsonAsync($"/api/documents/{docId}/delegations", SampleBody(assetId));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Create_ByLawyerNotOwner_BadRequest()
    {
        var (docId, assetId) = await CreateSourceWithAssetAsync();
        var otherLawyer = await _factory.CreateUserAsync(NewName("lawyer_other"), UserRole.Lawyer, branchId: await BranchIdAsync("DAM"));

        var client = _factory.AuthorizedClient(otherLawyer.Username);
        var response = await client.PostAsJsonAsync($"/api/documents/{docId}/delegations", SampleBody(assetId));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Assign_ByWrongBranchHead_BadRequest()
    {
        var (docId, assetId) = await CreateSourceWithAssetAsync();
        var lawyer1 = _factory.AuthorizedClient("lawyer1");
        var created = await (await lawyer1.PostAsJsonAsync($"/api/documents/{docId}/delegations", SampleBody(assetId)))
            .Content.ReadFromJsonAsync<DelegationDto>();

        var targetLawyer = await _factory.CreateUserAsync(NewName("lawyer_lat"), UserRole.Lawyer, branchId: await BranchIdAsync("LAT"));
        var latHead = await _factory.CreateUserAsync(NewName("head_lat"), UserRole.Head, branchId: await BranchIdAsync("LAT"));
        var client = _factory.AuthorizedClient(latHead.Username);
        var response = await client.PostAsJsonAsync($"/api/delegations/{created!.Id}/assign",
            new { assignedLawyerId = targetLawyer.Id });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExternalFlow_AppearsForDelegatedBranchHead_AssignsAndTargetsExternalBranch()
    {
        var (docId, assetId) = await CreateSourceWithAssetAsync();
        var latBranchId = await BranchIdAsync("LAT");

        // تسطير إنابة خارجية إلى فرع اللاذقية (محامي الملف المنيب في دمشق).
        var lawyer1 = _factory.AuthorizedClient("lawyer1");
        var body = SampleBody(assetId) with { IsExternal = true, ExternalBranchId = latBranchId };
        var response = await lawyer1.PostAsJsonAsync($"/api/documents/{docId}/delegations", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<DelegationDto>();
        Assert.NotNull(dto);
        Assert.True(dto!.IsExternal);
        Assert.Equal(latBranchId, dto.ExternalBranchId);

        // تظهر لرئيس قسم الفرع المناب (اللاذقية)، ولا تظهر لرئيس قسم الفرع المنيب.
        var damHead = _factory.AuthorizedClient("head1");
        var damPending = await (await damHead.GetAsync("/api/delegations/pending")).Content.ReadFromJsonAsync<List<DelegationDto>>();
        Assert.NotNull(damPending);
        Assert.DoesNotContain(damPending, d => d.Id == dto.Id);

        var latHead = await _factory.CreateUserAsync(NewName("head_external"), UserRole.Head, branchId: latBranchId);
        var latClient = _factory.AuthorizedClient(latHead.Username);
        var latPending = await (await latClient.GetAsync("/api/delegations/pending")).Content.ReadFromJsonAsync<List<DelegationDto>>();
        Assert.NotNull(latPending);
        Assert.Contains(latPending, d => d.Id == dto.Id);

        // يعتمدها رئيس قسم اللاذقية ويكلف محاميًا ضمن فرعه؛ يُنشأ الملف المناب في فرع اللاذقية.
        var latLawyer = await _factory.CreateUserAsync(NewName("lawyer_external"), UserRole.Lawyer, branchId: latBranchId);
        var assign = await latClient.PostAsJsonAsync($"/api/delegations/{dto.Id}/assign", new { assignedLawyerId = latLawyer.Id });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var assigned = await assign.Content.ReadFromJsonAsync<DelegationDto>();
        Assert.NotNull(assigned);
        Assert.True(assigned!.TargetDocumentId.HasValue);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var target = db.Documents.Single(d => d.Id == assigned.TargetDocumentId!.Value);
        Assert.Equal(latBranchId, target.BranchId);
        Assert.Equal("فرع اللاذقية", target.BranchName);
    }

    [Fact]
    public async Task ConsiderExecutedByDelegation_AfterCompletion_ClosesPartialAndPersistsTransfer()
    {
        var (docId, assetId) = await CreateSourceWithAssetAsync();
        var lawyer1 = _factory.AuthorizedClient("lawyer1");
        var created = await (await lawyer1.PostAsJsonAsync($"/api/documents/{docId}/delegations", SampleBody(assetId)))
            .Content.ReadFromJsonAsync<DelegationDto>();
        Assert.NotNull(created);

        var head = _factory.AuthorizedClient("head1");
        var targetLawyer = await _factory.CreateUserAsync(NewName("lawyer_cons"), UserRole.Lawyer, branchId: await BranchIdAsync("DAM"));
        await head.PostAsJsonAsync($"/api/delegations/{created!.Id}/assign", new { assignedLawyerId = targetLawyer.Id });
        var targetClient = _factory.AuthorizedClient(targetLawyer.Username);
        var regResponse = await targetClient.PostAsJsonAsync($"/api/delegations/{created.Id}/register",
            new { fileNumber = "892", fileYear = "2026", fileRegistrationDate = "5/8/2026" });
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);
        var registered = await regResponse.Content.ReadFromJsonAsync<DelegationDto>();
        var complete = await targetClient.PostAsJsonAsync($"/api/delegations/{created.Id}/complete",
            new
            {
                returnDate = "10/8/2026",
                sales = new[] { new { delegationAssetId = registered!.Assets.Single().Id, salePrice = 750_000m } },
                forcedExecutionDate = "12/8/2026",
                saleCoversFullDebt = false,
            });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completed = await complete.Content.ReadFromJsonAsync<DelegationDto>();
        Assert.NotNull(completed);
        Assert.Equal(false, completed!.SaleCoversFullDebt);

        // المنيب فُعّل تلقائيًا «منفذ جبريا — منفذ جزئيا» حتى يعتبره محاميه منفذًا كاملًا بهذا البيع.
        var before = await (await lawyer1.GetAsync($"/api/documents/{docId}")).Content.ReadFromJsonAsync<DocumentResponse>();
        Assert.Equal("منفذ جبريا", before!.ExecStatus);
        Assert.Equal("منفذ جزئيا", before.ExecSubStatus);

        var missingDate = await lawyer1.PostAsJsonAsync($"/api/documents/{docId}/consider-executed-by-delegation",
            new { fields = new { forcedTransferNoticeNumber = "77/2026" } });
        Assert.Equal(HttpStatusCode.BadRequest, missingDate.StatusCode);

        var consider = await lawyer1.PostAsJsonAsync($"/api/documents/{docId}/consider-executed-by-delegation",
            new { fields = new { forcedTransferDate = "١٥/٨/٢٠٢٦", forcedTransferNoticeNumber = "77/2026" } });
        Assert.Equal(HttpStatusCode.OK, consider.StatusCode);

        var after = await (await lawyer1.GetAsync($"/api/documents/{docId}")).Content.ReadFromJsonAsync<DocumentResponse>();
        Assert.Equal("منفذ كاملا", after!.ExecSubStatus);
        Assert.Equal("2026-08-15", after.ForcibleTransferDate);
        Assert.Equal("77/2026", after.ForcibleTransferNoticeNumber);

        // وقعة «منفذ جبريا» كاملة (بتحويل البدل) سُجِّلت في «وقوعات الملف» للعرض.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var occurrence = db.DocumentOccurrences.OrderByDescending(o => o.Id)
            .First(o => o.DocumentId == docId && o.OccurrenceType == OccurrenceTypeCatalog.Forcible);
        Assert.Contains("15/8/2026", occurrence.Details);
        Assert.Contains("77/2026", occurrence.Details);
    }

    private async Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return db.Branches.Single(b => b.Code == code).Id;
    }

    [Fact]
    public async Task ListForDocument_OtherLawyersFile_IsForbidden()
    {
        // تشعبات ملف محامٍ آخر لا تُكشف لأي محامٍ آخر (نفس قاعدة الوصول على صفحة الملف).
        var (docId, _) = await CreateSourceWithAssetAsync();
        var otherLawyer = await _factory.CreateUserAsync(NewName("lawyer_stranger"), UserRole.Lawyer, branchId: await BranchIdAsync("DAM"));

        var client = _factory.AuthorizedClient(otherLawyer.Username);
        var response = await client.GetAsync($"/api/documents/{docId}/delegations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListForDocument_UnknownFile_IsNotFound()
    {
        var client = _factory.AuthorizedClient("lawyer1");
        var response = await client.GetAsync("/api/documents/999999/delegations");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListForDocument_SourceBranchHead_IsAllowed()
    {
        var (docId, _) = await CreateSourceWithAssetAsync();

        var head = _factory.AuthorizedClient("head1");
        var response = await head.GetAsync($"/api/documents/{docId}/delegations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListForDocument_HeadOfAnotherBranch_IsForbidden()
    {
        var (docId, _) = await CreateSourceWithAssetAsync();
        var latHead = await _factory.CreateUserAsync(NewName("head_other"), UserRole.Head, branchId: await BranchIdAsync("LAT"));

        var client = _factory.AuthorizedClient(latHead.Username);
        var response = await client.GetAsync($"/api/documents/{docId}/delegations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
