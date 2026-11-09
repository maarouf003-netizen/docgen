namespace DocGenerator.Application.Common;

/// <summary>
/// خيارات سجل أرقام الأساس. <see cref="AllowCircuitlessLegacy"/> مخصّص **للاختبارات
/// وبناءات البيانات القديمة فقط**؛ القيمة الافتراضية في الإنتاج `false` لأن V-18
/// يمنع أي رقم بلا دائرة. لا تُفعّله في الإنتاج.
/// </summary>
public sealed class NumberRegistryOptions
{
    public bool AllowCircuitlessLegacy { get; set; }
}
