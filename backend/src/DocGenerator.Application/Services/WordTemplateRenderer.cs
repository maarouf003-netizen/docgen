using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Services;

/// <summary>
/// يعرض قالب Word أصلي (docx) عبر تعبئة placeholders بصيغة {{key}} / {{ key }} / {{r key}}.
/// محاكاة لسلوك docxtpl:
///  - معالجة على مستوى الـ runs في مكانها (لا تُهدم الفقرة أبداً)،
///  - تجميع نصوص الـ runs المنقسمة داخل الفقرة قبل المطابقة،
///  - القيمة التي تحتوي <w:r> تُدرج كنصوص XML خام (rich text)،
///  - القيمة العادية يُهرب نصها وتتحول الأسطر الجديدة إلى <w:br/>،
///  - فقرات الرسوم (مربعات النصوص/الصور) لا تُمس؛ تُعالج فقراتها الداخلية فرادى.
/// </summary>
public class WordTemplateRenderer : IDocumentRenderer
{
    private const string WordNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static readonly XNamespace W = WordNs;
    private static readonly XNamespace XmlNs = "http://www.w3.org/XML/1998/namespace";
    private static readonly Regex TokenRegex = new(
        @"\{\s*\{\s*(?:r\s+)?([A-Za-z0-9_]+)((?:\s+[A-Za-z0-9_]+)*)\s*\}\s*\}",
        RegexOptions.Compiled);

    private readonly WordTemplatesOptions _options;

    public WordTemplateRenderer(IOptions<WordTemplatesOptions> options) => _options = options.Value;

    public async Task<byte[]> RenderAsync(
        Dictionary<string, object> context,
        string templateCode,
        CancellationToken ct = default)
    {
        var templatePath = ResolveTemplatePath(templateCode);

        using var src = File.OpenRead(templatePath);
        using var srcZip = new ZipArchive(src, ZipArchiveMode.Read);
        using var outStream = new MemoryStream();

        using (var outZip = new ZipArchive(outStream, ZipArchiveMode.Create))
        {
            foreach (var entry in srcZip.Entries)
            {
                var newEntry = outZip.CreateEntry(entry.FullName);
                using var srcEntry = entry.Open();
                using var dstEntry = newEntry.Open();

                if (IsRenderedWordPart(entry.FullName))
                {
                    using var reader = new StreamReader(srcEntry, Encoding.UTF8, true);
                    var xml = await reader.ReadToEndAsync(ct);
                    if (xml.Contains('{'))
                        xml = ProcessXml(xml, context);
                    using var writer = new StreamWriter(dstEntry, new UTF8Encoding(false));
                    writer.Write(xml);
                    writer.Flush();
                }
                else
                {
                    await srcEntry.CopyToAsync(dstEntry, 81920, ct);
                }
            }
        }

        return outStream.ToArray();
    }

    private string ResolveTemplatePath(string templateCode)
    {
        if (!_options.Templates.TryGetValue(templateCode, out var fileName))
            throw new ArgumentException($"قالب غير معروف: {templateCode}");

        var path = Path.IsPathRooted(_options.Path)
            ? Path.Combine(_options.Path, fileName)
            : Path.Combine(AppContext.BaseDirectory, _options.Path, fileName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"القالب غير موجود: {fileName}", path);

        return path;
    }

    private static bool IsRenderedWordPart(string fullName)
    {
        if (!fullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase) ||
            !fullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return false;

        var lower = fullName.ToLowerInvariant();
        return lower == "word/document.xml"
            || lower.StartsWith("word/header", StringComparison.Ordinal)
            || lower.StartsWith("word/footer", StringComparison.Ordinal)
            || lower.StartsWith("word/footnotes", StringComparison.Ordinal)
            || lower.StartsWith("word/endnotes", StringComparison.Ordinal);
    }

    private static string ProcessXml(string xml, Dictionary<string, object> context)
    {
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);

        foreach (var paragraph in doc.Descendants(W + "p").ToList())
            ProcessParagraph(paragraph, context);

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static void ProcessParagraph(XElement paragraph, Dictionary<string, object> context)
    {
        var runs = paragraph.Elements(W + "r").ToList();
        if (runs.Count == 0)
            return;

        // الفقرات التي تحمل رسوماً (مربعات نصوص / صور / VML) لا يُعاد بناؤها أبداً وإلا حُذف الشكل كاملاً.
        // تُستبدل التوكينات داخل الـ runs النصية فقط في مكانها، بينما تُترك الـ runs الحاملة للرسوم كما هي.
        if (runs.Any(r => r.Descendants(W + "drawing").Any() || r.Descendants(W + "pict").Any()))
        {
            ReplaceTokensInDrawingParagraphRuns(runs, context);
            return;
        }

        var texts = runs.Select(r => string.Concat(r.Descendants(W + "t").Select(t => t.Value))).ToList();
        var full = string.Concat(texts);
        if (!full.Contains('{'))
            return;

        var matches = TokenRegex.Matches(full);
        if (matches.Count == 0)
            return;

        var content = BuildParagraphContent(runs, texts, full, matches, context);
        if (content is null)
            return;

        var pPr = paragraph.Elements().FirstOrDefault(e => e.Name == W + "pPr");
        var tail = paragraph.Elements().Where(e => e.Name != W + "pPr" && e.Name != W + "r").ToList();
        paragraph.RemoveNodes();
        if (pPr is not null)
            paragraph.Add(pPr);
        paragraph.Add(content);
        foreach (var element in tail)
            paragraph.Add(element);
    }

    private static void ReplaceTokensInDrawingParagraphRuns(List<XElement> runs, Dictionary<string, object> context)
    {
        foreach (var run in runs)
        {
            if (run.Descendants(W + "drawing").Any() || run.Descendants(W + "pict").Any())
                continue;

            var textNodes = run.Descendants(W + "t").ToList();
            if (textNodes.Count == 0)
                continue;

            var full = string.Concat(textNodes.Select(t => t.Value));
            if (!full.Contains('{'))
                continue;

            var matches = TokenRegex.Matches(full);
            if (matches.Count == 0)
                continue;

            var newNodes = new List<XElement>();
            var position = 0;
            foreach (Match match in matches)
            {
                if (match.Index > position)
                    newNodes.Add(CreateTextNode(full.Substring(position, match.Index - position)));

                var key = match.Groups[1].Value;
                var rest = match.Groups[2].Value;
                var raw = context.TryGetValue(key, out var value)
                    ? value?.ToString() ?? string.Empty
                    : string.Empty;

                // داخل الفقرات الحاملة للرسوم لا تُدرج نصوص XML خام؛ نستعمل النص المهرب فقط.
                if (raw.StartsWith("<w:", StringComparison.Ordinal))
                    raw = string.Empty;

                newNodes.AddRange(CreateTextNodesForValue(raw + rest));
                position = match.Index + match.Length;
            }

            if (position < full.Length)
                newNodes.Add(CreateTextNode(full.Substring(position)));

            foreach (var t in textNodes)
                t.Remove();
            foreach (var node in newNodes)
                run.Add(node);
        }
    }

    private static XElement CreateTextNode(string text) =>
        new(W + "t", new XAttribute(XmlNs + "space", "preserve"), text);

    private static IEnumerable<XElement> CreateTextNodesForValue(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                yield return new XElement(W + "br");
            yield return CreateTextNode(lines[i]);
        }
    }

    private static List<XElement>? BuildParagraphContent(
        List<XElement> runs,
        List<string> texts,
        string full,
        MatchCollection matches,
        Dictionary<string, object> context)
    {
        var runEnds = new int[texts.Count];
        var acc = 0;
        for (var i = 0; i < texts.Count; i++)
        {
            acc += texts[i].Length;
            runEnds[i] = acc;
        }

        int RunIndexAt(int offset)
        {
            for (var i = 0; i < runEnds.Length; i++)
                if (runEnds[i] > offset)
                    return i;
            return runEnds.Length - 1;
        }

        var content = new List<XElement>();

        void EmitPlain(int from, int to)
        {
            while (from < to)
            {
                var ri = RunIndexAt(from);
                var segEnd = Math.Min(to, runEnds[ri]);
                var segment = full.Substring(from, segEnd - from);
                if (segment.Length > 0)
                    content.Add(CloneRunWithText(runs[ri], segment));
                from = segEnd;
            }
        }

        void EmitReplacement(Match match)
        {
            var key = match.Groups[1].Value;
            var rest = match.Groups[2].Value;
            var raw = context.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;

            if (raw.StartsWith("<w:", StringComparison.Ordinal))
            {
                content.AddRange(ParseRawXml(raw));
                if (rest.Length > 0)
                    content.Add(CloneRunWithText(runs[RunIndexAt(match.Index)], rest));
                return;
            }

            var text = raw + rest;
            content.Add(CreateValueRun(runs[RunIndexAt(match.Index)], text));
        }

        var position = 0;
        foreach (Match match in matches)
        {
            if (match.Index > position)
                EmitPlain(position, match.Index);
            EmitReplacement(match);
            position = match.Index + match.Length;
        }

        if (position < full.Length)
            EmitPlain(position, full.Length);

        return content;
    }

    private static XElement CreateValueRun(XElement run, string text)
    {
        var clone = new XElement(run);
        foreach (var t in clone.Descendants(W + "t").ToList())
            t.Remove();

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                clone.Add(new XElement(W + "br"));
            clone.Add(new XElement(W + "t", new XAttribute(XmlNs + "space", "preserve"), lines[i]));
        }

        return clone;
    }

    private static XElement CloneRunWithText(XElement run, string text)
    {
        var clone = new XElement(run);
        foreach (var t in clone.Descendants(W + "t").ToList())
            t.Remove();
        clone.Add(new XElement(W + "t", new XAttribute(XmlNs + "space", "preserve"), text));
        return clone;
    }

    private static IEnumerable<XElement> ParseRawXml(string value)
    {
        var wrapped = $"<root xmlns:w=\"{WordNs}\">{value}</root>";
        return XElement.Parse(wrapped).Elements().ToList();
    }
}
