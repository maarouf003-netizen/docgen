using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات طلب صفحات التنبيهات (`P3-6`): السقوف الصارمة، ورفض فيضان الإزاحة
/// بحساب موسّع، وثبات أن كِبَر رقم الصفحة وحده ليس سببًا للرفض حين تبقى الإزاحة
/// داخل حدود `int` (صفحة بعيدة بصفحة واحدة = `200` بعناصر فارغة، لا `400`).
/// </summary>
public class AlertPageRequestTests
{
    [Theory]
    [InlineData(1, 50)]
    [InlineData(1, AlertPageRequest.MaxPageSize)]
    [InlineData(int.MaxValue, 1)]
    public void Validate_AcceptsInRangeValues_WithoutSkipOverflow(int page, int pageSize)
    {
        var request = new AlertPageRequest { Page = page, PageSize = pageSize };

        request.Validate();

        Assert.True(request.Skip >= 0);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validate_RejectsOutOfRangeValues(int page, int pageSize)
    {
        var request = new AlertPageRequest { Page = page, PageSize = pageSize };

        Assert.Throws<ArgumentOutOfRangeException>(request.Validate);
    }

    [Fact]
    public void Validate_RejectsSkipOverflow_WithPageParameter()
    {
        // (int.MaxValue - 1) * 100 كان يلتفّ إلى -200 في `int`، فيعتمد الرفض على
        // حارس المستودع لا على العقد — الآن يُرفض صراحةً باسم المعامل `Page`.
        var request = new AlertPageRequest { Page = int.MaxValue, PageSize = 100 };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(request.Validate);

        Assert.Equal("Page", ex.ParamName);
        Assert.Contains("كبير", ex.Message);
    }

    [Fact]
    public void Skip_ExtremePageWithSizeOne_IsIntMaxValueMinusOne()
    {
        // أعلى صفحة مسموحة فعلًا: الإزاحة 2,147,483,646 ≤ int.MaxValue.
        var request = new AlertPageRequest { Page = int.MaxValue, PageSize = 1 };

        request.Validate();

        Assert.Equal(2_147_483_646, request.Skip);
    }

    [Fact]
    public void Skip_ComputesShiftedOffset_ForOrdinaryPage()
    {
        var request = new AlertPageRequest { Page = 4, PageSize = 25 };

        Assert.Equal(75, request.Skip);
    }
}
