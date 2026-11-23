using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// قواعد «المتدخلين» الخالصة/المشتركة بين معالجي الإضافة والتعديل والحذف — نقطة واحدة
/// تمنع انحراف رسائل الحرّاس أو اعتماد بيانات السجل المرجعي بين المسارات.
/// </summary>
internal static class DocumentInterventionRules
{
    /// <summary>
    /// صفتا الملف المدعومتان هما «طالبة التنفيذ» و«منفذ عليها»؛ «عرض وايداع» مستثناة
    /// بقرار المالك (الجهة فيها عارضة للمبلغ لا خصمًا في التدخل).
    /// </summary>
    internal static void EnsureSupportedSide(Document doc)
    {
        if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
            throw new ArgumentException("لا يمكن إضافة متدخل في ملف صفة «عرض وايداع»");
    }

    /// <summary>
    /// للجهة العامة: يتحقق من قيد السجل المرجعي وجهته الأم، ويعتمد اسم الجهة المعتمد
    /// وفرعها ومحافظتها من السجل لا من نص العميل — فيبقى الربط صحيحًا مهما عبث الطلب.
    /// عند التعديل يُقبل القيد نفسه وإن كان قد عُطّل لاحقًا (حفاظًا على قابلية تعديل
    /// بيانات تاريخية مرتبطة بجهة عُطّلت)، أما القيد الجديد/المختلف فيجب أن يكون نشطًا.
    /// </summary>
    internal static async Task ResolveRegistryAsync(
        DocumentIntervention intervention,
        int? existingRegistryId,
        IRepository<PublicEntity> publicEntities,
        IRepository<PublicEntityGroup> publicEntityGroups,
        CancellationToken ct)
    {
        if (intervention.Nature != PartyNatureCatalog.PublicEntity)
            return;
        if (intervention.RegistryId is null)
            throw new ArgumentException("اختر جهة عامة صالحة من السجل المرجعي");

        var registry = await publicEntities.GetByIdAsync(intervention.RegistryId.Value, ct);
        var group = registry is null
            ? null
            : await publicEntityGroups.GetByIdAsync(registry.GroupId, ct);
        var keepExisting = existingRegistryId is not null
            && intervention.RegistryId == existingRegistryId;
        if (registry is null
            || group is null
            || (!registry.IsActive && !keepExisting)
            || (!group.IsActive && !keepExisting))
            throw new ArgumentException("اختر جهة عامة صالحة من السجل المرجعي");

        intervention.EntityName = group.CanonicalName;
        intervention.EntityBranch = registry.BranchName;
        intervention.Governorate = registry.Governorate;
    }
}
