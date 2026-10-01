using System.Net;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Api.Tests;

[Collection(ApiTestCollection.Name)]
public class StatisticsHeadScopeIntegrationTests
{
    private readonly ApiFactory _factory;

    public StatisticsHeadScopeIntegrationTests(ApiFactory factory) => _factory = factory;

    // ملاحظة طبقية: تجسيد «رئيس/محامٍ بلا فرع» عبر EF أصبح مستحيلًا (قيد
    // القاعدة يرفض الإدراج)، فانتقلت تغطية حراس النقاط (`RequireOwnBranch`)
    // إلى `StatisticsBranchGuardTests` بموكّل مزيف وهوية مطالبات مصنوعة —
    // للرئيس والمحامي معًا على النقاط الثلاث، مع ضابط إيجابي.

    // رئيس بفرع: نجاح فقط — الضابط الإيجابي مشمول في اختبار النقل
    // (`HeadBranchTransfer_InvalidatesPreviouslyIssuedTokens` يثبت
    // `/api/dashboard` بـ 200 قبل النقل و401 بعده).

    [Fact]
    public async Task Dashboard_EntityManager_Forbidden()
    {
        // مندوب الجهة بلا فرع بالتصميم وله بوابة إحصاءاته (`/api/portal/stats`) —
        // لوحة الإحصاءات العامة ليست له: 403 بدل إحصاءات كل الفروع ضمنيًا.
        var portalUser = await _factory.CreateUserAsync("portal_dash", UserRole.EntityManager, branchId: null);
        var client = _factory.ClientForUser(portalUser);

        var response = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MonthlyStats_EntityManager_Forbidden()
    {
        var portalUser = await _factory.CreateUserAsync("portal_monthly", UserRole.EntityManager, branchId: null);
        var client = _factory.ClientForUser(portalUser);

        var response = await client.GetAsync("/api/monthly-stats");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_LawyerWithBranch_Succeeds()
    {
        // الضابط الإيجابي لقيد الدور: المحامي داخل قائمة الأدوار المسماة
        // فيبقى يرى لوحته — القيد يُخرج المندوب وحده لا المستهلكين الشرعيين.
        var lawyer = _factory.AuthorizedClient("lawyer1");

        var response = await lawyer.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MonthlyStats_LawyerWithBranch_Succeeds()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");

        var response = await lawyer.GetAsync("/api/monthly-stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AvailablePeriods_EntityManager_Forbidden()
    {
        // كالنقطتين أعلاه: فترات كل الفروع ليست لمندوب الجهة
        // (بوابته لا تستدعيها) — 403 بدل تسريب ضمني.
        var portalUser = await _factory.CreateUserAsync("portal_periods", UserRole.EntityManager, branchId: null);
        var client = _factory.ClientForUser(portalUser);

        var response = await client.GetAsync("/api/stats/periods");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AvailablePeriods_LawyerWithBranch_Succeeds()
    {
        // الضابط الإيجابي: المحامي داخل القائمة فيرى فترات ملفاته.
        var lawyer = _factory.AuthorizedClient("lawyer1");

        var response = await lawyer.GetAsync("/api/stats/periods");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
