using System.Diagnostics;

namespace DocGenerator.Application.Common.Observability;

/// <summary>
/// `P5-01` — مصدر الأنشطة المشترك لطبقة `Application` (أوامر واستعلامات).
/// <para>
/// بلا مستمع مسجَّل على المصدر تعيد <see cref="ActivitySource.StartActivity(string)"/>
/// القيمة <c>null</c>، فيبقى إنشاء النشاط بلا تكلفة تُذكر ولا يتغيّر أي سلوك.
/// الأسماء هنا **بلا بيانات طلب** (لا أجسام ولا أسماء مستخدمين ولا كلمات مرور):
/// اسم نوع الطلب واسم المعالج فقط.
/// </para>
/// </summary>
public static class ApplicationDiagnostics
{
    /// <summary>اسم المصدر الذي يُسجَّل في مزوّد التتبع عند تفعيل الرصد.</summary>
    public const string ActivitySourceName = "DocGenerator.Application";

    /// <summary>وسم اسم نوع الطلب (أمر/استعلام) — بلا قيم الحقول.</summary>
    public const string RequestTypeTag = "docgen.request.type";

    /// <summary>وسم اسم صنف المعالج المنفّذ.</summary>
    public const string HandlerTag = "docgen.handler";

    /// <summary>وسم نتيجة التنفيذ: نجاح أو استثناء.</summary>
    public const string OutcomeTag = "docgen.outcome";

    public const string SuccessOutcome = "success";

    public const string ExceptionOutcome = "exception";

    /// <summary>
    /// المصدر الوحيد لجميع أنشطة المعالجات. <see cref="ActivitySource"/> آمن للاستخدام
    /// المتزامن (مواصفة `System.Diagnostics`)، فكل تنفيذ يملك نشاطه المستقل.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);
}
