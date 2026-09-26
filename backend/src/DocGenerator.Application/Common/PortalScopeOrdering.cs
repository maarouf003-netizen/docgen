using System.Globalization;

namespace DocGenerator.Application.Common;

/// <summary>
/// ترتيب العرض الوحيد لفروع النطاق — لغة عربية لا نقاط ترميز.
/// تستهلكه ثلاثة مواضع تقرأ بعضها ترتيب بعض، فأي مقارنين مختلفين ينتج قوائم
/// متباينة للنطاق نفسه:
/// (1) `PortalScopeResolution.Entries` (قائمة الفرع في الواجهة)،
/// (2) `BuildMatchedEntries` (خلية «فرع الجهة» في التصدير وبطاقة الملف)،
/// (3) كسر تعادل إحصاءات الفروع (`PerEntry`).
/// المصدر الوحيد هنا لا نسخ متفرقة — والاختبار
/// `Export_BranchCellOrderMatchesScopeDropdownOrder` يثبت التطابق ميكانيكيًا.
/// </summary>
public static class PortalScopeOrdering
{
    public static readonly StringComparer ArabicDisplay =
        StringComparer.Create(new CultureInfo("ar"), ignoreCase: false);
}
