using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IBranchManagementService, BranchManagementService>();
        services.AddScoped<IHeadAlertService, HeadAlertService>();
        services.AddScoped<IDocumentDelegationService, DocumentDelegationService>();
        services.AddScoped<IDocumentAppealService, DocumentAppealService>();
        services.AddScoped<IDocumentContextBuilder, DocumentContextBuilder>();
        services.AddScoped<IDocumentRenderer, WordTemplateRenderer>();
        services.AddScoped<IWordDocumentGenerator, WordDocumentGenerator>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        return services;
    }
}
