namespace DocGenerator.Application.Common;

/// <summary>
/// سياسة المبالغ المالية المركزية (W1/B3): لا سالب، ولا تجاوز لدقة العمود
/// <c>numeric(20,2)</c>، ولا أكثر من منزلتين عشريتين. تُستدعى من كل مسار كتابة مالية
/// (إنشاء/تعديل/حالة/إتمام إنابة) كي لا يعتمد الرفض على قيد القاعدة فيتحول إلى 500
/// أو على تقريب صامت يجعل التفقيط يخالف الرقم المخزَّن.
/// </summary>
public static class DocumentAmountPolicy
{
    /// <summary>أقصى قيمة يمثلها <c>numeric(20,2)</c>.</summary>
    public const decimal MaxAmount = 999_999_999_999_999_999.99m;

    /// <summary>يتحقق من مبلغ واحد؛ رسالة الخطأ تحمل اسم الحقل بالعربية.</summary>
    public static void EnsureValid(decimal? value, string label)
    {
        if (value is null)
            return;

        var amount = value.Value;
        if (amount < 0)
            throw new ArgumentException($"{label} لا يكون سالبًا");
        if (amount > MaxAmount)
            throw new ArgumentException($"{label} يتجاوز الحد الأقصى المسموح");
        if (decimal.Round(amount, 2) != amount)
            throw new ArgumentException($"{label} يقبل منزلتين عشريتين فقط");
    }
}
