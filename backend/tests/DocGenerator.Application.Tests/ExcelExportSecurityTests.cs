using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocGenerator.Application.Tests;

/// <summary>
/// P2-09 — حارس تكامل صيغ Excel: كل قيمة نصية تُكتب كـ<c>inlineString</c> ولا تُكتب
/// أبدًا كصيغة. هذا ما يمنع Formula Injection عند فتح الملف (الخلية النصية تُعرض
/// حرفيًا ولا تُنفَّذ)، والاختبار يثبت السلوك كي لا ينحدر مستقبلًا إلى خلية صيغة.
/// </summary>
[Trait("Category", "Unit")]
public class ExcelExportSecurityTests
{
    private static readonly string[] FormulaPayloads =
    [
        "=cmd|' /C calc'!A0",
        "+1+1",
        "-2+3",
        "@SUM(1+1)",
        "=HYPERLINK(\"http://example.test\",\"x\")",
    ];

    public static TheoryData<string> FormulaLikeRows
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var payload in FormulaPayloads)
            {
                data.Add(payload);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(FormulaLikeRows))]
    public void FormulaLikeValue_IsWrittenAsInlineText_NotAsFormula(string payload)
    {
        var service = new ExcelExportService();
        using var stream = service.BuildCircuitStatsWorkbook(
        [
            new CircuitStatsDto(
                CircuitId: 1,
                CircuitName: payload,
                BranchId: 1,
                BranchName: "فرع",
                IsActive: true,
                FileCount: 1,
                LawyerCount: 2,
                PendingCount: 3),
        ]);

        var workbookBytes = ReadAllBytes(stream);

        using var document = SpreadsheetDocument.Open(new MemoryStream(workbookBytes), false);
        var cells = document.WorkbookPart!.WorksheetParts.Single()
            .Worksheet.Descendants<Cell>().ToList();

        Assert.All(cells, cell => Assert.Null(cell.CellFormula));

        var payloadCell = Assert.Single(
            cells,
            cell => cell.InlineString?.Text?.Text == payload);

        Assert.Equal(CellValues.InlineString, payloadCell.DataType!.Value);
        Assert.Null(payloadCell.CellValue);
    }

    [Fact]
    public void WorksheetXml_ContainsNoFormulaElements()
    {
        var service = new ExcelExportService();
        using var stream = service.BuildCircuitStatsWorkbook(
        [
            new CircuitStatsDto(1, "=1+1", 1, "فرع", true, 1, 2, 3),
            new CircuitStatsDto(2, "+cmd|' /C calc'!A0", 1, "فرع", true, 1, 2, 3),
        ]);

        var worksheet = XDocument.Parse(ReadWorksheetXml(ReadAllBytes(stream)));
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        // لا عناصر صيغة إطلاقًا (`<f>`)، وكل خلية نص مضمّن (`<is>`).
        Assert.Empty(worksheet.Descendants(main + "f"));
        Assert.Equal(
            worksheet.Descendants(main + "c").Count(),
            worksheet.Descendants(main + "is").Count());
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string ReadWorksheetXml(byte[] workbookBytes)
    {
        using var zip = new ZipArchive(new MemoryStream(workbookBytes), ZipArchiveMode.Read);
        var entry = zip.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(entry);

        using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
