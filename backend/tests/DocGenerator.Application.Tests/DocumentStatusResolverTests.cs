using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using Xunit;

namespace DocGenerator.Application.Tests;

/// <summary>
/// مصفوفة اشتقاق حالة العرض الموحدة — المصدر الوحيد للقواعد بعد توحيدها
 /// (كانت مكررة بين ExcelExportService ومنطق الواجهة).
/// </summary>
public class DocumentStatusResolverTests
{
    private static DocumentResponse Doc(
        string? side = "applicant",
        string? execStatus = null,
        string? execSubStatus = null,
        string? executedStatus = null,
        bool isDraft = false) => new()
    {
        GeneralEntitySide = side,
        ExecStatus = execStatus,
        ExecSubStatus = execSubStatus,
        ExecutedStatus = executedStatus,
        IsDraft = isDraft,
    };

    [Fact]
    public void ExecutedLikeFamily_MapsFromExecutedStatus()
    {
        Assert.Equal("متداول", DocumentStatusResolver.Resolve(Doc(side: "executed", executedStatus: "")));
        Assert.Equal("منفذ", DocumentStatusResolver.Resolve(Doc(side: "executed", executedStatus: "منفذ")));
        Assert.Equal("مشطوب", DocumentStatusResolver.Resolve(Doc(side: "executed", executedStatus: "مشطوب")));
    }

    [Fact]
    public void ApplicantFamily_StatusMatrix()
    {
        Assert.Equal("تريث", DocumentStatusResolver.Resolve(Doc(execStatus: "تريث")));
        Assert.Equal("متداول / منفذ جزئيا",
            DocumentStatusResolver.Resolve(Doc(execStatus: "منفذ جبريا", execSubStatus: "منفذ جزئيا")));
        Assert.Equal("منفذ", DocumentStatusResolver.Resolve(Doc(execStatus: "منفذ جبريا")));
        Assert.Equal("منفذ", DocumentStatusResolver.Resolve(Doc(execStatus: "منفذ بالتسوية")));
        Assert.Equal("منفذ", DocumentStatusResolver.Resolve(Doc(execStatus: "منفذ إنابة")));
    }

    [Fact]
    public void ApplicantFamily_DraftVsCirculating()
    {
        Assert.Equal("تحت رفع", DocumentStatusResolver.Resolve(Doc(isDraft: true)));
        Assert.Equal("متداول", DocumentStatusResolver.Resolve(Doc()));
    }

    [Fact]
    public void StruckOff_ApplicantSide_IsStruckOff()
    {
        Assert.Equal("مشطوب", DocumentStatusResolver.Resolve(Doc(execStatus: "مشطوب")));
    }
}
