using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

public class BranchManagementIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public BranchManagementIntegrationTests(ApiFactory factory) => _factory = factory;

    private Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.Branches.Single(b => b.Code == code).Id);
    }

    [Fact]
    public async Task AnyRole_ListsBranches()
    {
        foreach (var username in new[] { "admin", "manager", "head1", "lawyer1" })
        {
            var client = _factory.AuthorizedClient(username);
            var response = await client.GetAsync("/api/branches");
            response.EnsureSuccessStatusCode();
            var branches = await response.Content.ReadFromJsonAsync<List<BranchDto>>();

            Assert.NotNull(branches);
            Assert.Contains(branches, b => b.Code == "DAM");
        }
    }

    [Fact]
    public async Task BranchManagement_NonAdminRoles_Forbidden()
    {
        foreach (var username in new[] { "manager", "head1", "lawyer1" })
        {
            var client = _factory.AuthorizedClient(username);
            var create = await client.PostAsJsonAsync("/api/branches", new
            {
                name = "فرع جديد",
                code = "NEW",
                address = (string?)null,
                phone = (string?)null,
            });
            Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

            var update = await client.PutAsJsonAsync($"/api/branches/{await BranchIdAsync("DAM")}", new
            {
                name = "الفرع الرئيسي - دمشق",
                code = "DAM",
                address = "دمشق",
                phone = (string?)null,
                isActive = true,
            });
            Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);

            var delete = await client.DeleteAsync($"/api/branches/{await BranchIdAsync("DAM")}");
            Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        }
    }

    [Fact]
    public async Task Admin_CreatesBranch_ReturnsCreatedWithDetails()
    {
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/branches", new
        {
            name = "فرع درعا",
            code = "DAR",
            address = "درعا",
            phone = "015123456",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var branch = await response.Content.ReadFromJsonAsync<BranchDto>();
        Assert.NotNull(branch);
        Assert.Equal("فرع درعا", branch.Name);
        Assert.Equal("DAR", branch.Code);
        Assert.True(branch.IsActive);
        Assert.Equal(0, branch.UserCount);
        Assert.Equal(0, branch.DocumentCount);

        var location = response.Headers.Location?.AbsolutePath;
        Assert.NotNull(location);
        Assert.Contains($"/api/branches/{branch.Id}", location);
    }

    [Fact]
    public async Task Admin_CreatesBranch_DuplicateCode_BadRequest()
    {
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/branches", new
        {
            name = "فرع مكرر",
            code = "DAM",
            address = (string?)null,
            phone = (string?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_UpdatesBranch_Deactivates()
    {
        var admin = _factory.AuthorizedClient("admin");
        var id = await BranchIdAsync("HMS");

        var response = await admin.PutAsJsonAsync($"/api/branches/{id}", new
        {
            name = "فرع حمص الجديد",
            code = "HMSX",
            address = "حمص الجديدة",
            phone = (string?)null,
            isActive = false,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var branch = await response.Content.ReadFromJsonAsync<BranchDto>();
        Assert.NotNull(branch);
        Assert.Equal("فرع حمص الجديد", branch.Name);
        Assert.Equal("HMSX", branch.Code);
        Assert.False(branch.IsActive);
    }

    [Fact]
    public async Task Admin_UpdatesBranch_NotFound()
    {
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PutAsJsonAsync("/api/branches/999999", new
        {
            name = "غير موجود",
            code = "XXX",
            address = (string?)null,
            phone = (string?)null,
            isActive = true,
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_DeletesUnusedBranch_NoContent()
    {
        var admin = _factory.AuthorizedClient("admin");
        var created = await admin.PostAsJsonAsync("/api/branches", new
        {
            name = "فرع القنيطرة",
            code = "QNT",
            address = (string?)null,
            phone = (string?)null,
        });
        var branch = await created.Content.ReadFromJsonAsync<BranchDto>();

        var delete = await admin.DeleteAsync($"/api/branches/{branch!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.Null(db.Branches.Find(branch.Id));
    }

    [Fact]
    public async Task Admin_DeletesBranchInUse_BadRequest()
    {
        // فرع دمشق فيه مستخدمون (head1/lawyer1) — يُرفض الحذف.
        var admin = _factory.AuthorizedClient("admin");
        var id = await BranchIdAsync("DAM");

        var response = await admin.DeleteAsync($"/api/branches/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.NotNull(db.Branches.Find(id));
    }

    [Fact]
    public async Task Admin_DeletesBranch_NotFound()
    {
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.DeleteAsync("/api/branches/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
