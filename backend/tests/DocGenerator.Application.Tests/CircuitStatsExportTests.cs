using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.Tests;

/// <summary>
/// وسم الصف الاصطناعي «بلا دائرة» في تصدير إحصاءات الدوائر (ب):
/// لمدير يرى الكل (`CircuitId = 0` بلا اسم فرع) تُكتب «كل الفروع» بدل الخلية
/// الفارغة المضللة — ولرئيس القسم اسم فرعه كما كان، والصفوف العادية بلا تغيير.
/// </summary>
public class CircuitStatsExportTests
{
    private static List<CircuitStatsDto> Rows() => new()
    {
        new CircuitStatsDto(0, "بلا دائرة", 0, null, true, 2, 1, 1),
        new CircuitStatsDto(0, "بلا دائرة", 5, "دمشق", true, 3, 2, 0),
        new CircuitStatsDto(7, "دائرة أ", 5, "دمشق", true, 4, 2, 1),
    };

    [Fact]
    public void BuildCircuitStatsWorkbook_LabelsNoCircuitBranchCell()
    {
        var bytes = new ExcelExportService().BuildCircuitStatsWorkbook(Rows());
        var xml = XlsxReader.FirstSheetXml(bytes);

        Assert.Equal("كل الفروع", XlsxReader.RowTexts(xml, 1)[1]);
        Assert.Equal("دمشق", XlsxReader.RowTexts(xml, 2)[1]);
        Assert.Equal("دمشق", XlsxReader.RowTexts(xml, 3)[1]);
    }

    [Fact]
    public void BuildCircuitStatsWorkbook_SectionFallbackUnchanged()
    {
        var bytes = new ExcelExportService().BuildCircuitStatsWorkbook(Rows());
        var xml = XlsxReader.FirstSheetXml(bytes);

        // احتياطي «القسم» للصفوف بلا شعبة — كما في الصفحة، بلا انشقاق عرض/تصدير.
        Assert.Equal("القسم", XlsxReader.RowTexts(xml, 1)[2]);
        Assert.Equal("القسم", XlsxReader.RowTexts(xml, 3)[2]);
    }
}
