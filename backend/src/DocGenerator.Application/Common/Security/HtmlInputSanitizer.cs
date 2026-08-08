using AngleSharp.Html.Parser;
using Ganss.Xss;

namespace DocGenerator.Application.Common.Security;

/// <summary>
/// تعقيم HTML الوارد من محرر النصوص الغني (قائمة بيضاء صارمة).
/// يسمح فقط بالنصوص العادية وعلامات التنسيق الأساسية واللون عبر span[style]،
/// ويعقّم قيم CSS في خاصية style (يمنع url() والوسائط الخبيثة).
/// يُستخدم على الإدخال قبل الحفظ، وعلى استخراج النص العادي للتدقيق والتصدير.
/// </summary>
public static class HtmlInputSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();
    private static readonly HtmlParser HtmlParser = new();

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
                 {
                     "p", "br", "strong", "b", "em", "i", "u", "s", "span", "ul", "ol", "li",
                 })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("style");

        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedCssProperties.Add("color");

        return sanitizer;
    }

    public static string Sanitize(string? html)
        => string.IsNullOrEmpty(html) ? string.Empty : Sanitizer.Sanitize(html).Trim();

    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        var document = HtmlParser.ParseDocument(html);
        var body = document.Body;
        if (body is null)
            return string.Empty;

        var blocks = new List<string>();
        foreach (var child in body.ChildNodes)
        {
            var text = child.TextContent?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;
            blocks.Add(NormalizeWhitespace(text));
        }

        return string.Join(' ', blocks).Trim();
    }

    private static string NormalizeWhitespace(string value)
        => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
