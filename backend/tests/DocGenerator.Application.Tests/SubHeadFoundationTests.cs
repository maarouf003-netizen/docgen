using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Users;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// عقد الأساس لدور رئيس الشعبة (المرحلة 1أ): قيمة التعداد + رفض الأدوار الرقمية
/// غير المعرفة في `ParseRole`. بلا أي تغيير سلوكي للأدوار القائمة.
/// </summary>
public class SubHeadFoundationTests : PostgresDbTest
{
    private readonly ICommandHandler<CreateUserCommand, UserListItemDto> _createUser;

    public SubHeadFoundationTests()
    {
        var users = new UserRepository(_db);
        var branches = new Repository<Branch>(_db);
        var sections = new Repository<Section>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        var service = new UserManagementService(
            users, branches, sections,
            new Repository<HeadSuccession>(_db),
            new Repository<Correspondence>(_db),
            new Repository<CorrespondenceMessage>(_db),
            new DocumentRepository(_db),
            new HeadAlertSuccessionService(
                new HeadAlertRepository(_db),
                new HeadAlertOutboxStore(_db),
                uow,
                new FakeAuditLogger(),
                new DbExceptionClassifier(),
                tx),
            uow, new FakeAuditLogger());

        _createUser = new CreateUserCommandHandler(
            (IUserManagementUserWriteRules)service, users, new Repository<HeadSuccession>(_db), uow,
            new PasswordHasher(), tx, new FakeAuditLogger(),
            Array.Empty<IPipelineBehavior<CreateUserCommand, UserListItemDto>>());
    }


    [PostgresFact]
    public void SubHead_HasValueSix_AndIsDefined()
    {
        Assert.Equal(6, (int)UserRole.SubHead);
        Assert.True(Enum.IsDefined(UserRole.SubHead));
    }

    [PostgresTheory]
    [InlineData("99")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task CreateUser_UndefinedNumericRole_Throws(string role)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _createUser.CreateUserAsync(new CreateUserRequest("x", "مستخدم", role, null, "TestPass1234"), "admin"));
    }

    [PostgresTheory]
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
            _createUser.CreateUserAsync(
                new CreateUserRequest("sub_tmp", "رئيس شعبة مؤقت", role, branchId, "TestPass1234"), "admin"));
        Assert.Contains("الشعبة إلزامية", ex.Message);
    }

    [PostgresFact]
    public async Task CreateUser_SubHeadWithSection_Succeeds()
    {
        _db.Branches.Add(new Branch { Name = "حماة", Code = "HAM" });
        await _db.SaveChangesAsync();
        var branchId = _db.Branches.Single(b => b.Code == "HAM").Id;
        var section = new Section { BranchId = branchId, Name = "شعبة مصياف", NameNorm = "شعبة مصياف", IsActive = true };
        _db.Sections.Add(section);
        await _db.SaveChangesAsync();

        var user = await _createUser.CreateUserAsync(
            new CreateUserRequest("sub_tmp", "رئيس شعبة مؤقت", "subhead", branchId, "TestPass1234", section.Id), "admin");

        Assert.Equal("subhead", user.Role);
        Assert.Equal(branchId, user.BranchId);
        Assert.Equal(section.Id, user.SectionId);
        Assert.Equal("شعبة مصياف", user.SectionName);
    }

    [PostgresFact]
    public async Task CreateUser_BranchlessSubHead_ThrowsBeforeTouchingDatabase()
    {
        // مغلق بإحكام منذ 1ب: رئيس الشعبة فرعي بالتصميم — ترفض الخدمة بلا فرع
        // برسالة واضحة قبل أي كتابة (وقيد القاعدة ظهرًا للكتابة المباشرة).
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _createUser.CreateUserAsync(
                new CreateUserRequest("sub_non", "رئيس شعبة شاذ", "subhead", null, "TestPass1234"), "admin"));
    }
}
