using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// عقد الأساس لدور رئيس الشعبة (المرحلة 1أ): قيمة التعداد + رفض الأدوار الرقمية
/// غير المعرفة في `ParseRole`. بلا أي تغيير سلوكي للأدوار القائمة.
/// </summary>
public class SubHeadFoundationTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IUserManagementService _service;

    public SubHeadFoundationTests()
    {
        _db = TestDb.Create();
        var users = new UserRepository(_db);
        var branches = new Repository<Branch>(_db);
        var sections = new Repository<Section>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = new UserManagementService(
            users, branches, sections,
            new Repository<ExecutionCircuit>(_db),
            new Repository<HeadSuccession>(_db),
            new Repository<Correspondence>(_db),
            new Repository<CorrespondenceMessage>(_db),
            new Repository<HeadAlert>(_db),
            new Repository<HeadAlertRecipient>(_db),
            new DocumentRepository(_db),
            uow, new PasswordHasher(), tx, new FakeAuditLogger());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void SubHead_HasValueSix_AndIsDefined()
    {
        Assert.Equal(6, (int)UserRole.SubHead);
        Assert.True(Enum.IsDefined(UserRole.SubHead));
    }

    [Theory]
    [InlineData("99")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task CreateUser_UndefinedNumericRole_Throws(string role)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateUserAsync(new CreateUserRequest("x", "مستخدم", role, null, "123456"), "admin"));
    }

    [Theory]
    [InlineData("subhead")]
    [InlineData("SubHead")]
    [InlineData("6")]
    public async Task CreateUser_SubHeadRole_RequiresSection(string role)
    {
        // ربط الشعبة إلزامي منذ 2ج (§9.1): بلا شعبة يُرفض قبل أي كتابة.
        _db.Branches.Add(new Branch { Name = "حماة", Code = "HAM" });
        await _db.SaveChangesAsync();
        var branchId = _db.Branches.Single(b => b.Code == "HAM").Id;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateUserAsync(
                new CreateUserRequest("sub_tmp", "رئيس شعبة مؤقت", role, branchId, "123456"), "admin"));
        Assert.Contains("الشعبة إلزامية", ex.Message);
    }

    [Fact]
    public async Task CreateUser_SubHeadWithSection_Succeeds()
    {
        _db.Branches.Add(new Branch { Name = "حماة", Code = "HAM" });
        await _db.SaveChangesAsync();
        var branchId = _db.Branches.Single(b => b.Code == "HAM").Id;
        var section = new Section { BranchId = branchId, Name = "شعبة مصياف", NameNorm = "شعبة مصياف", IsActive = true };
        _db.Sections.Add(section);
        await _db.SaveChangesAsync();

        var user = await _service.CreateUserAsync(
            new CreateUserRequest("sub_tmp", "رئيس شعبة مؤقت", "subhead", branchId, "123456", section.Id), "admin");

        Assert.Equal("subhead", user.Role);
        Assert.Equal(branchId, user.BranchId);
        Assert.Equal(section.Id, user.SectionId);
        Assert.Equal("شعبة مصياف", user.SectionName);
    }

    [Fact]
    public async Task CreateUser_BranchlessSubHead_ThrowsBeforeTouchingDatabase()
    {
        // مغلق بإحكام منذ 1ب: رئيس الشعبة فرعي بالتصميم — ترفض الخدمة بلا فرع
        // برسالة واضحة قبل أي كتابة (وقيد القاعدة ظهرًا للكتابة المباشرة).
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateUserAsync(
                new CreateUserRequest("sub_non", "رئيس شعبة شاذ", "subhead", null, "123456"), "admin"));
    }
}
