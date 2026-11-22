namespace DocGenerator.Application.Common;

/// <summary>
/// تخزين مؤقت للبيانات المرجعية **البحتة** فقط (أسماء/معرّفات الفروع والشعب والدوائر)،
/// لا العدّادات الحيّة التي تتغير مع كل مستند/مستخدم. الإبطال محلي بمفتاح مجموعة
/// مُصدَّر، مع TTL قصير كشبكة أمان؛ ولا يُستخدم لبيانات الصلاحيات أو النطاق.
/// </summary>
public interface IReferenceDataCache
{
    /// <summary>يقرأ من الذاكرة أو ينشئ القيمة عبر المصنع ويخزّنها تحت (المجموعة، المفتاح).</summary>
    Task<T> GetOrCreateAsync<T>(
        string group,
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct = default) where T : class;

    /// <summary>يبطل كل مفاتيح المجموعة (يُستدعى بعد أي كتابة على الكيان المرجعي).</summary>
    void Invalidate(string group);
}

/// <summary>أسماء مجموعات التخزين المرجعي — مصدر واحد يمنع أخطاء المفاتيح النصية.</summary>
public static class ReferenceDataGroups
{
    public const string Branches = "branches";
    public const string Sections = "sections";
    public const string Circuits = "circuits";
}
