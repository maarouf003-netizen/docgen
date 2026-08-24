using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common;

/// <summary>
/// النص الموحّد لطالب التنفيذ في وضع «طالبة تنفيذ» من قائمة الجهات:
/// «الجهة - محافظة X و الجهة - محافظة Y» — يُشتق ليغذي البحث والتصدير والتوليد،
/// ويُعاد بناؤه عند مزامنة أسماء الجهات من السجل المرجعي.
/// الفرع لا يُضمّن هنا؛ يُعرض ويُفلتر عبر حقل الفرع المستقل في ApplicantPublicEntities.Branch.
/// </summary>
public static class ApplicantTextBuilder
{
    public static string Build(IEnumerable<ApplicantPublicEntity> entities) =>
        string.Join(" و ", entities
            .Select(e =>
            {
                var name = (e.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                    return string.Empty;
                var governorate = (e.Governorate ?? string.Empty).Trim();
                return string.IsNullOrWhiteSpace(governorate) ? name : $"{name} - محافظة {governorate}";
            })
            .Where(v => v.Length > 0));
}
