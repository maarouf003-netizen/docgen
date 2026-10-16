namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لسقوف أطراف الملف في صفة «الجهة العامة طالبة التنفيذ» (مصرفي/عادي):
/// إجمالي المنفذ عليهم 50 شاملًا المقترض (1 مقترض + حتى 49 كفيلًا/منفذًا عليه آخر).
/// يطابق الثابت الأمامي `MAX_TOTAL_DEBTORS`/`MAX_GUARANTORS` في `documentFormConstants.ts` —
/// أي تغيير هنا يستلزم تغييرًا مقابلًا هناك (وتحديث قالب `Seizure.docx` إن تجاوز 50).
/// الورثة والممثلون الشرعيون بلا سقف بقرار المالك.
/// </summary>
public static class PartyLimitsCatalog
{
    /// <summary>إجمالي المنفذ عليهم في الملف شاملًا المقترض (مقترض واحد + كفلاء).</summary>
    public const int MaxTotalDebtors = 50;

    /// <summary>الحد الأقصى لصفوف الكفلاء/المنفذ عليهم الآخرين (الإجمالي ناقص المقترض).</summary>
    public const int MaxGuarantors = MaxTotalDebtors - 1;
}
