using System.Net;
using System.Net.Http.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// تكامل الشعب (المرحلة 2أ): قراءة للجميع المصادَّق، وكتابة للمشرف والمدير
/// (قرار §2.15) — مع عقود `400/404/409` المعتمدة.
///
public sealed class SectionsIntegrationTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return (await db.Branches.SingleAsync(b => b.Code == code)).Id;
    }

    private async Task<int> CreateSectionAsync(string name, int? branchId = null)
    {
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/sections", new
        {
            branchId = branchId ?? await BranchIdAsync("DAM"),
            name,
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<SectionDto>();
        return created!.Id;
    }

    private async Task AttachCircuitAsync(int sectionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var branchId = await BranchIdAsync("DAM");
        var headId = (await db.Users.SingleAsync(u => u.Username == "head1")).Id;
        db.ExecutionCircuits.Add(new ExecutionCircuit
        {
            BranchId = branchId,
            SectionId = sectionId,
            Name = "دائرة الشعب",
            NameNorm = "دائرة الشعب",
            IsActive = true,
            CreatedById = headId,
        });
        await db.SaveChangesAsync();
    }

    private sealed record SectionDto(int Id, int BranchId, string? BranchName, string Name, bool IsActive, int CircuitCount, string? HeadName);

    [Theory]
    [InlineData("lawyer1")]
    [InlineData("head1")]
    [InlineData("manager")]
    [InlineData("admin")]
    public async Task List_AllAuthenticatedRoles_Ok(string username)
    {
        var client = _factory.AuthorizedClient(username);
        var response = await client.GetAsync($"/api/sections?branchId={await BranchIdAsync("DAM")}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_MissingBranchId_BadRequest()
    {
        var client = _factory.AuthorizedClient("head1");
        var response = await client.GetAsync("/api/sections");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_UnknownBranch_BadRequest()
    {
        var client = _factory.AuthorizedClient("head1");
        var response = await client.GetAsync("/api/sections?branchId=999999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("lawyer1")]
    [InlineData("head1")]
    public async Task Create_NonManagerRoles_Forbidden(string username)
    {
        var client = _factory.AuthorizedClient(username);
        var response = await client.PostAsJsonAsync("/api/sections", new
        {
            branchId = await BranchIdAsync("DAM"),
            name = "شعبة ممنوعة",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("admin")]
    public async Task Create_ManagerAndAdmin_Created(string username)
    {
        var client = _factory.AuthorizedClient(username);
        var response = await client.PostAsJsonAsync("/api/sections", new
        {
            branchId = await BranchIdAsync("DAM"),
            name = "شعبة مصياف",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SectionDto>();
        Assert.NotNull(created);
        Assert.Equal("شعبة مصياف", created!.Name);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task Create_Duplicate_BadRequest()
    {
        // الفحص الخدمي المسبق يرفض التكرار `400`؛ وظهر القاعدة (`409`) لسباق
        // التزامن فقط — كلاهما بنفس الرسالة المعتمدة.
        await CreateSectionAsync("شعبة مصياف");
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/sections", new
        {
            branchId = await BranchIdAsync("DAM"),
            name = "شعبة مصياف",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("موجودة مسبقًا", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_BlankName_BadRequest()
    {
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/sections", new
        {
            branchId = await BranchIdAsync("DAM"),
            name = "   ",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_Missing_NotFound()
    {
        var client = _factory.AuthorizedClient("head1");
        var response = await client.GetAsync("/api/sections/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rename_Manager_Ok()
    {
        var id = await CreateSectionAsync("شعبة مصياف");
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.PutAsJsonAsync($"/api/sections/{id}/rename", new { name = "شعبة السلمية" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renamed = await response.Content.ReadFromJsonAsync<SectionDto>();
        Assert.Equal("شعبة السلمية", renamed!.Name);
    }

    [Fact]
    public async Task Rename_Head_Forbidden()
    {
        var id = await CreateSectionAsync("شعبة مصياف");
        var head = _factory.AuthorizedClient("head1");
        var response = await head.PutAsJsonAsync($"/api/sections/{id}/rename", new { name = "شعبة السلمية" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Rename_Missing_NotFound()
    {
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.PutAsJsonAsync("/api/sections/999999/rename", new { name = "اسم" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetActive_DeactivateWithCircuits_BadRequest()
    {
        var id = await CreateSectionAsync("شعبة مصياف");
        await AttachCircuitAsync(id);
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.PutAsJsonAsync($"/api/sections/{id}/active", new { isActive = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("انقل دوائرها", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delete_WithCircuits_BadRequest()
    {
        var id = await CreateSectionAsync("شعبة مصياف");
        await AttachCircuitAsync(id);
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.DeleteAsync($"/api/sections/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Empty_NoContent()
    {
        var id = await CreateSectionAsync("شعبة مصياف");
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.DeleteAsync($"/api/sections/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Missing_NotFound()
    {
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.DeleteAsync("/api/sections/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
