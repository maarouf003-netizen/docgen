using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبار إظهار RF-012 (SEC-014/INT-006): عطل تدقيق منتصف مسار الفشل يجب أن يتراجع
/// عن عدّاد الإخفاق أيضًا. يفشل قبل الإصلاح (العدّاد 1 رغم الاستثناء) ويخضر بعده (صفر).
/// بنية حقيقية: سياق EF + مستودع + وحدة عمل + عدّاء معاملات — والحاقن الوحيد المزيف هو التدقيق.
/// </summary>
public class RF012LoginAtomicityTests : IDisposable
{
    private sealed class ThrowingAuditLogger : IAuditLogger
    {
        public Task LogAsync(string? userName, string actionType, int? documentId = null,
            string? documentType = null, string? details = null, CancellationToken ct = default)
            => throw new InvalidOperationException("injected audit failure");

        public Task LogManyAsync(IReadOnlyList<AuditLogEntry> entries, CancellationToken ct = default)
            => throw new InvalidOperationException("injected audit failure");

        public Task LogDocumentChangeAsync(string? userName, string actionType, int documentId,
            string? documentType, string details, IReadOnlyList<DocumentFieldChange> changes,
            CancellationToken ct = default)
            => throw new InvalidOperationException("injected audit failure");
    }

    private readonly DocGeneratorDbContext _db;
    private readonly PasswordHasher _hasher = new();

    public RF012LoginAtomicityTests()
    {
        _db = TestDb.Create();
        _db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" });
        _db.Users.Add(new User
        {
            Username = "atomic",
            FullName = "ذري",
            Role = UserRole.Lawyer,
            BranchId = 1,
            IsActive = true,
            PasswordHash = _hasher.Hash("123456"),
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void Dispose() => _db.Dispose();

    private AuthService CreateService(IAuditLogger audit) => new(
        new UserRepository(_db),
        new UnitOfWork(_db),
        _hasher,
        new FakeTokenService(),
        new TransactionRunner(_db),
        audit,
        Options.Create(new LockoutOptions()));

    [Fact]
    public async Task WrongPassword_WithFailingAudit_RollsBackFailureCounter()
    {
        var service = CreateService(new ThrowingAuditLogger());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.LoginAsync(new LoginRequest("atomic", "wrong-pass")));

        _db.ChangeTracker.Clear();
        var user = _db.Users.Single(u => u.Username == "atomic");
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockoutEndUtc);
    }

    [Fact]
    public async Task WrongPassword_WithWorkingAudit_IncrementsCounterAndLogs()
    {
        var audit = new FakeAuditLogger();
        var service = CreateService(audit);

        var result = await service.LoginAsync(new LoginRequest("atomic", "wrong-pass"));

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        _db.ChangeTracker.Clear();
        Assert.Equal(1, _db.Users.Single(u => u.Username == "atomic").FailedLoginCount);
        Assert.Contains("login_failed", audit.Actions);
    }
}
