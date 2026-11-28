using DocGenerator.Domain.Enums;
using DocGenerator.TestSupport.Builders;

namespace DocGenerator.Application.Tests;

/// <summary>
/// `P1-03a` — builder المستند: افتراضيات حتمية لا تلوّث فترات 2026، والتعديلات صريحة.
/// </summary>
public class DocumentBuilderTests
{
    [Fact]
    public void Defaults_AreDeterministicAndOutsideCurrentPeriods()
    {
        var document = DocumentBuilder.Create().Build();
        var expectedLegacy = new DateTime(2020, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(1, document.BranchId);
        Assert.Equal(1, document.CreatedById);
        Assert.False(document.IsDraft);
        Assert.Equal(string.Empty, document.ExecStatus);
        Assert.Null(document.ExecSubStatus);
        Assert.Equal(string.Empty, document.ExecutedStatus);
        Assert.Equal(GeneralEntitySideCatalog.Applicant, document.GeneralEntitySide);
        Assert.Equal("مقترض", document.BorrowerName);
        Assert.Equal("عائلة", document.BorrowerFamily);
        Assert.Equal(0m, document.AmountNumeric);
        Assert.Null(document.CollectedAmount);
        Assert.Null(document.RegistrationDate);
        Assert.Null(document.FileReceiptDate);
        Assert.Equal(expectedLegacy, document.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, document.CreatedAt.Kind);
        Assert.True(document.CreatedAt.Year < 2026, "الافتراضي يجب ألا يلوّث فترات 2026");
    }

    [Fact]
    public void CreatedAt_NormalizesLocalAndUnspecifiedToUtc()
    {
        var local = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);
        var fromLocal = DocumentBuilder.Create().CreatedAt(local).Build().CreatedAt;
        Assert.Equal(DateTimeKind.Utc, fromLocal.Kind);
        Assert.Equal(local.ToUniversalTime(), fromLocal);

        var unspecified = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
        var fromUnspecified = DocumentBuilder.Create().CreatedAt(unspecified).Build().CreatedAt;
        Assert.Equal(DateTimeKind.Utc, fromUnspecified.Kind);
        Assert.Equal(unspecified.Ticks, fromUnspecified.Ticks);
    }

    [Fact]
    public void WithMethods_ApplyExplicitValues()
    {
        var createdAt = new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc);
        var document = DocumentBuilder.Create()
            .WithBranch(null)
            .WithCreatedBy(7)
            .Draft()
            .WithExecStatus(ExecutionStatusCatalog.Deferred)
            .WithExecSubStatus(ExecutionStatusCatalog.SubPartiallyExecuted)
            .WithExecutedStatus(ExecutedStatusCatalog.Executed)
            .WithSide(GeneralEntitySideCatalog.Executed)
            .Borrower("حدود", "التوقيت", "الأب")
            .Amount(700, "دولار أمريكي")
            .Collected(200)
            .CreatedAt(createdAt)
            .RegistrationDate(new DateTime(2026, 8, 1, 13, 45, 0))
            .FileReceiptDate(new DateTime(2026, 7, 1))
            .Contract(ContractTypeCatalog.Banking)
            .Build();

        Assert.Null(document.BranchId);
        Assert.Equal(7, document.CreatedById);
        Assert.True(document.IsDraft);
        Assert.Equal(ExecutionStatusCatalog.Deferred, document.ExecStatus);
        Assert.Equal(ExecutionStatusCatalog.SubPartiallyExecuted, document.ExecSubStatus);
        Assert.Equal(ExecutedStatusCatalog.Executed, document.ExecutedStatus);
        Assert.Equal(GeneralEntitySideCatalog.Executed, document.GeneralEntitySide);
        Assert.Equal("حدود", document.BorrowerName);
        Assert.Equal("التوقيت", document.BorrowerFamily);
        Assert.Equal("الأب", document.BorrowerFather);
        Assert.Equal(700m, document.AmountNumeric);
        Assert.Equal("دولار أمريكي", document.Currency);
        Assert.Equal(200m, document.CollectedAmount);
        Assert.Equal(createdAt, document.CreatedAt);
        // التاريخ يومي: الوقت يُطوى إلى منتصف الليل.
        Assert.Equal(new DateTime(2026, 8, 1), document.RegistrationDate?.DateParsed);
        Assert.Equal(new DateTime(2026, 7, 1), document.FileReceiptDate);
        Assert.Equal(ContractTypeCatalog.Banking, document.ContractTypeSelector);
    }
}
