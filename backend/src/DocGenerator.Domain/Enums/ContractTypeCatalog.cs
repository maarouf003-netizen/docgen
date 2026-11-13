namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لقيم «نوع السند» (`ContractTypeSelector`) وقاعدة اختيار مجموعة المبالغ.
/// المصرفي يقرأ `Amount*`، والعادي يقرأ `InclusionAmount*` — لا تُجمع المجموعتان.
/// أي قيمة غير «عادي» (بما فيها الفارغ/المجهول) تُعامل مصرفيًا، توافقًا مع السلوك القائم.
/// </summary>
public static class ContractTypeCatalog
{
    public const string Banking = "مصرفي";
    public const string Ordinary = "عادي";

    /// <summary>
    /// هل السند مصرفي؟ القيمة «عادي» (بعد التشذيب) فقط هي العادي؛ ما عداها مصرفي.
    /// </summary>
    public static bool IsBanking(string? selector) =>
        !string.Equals(selector?.Trim(), Ordinary, StringComparison.Ordinal);
}
