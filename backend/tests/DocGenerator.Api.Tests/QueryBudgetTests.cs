using System.Net;
using DocGenerator.Api.Tests.TestServices;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P3-08 — ميزانية استعلامات لكل طلب (حارس N+1).
/// <para>
/// السقوف مثبّتة يدويًا بعد قياس على بذرة الاختبار، وبهامش صغير: الهدف كشف أي انحدار
/// يجعل عدد الاستعلامات ينمو مع عدد الصفوف (N+1)، لا تجميد العدد بدقة بايت.
/// القياس المرجعي (2026-11-16): قائمة 15 صفًا = 18 استعلامًا، وتفاصيل صف واحد = 18
/// استعلامًا — أي أن العدد **لا ينمو** مع الصفوف. أي N+1 في القائمة سيضيف 15 استعلامًا
/// على الأقل فيتجاوز السقف فورًا.
/// </para>
/// <para>
/// كل اختبار يستخدم <see cref="ApiFactory"/> خاصًا به لأن العدّاد Singleton داخل حاوية
/// المضيف، فلا تتقاطع الاختبارات المتوازية.
/// </para>
/// </summary>
public class QueryBudgetTests
{
    /// <summary>سقف قائمة 15 صفًا: القياس 18 + هامش 4.</summary>
    private const int ListBudgetForFifteenRows = 22;

    /// <summary>سقف تفاصيل صف واحد: القياس 18 + هامش 2.</summary>
    private const int DetailBudget = 20;

    /// <summary>سقف تصدير Excel لخمسة عشر صفًا: القياس 10 + هامش 4.</summary>
    private const int ExportBudget = 14;

    [Fact]
    public async Task DocumentsList_PageOfFifteen_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var login = await factory.LoginAsync("lawyer1", "123456");
        var token = login!.Token!;

        for (var i = 1; i <= 15; i++)
        {
            await factory.CreateDocumentAsync(token, borrowerName: $"مستلف اختبار {i}");
        }

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.ClientWithToken(token).GetAsync("/api/documents?perPage=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= ListBudgetForFifteenRows,
            $"تجاوزت قائمة المستندات ميزانية الاستعلامات: {counter.Count} > {ListBudgetForFifteenRows}");
    }

    [Fact]
    public async Task DocumentDetail_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var login = await factory.LoginAsync("lawyer1", "123456");
        var token = login!.Token!;
        var documentId = await factory.CreateDocumentAsync(token);

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.ClientWithToken(token).GetAsync($"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= DetailBudget,
            $"تجاوزت تفاصيل المستند ميزانية الاستعلامات: {counter.Count} > {DetailBudget}");
    }

    [Fact]
    public async Task DocumentsExport_WithFifteenRows_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var login = await factory.LoginAsync("lawyer1", "123456");
        var token = login!.Token!;

        for (var i = 1; i <= 15; i++)
        {
            await factory.CreateDocumentAsync(token, borrowerName: $"مستلف تصدير {i}");
        }

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.ClientWithToken(token).GetAsync("/api/documents/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= ExportBudget,
            $"تجاوز تصدير المستندات ميزانية الاستعلامات: {counter.Count} > {ExportBudget}");
    }
}
