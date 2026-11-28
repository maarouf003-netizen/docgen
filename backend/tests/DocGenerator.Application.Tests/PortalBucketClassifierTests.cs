using DocGenerator.Application.Common;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

/// <summary>
/// `W5/B3`: سلّة البوابة تطابق شارة العرض — طرف «منفذ عليه/إيداع» حالته من
/// `ExecutedStatus` لا `ExecStatus`، فلا «متداول» لملف شارته «منفذ».
/// </summary>
public sealed class PortalBucketClassifierTests
{
    [Theory]
    [InlineData(GeneralEntitySideCatalog.Executed, ExecutedStatusCatalog.Executed, ExecutionStatusCatalog.ExecutedFilter)]
    [InlineData(GeneralEntitySideCatalog.Deposit, ExecutedStatusCatalog.Executed, ExecutionStatusCatalog.ExecutedFilter)]
    [InlineData(GeneralEntitySideCatalog.Executed, "", ExecutionStatusCatalog.StateCirculating)]
    [InlineData(GeneralEntitySideCatalog.Executed, ExecutedStatusCatalog.StruckOff, ExecutionStatusCatalog.StateStruckOff)]
    public void ExecutedSide_IsClassifiedFromExecutedStatus(string side, string executedStatus, string expected)
    {
        var bucket = PortalBucketClassifier.Classify(
            isDraft: false, execStatus: "", execSubStatus: null,
            generalEntitySide: side, executedStatus: executedStatus);

        Assert.Equal(expected, bucket);
    }

    [Fact]
    public void ExecutedSide_TakesPrecedenceOverApplicantStatus()
    {
        // لا يجتمع الطرفان عمليًا، لكن الأسبقية تحمي بيانات قديمة متضاربة.
        var bucket = PortalBucketClassifier.Classify(
            isDraft: false, execStatus: ExecutionStatusCatalog.Deferred, execSubStatus: null,
            generalEntitySide: GeneralEntitySideCatalog.Deposit, executedStatus: ExecutedStatusCatalog.Executed);

        Assert.Equal(ExecutionStatusCatalog.ExecutedFilter, bucket);
    }

    [Theory]
    [InlineData(ExecutionStatusCatalog.ExecutedBySettlement, null, ExecutionStatusCatalog.ExecutedFilter)]
    [InlineData(ExecutionStatusCatalog.ExecutedForcibly, ExecutionStatusCatalog.SubPartiallyExecuted, ExecutionStatusCatalog.StateCirculating)]
    [InlineData(ExecutionStatusCatalog.Deferred, null, ExecutionStatusCatalog.Deferred)]
    [InlineData(ExecutionStatusCatalog.ReferredToStart, null, ExecutionStatusCatalog.ReferredToStart)]
    [InlineData("", null, ExecutionStatusCatalog.StateCirculating)]
    public void ApplicantSide_KeepsExistingClassification(string execStatus, string? sub, string expected)
    {
        var bucket = PortalBucketClassifier.Classify(
            isDraft: false, execStatus: execStatus, execSubStatus: sub,
            generalEntitySide: GeneralEntitySideCatalog.Applicant, executedStatus: null);

        Assert.Equal(expected, bucket);
    }

    [Fact]
    public void DraftWithoutStatus_IsDraftBucket()
    {
        var bucket = PortalBucketClassifier.Classify(
            isDraft: true, execStatus: "", execSubStatus: null,
            generalEntitySide: GeneralEntitySideCatalog.Applicant, executedStatus: null);

        Assert.Equal(ExecutionStatusCatalog.DraftFilter, bucket);
    }
}
