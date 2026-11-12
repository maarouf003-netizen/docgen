namespace DocGenerator.Application.Common;

/// <summary>
/// قواعد «تذكيرات الشطب»: العتبة (5 أشهر قبل مهلة الشطب القانونية البالغة 6 أشهر) وحدّ
/// «مضى 5 أشهر». **مصدر الحقيقة الوحيد** للعتبة والقطع — يستخدمه المستودع (استعلام SQL)
/// والخدمة والاختبارات، فلا تنحرف نسخة عن أخرى.
///
/// <para><b>آخر نشاط الملف</b> (يقاس عليه): أحدث <c>ActionDateParsed</c> على الإجراءات فقط
/// (`Type == "action"`، والملاحظات لا تُحتسب)، فإن غاب فتاريخ القيد المحلول، فإن غاب
/// فتاريخ إدخال الملف. القاعدة الكاملة في
/// <c>docs/STRIKE_OFF_REMINDERS_PLAN.md</c> §1.</para>
/// </summary>
public static class StrikeOffReminderCalculator
{
    /// <summary>عتبة التذكير بالأشهر (قبل الشطب بشهر واحد).</summary>
    public const int Months = 5;

    /// <summary>حجم الصفحة الافتراضي في قائمة التذكيرات.</summary>
    public const int DefaultPerPage = 20;

    /// <summary>الحد الأقصى المسموح لحجم الصفحة (حماية من الطلبات الضخمة).</summary>
    public const int MaxPerPage = 100;

    /// <summary>
    /// حدّ «مضى 5 أشهر» كـ UTC: منتصف ليل اليوم بتوقيت النظام ناقص 5 أشهر محوّلًا إلى UTC.
    /// التخزين UTC دائمًا؛ وتواريخ «اليوم» (<c>ActionDateParsed</c>/<c>DateParsed</c>) تُخزَّن
    /// منتصف ليل موسومًا UTC، فيبقى الفارق ساعات توقيت النظام على حدّ يوم واحد — مقبول
    /// لقاعدة تُقاس بالأشهر (قرار موثّق في الخطة).
    /// </summary>
    public static DateTime CutoffUtc(DateTime nowLocal, TimeZoneInfo timeZone)
        => TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(nowLocal.Date.AddMonths(-Months), DateTimeKind.Unspecified),
            timeZone);

    /// <summary>هل بلغ الملف العتبة؟ (آخر نشاط ≤ الحد ⇒ مستحق للتذكير).</summary>
    public static bool IsDue(DateTime lastActivityUtc, DateTime cutoffUtc)
        => lastActivityUtc <= cutoffUtc;
}
