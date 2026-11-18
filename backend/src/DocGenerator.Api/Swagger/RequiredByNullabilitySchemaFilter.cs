using System.Reflection;
using System.Text.Json;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DocGenerator.Api.Swagger;

/// <summary>
/// `P6-06`: يشتق `required` في مخطط `OpenAPI` من **nullability** أنواع C# نفسها، بدل
/// ترك كل الحقول اختيارية.
///
/// <para>
/// `SupportNonNullableReferenceTypes` وحدها لا تكفي: أنواع القيم (`int`/`bool`/`long`)
/// وسجلات <c>record</c> الموضعية تُصدَّر اختيارية، فيصير النوع المولَّد ضعيفًا
/// (<c>name?: string | null</c> بدل <c>name: string</c>) ويستحيل إحلاله محل الأنواع
/// اليدوية. الفلتر يقرأ <see cref="NullabilityInfoContext"/> لكل خاصية ويضيف غير
/// القابلة للفراغ إلى <c>required</c>.
/// </para>
///
/// <para>
/// يستثني خصائص الفهرس (لا نظير لها في JSON)، ويتجاهل الخصائص غير الموجودة في المخطط
/// (مخفية بـ<c>[JsonIgnore]</c> أو غير عامة). أسماء الحقول تُطابق camelCase الذي يفرضه
/// MVC على الاستجابات.
/// </para>
/// </summary>
public sealed class RequiredByNullabilitySchemaFilter : ISchemaFilter
{
    private static readonly NullabilityInfoContext Nullability = new();

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties is null || context.Type is null)
            return;

        schema.Required ??= new HashSet<string>(StringComparer.Ordinal);

        var properties = context.Type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0);

        foreach (var property in properties)
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            if (!schema.Properties.ContainsKey(name))
                continue;

            var info = Nullability.Create(property);
            var isNullable = info.ReadState == NullabilityState.Nullable
                || info.WriteState == NullabilityState.Nullable;

            if (!isNullable && !schema.Required.Contains(name))
                schema.Required.Add(name);
        }
    }
}
