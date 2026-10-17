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

    [Theory]
    [InlineData("short")]
    [InlineData("  short  ")]
    [InlineData("password123")]
    public async Task BootstrapAsync_WithoutUsersAndWeakPassword_Throws(string password)
    {
        using var db = TestDb.Create();
        var hasher = new PasswordHasher();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DbSeeder.BootstrapAsync(db, hasher, password));
        Assert.Contains("كلمة المرور", ex.Message);
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task SeedAsync_WhenEmpty_CreatesBranchesAndDevUsers()
    {
        using var db = TestDb.Create();
        var hasher = new PasswordHasher();

        // العقد الجديد (RF-006): كلمة صريحة تُستخدَم كما هي (كانت "123456" ثابتة ملتزمة قبل الإصلاح).
        var used = await DbSeeder.SeedAsync(db, hasher, devPassword: "123456");

        Assert.Equal("123456", used);
        Assert.Equal(5, db.Branches.Count());
        Assert.Equal(
            new[] { "admin", "head1", "lawyer1", "manager" },
            db.Users.Select(u => u.Username).OrderBy(x => x).ToArray());
        Assert.All(db.Users, u => Assert.True(hasher.Verify("123456", u.PasswordHash)));
        // الفروع الخمسة تُربط بلا مبالغة: مسار التطوير وحسب (لا يُستخدم في الإنتاج).
        Assert.Equal(2, db.Users.Count(u => u.BranchId != null));
    }

    [Fact]
    public async Task SeedAsync_WhenUsersExist_DoesNotDuplicateBranchesOrUsers()
    {
        using var db = TestDb.Create();
        var hasher = new PasswordHasher();
        db.Users.Add(new User { Username = "custom", Role = UserRole.Manager, PasswordHash = hasher.Hash("x") });
        await db.SaveChangesAsync();

        await DbSeeder.SeedAsync(db, hasher);
        await DbSeeder.SeedAsync(db, hasher);

        Assert.Equal(5, db.Branches.Count());
        var user = Assert.Single(db.Users);
        Assert.Equal("custom", user.Username);
    }
}

/// <summary>
/// حارس عقد الطباعة (RF-006): سطر كلمة البذر يُطبع عند إنشاء الحسابات فعليًا فقط —
/// إقلاع التطوير على قاعدة مسبوكة لا يُطبع كلمة جديدة لا تصح لشيء (مراجعة 2026-10-17).
/// الكونسول حالة مشتركة عالمية — مجموعة تتابعية لتجنب التزاحم مع طبقات سجلات أخرى.
/// </summary>
[Collection("Sequential")]
public class DbSeederPrintTests
{
    [Fact]
    public async Task SeedAsync_PrintsPasswordOnlyWhenAccountsActuallyCreated()
    {
        using var db = TestDb.Create();
        var hasher = new PasswordHasher();
        var originalOut = Console.Out;
        try
        {
            var firstCapture = new StringWriter();
            Console.SetOut(firstCapture);

            var seedPassword = await DbSeeder.SeedAsync(db, hasher);
            var firstOutput = firstCapture.ToString();
            Assert.Contains("[DbSeeder] Development accounts seeded. One-time password:", firstOutput);
            Assert.Contains(seedPassword, firstOutput);

            var secondCapture = new StringWriter();
            Console.SetOut(secondCapture);

            await DbSeeder.SeedAsync(db, hasher);
            Assert.DoesNotContain("One-time password", secondCapture.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task SeedAsync_WithExplicitDevPassword_PrintsNothing()
    {
        using var db = TestDb.Create();
        var hasher = new PasswordHasher();
        var originalOut = Console.Out;
        try
        {
            var capture = new StringWriter();
            Console.SetOut(capture);

            var used = await DbSeeder.SeedAsync(db, hasher, devPassword: "123456");
            Assert.Equal("123456", used);
            Assert.DoesNotContain("One-time password", capture.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
