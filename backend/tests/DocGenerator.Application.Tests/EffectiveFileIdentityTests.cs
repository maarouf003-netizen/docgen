using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Tests;

public class EffectiveFileIdentityTests
{
    private static Document NumberDoc(string fileNumber, string fileYear)
        => new() { Id = 1, FileNumber = fileNumber, FileYear = fileYear };

    private static DocumentBaseNumber Row(int year, string number, DateTime createdAt)
        => new() { DocumentId = 1, Year = year, BaseNumber = number, CreatedAt = createdAt, CreatedById = 1 };

    [Fact]
    public void LatestFrom_SameYearRows_PicksNewestCreatedAt()
    {
        var doc = NumberDoc("520", "2024");
        doc.BaseNumbers.Add(Row(2026, "1500", new DateTime(2026, 1, 5)));
        doc.BaseNumbers.Add(Row(2026, "1501", new DateTime(2026, 1, 10)));
        doc.BaseNumbers.Add(Row(2025, "900", new DateTime(2025, 1, 1)));

        var latest = EffectiveFileIdentity.Latest(doc)!;
        Assert.Equal("1501", latest.BaseNumber);
        Assert.Equal(2026, latest.Year);
        Assert.Equal("2026", EffectiveFileIdentity.Year(doc));
    }

    [Fact]
    public void LatestFrom_FutureYearExcluded_ThenIncludedWhenItsYearArrives()
    {
        var doc = NumberDoc("520", "2024");
        doc.BaseNumbers.Add(Row(2027, "2000", new DateTime(2027, 1, 1)));

        // سنة 2027 مستقبلية بحلول 2026 → يُرجع رقم الملف الأصلي.
        Assert.Equal("520", EffectiveFileIdentity.Number(doc, 2026));
        Assert.Equal("2024", EffectiveFileIdentity.Year(doc, 2026));

        // بحلول سنة 2027 يصبح السجل الفعّال.
        Assert.Equal("2000", EffectiveFileIdentity.Number(doc, 2027));
        Assert.Equal("2027", EffectiveFileIdentity.Year(doc, 2027));
    }

    [Fact]
    public void LatestFrom_AppealSameYearRows_PicksNewestCreatedAt()
    {
        var a = new AppealBaseNumber
        {
            AppealId = 1,
            Year = 2026,
            BaseNumber = "1501",
            CreatedAt = new DateTime(2026, 1, 10),
            CreatedById = 1,
        };
        var b = new AppealBaseNumber
        {
            AppealId = 1,
            Year = 2026,
            BaseNumber = "1500",
            CreatedAt = new DateTime(2026, 1, 5),
            CreatedById = 1,
        };

        var latest = EffectiveFileIdentity.LatestFrom(new[] { a, b })!;
        Assert.Equal("1501", latest.BaseNumber);
        Assert.Equal(2026, latest.Year);
    }

    [Fact]
    public void Number_FallsBackToFileNumberWhenNoBaseNumbers()
    {
        var doc = NumberDoc("520", "2024");
        Assert.Equal("520", EffectiveFileIdentity.Number(doc));
        Assert.Equal("2024", EffectiveFileIdentity.Year(doc));
    }

    [Fact]
    public void Number_NullDoc_ReturnsNull()
    {
        Assert.Null(EffectiveFileIdentity.Number(null));
        Assert.Null(EffectiveFileIdentity.Year(null));
    }
}
