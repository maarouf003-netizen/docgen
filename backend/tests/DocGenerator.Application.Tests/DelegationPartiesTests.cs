using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Tests;

/// <summary>
/// أطراف الإنابة (مراجعة PR-4 `ح‑1`): المنطق النقي الذي كان في
/// <c>IDocumentDelegationService.IsPartyAsync</c> ونُقل إلى <c>Application.Common</c>
/// ليستطيع <c>HeadAlertService</c> تطبيقه بلا استدعاء خدمة أخرى وبلا استعلام إضافي.
/// الأطراف الثلاثة هي السلوك القائم بلا توسعة.
/// </summary>
public class DelegationPartiesTests
{
    [Fact]
    public void IsParty_TrueForSourceDocumentOwner()
    {
        Assert.True(DelegationParties.IsParty(NewDelegation(), userId: 10));
    }

    [Fact]
    public void IsParty_TrueForTargetDocumentOwner()
    {
        Assert.True(DelegationParties.IsParty(NewDelegation(), userId: 20));
    }

    [Fact]
    public void IsParty_TrueForAssignedLawyer()
    {
        Assert.True(DelegationParties.IsParty(NewDelegation(), userId: 30));
    }

    [Fact]
    public void IsParty_FalseForUnrelatedUser()
    {
        Assert.False(DelegationParties.IsParty(NewDelegation(), userId: 40));
    }

    /// <summary>
    /// روابط غير محمَّلة (لا <c>Include</c>): لا انفجار — الطرفية تُحدَّد بالحقل المتاح.
    /// </summary>
    [Fact]
    public void IsParty_WithUnloadedNavigations_DoesNotThrow()
    {
        var delegation = new DocumentDelegation { SourceDocument = null!, AssignedLawyerId = 30 };

        Assert.False(DelegationParties.IsParty(delegation, userId: 10));
        Assert.True(DelegationParties.IsParty(delegation, userId: 30));
    }

    [Fact]
    public void IsParty_ThrowsOnNullDelegation()
    {
        Assert.Throws<ArgumentNullException>(() => DelegationParties.IsParty(null!, userId: 1));
    }

    private static DocumentDelegation NewDelegation() => new()
    {
        SourceDocument = new Document { CreatedById = 10 },
        TargetDocument = new Document { CreatedById = 20 },
        AssignedLawyerId = 30,
    };
}
