using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class CorrespondenceService
{
    /// <summary>
    /// صلاحية التسطير على ملف: المحامي المالك أو متابعه، ورئيس الفرع/الملف الإرثي،
    /// وشعبة دائرة الملف، ومندوب نطاق الجهة. مشتركة بين الإنشاء والبحث عن المستلمين
    /// (وعبر عقد الكتابة `ICorrespondenceWriteRules` لمعالجي الأوامر).
    /// </summary>
    internal static async Task<bool> MayAttachAsync(Document document, int actorUserId, UserRole role,
        int? actorBranchId, int? actorSectionId, IRepository<ExecutionCircuit> circuits,
        IAppealRepository appeals, IDelegationRepository delegations, IPortalRepository portal,
        CancellationToken ct)
    {
        switch (role)
        {
            case UserRole.Lawyer:
                return document.CreatedById == actorUserId
                    || await FollowsDocumentAsync(appeals, delegations, document.Id, actorUserId, ct);
            case UserRole.Head:
                // حارس null صريح: مقارنة int?==int? المباشرة تعدّ null==null صوابًا،
                // فيرى رئيس بلا فرع ملفًا بلا فرع — ثغرة حسابات مشوّهة.
                // الملف القديم بلا فرع متاح لرئيس أي فرع (مراسلاته تُرشَّح لاحقًا
                // لفرعه/محافظته في ListByDocumentAsync، ورقمها من فرع المنشئ).
                return actorBranchId is not null
                    && (actorBranchId == document.BranchId || document.BranchId is null);
            case UserRole.SubHead:
                // نطاق الشعبة (§10): فرعه ودائرة شعبته حصرًا — بلا تدهور لملف
                // بلا دائرة (مرآة MayListDocumentAsync).
                if (actorBranchId is null || actorBranchId != document.BranchId || actorSectionId is null)
                    return false;
                if (document.ExecutionCircuitId is null)
                    return false;
                var subCircuit = document.ExecutionCircuit
                    ?? await circuits.GetByIdAsync(document.ExecutionCircuitId.Value, ct);
                return subCircuit?.SectionId == actorSectionId;
            case UserRole.EntityManager:
                return await IsDocumentInDelegateScopeAsync(portal, document.Id, actorUserId, ct);
            default:
                return false;
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

    internal static async Task<bool> CanViewAsync(Correspondence letter, int actorUserId, UserRole role,
        int? actorBranchId, int? actorSectionId, IDocumentRepository documents,
        IRepository<ExecutionCircuit> circuits, IBranchRepository branches, CancellationToken ct)
    {
        // الطرفان (المنشئ والمستلم المعيَّن) يريان دائمًا.
        if (letter.CreatedById == actorUserId || letter.TargetUserId == actorUserId)
            return true;

        switch (role)
        {
            case UserRole.Head when actorBranchId is not null:
                // رئيس القسم يرى مراسلات محافظته: فرعه + العامة من مندوبيها.
                return string.Equals(
                    letter.Governorate,
                    await ResolveHeadGovernorateAsync(branches, actorBranchId.Value, ct),
                    StringComparison.Ordinal);
            case UserRole.SubHead when actorBranchId is not null && actorSectionId is not null:
                // نطاق الشعبة (§10): فرعه وملك دائرته (شعبته) — بلا تدهور.
                return letter.BranchId == actorBranchId
                    && await OwnerSectionOfAsync(letter, documents, circuits, ct) == actorSectionId;
            case UserRole.Manager or UserRole.Admin:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// مالك المراسلة (§10): شعبة دائرة ملفها (بلا دائرة → لا مالك شعبي)،
    /// وبلا ملف الشعبة المستلمة المجمدة. الملف المحذوف مصدره يعامَل بلا مالك.
    /// </summary>
    internal static async Task<int?> OwnerSectionOfAsync(Correspondence letter,
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

    /// <summary>
    /// الفرع/المحافظة/بادئة الرقم عند التسطير: للمرتبطة بملف من فرع الملف،
    /// وللعامة من فرع المنشئ، وللعامة من مندوب (بلا فرع) من محافظة جهته.
    /// </summary>
    private async Task<(int? BranchId, string Governorate, string Prefix)> ResolveScopeAsync(
        Document? document, UserRole role, int actorUserId, int? actorBranchId,
        CancellationToken ct)
    {
        if (document is not null)
        {
            // الملفات القديمة بلا فرع: يرتد المحامي/الرئيس إلى فرعه (لتظل المراسلة
            // ضمن محافظته ورقمه)، أما المندوب فلا فرع له — يُرفض بوضوح.
            if (document.BranchId is null)
            {
                if (role is not (UserRole.Lawyer or UserRole.Head) || actorBranchId is null)
                    throw new ArgumentException("فرع الملف غير موجود");
                var actorBranch = await _branches.GetByIdAsync(actorBranchId.Value, ct)
                    ?? throw new ArgumentException("الفرع غير موجود");
                if (string.IsNullOrWhiteSpace(actorBranch.Governorate))
                    throw new ArgumentException("تعذّر تحديد محافظة الفرع");
                return (actorBranch.Id, actorBranch.Governorate.Trim(), actorBranch.Code);
            }
            var branch = await _branches.GetByIdAsync(document.BranchId.Value, ct)
                ?? throw new ArgumentException("فرع الملف غير موجود");
            if (string.IsNullOrWhiteSpace(branch.Governorate))
                throw new ArgumentException("تعذّر تحديد محافظة الملف");
            return (branch.Id, branch.Governorate.Trim(), branch.Code);
        }

        if (role is UserRole.Lawyer or UserRole.Head or UserRole.SubHead)
        {
            if (actorBranchId is null)
                throw new ArgumentException("الحساب دون فرع لا يمكنه تسطير مراسلة عامة");
            var branch = await _branches.GetByIdAsync(actorBranchId.Value, ct)
                ?? throw new ArgumentException("الفرع غير موجود");
            if (string.IsNullOrWhiteSpace(branch.Governorate))
                throw new ArgumentException("تعذّر تحديد محافظة الفرع");
            return (branch.Id, branch.Governorate.Trim(), branch.Code);
        }

        // عامة من مندوب جهة: بلا فرع — المحافظة والبادئة من نطاق جهته.
        var scope = await _portal.ResolveForUserAsync(actorUserId, ct)
            ?? throw new ArgumentException("حساب المندوب بلا نطاق جهة");
        if (scope.Entries.Count == 0)
            throw new ArgumentException("حساب المندوب بلا نطاق نشط");
        // القيود المرجعة من المحلل نشطة حتمًا (يفلتر النهائي/غير المراجَع/النشط)؛
        // الفحص الصريح على IsActive يحمي من أي انحراف مستقبلي في العقد بدل
        // الانهيار، وأول قيد هو المرجع الحتمي نفسه في المسار الحالي.
        // (القيود ValueTuple فلا ?. عليها — الافتراضي IsActive=false فيسقط للاحتياط.)
        var activeEntry = scope.Entries.FirstOrDefault(e => e.IsActive);
        var governorate = activeEntry.IsActive
            ? activeEntry.Governorate
            : scope.Entries[0].Governorate;
        if (string.IsNullOrWhiteSpace(governorate))
            throw new ArgumentException("حساب المندوب بلا نطاق نشط");
        return (null, governorate.Trim(), governorate.Trim());
    }

    internal static async Task<string> ResolveHeadGovernorateAsync(IBranchRepository branches,
        int branchId, CancellationToken ct)
    {
        var branch = await branches.GetByIdAsync(branchId, ct)
            ?? throw new ArgumentException("الفرع غير موجود");
        return branch.Governorate?.Trim() ?? string.Empty;
    }

    internal static async Task<bool> IsDocumentInDelegateScopeAsync(IPortalRepository portal,
        int documentId, int userId, CancellationToken ct)
    {
        var scope = await portal.ResolveForUserAsync(userId, ct);
        if (scope is null || scope.EntryIds.Count == 0)
            return false;
        return await portal.IsDocumentInScopeAsync(documentId, scope.EntryIds, ct);
    }
}
