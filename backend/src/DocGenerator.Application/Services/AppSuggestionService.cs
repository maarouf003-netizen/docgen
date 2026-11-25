using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// اقتراحات تطوير التطبيق: الإرسال للمستخدمين (المحامي الآن)، والقراءة الشاملة
/// وتعليم المقروء للمشرف والمدير (<c>BQ-001</c>) — والكتابة ضمن معاملة مع سجل التدقيق.
/// القراءة في معالجات الاستعلام تحت <c>UseCases/Suggestions</c>؛ وبقي هذا النوع
/// حدودًا ثابتة وإسقاطًا مشتركًا (مصدر واحد بلا تكرار).
/// </summary>
internal static class AppSuggestionService
{
    internal const int MessageMaxLength = 2000;

    /// <summary>حجم صفحة صندوق المشرف الافتراضي والأقصى — يمنع جلب الصندوق كاملًا.</summary>
    internal const int DefaultPerPage = 20;
    internal const int MaxPerPage = 50;

    internal static AppSuggestionDto ToDto(AppSuggestion s, bool includeSender) => new(
        s.Id,
        s.Message,
        s.CreatedAt,
        s.IsRead,
        includeSender ? s.Sender?.FullName ?? s.Sender?.Username : null);
}
