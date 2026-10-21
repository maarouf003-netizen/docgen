using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حراسة كتالوج أنواع التنبيهات (الخطة §5 و§14.1): 39 رمزًا + الرمز المحجوز،
/// ومصفوفة الجمهور لكل رمز نظامي، ومجموعات العائلات، والرموز المتقاعدة.
/// </summary>
public class HeadAlertKindCatalogTests
{
    [Fact]
    public void All_ContainsExactlyThirtyNineCodes()
    {
        Assert.Equal(39, HeadAlertKindCatalog.All.Count);
    }

    [Fact]
    public void All_CodesAreDistinctLowercaseSnakeCase()
    {
        foreach (var kind in HeadAlertKindCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(kind));
            Assert.Equal(kind.ToLowerInvariant(), kind);
            Assert.DoesNotContain(' ', kind);
            Assert.DoesNotContain('-', kind);
        }
    }

    [Fact]
    public void Legacy_IsReservedAndOutsideAll()
    {
        Assert.Equal("legacy", HeadAlertKindCatalog.Legacy);
        Assert.DoesNotContain(HeadAlertKindCatalog.Legacy, HeadAlertKindCatalog.All);
        Assert.False(HeadAlertKindCatalog.IsValid(HeadAlertKindCatalog.Legacy));
        Assert.False(HeadAlertKindCatalog.IsWritable(HeadAlertKindCatalog.Legacy));
    }

    [Fact]
    public void IsValid_AcceptsOnlyCatalogCodes()
    {
        Assert.True(HeadAlertKindCatalog.IsValid(HeadAlertKindCatalog.DelegationPendingApproval));
        Assert.False(HeadAlertKindCatalog.IsValid("delegation_unknown"));
        Assert.False(HeadAlertKindCatalog.IsValid(null));
        Assert.False(HeadAlertKindCatalog.IsValid("  "));
    }

    [Fact]
    public void Retired_CodesAreNotWritable()
    {
        Assert.Equal(2, HeadAlertKindCatalog.Retired.Count);
        foreach (var retired in HeadAlertKindCatalog.Retired)
        {
            Assert.True(HeadAlertKindCatalog.IsValid(retired));
            Assert.False(HeadAlertKindCatalog.IsWritable(retired));
        }

        Assert.Equal(37, HeadAlertKindCatalog.All.Count - HeadAlertKindCatalog.Retired.Count);
    }

    [Fact]
    public void AllowedAudiences_EverySystemCodeHasNonEmptyAudience()
    {
        var systemCodes = HeadAlertKindCatalog.All
            .Where(k => !HeadAlertKindCatalog.ManualKinds.Contains(k))
            .ToList();

        Assert.Equal(35, systemCodes.Count);

        foreach (var kind in systemCodes)
        {
            var audiences = HeadAlertKindCatalog.AllowedAudiences(kind);
            Assert.NotEmpty(audiences);
        }
    }

    [Fact]
    public void AllowedAudiences_CoversExactlyTheSystemCodes()
    {
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in HeadAlertKindCatalog.All)
        {
            if (HeadAlertKindCatalog.ManualKinds.Contains(kind))
                continue;
            foreach (var _ in HeadAlertKindCatalog.AllowedAudiences(kind))
                covered.Add(kind);
        }

        Assert.Equal(35, covered.Count);
        Assert.Equal(
            HeadAlertKindCatalog.All.Where(k => !HeadAlertKindCatalog.ManualKinds.Contains(k)).OrderBy(k => k),
            covered.OrderBy(k => k));
    }

    [Fact]
    public void AllowedAudiences_RejectsManualLegacyAndUnknown()
    {
        foreach (var manual in HeadAlertKindCatalog.ManualKinds)
        {
            Assert.Throws<ArgumentException>(() => HeadAlertKindCatalog.AllowedAudiences(manual));
        }

        Assert.Throws<ArgumentException>(() => HeadAlertKindCatalog.AllowedAudiences(HeadAlertKindCatalog.Legacy));
        Assert.Throws<ArgumentException>(() => HeadAlertKindCatalog.AllowedAudiences("unknown_kind"));
        Assert.Throws<ArgumentException>(() => HeadAlertKindCatalog.AllowedAudiences(" "));
    }

    [Theory]
    [InlineData(HeadAlertKindCatalog.DelegationPendingApproval, HeadAlertAudience.SingleHead)]
    [InlineData(HeadAlertKindCatalog.DelegationSentToSubhead, HeadAlertAudience.SingleHead)]
    [InlineData(HeadAlertKindCatalog.DelegationPendingCompletion, HeadAlertAudience.SingleHead)]
    [InlineData(HeadAlertKindCatalog.CircuitTransferred, HeadAlertAudience.SingleHead)]
    [InlineData(HeadAlertKindCatalog.AppealReferralToHead, HeadAlertAudience.SingleHead)]
    [InlineData(HeadAlertKindCatalog.AppealReturnedToHead, HeadAlertAudience.SingleHead)]
    [InlineData(HeadAlertKindCatalog.AppealPendingToOwner, HeadAlertAudience.SingleHead)]
    [InlineData(HeadAlertKindCatalog.EntityRegistryEntryReview, HeadAlertAudience.SingleHead)]
    [InlineData(HeadAlertKindCatalog.EntityEditProposalReview, HeadAlertAudience.SingleHead)]
    [InlineData(HeadAlertKindCatalog.DelegationMirrorDeferred, HeadAlertAudience.SingleLawyer)]
    [InlineData(HeadAlertKindCatalog.DelegationMirrorReturned, HeadAlertAudience.SingleLawyer)]
    [InlineData(HeadAlertKindCatalog.DelegationMirrorSettled, HeadAlertAudience.SingleLawyer)]
    [InlineData(HeadAlertKindCatalog.DelegationMirrorFullyForcibly, HeadAlertAudience.SingleLawyer)]
    [InlineData(HeadAlertKindCatalog.DelegationMirrorUpdated, HeadAlertAudience.SingleLawyer)]
    [InlineData(HeadAlertKindCatalog.DelegationMirrorLocalAddition, HeadAlertAudience.SingleLawyer)]
    [InlineData(HeadAlertKindCatalog.DelegationMirrorStatusChanged, HeadAlertAudience.SingleLawyer)]
    [InlineData(HeadAlertKindCatalog.CircuitPendingDocuments, HeadAlertAudience.SingleLawyer)]
    [InlineData(HeadAlertKindCatalog.AppealAssignedToLawyer, HeadAlertAudience.SingleLawyer)]
    [InlineData(HeadAlertKindCatalog.DelegationAssignedToLawyer, HeadAlertAudience.DocumentCreator)]
    [InlineData(HeadAlertKindCatalog.DelegationRejectedWrongCourt, HeadAlertAudience.DocumentCreator)]
    [InlineData(HeadAlertKindCatalog.DelegationCompletedReturned, HeadAlertAudience.DocumentCreator)]
    [InlineData(HeadAlertKindCatalog.AppealFinalToBaseLawyer, HeadAlertAudience.DocumentCreator)]
    [InlineData(HeadAlertKindCatalog.EntityBranchChanged, HeadAlertAudience.BranchHeads)]
    [InlineData(HeadAlertKindCatalog.EntityEntryMoved, HeadAlertAudience.BranchHeads)]
    [InlineData(HeadAlertKindCatalog.EntityEntriesMovedAll, HeadAlertAudience.BranchHeads)]
    [InlineData(HeadAlertKindCatalog.EntityRenameHeads, HeadAlertAudience.BranchHeads)]
    [InlineData(HeadAlertKindCatalog.EntityMergeHeads, HeadAlertAudience.BranchHeads)]
    [InlineData(HeadAlertKindCatalog.EntityUnifyHeads, HeadAlertAudience.BranchHeads)]
    [InlineData(HeadAlertKindCatalog.EntityAbolishHeads, HeadAlertAudience.BranchHeads)]
    [InlineData(HeadAlertKindCatalog.EntityRenameLawyers, HeadAlertAudience.BranchLawyers)]
    [InlineData(HeadAlertKindCatalog.EntityMergeLawyers, HeadAlertAudience.BranchLawyers)]
    [InlineData(HeadAlertKindCatalog.EntityUnifyLawyers, HeadAlertAudience.BranchLawyers)]
    [InlineData(HeadAlertKindCatalog.EntityAbolishLawyers, HeadAlertAudience.BranchLawyers)]
    public void AllowedAudiences_MatrixMatchesPlan(string kind, HeadAlertAudience expected)
    {
        Assert.Contains(expected, HeadAlertKindCatalog.AllowedAudiences(kind));
    }

    [Theory]
    [InlineData(HeadAlertKindCatalog.ReviewLetterReply)]
    [InlineData(HeadAlertKindCatalog.EntityRenameNoticeToCreator)]
    public void AllowedAudiences_RoleDependentKindsAllowLawyerOrHead(string kind)
    {
        var audiences = HeadAlertKindCatalog.AllowedAudiences(kind);
        Assert.Contains(HeadAlertAudience.SingleLawyer, audiences);
        Assert.Contains(HeadAlertAudience.SingleHead, audiences);
        Assert.Equal(2, audiences.Count);    }

    [Fact]
    public void FamilySets_AreSubsetsOfAll_WithExpectedCounts()
    {
        Assert.Equal(13, HeadAlertKindCatalog.DelegationKinds.Count);
        Assert.Equal(5, HeadAlertKindCatalog.AppealKinds.Count);
        Assert.Equal(2, HeadAlertKindCatalog.CircuitKinds.Count);
        Assert.Single(HeadAlertKindCatalog.ReviewLetterKinds);

        foreach (var family in new[]
        {
            HeadAlertKindCatalog.DelegationKinds,
            HeadAlertKindCatalog.AppealKinds,
            HeadAlertKindCatalog.CircuitKinds,
            HeadAlertKindCatalog.ReviewLetterKinds,
        })
        {
            Assert.NotEmpty(family);
            Assert.All(family, kind => Assert.Contains(kind, HeadAlertKindCatalog.All));
        }
    }

    [Fact]
    public void FamilySets_AreMutuallyDisjoint()
    {
        var families = new[]
        {
            HeadAlertKindCatalog.DelegationKinds,
            HeadAlertKindCatalog.AppealKinds,
            HeadAlertKindCatalog.CircuitKinds,
            HeadAlertKindCatalog.ReviewLetterKinds,
        };

        for (var i = 0; i < families.Length; i++)
        {
            for (var j = i + 1; j < families.Length; j++)
            {
                Assert.Empty(families[i].Intersect(families[j]));
            }
        }
    }

    [Fact]
    public void ToLabel_CoversEveryCatalogCodeWithNonBlankDistinctLabel()
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var kind in HeadAlertKindCatalog.All)
        {
            var label = HeadAlertKindCatalog.ToLabel(kind);
            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.NotEqual(kind, label);
            labels[kind] = label;
        }

        Assert.False(string.IsNullOrWhiteSpace(HeadAlertKindCatalog.ToLabel(HeadAlertKindCatalog.Legacy)));
        Assert.Equal("تنبيه سابق", HeadAlertKindCatalog.ToLabel(HeadAlertKindCatalog.Legacy));
        Assert.Equal("رمز غير معروف", HeadAlertKindCatalog.ToLabel("رمز غير معروف"));
    }
}
