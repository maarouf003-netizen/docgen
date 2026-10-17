using DocGenerator.Application.Services;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات إظهار RF-006 (SEC-004): كلمة البذر الملتزمة يجب ألا تعمل.
/// الأول يفشل قبل الإصلاح ويخضر بعده؛ البقية للعقد الجديد.
/// مجموعة تتابعية: أثنان هنا يُبذران قاعِدة فارغة بلا تجاوز فتُطبع كلمة البذر على
/// <c>Console</c> (الحالة العامة للعملية) — التتابع يمنع تزاحمها مع
/// <c>DbSeederPrintTests</c> التي تلتقط المخرجات (مراجعة 2026-10-17).
/// </summary>
[Collection("Sequential")]
public class RF006SeedPasswordTests
{
    [Fact]
    public async Task SeedAsync_CommittedDefaultPassword_NotUsable()
    {
        using var db = TestDb.Create();
        var hasher = new PasswordHasher();

        await DbSeeder.SeedAsync(db, hasher);

        Assert.All(db.Users, u => Assert.False(hasher.Verify("123456", u.PasswordHash)));
    }

    [Fact]
    public async Task SeedAsync_WithoutPassword_ReturnsRandomWorkingPassword()
    {
        using var db = TestDb.Create();
        var hasher = new PasswordHasher();

        var first = await DbSeeder.SeedAsync(db, hasher);

        Assert.NotNull(first);
        Assert.True(first.Length >= 12);
        Assert.All(db.Users, u => Assert.True(hasher.Verify(first, u.PasswordHash)));
    }

    [Fact]
    public async Task SeedAsync_TwoSeeds_UseDifferentPasswords()
    {
        using var first = TestDb.Create();
        using var second = TestDb.Create();
        var hasher = new PasswordHasher();

        var firstPassword = await DbSeeder.SeedAsync(first, hasher);
        var secondPassword = await DbSeeder.SeedAsync(second, hasher);

        Assert.NotEqual(firstPassword, secondPassword);
    }
}
