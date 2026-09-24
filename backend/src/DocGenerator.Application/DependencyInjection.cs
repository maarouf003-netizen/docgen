using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        // ساعة النظام المحقونة في كل خدمات الطلبات: قرارات السنة والطوابع الزمنية الموحدة
        // تأخذ منها. TimeProvider.System آمن للمشاركة بين النطاقات (قراءات سلاسل فقط).
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        // منطقة زمن النظام من الإعدادات (TimeZone:Id — IANA أو Windows) — قرار السنة يعتمدها.
        var timeZoneId = configuration?["TimeZone:Id"];
        services.AddSingleton(ServerClock.ResolveTimeZone(timeZoneId));
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IBranchManagementService, BranchManagementService>();
        services.AddScoped<IHeadAlertService, HeadAlertService>();
        services.AddScoped<IPublicEntityService, PublicEntityService>();
        services.AddScoped<IPortalService, PortalService>();
        services.AddScoped<IEntityDelegateService, EntityDelegateService>();
        services.AddScoped<IReviewLetterService, ReviewLetterService>();
        services.AddScoped<ICorrespondenceService, CorrespondenceService>();
        services.AddScoped<IDocumentDelegationService, DocumentDelegationService>();
        services.AddScoped<IDocumentAppealService, DocumentAppealService>();
        services.AddScoped<IDocumentContextBuilder, DocumentContextBuilder>();
        services.AddScoped<IDocumentRenderer, WordTemplateRenderer>();
        services.AddScoped<IWordDocumentGenerator, WordDocumentGenerator>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        return services;
    }
}
