using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Common;

/// <summary>
/// صفحة ملخصات تنبيهات من المستودع: الصفوف المسقطة في SQL والعدد الكلي المحسوب
/// باستعلام <c>COUNT</c> منفصل بنفس الفلاتر (الخطة §13) — لا <c>Count()</c> بعد
/// <c>Skip/Take</c>، ولا سقف صامت.
/// </summary>
public sealed record HeadAlertSummaryPage(
    IReadOnlyList<HeadAlertSummaryDto> Items,
    int TotalCount);
