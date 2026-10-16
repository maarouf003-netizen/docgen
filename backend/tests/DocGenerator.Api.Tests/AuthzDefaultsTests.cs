using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// M-01 (جاهزية الإنتاج — دفاع عمقي): يحرس الثابت القائل إن كل إجراء متحكم إما
/// محمي بسمة تفويض صريحة على الإجراء/الصنف أو مستثنى عمدًا بـ[AllowAnonymous] —
/// فلا يمكن أن يُضاف إجراء منسي بلا حماية دون أن يسقط هذا الاختبار. الافتراضي
/// التشغيلي نفسه يُفرَض بـ`MapControllers().RequireAuthorization()` في `Program.cs`،
/// لكن هذا الاختبار يتجاهل بيانات الاتفاقية عمدًا حتى لا يكون أخضر دائمًا.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class AuthzDefaultsTests
{
    private readonly ApiFactory _factory;

    public AuthzDefaultsTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void EveryControllerAction_IsAuthorizedOrExplicitlyAnonymous()
    {
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var controllerEndpoints = dataSource.Endpoints
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .ToList();

        Assert.NotEmpty(controllerEndpoints);

        foreach (var endpoint in controllerEndpoints)
        {
            // لا نعتمد بيانات النقطة النهائية هنا عمدًا: اتفاقية
            // `MapControllers().RequireAuthorization()` تضيف بيانات تفويض لكل
            // الإجراءات، ففحصها يجعل الاختبار أخضر دائمًا. نفحص السمات الصريحة
            // على الإجراء/الصنف بالانعكاس بدلًا من ذلك.
            var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!;
            var methodAttributes = action.MethodInfo.GetCustomAttributes(inherit: true);
            var controllerAttributes = action.ControllerTypeInfo.GetCustomAttributes(inherit: true);
            var hasExplicitAuthorize = methodAttributes.OfType<IAuthorizeData>().Any()
                || controllerAttributes.OfType<IAuthorizeData>().Any();
            var isExplicitlyAnonymous = methodAttributes.OfType<IAllowAnonymous>().Any()
                || controllerAttributes.OfType<IAllowAnonymous>().Any();

            Assert.True(
                isExplicitlyAnonymous || hasExplicitAuthorize,
                $"إجراء متحكم بلا حماية صريحة (لا [Authorize] ولا [AllowAnonymous] على الإجراء/الصنف): {endpoint.DisplayName}");
        }
    }
}
