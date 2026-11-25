using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>
/// مساعدات مندوبي الجهات المشتركة (`P4-01ct`): محافظة فرع الفاعل، ونطاق المندوب
/// داخل المحافظة، وحراسة الكتابة، وإسقاط الـ`DTO` مع أسماء النطاق — مصدر واحد
/// لاستعلام القراءة ومعالجات الإنشاء/التعديل بلا تكرار.
/// </summary>
internal static class EntityDelegateProjection
{
    /// <summary>
    /// محافظة فرع الفاعل إن كان رئيس قسم أو شعبة بمستودع فروع حاضر — وإلا null (بلا قيد).
    /// </summary>
    internal static async Task<string?> HeadGovernorateAsync(
        EntityRegistryActor? actor, IRepository<Branch>? branches, CancellationToken ct)
    {
        if (actor?.Role is not (UserRole.Head or UserRole.SubHead) || !actor.BranchId.HasValue || branches is null)
            return null;
        var branch = await branches.GetByIdAsync(actor.BranchId.Value, ct);
        var governorate = branch?.Governorate?.Trim();
        return string.IsNullOrEmpty(governorate) ? null : governorate;
    }

    /// <summary>
    /// نطاق المندوب داخل محافظة (`BQ-002`): القيد بنفس محافظته؛ الهوية بقيودها
    /// النشطة كلها داخلها (الفارغة تُقبَل — لا بيانات تُسرَّب). المخالفة `403`.
    /// </summary>
    internal static async Task EnsureHeadGovernorateAsync(
        EntityRegistryActor? actor, int? groupId, int? entryId,
        IRepository<Branch>? branches, IPublicEntityRepository registry, CancellationToken ct)
    {
        var governorate = await HeadGovernorateAsync(actor, branches, ct);
        if (governorate is null)
            return;
        if (!await IsScopeInGovernorateAsync(groupId, entryId, governorate, registry, ct))
            throw new UnauthorizedAccessException("نطاق المندوب خارج محافظة فرعك");
    }

    internal static async Task<bool> IsScopeInGovernorateAsync(
        int? groupId, int? entryId, string governorate,
        IPublicEntityRepository registry, CancellationToken ct)
    {
        // `PB-001`: مقارنة معيارية للطرفين (كانت `Trim` فقط).
        var normGov = ArabicNameNormalizer.Normalize(governorate);
        if (entryId.HasValue)
        {
            var entry = await registry.GetEntryAsync(entryId.Value, ct);
            return entry is not null
                && string.Equals(ArabicNameNormalizer.Normalize(entry.Governorate), normGov, StringComparison.Ordinal);
        }
        if (groupId.HasValue)
        {
            var entries = await registry.ListEntriesByGroupAsync(groupId.Value, ct);
            var actives = entries.Where(e => e.IsActive).ToList();
            return actives.Count == 0
                || actives.All(e => string.Equals(ArabicNameNormalizer.Normalize(e.Governorate), normGov, StringComparison.Ordinal));
        }
        return false;
    }

    /// <summary>يبني الـ`DTO` مع أسماء النطاق، محمّلًا إياها عند غيابها عن الكيان المتتبَّع.</summary>
    internal static async Task<DelegateDto> BuildDtoWithScopeAsync(
        User user, IPublicEntityRepository registry, CancellationToken ct)
    {
        var groupName = user.PortalGroup?.CanonicalName;
        // قد يملأ EF navigation user.PortalEntry عبر fix-up بكيان مُتتبَّع حُمِّل
        // بلا Include(Group) (كما في ResolveScopeAsync عبر GetEntryAsync)،
        // فتكون Group null رغم أن PortalEntry نفسها غير null — لذا الفحص على Group.
        var entryLabel = user.PortalEntry?.Group is null
            ? null
            : $"{user.PortalEntry.Group.CanonicalName} / {user.PortalEntry.BranchName}";

        if (user.PortalGroupId.HasValue && groupName is null)
        {
            var group = await registry.GetGroupAsync(user.PortalGroupId.Value, ct);
            groupName = group?.CanonicalName;
        }
        if (user.PortalEntryId.HasValue && entryLabel is null)
        {
            var entry = await registry.GetEntryWithDetailsAsync(user.PortalEntryId.Value, ct);
            entryLabel = entry?.Group is null ? null : $"{entry.Group.CanonicalName} / {entry.BranchName}";
        }

        return new DelegateDto(
            user.Id, user.Username, user.FullName, user.IsActive,
            user.PortalGroupId, groupName,
            user.PortalEntryId, entryLabel,
            user.CreatedAt, user.Version);
    }
}
