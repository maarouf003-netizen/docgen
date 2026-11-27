using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات حتمية لمصنّف أخطاء القيد (B3) على `PostgreSQL` وحده (‏`ADR-0001`):
/// رموز المزوّد المصطنعة تُصنَّف كما هي، وغيرها سالبًا. تعارض التفرّد/التزامن الحقيقي
/// من قاعدة حيّة مغطى في <see cref="DbExceptionClassifierDatabaseTests"/>.
/// </summary>
public class DbExceptionClassifierTests
{
    private readonly IDbExceptionClassifier _classifier = new DbExceptionClassifier();

    private static Npgsql.PostgresException Postgres(string sqlState, string? constraintName = null) =>
        new("provider error", "ERROR", "ERROR", sqlState, constraintName: constraintName);

    [Fact]
    public void IsUniqueViolation_PostgresUnique_True()
    {
        var ex = new DbUpdateException("فشل حفظ", Postgres("23505", "IX_Users_Username"));
        Assert.True(_classifier.IsUniqueViolation(ex));
    }

    [Theory]
    [InlineData("23503")] // foreign_key_violation
    [InlineData("23502")] // not_null_violation
    [InlineData("23514")] // check_violation
    public void IsUniqueViolation_OtherConstraintCodes_False(string sqlState)
    {
        // رمز 23xxx غير 23505 (FK/NOT NULL/CHECK) لا يجوز أن يُصنَّف تفردًا وإلا تحوّل
        // عطل بيانات حقيقي إلى 409 «قيمة مكررة» في المعالج العام.
        var ex = new DbUpdateException("فشل حفظ", Postgres(sqlState));
        Assert.False(_classifier.IsUniqueViolation(ex));
    }

    [Fact]
    public void IsUniqueViolation_UnknownSqlState_False()
    {
        // Fail-safe: خطأ مزوّد برمز لا نعرفه (أو بلا 23505) يبقى 500 لا 409.
        var ex = new DbUpdateException("فشل حفظ", Postgres("XX000", "IX_Users_Username"));
        Assert.False(_classifier.IsUniqueViolation(ex));
    }

    [Fact]
    public void IsUniqueViolation_PlainException_False()
    {
        Assert.False(_classifier.IsUniqueViolation(new InvalidOperationException("عطل عام")));
    }

    [Fact]
    public void IsForeignKeyViolation_PostgresForeignKey_True()
    {
        var ex = new DbUpdateException("فشل حفظ", Postgres("23503", "FK_X"));
        Assert.True(_classifier.IsForeignKeyViolation(ex));
        Assert.False(_classifier.IsForeignKeyViolation(
            new DbUpdateException("فشل حفظ", Postgres("23505"))));
    }

    [Fact]
    public void IsMissingDocumentCircuitViolation_PostgresCheck_True()
    {
        var postgres = new Npgsql.PostgresException(
            "new row violates check constraint",
            "ERROR",
            "ERROR",
            "23514",
            constraintName: "CK_Documents_FileNumberRequiresCircuit");

        Assert.True(_classifier.IsMissingDocumentCircuitViolation(
            new DbUpdateException("فشل حفظ", postgres)));
    }

    [Fact]
    public void IsMissingDocumentCircuitViolation_OtherCheck_False()
    {
        var postgres = new Npgsql.PostgresException(
            "new row violates check constraint",
            "ERROR",
            "ERROR",
            "23514",
            constraintName: "CK_HeadAlerts_Kind_NotEmpty");

        Assert.False(_classifier.IsMissingDocumentCircuitViolation(
            new DbUpdateException("فشل حفظ", postgres)));
    }

    [Fact]
    public void IsAmountConstraintViolation_DocumentsAmountCheck_True()
    {
        Assert.True(_classifier.IsAmountConstraintViolation(
            new DbUpdateException("فشل حفظ", Postgres("23514", "CK_Documents_AmountNumeric_NonNegative"))));
        Assert.True(_classifier.IsAmountConstraintViolation(
            new DbUpdateException("فشل حفظ", Postgres("22003"))));
    }

    [Fact]
    public void IsAmountConstraintViolation_VersionOrOtherTable_False()
    {
        // تقييد التصنيف على قيود مبالغ Documents يمنع تحويل أخطاء الإصدار الداخلية إلى 400 مالي.
        Assert.False(_classifier.IsAmountConstraintViolation(
            new DbUpdateException("فشل حفظ", Postgres("23514", "CK_Documents_VersionNonNegative"))));
        Assert.False(_classifier.IsAmountConstraintViolation(
            new DbUpdateException("فشل حفظ", Postgres("23514", "CK_Delegations_Amount_NonNegative"))));
    }

    [Fact]
    public void IsConcurrencyViolation_PlainException_False()
    {
        Assert.False(_classifier.IsConcurrencyViolation(new InvalidOperationException("عطل عام")));
        Assert.False(_classifier.IsConcurrencyViolation(
            new DbUpdateException("فشل حفظ", new InvalidOperationException("سبب داخلي"))));
    }

    [Fact]
    public void IsConcurrencyViolation_SerializationAndDeadlock_True()
    {
        // انشغال/تنازع التخزين المتزامن: كتابتان حقيقيتان تتصادمان خارج `WHERE` الرمز
        // (`serialization_failure`/`deadlock_detected`) — تُترجمان 409 ودية لا 500 خام.
        Assert.True(_classifier.IsConcurrencyViolation(
            new DbUpdateException("فشل حفظ", Postgres("40001"))));
        Assert.True(_classifier.IsConcurrencyViolation(Postgres("40P01")));
        Assert.False(_classifier.IsConcurrencyViolation(Postgres("23505")));
    }
}

/// <summary>
/// نصف مسار الترجمة على قاعدة <c>PostgreSQL</c> حيّة: تعارض فريد حقيقي من المزود
/// (‏`23505`) وتعارض تزامن حقيقي (‏`WHERE Version`). النصف الآخر (موضع الالتقاط حول
/// حفظ الحجوزات) مثبت بمراجعة الموضع وجولات السلامة في `DelegationConcurrencyTests`.
/// </summary>
public class DbExceptionClassifierDatabaseTests : PostgresDbTest
{
    private readonly IDbExceptionClassifier _classifier = new DbExceptionClassifier();

    private async Task<(int SourceId, int DelegationId)> SeedAsync()
    {
        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        var lawyer = new User
        {
            Username = "classifier_lawyer",
            FullName = "محامي المصنف",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        _db.Users.Add(lawyer);
        await _db.SaveChangesAsync();

        var source = new Document
        {
            CreatedById = lawyer.Id,
            BranchId = branch.Id,
            IsDraft = false,
        };
        _db.Documents.Add(source);
        await _db.SaveChangesAsync();

        var delegation = new DocumentDelegation
        {
            SourceDocumentId = source.Id,
            CreatedById = lawyer.Id,
            DelegatedCourt = "دائرة تنفيذ حلب",
            Status = DelegationStatusCatalog.PendingHead,
        };
        _db.DocumentDelegations.Add(delegation);
        await _db.SaveChangesAsync();

        return (source.Id, delegation.Id);
    }

    [PostgresFact]
    public async Task IsUniqueViolation_DuplicateReservation_True()
    {
        // تعارض فريد حقيقي من المزود (لا محاكاة): صفّان بنفس (المنيب + الأصل).
        var (sourceId, delegationId) = await SeedAsync();
        _db.DelegationAssetReservations.Add(new DelegationAssetReservation
        {
            DelegationId = delegationId,
            SourceDocumentId = sourceId,
            AssetId = 5,
        });
        await _db.SaveChangesAsync();

        _db.DelegationAssetReservations.Add(new DelegationAssetReservation
        {
            DelegationId = delegationId,
            SourceDocumentId = sourceId,
            AssetId = 5,
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());

        Assert.True(_classifier.IsUniqueViolation(ex));
    }

    [PostgresFact]
    public async Task IsConcurrencyViolation_StaleVersionSave_True()
    {
        // تعارض تزامن حقيقي على مستوى `EF` (RF-010 — لا محاكاة لمصنّف):
        // قيمة أصلية قديمة لا تطابق أي صف فيفشل `WHERE Version` (السباق الحقيقي
        // عبر سياقين مغطى تكامليًا في `RF010ConcurrencyTests`).
        await SeedAsync();
        var staleId = _db.Documents.Select(d => d.Id).First();

        var stale = await _db.Documents.FindAsync(staleId);
        stale!.BorrowerName = "المتأخر";
        stale.Version++;
        _db.Entry(stale).Property(d => d.Version).OriginalValue = -1;
        var ex = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => _db.SaveChangesAsync());

        Assert.True(_classifier.IsConcurrencyViolation(ex));
        Assert.False(_classifier.IsUniqueViolation(ex));
    }
}
