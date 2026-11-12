namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لحدود حقول «المتدخل» ومجموعته في الملف. تُستهلك في تكوينات
/// قاعدة البيانات (أطوال الأعمدة) وفي تحقق الخدمة (رسائل 400 بدل انفجار المزود)
/// وفي الواجهة (maxLength) — فلا تنحرف القيود بين الطبقات.
/// </summary>
public static class InterventionLimitsCatalog
{
    /// <summary>الحد الأقصى لعدد المتدخلين في الملف الواحد (حماية من الإساءة لا قيد عمل).</summary>
    public const int MaxPerDocument = 50;

    public const int MaxNature = 20;
    public const int MaxReceiptNumber = 100;
    public const int MaxSubject = 2000;
    public const int MaxNotes = 2000;

    public const int MaxName = 100;
    public const int MaxBirth = 50;
    public const int MaxRegister = 100;
    public const int MaxNationalId = 50;
    public const int MaxAddressType = 50;
    public const int MaxAddress = 300;

    public const int MaxEntityName = 200;
    public const int MaxEntityBranch = 200;
    public const int MaxGovernorate = 100;
    public const int MaxRegistrationNumber = 100;
    public const int MaxRepresentedBy = 200;

    public const int MaxRepresentativeCapacity = 30;
}
