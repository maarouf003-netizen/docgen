using DocGenerator.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public static class TestDb
{
    public static Infrastructure.Persistence.DocGeneratorDbContext Create()
    {
        var options = new DbContextOptionsBuilder<Infrastructure.Persistence.DocGeneratorDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var db = new Infrastructure.Persistence.DocGeneratorDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_Succeeds()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("123456");
        Assert.True(hasher.Verify("123456", hash));
        Assert.False(hasher.Verify("wrong", hash));
    }

    [Fact]
    public void Verify_LegacySha256Hash_Succeeds()
    {
        // صيغة قديمة بدون ملح (64 حرف hex)
        var legacy = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("123456"))).ToLowerInvariant();
        var hasher = new PasswordHasher();
        Assert.True(hasher.Verify("123456", legacy));
    }

    [Fact]
    public void Verify_EmptyOrGarbage_ReturnsFalse()
    {
        var hasher = new PasswordHasher();
        Assert.False(hasher.Verify("x", ""));
        Assert.False(hasher.Verify("x", "garbage"));
        Assert.False(hasher.Verify("x", "abc:def"));
    }
}

public class NumberToWordsTests
{
    [Theory]
    [InlineData(0, "صفر")]
    [InlineData(5, "خمسة")]
    [InlineData(12, "اثنا عشر")]
    [InlineData(45, "خمسة وأربعون")]
    [InlineData(130, "مائة وثلاثون")]
    [InlineData(1500, "ألف وخمسمائة")]
    public void Convert_Various_MatchesExpected(long value, string expected)
    {
        Assert.Equal(expected, NumberToWords.Convert(value));
    }

    [Fact]
    public void Convert_LargeNumber_ProducesNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(NumberToWords.Convert(1_234_567)));
    }
}
