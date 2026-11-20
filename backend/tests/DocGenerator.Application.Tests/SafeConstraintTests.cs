using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace DocGenerator.Application.Tests;

/// <summary>
/// P0-07 — القيود الآمنة المضافة بعد P0-06:
/// (1) كل كيان قابل للتحرير يحمل قيد قاعدة <c>[Version] &gt;= 0</c>؛
/// (2) فهرس <c>(BranchId, SectionId)</c> على المستخدمين موجود وغير فريد؛
/// (3) الكتابة الخام السالبة تُرفض من القاعدة نفسها لا من الخدمة.
/// </summary>
public class SafeConstraintTests : PostgresDbTest
{
    [PostgresTheory]
    [InlineData(typeof(Document))]
    [InlineData(typeof(DocumentAppeal))]
    [InlineData(typeof(DocumentDelegation))]
    [InlineData(typeof(ExecutionCircuit))]
    [InlineData(typeof(User))]
    [InlineData(typeof(PublicEntity))]
    [InlineData(typeof(PublicEntityGroup))]
    [InlineData(typeof(Correspondence))]
    [InlineData(typeof(Section))]
    [InlineData(typeof(Branch))]
    [InlineData(typeof(ReviewLetter))]
    [InlineData(typeof(ReviewLetterMessage))]
    [InlineData(typeof(PersonalReminder))]
    [InlineData(typeof(ForumMessage))]
    [InlineData(typeof(ExecutionAction))]
    [InlineData(typeof(DocumentIntervention))]
    public void EveryVersionedEntity_HasNonNegativeVersionCheck(Type entityType)
    {
        // القيود لا تُحفظ في النموذج المحسّن للقراءة وقت التشغيل — تُقرأ من نموذج التصميم.
        var model = _db.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(entityType);
        Assert.NotNull(entity);

        var expected = $"CK_{entity.GetTableName()}_VersionNonNegative";
        var names = entity.GetCheckConstraints().Select(c => c.Name).ToList();
        Assert.True(names.Contains(expected), $"expected {expected}; actual: {string.Join("|", names)}");
    }

    [PostgresFact]
    public void Users_HaveBranchSectionCompositeIndex()
    {
        var model = _db.GetService<IDesignTimeModel>().Model;
        var user = model.FindEntityType(typeof(User))!;

        var index = user.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["BranchId", "SectionId"]));

        Assert.False(index.IsUnique);
    }

    [PostgresFact]
    public async Task RawNegativeVersion_IsRejectedByDatabase()
    {
        var branch = new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" };
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        var ex = await ExecuteRawAsync(
            "UPDATE \"Branches\" SET \"Version\" = -1 WHERE \"Id\" = @id", branch.Id);

        Assert.Equal("23514", ex.SqlState);
        Assert.Equal("CK_Branches_VersionNonNegative", ex.ConstraintName);
    }

    private static readonly string[] AmountColumns =
    [
        "AmountNumeric", "Amount2Numeric", "Amount3Numeric",
        "InclusionAmountNumeric", "InclusionAmount2Numeric", "InclusionAmount3Numeric",
        "CollectedAmount", "CollectedAmount2", "CollectedAmount3",
        "ExecutedRequiredAmount", "ExecutedRequiredAmount2", "ExecutedRequiredAmount3",
        "ExecutedPaidAmount", "ExecutedPaidAmount2", "ExecutedPaidAmount3",
    ];

    [PostgresFact]
    public void Documents_HaveNonNegativeAmountChecks()
    {
        var model = _db.GetService<IDesignTimeModel>().Model;
        var documents = model.FindEntityType(typeof(Document))!;
        var names = documents.GetCheckConstraints().Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var column in AmountColumns)
            Assert.Contains($"CK_Documents_{column}_NonNegative", names);
    }

    [PostgresFact]
    public async Task RawNegativeAmounts_AreRejectedByDatabase()
    {
        var branch = new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" };
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        var user = new User
        {
            Username = "amount_guard",
            FullName = "حارس المبالغ",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var document = new Document
        {
            CreatedById = user.Id,
            BranchId = branch.Id,
            DocumentType = "سند تنفيذ",
        };
        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        foreach (var column in AmountColumns)
        {
            var ex = await ExecuteRawAsync(
                $"UPDATE \"Documents\" SET \"{column}\" = -1 WHERE \"Id\" = @id", document.Id);

            Assert.Equal("23514", ex.SqlState);
            Assert.Equal($"CK_Documents_{column}_NonNegative", ex.ConstraintName);
        }
    }

    /// <summary>
    /// ينفّذ أمر تحديث خامًا ويتوقع رفض القاعدة، فيرجع استثناء المزوّد للتحقق من
    /// اسم القيد نفسه لا من نوع عام.
    /// </summary>
    private async Task<PostgresException> ExecuteRawAsync(string sql, int id)
    {
        var connection = _db.Database.GetDbConnection();
        await _db.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "id";
        parameter.Value = id;
        command.Parameters.Add(parameter);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        return ex;
    }
}
