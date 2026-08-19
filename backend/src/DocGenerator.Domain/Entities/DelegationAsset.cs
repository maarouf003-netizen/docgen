namespace DocGenerator.Domain.Entities;

/// <summary>
/// أصلٌ (عقار أو متجر مسجل أو متجر غير مسجل) جرى التنفيذ عليه إنابةً ضمن ملف مناب:
/// سجل لكل أصل من الأموال موضوع الإنابة مع بدل المبيع (بالليرة السورية) عند البيع بالمزاد العلني.
///
/// يُحفظ «وصف قراءة» للأصل (النوع + وصفه) بدل مفتاح أجنبي إلى Asset، لأن أصول الملف المنيب
/// تُعاد بناؤها بمعرفات جديدة عند كل تعديل (Assets.Clear ثم إعادة إنشاء)، فتحافظ لقطة الإنابة
/// على الأصول كما كانت عند التسطير دون عمود يشير لصفٍّ قد يُحذف.
/// </summary>
public class DelegationAsset
{
    public int Id { get; set; }
    public int DelegationId { get; set; }

    /// <summary>نوع الأصل (AssetKindCatalog): عقار/مركبة/متجر/كفالة رواتب/متجر غير مسجل.</summary>
    public string AssetKind { get; set; } = string.Empty;

    /// <summary>وصف قراءة للأصل موضوع الإنابة (مثال: «عقار رقم 77 — المزة»).</summary>
    public string AssetLabel { get; set; } = string.Empty;

    /// <summary>بدل المبيع لهذا الأصل بالمزاد العلني (بالليرة السورية) — يُملأ عند إتمام الإنابة.</summary>
    public decimal? SalePrice { get; set; }

    public DocumentDelegation Delegation { get; set; } = null!;
}
