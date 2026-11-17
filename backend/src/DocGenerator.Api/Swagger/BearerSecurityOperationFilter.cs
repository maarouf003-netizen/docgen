using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DocGenerator.Api.Swagger;

/// <summary>
/// `P4-04`: مخطط الأمان `Bearer` يُضاف **لكل عملية مصادَقة**، ويُترك فارغًا لعمليات
/// <see cref="IAllowAnonymous"/> (دخول/خروج/تقارير CSP/سنة النظام). المتطلب العام على
/// مستوى الوثيقة كان يجعل العميل المولَّد يظنّ أن `login` يحتاج رمزًا — وهذا خطأ عقد.
/// </summary>
public sealed class BearerSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            return;

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            }] = Array.Empty<string>(),
        });
    }
}
