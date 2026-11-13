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
    /// <summary>حد عمود `Document.Applicant` (200) — القص يمنع فشل PostgreSQL عند تعدد الجهات.</summary>
    public const int MaxLength = 200;

    public static string Build(IEnumerable<ApplicantPublicEntity> entities)
    {
        var text = string.Join(" و ", entities
            .Select(e =>
            {
                var name = (e.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                    return string.Empty;
                var governorate = (e.Governorate ?? string.Empty).Trim();
                return string.IsNullOrWhiteSpace(governorate) ? name : $"{name} - محافظة {governorate}";
            })
            .Where(v => v.Length > 0));

        if (text.Length <= MaxLength)
            return text;

        // قصّ آمن لبداية زوج بديل UTF-16 ثم علامة استمرار.
        var end = MaxLength - 1;
        if (end > 0 && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end]))
            end--;
        return string.Concat(text.AsSpan(0, end), "…");
    }
}
