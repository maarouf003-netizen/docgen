using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

public class DbSeederTests
{
    [Fact]
    public async Task BootstrapAsync_WithExistingUsers_DoesNothing()
    {
        using var db = TestDb.Create();
        var hasher = new PasswordHasher();
        db.Users.Add(new User { Username = "existing", Role = UserRole.Admin, PasswordHash = hasher.Hash("x") });
        await db.SaveChangesAsync();

        await DbSeeder.BootstrapAsync(db, hasher, "secret-admin-pass");

        var user = Assert.Single(db.Users);
        Assert.Equal("existing", user.Username);
    }

    [Fact]
    public async Task BootstrapAsync_WithoutUsersAndWithPassword_CreatesAdmin()
    {
        using var db = TestDb.Create();
        var hasher = new PasswordHasher();

        await DbSeeder.BootstrapAsync(db, hasher, "strong-admin-pass");

        var admin = Assert.Single(db.Users);
        Assert.Equal("admin", admin.Username);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.True(hasher.Verify("strong-admin-pass", admin.PasswordHash));
    }

    [Fact]
    public async Task BootstrapAsync_WithoutUsersAndWithoutPassword_Throws()
    {
        using var db = TestDb.Create();
        var hasher = new PasswordHasher();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DbSeeder.BootstrapAsync(db, hasher, null));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public async Task BootstrapAsync_WithoutUsersAndBlankPassword_Throws(string? password)
    {
        using var db = TestDb.Create();
        var hasher = new PasswordHasher();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DbSeeder.BootstrapAsync(db, hasher, password));
    }
}
