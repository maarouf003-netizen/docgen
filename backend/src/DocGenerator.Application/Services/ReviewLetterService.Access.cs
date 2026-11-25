using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class ReviewLetterService
{
    /// <summary>
    /// مالك الكتاب (§10): شعبة دائرة ملفه (بلا دائرة → القسم)، وبلا ملف الشعبة
    /// المستلمة (`null` → القسم). الملف المحذوف مصدره يعامَل كبلا دائرة.
    /// </summary>
    internal static async Task<int?> OwnerSectionOfAsync(ReviewLetter letter,
        IDocumentRepository documents, IRepository<ExecutionCircuit> circuits, CancellationToken ct)
    {
        if (letter.DocumentId is null)
            return letter.RecipientSectionId;
        var doc = letter.Document ?? await documents.GetByIdAsync(letter.DocumentId.Value, ct);
        if (doc?.ExecutionCircuitId is null)
            return null;
        var circuit = doc.ExecutionCircuit ?? await circuits.GetByIdAsync(doc.ExecutionCircuitId.Value, ct);
        return circuit?.SectionId;
    }

    /// <summary>مالك دائرة ملف (§10.1) — يُشتق منه مستلم الكتاب المرتبط تلقائيًا.</summary>
    internal static async Task<int?> FileOwnerSectionAsync(Document document,
        IRepository<ExecutionCircuit> circuits, CancellationToken ct)
    {
        if (document.ExecutionCircuitId is null)
            return null;
        var circuit = document.ExecutionCircuit
            ?? await circuits.GetByIdAsync(document.ExecutionCircuitId.Value, ct);
        return circuit?.SectionId;
    }

    /// <summary>
    /// اختيار مستلم الكتاب العام (§10.2 + قرار 28): فارغٌ يعني رئيس القسم،
    /// وغيره شعبة نشطة بفرع الكتاب ولها رئيس مفعّل — وإلا «يجب اختيار المستلم».
    /// </summary>
    private async Task<Section?> ResolveGeneralRecipientSectionAsync(
        int? recipientSectionId, int actorBranchId, CancellationToken ct)
    {
        if (recipientSectionId is null)
            return null;
        var section = await _sections.GetByIdAsync(recipientSectionId.Value, ct);
        var head = section is not null
            ? await _users.FindActiveHeadAsync(UserRole.SubHead, section.BranchId, section.Id, ct)
            : null;
        if (section is null || section.BranchId != actorBranchId || !section.IsActive || head is null)
            throw new ArgumentException("يجب اختيار المستلم — حدّد رئيس القسم أو شعبة نشطة برئيس مفعّل");
        return section;
    }

    /// <summary>
    /// حارس المالك للرد (§10): القسم لكتبه، والشعبة لكتبها — «مقصور على كتب نطاقه».
    /// </summary>
    private static void EnsureLetterOwner(UserRole role, int? actorSectionId, int? ownerSectionId)
    {
        if (role == UserRole.Head)
        {
            if (ownerSectionId.HasValue)
                throw new UnauthorizedAccessException("مقصور على كتب نطاقه");
        }
        else if (role == UserRole.SubHead)
        {
            if (actorSectionId is null)
                throw new UnauthorizedAccessException("حسابك بلا شعبة — أعد الدخول");
            if (ownerSectionId != actorSectionId)
                throw new UnauthorizedAccessException("مقصور على كتب نطاقه");
        }
        else
        {
            throw new UnauthorizedAccessException("رد المطالعات للرؤساء فقط");
        }
    }

    /// <summary>
    /// متابعة الملف: إسناد متابعة استئناف عليه، أو إنابة مصدره/منابُه هذا الملف
    /// ومسندة إلى المحامي نفسه.
    /// </summary>
    internal static async Task<bool> FollowsDocumentAsync(IAppealRepository appeals,
        IDelegationRepository delegations, int documentId, int userId, CancellationToken ct)
    {
        if (await appeals.IsAssignedFollowerAsync(documentId, userId, ct))
            return true;

        var sourceDelegations = await delegations.ListBySourceAsync(documentId, ct);
        if (sourceDelegations.Any(d => d.AssignedLawyerId == userId))
            return true;

        var targetDelegation = await delegations.FindByTargetAsync(documentId, ct);
        return targetDelegation is { AssignedLawyerId: not null } target
            && target.AssignedLawyerId == userId;
    }
}
