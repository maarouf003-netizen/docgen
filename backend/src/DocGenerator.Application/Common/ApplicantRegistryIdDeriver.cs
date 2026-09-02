using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common;

/// <summary>
/// اشتقاق نسخة تسريع فلترة الجهة الطالبة (ApplicantRegistryId) من صفوف الجهات نفسها:
/// أول ربط سجلي غير فارغ، ويُصفَّر تلقائيًا حين تفرغ القائمة أو يزول الربط.
/// موحَّد المصدر لكل الكُتّاب (DocumentService.Apply / DocumentDelegationService / PublicEntityService).
/// </summary>
public static class ApplicantRegistryIdDeriver
{
    public static int? Derive(Document doc) =>
        doc.ApplicantPublicEntities
            .Select(a => a.RegistryId)
            .FirstOrDefault(id => id.HasValue);
}
