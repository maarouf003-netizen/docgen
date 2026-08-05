using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocGenerator.Application.Common.Options;
using DocGenerator.Application.Services;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Tests;

public class WordTemplateRendererTests : IDisposable
{
    private readonly string _templateDir;

    public WordTemplateRendererTests()
    {
        _templateDir = Path.Combine(Path.GetTempPath(), $"docgen_tpl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_templateDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_templateDir, recursive: true); } catch { /* ignore */ }
    }

    private static byte[] CreateDocx(params string[][] paragraphs)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = new Body();
            foreach (var runs in paragraphs)
            {
                var paragraph = new Paragraph();
                foreach (var text in runs)
                    paragraph.Append(new Run(new Text(text)));
                body.Append(paragraph);
            }
            main.Document.Append(body);
            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static byte[] CreateDocxWithTextBox(string[] textBoxRuns, string paragraphText)
    {
        var runsXml = string.Concat(textBoxRuns.Select(r => $"<w:r><w:t>{r}</w:t></w:r>"));
        var documentXml = (
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" +
            @"<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"" " +
            @"xmlns:wps=""http://schemas.microsoft.com/office/word/2010/wordprocessingShape"" " +
            @"xmlns:mc=""http://schemas.openxmlformats.org/markup-compatibility/2006"">" +
            @"<w:body><w:p><w:r><mc:AlternateContent><mc:Choice Requires=""wps""><w:drawing>" +
            @"<wps:wsp><wps:cNvSpPr txBox=""1""/><wps:txbx><w:txbxContent><w:p>" +
            @"__RUNS__" +
            @"</w:p></w:txbxContent></wps:txbx></wps:wsp></w:drawing></mc:Choice></mc:AlternateContent></w:r></w:p>" +
            @"<w:p><w:r><w:t>__PARA__</w:t></w:r></w:p><w:sectPr/></w:body></w:document>")
            .Replace("__RUNS__", runsXml)
            .Replace("__PARA__", paragraphText);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(zip.CreateEntry("[Content_Types].xml").Open()))
                w.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types""><Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/><Default Extension=""xml"" ContentType=""application/xml""/><Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/></Types>");
            using (var w = new StreamWriter(zip.CreateEntry("_rels/.rels").Open()))
                w.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/></Relationships>");
            using (var w = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
                w.Write(documentXml);
        }
        return ms.ToArray();
    }

    private WordTemplateRenderer CreateRenderer(string fileName)
    {
        var options = new WordTemplatesOptions { Path = _templateDir };
        options.Templates[Path.GetFileNameWithoutExtension(fileName)] = fileName;
        return new WordTemplateRenderer(Options.Create(options));
    }

    private void WriteTemplate(string fileName, params string[][] paragraphs)
        => File.WriteAllBytes(Path.Combine(_templateDir, fileName), CreateDocx(paragraphs));

    private static string ReadDocumentXml(byte[] docx)
    {
        using var ms = new MemoryStream(docx);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var entry = zip.GetEntry("word/document.xml");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string ReadDocumentText(byte[] docx)
    {
        var xml = ReadDocumentXml(docx);
        return string.Concat(System.Text.RegularExpressions.Regex
            .Matches(xml, "<w:t[^>]*>([^<]*)</w:t>")
            .Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)));
    }

    [Fact]
    public async Task Render_ReplacesSingleRunPlaceholder()
    {
        WriteTemplate("a.docx", new[] { "المحكمة: {{court}}" });
        var renderer = CreateRenderer("a.docx");

        var result = await renderer.RenderAsync(
            new Dictionary<string, object> { ["court"] = "دمشق" }, "a");

        var text = ReadDocumentText(result);
        Assert.Contains("المحكمة: دمشق", text);
        Assert.DoesNotContain("{{", text);
        Assert.DoesNotContain("}}", text);
    }

    [Fact]
    public async Task Render_ReplacesPlaceholderSplitAcrossRuns()
    {
        WriteTemplate("a.docx",
            new[] { "{{ borrower_", "name }}", " آخر" });
        var renderer = CreateRenderer("a.docx");

        var result = await renderer.RenderAsync(
            new Dictionary<string, object> { ["borrower_name"] = "أحمد الخطيب" }, "a");

        Assert.Contains("أحمد الخطيب آخر", ReadDocumentText(result));
    }

    [Fact]
    public async Task Render_RichPrefixPlaceholder_InsertsRawXmlRuns()
    {
        WriteTemplate("a.docx", new[] { "{{r execution_debtors_and_its_adresses}}" });
        var renderer = CreateRenderer("a.docx");

        var result = await renderer.RenderAsync(
            new Dictionary<string, object>
            {
                ["execution_debtors_and_its_adresses"] =
                    "<w:r><w:rPr><w:b/></w:rPr><w:t xml:space=\"preserve\">الاسم الكامل</w:t></w:r>"
            }, "a");

        var xml = ReadDocumentXml(result);
        Assert.Contains("الاسم الكامل", xml);
        Assert.Contains("<w:b", xml);
        Assert.DoesNotContain("{{", xml);
    }

    [Fact]
    public async Task Render_NewlinesBecomeBreaks()
    {
        WriteTemplate("a.docx", new[] { "{{amount_words}}" });
        var renderer = CreateRenderer("a.docx");

        var result = await renderer.RenderAsync(
            new Dictionary<string, object> { ["amount_words"] = "سطر أول\nسطر ثاني" }, "a");

        var xml = ReadDocumentXml(result);
        Assert.Contains("<w:br", xml);
        Assert.Contains("سطر أول", ReadDocumentText(result));
        Assert.Contains("سطر ثاني", ReadDocumentText(result));
    }

    [Fact]
    public async Task Render_MissingKey_RendersEmpty()
    {
        WriteTemplate("a.docx", new[] { "قبل {{missing_key}} بعد" });
        var renderer = CreateRenderer("a.docx");

        var result = await renderer.RenderAsync(new Dictionary<string, object>(), "a");

        Assert.Equal("قبل  بعد", ReadDocumentText(result));
    }

    [Fact]
    public async Task Render_MissingKey_RemovesToken()
    {
        WriteTemplate("a.docx", new[] { "{{court}} و {{other}}" });
        var renderer = CreateRenderer("a.docx");

        var result = await renderer.RenderAsync(
            new Dictionary<string, object> { ["court"] = "حلب" }, "a");

        var text = ReadDocumentText(result);
        Assert.Contains("حلب", text);
        Assert.Equal("حلب و ", text);
    }

    [Fact]
    public async Task Render_OutputIsValidDocxPackage()
    {
        WriteTemplate("a.docx", new[] { "نص: {{court}}" });
        var renderer = CreateRenderer("a.docx");

        var result = await renderer.RenderAsync(
            new Dictionary<string, object> { ["court"] = "دمشق" }, "a");

        Assert.True(result.Length > 2);
        Assert.Equal((byte)0x50, result[0]); // 'P'
        Assert.Equal((byte)0x4B, result[1]); // 'K'
        using var ms = new MemoryStream(result);
        using var doc = WordprocessingDocument.Open(ms, false);
        Assert.NotNull(doc.MainDocumentPart);
        Assert.Contains("دمشق", doc.MainDocumentPart!.Document.Body!.InnerText);
    }

    [Fact]
    public async Task Render_SinglePlaceholder_EmptyValue_RemovesToken()
    {
        WriteTemplate("a.docx", new[] { "{{borrower_mother}}" });
        var renderer = CreateRenderer("a.docx");

        var result = await renderer.RenderAsync(new Dictionary<string, object>(), "a");

        var text = ReadDocumentText(result);
        Assert.DoesNotContain("{{", text);
        Assert.DoesNotContain("}}", text);
    }

    [Fact]
    public async Task Render_PlaceholderInsideTextBox_PreservesTextBoxAndReplaces()
    {
        File.WriteAllBytes(
            Path.Combine(_templateDir, "tb.docx"),
            CreateDocxWithTextBox(new[] { "{{court}}" }, ""));
        var renderer = CreateRenderer("tb.docx");

        var result = await renderer.RenderAsync(
            new Dictionary<string, object> { ["court"] = "دمشق" }, "tb");

        var xml = ReadDocumentXml(result);
        Assert.Contains("دمشق", xml);
        Assert.DoesNotContain("{{", xml);
        Assert.Contains("<w:txbxContent>", xml);
        Assert.Contains("<w:drawing>", xml);
    }

    [Fact]
    public async Task Render_PlaceholderSplitAcrossRunsInsideTextBox_PreservesTextBox()
    {
        File.WriteAllBytes(
            Path.Combine(_templateDir, "tb.docx"),
            CreateDocxWithTextBox(new[] { "{{cour", "t}}" }, ""));
        var renderer = CreateRenderer("tb.docx");

        var result = await renderer.RenderAsync(
            new Dictionary<string, object> { ["court"] = "حلب" }, "tb");

        var xml = ReadDocumentXml(result);
        Assert.Contains("حلب", xml);
        Assert.DoesNotContain("{{", xml);
        Assert.Contains("<w:txbxContent>", xml);
        Assert.Contains("<w:drawing>", xml);
    }

    [Fact]
    public async Task Render_SpacedPlaceholder_UsesFirstIdentifierAsKey()
    {
        WriteTemplate("a.docx", new[] { "المتضمن {{contain 1 }}" });
        var renderer = CreateRenderer("a.docx");

        var result = await renderer.RenderAsync(
            new Dictionary<string, object> { ["contain"] = "دفع مبلغ مئة" }, "a");

        Assert.Equal("المتضمن دفع مبلغ مئة 1", ReadDocumentText(result));
    }

    [Fact]
    public async Task Render_SplitOpenBracePlaceholder_Replaces()
    {
        WriteTemplate("a.docx", new[] { "القضية { {file_type}}" });
        var renderer = CreateRenderer("a.docx");

        var result = await renderer.RenderAsync(
            new Dictionary<string, object> { ["file_type"] = "سند دين" }, "a");

        Assert.Contains("سند دين", ReadDocumentText(result));
        Assert.DoesNotContain("{ {", ReadDocumentText(result));
    }

    [Fact]
    public async Task Render_UnknownTemplateCode_ThrowsArgumentException()
    {
        var renderer = CreateRenderer("a.docx");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            renderer.RenderAsync(new Dictionary<string, object>(), "zzz"));
    }

    [Fact]
    public async Task Render_MissingTemplateFile_ThrowsFileNotFoundException()
    {
        var renderer = CreateRenderer("missing.docx");
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            renderer.RenderAsync(new Dictionary<string, object>(), "missing"));
    }
}
