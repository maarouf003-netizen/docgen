using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Authorization.Documents;

/// <summary>تسجيل سياسة وصول المستندات (`P2-01a`).</summary>
public static class DocumentAuthorizationSetup
{
    public static IServiceCollection AddDocumentAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IUserCapabilities, UserCapabilities>();
        services.AddScoped<IDocumentAuthorization, DocumentAuthorization>();
        // معالج المتطلب الموردي — يُحلّ من DI عند كل AuthorizeAsync.
        services.AddScoped<IAuthorizationHandler, DocumentAccessHandler>();
        return services;
    }
}
