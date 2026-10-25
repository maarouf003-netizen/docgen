using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حراسة بناء مفتاح منع التكرار (الخطة §6.4 و§14.1): القوالب المعتمدة، والنطاق،
/// وإلزام النوع، وعدم القصّ الصامت، وإعادة توجيه المستلم عند التعاقب.
/// </summary>
public class HeadAlertDedupeTests
{
    private static HeadAlertDedupeScope Scope(
        string kind,
        HeadAlertAudience audience,
        int branchId = 3,
        int? recipientUserId = null,
        int? delegationId = null,
        int? appealId = null,
        int? reviewLetterId = null,
        int? documentId = null,
        int? publicEntityId = null,
        int? executionCircuitId = null,
        string? entityOperation = null,
        string? entityGroupKey = null,
        string? circuitKey = null)
        => new()
        {
            Kind = kind,
            Audience = audience,
            BranchId = branchId,
            RecipientUserId = recipientUserId,
            DelegationId = delegationId,
            AppealId = appealId,
            ReviewLetterId = reviewLetterId,
            DocumentId = documentId,
            PublicEntityId = publicEntityId,
            ExecutionCircuitId = executionCircuitId,
            EntityOperation = entityOperation,
            EntityGroupKey = entityGroupKey,
            CircuitKey = circuitKey,
        };

    [Fact]
    public void Build_DelegationRecipient_UsesApprovedTemplate()
    {
        var key = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.DelegationMirrorUpdated,
            HeadAlertAudience.SingleLawyer,
            recipientUserId: 42,
            delegationId: 7));

        Assert.Equal("delegation:7:recipient:42:delegation_mirror_updated", key);
    }

    [Fact]
    public void Build_DelegationBranchAudience_UsesBranchScope()
    {
        var lawyers = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.DelegationPendingApproval,
            HeadAlertAudience.BranchLawyers,
            branchId: 5,
            delegationId: 7));

        var heads = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.DelegationPendingApproval,
            HeadAlertAudience.BranchHeads,
            branchId: 5,
            delegationId: 7));

        Assert.Equal("delegation:7:branch:5:audience:lawyers:delegation_pending_approval", lawyers);
        Assert.Equal("delegation:7:branch:5:audience:heads:delegation_pending_approval", heads);
        Assert.NotEqual(lawyers, heads);
    }

    [Fact]
    public void Build_CircuitPending_UsesBranchAndRecipient()
    {
        var key = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.CircuitPendingDocuments,
            HeadAlertAudience.SingleLawyer,
            branchId: 9,
            recipientUserId: 11,
            executionCircuitId: 4));

        Assert.Equal("circuit-pending:9:4:recipient:11:circuit_pending_documents", key);
    }

    [Fact]
    public void Build_CircuitPending_WithoutCircuit_FallsBackToNormalizedCourtName()
    {
        var key = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.CircuitPendingDocuments,
            HeadAlertAudience.SingleLawyer,
            branchId: 9,
            recipientUserId: 11,
            circuitKey: "  دائرة  دمشق  "));

        Assert.Equal("circuit-pending:9:دائرة دمشق:recipient:11:circuit_pending_documents", key);
    }

    [Fact]
    public void Build_CircuitTransferred_UsesCircuitRecipientTemplate()
    {
        var key = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.CircuitTransferred,
            HeadAlertAudience.SingleHead,
            recipientUserId: 2,
            executionCircuitId: 8));

        Assert.Equal("circuit:8:recipient:2:circuit_transferred", key);
    }

    [Fact]
    public void Build_AppealRecipient_UsesApprovedTemplate()
    {
        var key = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.AppealAssignedToLawyer,
            HeadAlertAudience.SingleLawyer,
            recipientUserId: 21,
            appealId: 6));

        Assert.Equal("appeal:6:recipient:21:appeal_assigned_to_lawyer", key);
    }

    [Fact]
    public void Build_ReviewLetter_UsesApprovedTemplate()
    {
        var key = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.ReviewLetterReply,
            HeadAlertAudience.SingleLawyer,
            recipientUserId: 13,
            reviewLetterId: 4));

        Assert.Equal("review:4:recipient:13:review_letter_reply", key);
    }

    [Fact]
    public void Build_DocumentCreator_UsesDocumentTemplate_EvenForDelegationEvent()
    {
        var key = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.DelegationAssignedToLawyer,
            HeadAlertAudience.DocumentCreator,
            recipientUserId: 33,
            documentId: 12,
            delegationId: 7));

        Assert.Equal("document:12:recipient:33:delegation_assigned_to_lawyer", key);
    }

    [Fact]
    public void Build_DocumentRecipient_WithoutDocument_UsesDocumentRecipientTemplate()
    {
        var key = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.DelegationAssignedToLawyer,
            HeadAlertAudience.DocumentCreator,
            recipientUserId: 33));

        Assert.Equal("document-recipient:recipient:33:delegation_assigned_to_lawyer", key);
        Assert.DoesNotContain("::", key);
    }

    [Fact]
    public void Build_EntityRecipient_UsesOperationAndEntity()
    {
        var key = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.EntityRenameNoticeToCreator,
            HeadAlertAudience.SingleLawyer,
            recipientUserId: 44,
            publicEntityId: 9,
            entityOperation: "Rename"));

        Assert.Equal("entity:rename:9:recipient:44:entity_rename_notice_to_creator", key);
    }

    [Fact]
    public void Build_EntityBranchAudience_UsesBranchScope()
    {
        var key = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.EntityRenameLawyers,
            HeadAlertAudience.BranchLawyers,
            branchId: 6,
            publicEntityId: 9,
            entityOperation: "rename"));

        Assert.Equal("entity:rename:9:branch:6:audience:lawyers:entity_rename_lawyers", key);
    }

    [Fact]
    public void Build_EntityGroupKeyWithoutPublicEntityId_UsesGroupScope()
    {
        var key = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.EntityRenameLawyers,
            HeadAlertAudience.BranchLawyers,
            branchId: 6,
            entityOperation: "rename",
            entityGroupKey: "group-7"));

        Assert.Equal("entity:rename:group-7:branch:6:audience:lawyers:entity_rename_lawyers", key);
    }

    [Fact]
    public void Build_EverySystemKeyContainsTheKindExplicitly()
    {
        var kinds = HeadAlertKindCatalog.All.Where(k => !HeadAlertKindCatalog.ManualKinds.Contains(k));

        foreach (var kind in kinds)
        {
            var key = HeadAlertDedupe.Build(Scope(
                kind,
                HeadAlertAudience.BranchLawyers,
                branchId: 1,
                delegationId: 2,
                appealId: 3,
                reviewLetterId: 4,
                documentId: 5,
                publicEntityId: 6,
                executionCircuitId: 7,
                entityOperation: "rename"));

            Assert.EndsWith($":{kind}", key);
            Assert.Contains(kind, key);
        }
    }

    [Fact]
    public void Build_DifferentKindsNeverCollide()
    {
        var first = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.DelegationMirrorUpdated, HeadAlertAudience.SingleLawyer, recipientUserId: 1, delegationId: 2));
        var second = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.DelegationMirrorLocalAddition, HeadAlertAudience.SingleLawyer, recipientUserId: 1, delegationId: 2));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Build_DifferentBranchesOrRecipientsNeverCollide()
    {
        var branchA = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.EntityRenameLawyers, HeadAlertAudience.BranchLawyers, branchId: 1, publicEntityId: 5, entityOperation: "rename"));
        var branchB = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.EntityRenameLawyers, HeadAlertAudience.BranchLawyers, branchId: 2, publicEntityId: 5, entityOperation: "rename"));

        var userA = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.DelegationMirrorUpdated, HeadAlertAudience.SingleLawyer, recipientUserId: 1, delegationId: 2));
        var userB = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.DelegationMirrorUpdated, HeadAlertAudience.SingleLawyer, recipientUserId: 2, delegationId: 2));

        Assert.NotEqual(branchA, branchB);
        Assert.NotEqual(userA, userB);
    }

    [Fact]
    public void Build_SingleAndAudienceVersionsOfSameEventDoNotCollide()
    {
        var single = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.DelegationPendingApproval, HeadAlertAudience.SingleHead, recipientUserId: 3, delegationId: 2));
        var broadcast = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.DelegationPendingApproval, HeadAlertAudience.BranchHeads, branchId: 1, delegationId: 2));

        Assert.NotEqual(single, broadcast);
    }

    [Fact]
    public void Build_RejectsEmptyUnknownAndLegacyKinds()
    {
        Assert.Throws<ArgumentException>(() => HeadAlertDedupe.Build(Scope(" ", HeadAlertAudience.SingleLawyer, recipientUserId: 1)));
        Assert.Throws<ArgumentException>(() => HeadAlertDedupe.Build(Scope("unknown", HeadAlertAudience.SingleLawyer, recipientUserId: 1)));
        Assert.Throws<ArgumentException>(() => HeadAlertDedupe.Build(Scope(HeadAlertKindCatalog.Legacy, HeadAlertAudience.SingleLawyer, recipientUserId: 1)));
    }

    [Fact]
    public void Build_RejectsSingleAudienceWithoutRecipient()
    {
        Assert.Throws<ArgumentException>(() => HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.DelegationMirrorUpdated, HeadAlertAudience.SingleLawyer, delegationId: 2)));
    }

    [Fact]
    public void Build_NeverProducesEmptySegment()
    {
        var key = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.DelegationMirrorUpdated, HeadAlertAudience.SingleLawyer, recipientUserId: 7, delegationId: 2));

        Assert.DoesNotContain("::", key);
        Assert.All(key.Split(':'), segment => Assert.False(string.IsNullOrWhiteSpace(segment)));
    }

    [Fact]
    public void Build_LongTextPartIsHashedNotTruncated()
    {
        var longGroup = new string('س', 400);
        var key = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.EntityEntriesMovedAll,
            HeadAlertAudience.BranchHeads,
            branchId: 1,
            publicEntityId: 5,
            entityOperation: "move",
            entityGroupKey: longGroup));

        Assert.True(key.Length <= HeadAlertDedupe.MaxKeyLength);
        Assert.DoesNotContain(longGroup, key);
        Assert.EndsWith(":entity_entries_moved_all", key);
        Assert.Contains(":h:", key);
    }

    [Fact]
    public void Build_HashingIsStable()
    {
        var longGroup = new string('س', 400);
        var first = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.EntityEntriesMovedAll, HeadAlertAudience.BranchHeads, branchId: 1,
            publicEntityId: 5, entityOperation: "move", entityGroupKey: longGroup));
        var second = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.EntityEntriesMovedAll, HeadAlertAudience.BranchHeads, branchId: 1,
            publicEntityId: 5, entityOperation: "move", entityGroupKey: longGroup));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Build_NormalizesWhitespaceAndCaseInTextParts()
    {
        var first = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.EntityRenameNoticeToCreator, HeadAlertAudience.SingleLawyer,
            recipientUserId: 1, publicEntityId: 2, entityOperation: "  RENAME  "));
        var second = HeadAlertDedupe.Build(Scope(
            HeadAlertKindCatalog.EntityRenameNoticeToCreator, HeadAlertAudience.SingleLawyer,
            recipientUserId: 1, publicEntityId: 2, entityOperation: "rename"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void RekeyRecipient_ReplacesTheFullRecipientSegment()
    {
        var key = "delegation:7:recipient:42:delegation_mirror_updated";
        var rekeyed = HeadAlertDedupe.RekeyRecipient(key, 42, 99);

        Assert.Equal("delegation:7:recipient:99:delegation_mirror_updated", rekeyed);
    }

    [Fact]
    public void RekeyRecipient_WorksForCircuitPendingAndDocumentRecipient()
    {
        Assert.Equal(
            "circuit-pending:9:4:recipient:99:circuit_pending_documents",
            HeadAlertDedupe.RekeyRecipient("circuit-pending:9:4:recipient:42:circuit_pending_documents", 42, 99));

        Assert.Equal(
            "document-recipient:recipient:99:delegation_assigned_to_lawyer",
            HeadAlertDedupe.RekeyRecipient("document-recipient:recipient:42:delegation_assigned_to_lawyer", 42, 99));
    }

    [Fact]
    public void RekeyRecipient_DoesNotMatchAPrefixOfAnotherIdentifier()
    {
        // «recipient:4:» لا يطابق «recipient:42:» — الفاصل الختامي جزء من المقطع.
        var key = "delegation:7:recipient:42:delegation_mirror_updated";
        Assert.Equal(key, HeadAlertDedupe.RekeyRecipient(key, 4, 99));
    }

    [Fact]
    public void RekeyRecipient_ReturnsSameKeyWhenSegmentMissingOrKeyInvalid()
    {
        var key = "delegation:7:branch:1:audience:heads:delegation_pending_approval";
        Assert.Equal(key, HeadAlertDedupe.RekeyRecipient(key, 42, 99));

        Assert.Null(HeadAlertDedupe.RekeyRecipient(null, 42, 99));
        Assert.Equal("legacy", HeadAlertDedupe.RekeyRecipient("legacy", 42, 99));
    }

    [Fact]
    public void ValidateDedupeKey_RejectsTooLongKeys()
    {
        var tooLong = new string('a', HeadAlertDedupe.MaxKeyLength + 1);
        Assert.Throws<ArgumentException>(() => HeadAlertDedupe.ValidateDedupeKey(tooLong));

        var atLimit = new string('a', HeadAlertDedupe.MaxKeyLength);
        Assert.Equal(atLimit, HeadAlertDedupe.ValidateDedupeKey(atLimit));
    }
}
