using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace DocGenerator.Application.Tests;

/// <summary>
/// قارئ مصنّفات ‏XLSX‏ للاختبارات: يفتح الحزمة ويقرأ ورقة العمل الأولى نصًّا
/// (أو اسم الورقة ومدى ‏AutoFilter‏) — لتثبيت محتوى المصنَّف لا مجرد «إنه ملف zip».
/// التحليل بالاسم المحلي لا بالبادئة، فلا يعتمد على شكل الـnamespace الذي يكتبه
/// مولّد OpenXML ولا على إصداره. مُشارك بين اختبارات تصدير البوابة وتصدير سجل
/// التغييرات لأن كليهما يمرّ بالمحرّك نفسه.
/// </summary>
public static class XlsxReader
{
    /// <summary>XML‏ ورقة العمل الأولى (بما فيه صفّ العناوين وصفوف البيانات).</summary>
    public static string FirstSheetXml(byte[] xlsx)
    {
        using var stream = new MemoryStream(xlsx);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.Entries.First(e => e.FullName.EndsWith("sheet1.xml", StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>نصوص خلايا صفٍّ بعينه (الأفقي يُهمَل؛ الترتيب يتبع ترتيب الخلايا).</summary>
    public static List<string> RowTexts(string sheetXml, int rowIndex = 0)
        => XDocument.Parse(sheetXml)
            .Descendants()
            .Where(e => e.Name.LocalName == "row").ElementAt(rowIndex)
            .Descendants()
            .Where(e => e.Name.LocalName == "t")
            .Select(t => t.Value)
            .ToList();

    /// <summary>اسم الورقة الأولى كما سجّله المصنِّف (مكوّن صالح لاسم تبويب إكسل).</summary>
    public static string FirstSheetName(byte[] xlsx)
    {
        using var stream = new MemoryStream(xlsx);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.Entries.First(e => e.FullName.EndsWith("workbook.xml", StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return XDocument.Parse(reader.ReadToEnd())
            .Descendants()
            .First(e => e.Name.LocalName == "sheet")
            .Attribute("name")!.Value;
    }

    /// <summary>عدد صفوف البيانات (كل صفوف الورقة ناقص صفَّ العناوين).</summary>
    public static int DataRowCount(string sheetXml)
        => XDocument.Parse(sheetXml)
            .Descendants()
            .Count(e => e.Name.LocalName == "row") - 1;

    /// <summary>مدى فلتر الأعمدة كما سجّله المصنِّف (‏A1:G12‏ ونحوه)، أو فارغًا إن لم يُكتب.</summary>
    public static string AutoFilterReference(string sheetXml)
        => XDocument.Parse(sheetXml)
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "autoFilter")
            ?.Attribute("ref")?.Value ?? string.Empty;
}
