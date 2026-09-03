using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common;

/// <summary>
/// تطبيع ملاك الأصل: تجاهل الفارغ، قصّ الطرفين، وإلغاء التكرار مع حفظ ترتيب الاختيار.
/// استُخرج من DocumentService ضمن إعادة الهيكلة المتدرجة — السلوك مطابق حرفيًا.
/// </summary>
public static class AssetMapper
{

    public static List<AssetOwner> NormalizeOwners(IEnumerable<string?>? owners)
    {
        var result = new List<AssetOwner>();
        if (owners is null)
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var order = 0;
        foreach (var owner in owners)
        {
            var name = (owner ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                continue;

            result.Add(new AssetOwner { Name = name, Order = order++ });
        }

        return result;
    }

    /// <summary>
    /// تصفية صفوف الورثة الصالحة فقط: يُتجاهل الوريث الخالي من الاسم الثلاثي كاملًا
    /// (الاسم واسم الأب والنسبة جميعًا)، ويُقيَّد نوع العنوان بالقيم المسموح بها
    /// («عنوان»/«موطن مختار»/«وكيل») مع معاملة أي قيمة أخرى أو فارغة كـ«عنوان»،
    /// وصفة الوريث بالقيم المسموح بها («أصالة»/«إضافة لتركة»/«أصالة وإضافة»)
    /// مع معاملة أي قيمة أخرى أو فارغة كـ«أصالة».
    /// </summary>
}