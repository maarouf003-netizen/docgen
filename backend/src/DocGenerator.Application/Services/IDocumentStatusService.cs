using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// مساعدات حالة المستند التي تُستدعى داخل معاملة مستدعٍ خارجي (نواة←حالات — مسموح):
/// تطبيق بيان التجديد وتسجيل وقعة الشطب. أما انتقالات الحالة نفسها فصارت أوامر
/// في `P4-01bd` (لكل منها معاملتها وتدقيقها).
/// </summary>
public interface IDocumentStatusService
{
    /// <summary>
    /// تطبيق بيان تجديد الملف المشطوب: رقم الملف الجديد إلزامي (ومعه سنة الإعادة في نظام
    /// «طالبة تنفيذ»)، وتُفسَّر التواريخ النصية الحرة، ويُسجَّل رقم أساس لسنة الإعادة فيعود
    /// الملف بالرقم والنوع الجديدين — داخل معاملة المستدعي حصرًا (لا تفتح معاملة خاصة).
    /// </summary>
    Task ApplyRenewalAsync(Document doc, RenewalRequest? renewal, bool executedLike, int? userId, CancellationToken ct);

    /// <summary>
    /// تسجيل وقعة الشطب في «وقوعات الملف» عند انتقال ملف «منفذ عليه»/«عرض وايداع»
    /// إلى الحالة «مشطوب» — داخل معاملة المستدعي حصرًا (لا تفتح معاملة خاصة).
    /// </summary>
    Task AddStruckOffOccurrenceAsync(Document doc, int? userId, CancellationToken ct);
}
