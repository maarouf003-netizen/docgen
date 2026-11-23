using System.Text.RegularExpressions;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// تحقق محتوى رسالة المنتدى وإعادة صياغة الذِكر — نقطة واحدة تستهلكها الخدمة
/// ومعالجا النشر/التعديل، فلا تنحرف الرسائل أو السقوف بين المسارات.
/// </summary>
internal static class ForumMessageContent
{
    /// <summary>
    /// رمز الذِكر القانوني `@[id:الاسم]` — مولَّد من المنتقي، ويُعاد توليده من
    /// الخادم عند الحفظ (فلا يُزوَّر اسم لمعرف). الشاذ منه نص حرفي بلا شريحة.
    /// </summary>
    private static readonly Regex MentionToken =
        new(@"@\[(\d{1,9}):([^\[\]]{1,100})\]", RegexOptions.Compiled);

    internal static string RequireBody(string? body)
    {
        var trimmed = body?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("نص الرسالة مطلوب");
        if (trimmed.Length > ForumService.MaxBodyLength)
            throw new ArgumentException($"نص الرسالة يتجاوز الحد الأقصى ({ForumService.MaxBodyLength} حرفًا)");
        return trimmed;
    }

    /// <summary>
    /// تحقق الذِكر وإعادة صياغته القانونية: كل رمز `@[id:…]` يُطابَق مع حساب
    /// قائم نشط (والا `400`)، والسقف عشرة مميزين، ثم يُعاد توليد الرموز بالاسم
    /// الرسمي الحالي (فلا يُزوَّر اسم لمعرف ولا يُحقَن `HTML` — النص عادي).
    /// الشاذ من الصيغة يبقى نصًا حرفيًا بلا شريحة.
    /// </summary>
    internal static async Task<string> ResolveMentionsAsync(
        string body, IUserRepository users, CancellationToken ct)
    {
        var matches = MentionToken.Matches(body);
        if (matches.Count == 0)
            return body;

        var ids = new HashSet<int>();
        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out var id))
                ids.Add(id);
        }
        if (ids.Count > ForumService.MaxMentionsPerMessage)
            throw new ArgumentException(
                $"الذِكر محدود بـ ({ForumService.MaxMentionsPerMessage}) أشخاص في الرسالة الواحدة");

        var canonical = new Dictionary<int, string>();
        foreach (var id in ids)
        {
            // رسالة موحدة للغائب والمعطّل والمندوب عمدًا: التفريق بينها يكشف
            // وجود حسابات معطّلة وأسماءها لمن يخمن المعرفات (البحث يعرض
            // النشطين الداخليين فقط)، والمندوب مستبعد من المنتدى كله.
            var user = await users.GetByIdAsync(id, ct);
            if (user is null || !user.IsActive || user.Role == UserRole.EntityManager)
                throw new ArgumentException("مستخدم مذكور غير صالح");
            canonical[id] = ForumIdentityParts.DisplayName(user);
        }

        var rewritten = MentionToken.Replace(body, match =>
            int.TryParse(match.Groups[1].Value, out var id) && canonical.TryGetValue(id, out var name)
                ? "@[" + id + ":" + name + "]"
                : match.Value);
        if (rewritten.Length > ForumService.MaxBodyLength)
            throw new ArgumentException($"نص الرسالة يتجاوز الحد الأقصى ({ForumService.MaxBodyLength} حرفًا) بعد صياغة الذِكر");
        return rewritten;
    }

    internal static string Truncate(string value, int max)
        => value.Length <= max ? value : value.Substring(0, max);
}
