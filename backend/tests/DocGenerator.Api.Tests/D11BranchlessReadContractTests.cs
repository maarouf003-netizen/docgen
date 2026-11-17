using System.Net;
using System.Text.Json;
using DocGenerator.Api.Http;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// قرار `D11` الموحّد (2026-11-17): «رئيس بلا فرع» **قراءةً/عدًّا ⇒ 403**،
/// وكتابةً ⇒ `400` (الكتابة مغطاة في `AlertsIntegrationTests`).
///
/// <para>
/// الحالة غير قابلة للتجسيد عبر `EF` (قيد `CK_Users_BranchRequiredForBranchRoles`)،
/// فتُسكّ جلسة شاذة من نسخة كيان حقيقي بلا فرع بنفس نمط `ClientForUser` المستخدم في
/// حراس D11 القائمة — والتحقق من `TokenVersion` يبقى قائمًا.
/// </para>
/// </summary>
[Collection(ApiTestCollection.Name)]
public class D11BranchlessReadContractTests
{
    private readonly ApiFactory _factory;

    public D11BranchlessReadContractTests(ApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/appeals")]
    [InlineData("/api/delegations/pending")]
    [InlineData("/api/delegations/pending-count")]
    [InlineData("/api/review-letters/pending-count")]
    [InlineData("/api/dashboard")]
    public async Task BranchlessHead_ReadSurfaces_AreForbiddenWithBranchCode(string path)
    {
        var client = _factory.ClientForUser(await CreateBranchlessHeadAsync());

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ApiProblem.ContentType, response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(403, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(ApiErrorCodes.BranchRequired, json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()));
    }

    private async Task<User> CreateBranchlessHeadAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var head = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "head1");
        return new User
        {
            Id = head.Id,
            Username = head.Username,
            FullName = head.FullName,
            Role = UserRole.Head,
            BranchId = null,
            SectionId = head.SectionId,
            TokenVersion = head.TokenVersion,
            IsActive = true,
        };
    }
}
