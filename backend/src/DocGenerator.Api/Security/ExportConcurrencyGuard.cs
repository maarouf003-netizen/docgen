using System.Collections.Concurrent;

namespace DocGenerator.Api.Security;

/// <summary>
/// حارس تصدير واحد جارٍ لكل فاعل: يمنع تكديس توليدات المصنفات الثقيلة
/// (تبويبان/تحديث `F5`/نوافذ متعددة) التي تضرب ذاكرة الخادم — الطلب الثاني
/// أثناء الجريان يُردّ `429` فورًا بدل طلب ثانٍ كامل.
/// الخانة تُحرَّر في `finally` دائمًا (نجاح/فشل/إلغاء عميل) فلا تعلق أبدًا.
/// ملاحظة التوسع: ذاكرة محلية لكل عقدة كالمحدد العام (`RateLimitingSetup`)
/// — كافٍ لعقدة واحدة.
/// </summary>
public sealed class ExportConcurrencyGuard
{
    private readonly ConcurrentDictionary<string, byte> _running = new();

    /// <summary>محاولة دخول غير حاجبة: `true` إن كانت الخانة حرة (وأُخذت).</summary>
    public bool TryEnter(string key) => _running.TryAdd(key, 0);

    /// <summary>تحرير الخانة — آمن التكرار (لا يرمي على مفتاح غائب).</summary>
    public void Exit(string key) => _running.TryRemove(key, out _);

    /// <summary>
    /// مفتاح الفاعل — نفس مخطط `RateLimitingSetup.SubjectOf` (`user:` أو `ip:`)
    /// مسبوقًا بنطاق `export:` لعزل مساحة المفاتيح. الصيغة عقد ثابت
    /// تعتمد عليه الاختبارات — لا تُغيَّر بصمت.
    /// </summary>
    public static string KeyFor(string? userId, string? ip)
        => "export:" + (userId is not null ? $"user:{userId}" : $"ip:{ip ?? "unknown"}");
}
