using System.Text.Json;

using DocGenerator.Application.DTOs;

using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// عقد السلك لحقل «الإضافات المحلية» (`DelegationDto.LocalAddition`): أسماء الحقول (camelCase)
/// وصيغة `lastAt` زمنًا UTC صريحًا.
///
/// **نطاق مقصود:** هذه الاختبارات لا تعيد اختبار القيم — قيم `count`/`lastMessage` مُثبتة على
/// مستوى الخدمة (`ListForDocumentAsync`)، ونقطة النهاية تُعيد `DelegationDto` مباشرةً بلا طبقة
/// تحويل (`DelegationsController.ListForDocument`). ما لا يغطيه أي اختبار آخر هو **أسماء السلك**:
/// أنواع الواجهة مكتوبة يدويًا (`src/types/delegation.ts`) و`mock` اختباراتها لا يعرف الخادم،
/// فإعادة تسمية حقل تُخفي بطاقة «تشعبات الملف» بصمت في الإنتاج.
///
/// تُستخدم خيارات التسلسل **الحقيقية** من حاوية المضيف (لا خيارات مُعاد تركيبها هنا)، فلا تنحرف
/// هذه الحارس إن تغيّرت سياسة التسمية/التواريخ في `Program.cs`.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class DelegationsLocalAdditionWireContractTests
{
    private readonly ApiFactory _factory;

    public DelegationsLocalAdditionWireContractTests(ApiFactory factory) => _factory = factory;

    /// <summary>خيارات JSON الفعلية لخط أنابيب الـAPI (نفس ما يستخدمه تسلسل الاستجابات).</summary>
    private JsonSerializerOptions WireOptions
        => _factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

    [Fact]
    public void LocalAddition_UsesExactlyTheSharedManifestKeys()
    {
        var dto = new DelegationLocalAdditionDto(
            2,
            "أُضيف الوريث محمود؛ أُضيف الوريث سامر",
            new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc));

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto, WireOptions));

        var expected = ReadSharedContract("localAddition")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(expected, SortedKeys(doc.RootElement));
    }

    [Fact]
    public void LocalAddition_NullLastAt_SerializesAsJsonNull_NotOmitted()
    {
        // الغياب قيمةٌ دلالية في الواجهة (`lastAt && …`): حذف الحقل عند null أو تسلسله ""
        // يغيّر العقد بصمت. المطلوب: المفتاح موجود وقيمته null.
        var dto = new DelegationLocalAdditionDto(1, "أُضيف الممثل الشرعي للمقترض", null);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto, WireOptions));

        Assert.True(doc.RootElement.TryGetProperty("lastAt", out var lastAt));
        Assert.Equal(JsonValueKind.Null, lastAt.ValueKind);
    }

    [Fact]
    public void LocalAddition_UtcLastAt_SerializesWithExplicitUtcDesignator()
    {
        // الواجهة تفكّ التاريخ بـ`new Date(...)`: بلا لاحقة `Z` يُقرأ محليًا فينزاح العرض
        // بمقدار إزاحة توقيت دمشق (+3 ساعات). القراءة من القاعدة تُعيد `Utc` عبر
        // `UtcDateTimeConverter` المركزي، فيجب أن يظهر ذلك صريحًا على السلك.
        var dto = new DelegationLocalAdditionDto(
            1,
            "أُضيف الوريث محمود",
            new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc));

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto, WireOptions));
        var lastAt = doc.RootElement.GetProperty("lastAt").GetString();

        Assert.NotNull(lastAt);
        Assert.EndsWith("Z", lastAt!, StringComparison.Ordinal);
        var parsed = DateTime.Parse(
            lastAt!, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.Equal(DateTimeKind.Utc, parsed.Kind);
        Assert.Equal(new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc), parsed);
    }

    private static List<string> SortedKeys(JsonElement obj)
        => obj.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

    /// <summary>
    /// بيان العقد المشترك (`frontend/src/test/contracts/delegation-contracts.json`): مصدر الحقيقة
    /// الوحيد لأسماء حقول السلك. يُقرأ من الشجرة لا من مجلد البناء، فيعمل تحت `dotnet test`
    /// من أي دليل عمل.
    /// </summary>
    private static JsonElement ReadSharedContract(string section)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName, "frontend", "src", "test", "contracts", "delegation-contracts.json");
            if (File.Exists(candidate))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(candidate));
                return doc.RootElement.GetProperty(section).Clone();
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException("بيان العقد المشترك غير موجود في الشجرة");
    }
}
