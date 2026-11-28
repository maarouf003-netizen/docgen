using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

/// <summary>
/// `W5/F3`: تطبيع الصفحة يمنع التفاف الإزاحة `(page-1)*perPage` في `int`
/// (كان `page=int.MaxValue` يعطي إزاحة سالبة ⇒ 500 من PostgreSQL)، ويثبّت سقف الحجم.
/// </summary>
public sealed class PageRequestTests
{
    [Theory]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-5, -5, 1, 20)]
    [InlineData(1, 20, 1, 20)]
    [InlineData(3, 50, 3, 50)]
    public void Normalize_ClampsLowValuesAndDefault(int page, int perPage, int expectedPage, int expectedPerPage)
    {
        var (normalizedPage, normalizedPerPage) = PageRequest.Normalize(page, perPage);

        Assert.Equal(expectedPage, normalizedPage);
        Assert.Equal(expectedPerPage, normalizedPerPage);
    }

    [Theory]
    [InlineData(101, 100)]
    [InlineData(1000, 100)]
    [InlineData(int.MaxValue, 100)]
    public void Normalize_CapsPerPageAtMaximum(int perPage, int expectedPerPage)
    {
        var (_, normalizedPerPage) = PageRequest.Normalize(1, perPage);

        Assert.Equal(expectedPerPage, normalizedPerPage);
    }

    [Fact]
    public void Normalize_LargePage_DoesNotOverflowIntSkip()
    {
        var (page, perPage) = PageRequest.Normalize(int.MaxValue, int.MaxValue);

        Assert.Equal(PageRequest.MaxPage, page);
        Assert.Equal(PageRequest.MaxPerPage, perPage);
        var skip = ((long)page - 1) * perPage;
        Assert.InRange(skip, 0, int.MaxValue);
    }
}
