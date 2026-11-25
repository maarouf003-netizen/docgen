using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// منتدى المحامين: تيار واحد مسطّح عابر للفروع عمدًا — نص عادي فقط، والهوية لقطات
/// تُجمَع من المستخدم وفرعه وشعبته لحظة الإرسال (لا من مدخلات العميل إطلاقًا)،
/// والحذف صلب دائمًا. بلا سجل تدقيق (خارج النطاق بقرار المالك).
/// القراءة/البحث في معالجات الاستعلام تحت <c>UseCases/Forum</c>؛ النشر/التعديل/
/// الحذف/التثبيت/القراءة عبر أوامر <c>P4-01</c>؛ وبقي هذا النوع حدودًا ثابتة
/// وإسقاطًا مشتركًا بين المعالجين والمساعدات (مصدر واحد بلا تكرار).
/// </summary>
internal static class ForumService
{
    /// <summary>حدّ النص (يطابق عمود القاعدة — يُفرض هنا برسالة عربية لا بالاستثناء الخام).</summary>
    internal const int MaxBodyLength = 2000;
    internal const int DefaultLimit = 30;
    internal const int MaxLimit = 100;
    internal const int MaxQuotedAuthorLength = 150;
    internal const int MaxQuotedExcerptLength = 200;
    /// <summary>سقف المذكورين المميزين في الرسالة الواحدة (مرحلة 2).</summary>
    internal const int MaxMentionsPerMessage = 10;
    /// <summary>سقف مرشحي منتقي الذِكر المعروضين.</summary>
    internal const int MentionLookupLimit = 20;

    internal static ForumMessageDto ToDto(ForumMessage m, int readCount) => new(
        m.Id,
        m.Body,
        m.AuthorId,
        m.AuthorName,
        m.AuthorRole,
        m.AuthorLocation,
        m.AuthorSection,
        m.IsPinned,
        m.QuotedMessageId,
        m.QuotedAuthorName,
        m.QuotedExcerpt,
        m.CreatedAt,
        m.EditedById,
        m.EditedAtUtc,
        readCount,
        m.Version);
}

/// <summary>
/// أجزاء هوية المستخدم للقطات المنتدى (الاسم/الموقع/الشعبة): مصدر واحد تشترك
/// فيه لقطة الكاتب ومرشحو الذِكر وإعادة صياغته — بلا ازدواج منطق.
/// </summary>
internal static class ForumIdentityParts
{
    internal static string DisplayName(User user)
        => string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName.Trim();

    internal static string ResolveLocation(User user)
    {
        var governorate = user.Branch?.Governorate?.Trim();
        if (!string.IsNullOrEmpty(governorate))
            return governorate;
        var name = user.Branch?.Name?.Trim();
        return string.IsNullOrEmpty(name) ? "كل الفروع" : name;
    }

    internal static string? ResolveSection(User user)
    {
        if (user.Role != UserRole.SubHead)
            return null;
        var name = user.Section?.Name?.Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }
}
