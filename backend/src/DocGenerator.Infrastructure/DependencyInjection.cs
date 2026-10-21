using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Infrastructure.Persistence;
using DocGenerator.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocGenerator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        bool usePostgres = false)
    {
        if (usePostgres)
        {
            // عند Postgres يُسجَّل السياق المشتق كتنفيذ لنوع الخدمة، فتعمل كل التبعيات القائمة
            // عليه دون تغيير، ويطبق MigrateAsync هجرات Postgres (المنفصلة عن هجرات SQLite).
            services.AddDbContext<DocGeneratorDbContext, DocGeneratorPostgresDbContext>(options =>
                options.UseNpgsql(connectionString, npg => npg
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        }
        else
        {
            services.AddDbContext<DocGeneratorDbContext>(options =>
                options.UseSqlite(connectionString, sqlite => sqlite
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        }
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IHeadAlertRepository, HeadAlertRepository>();
        services.AddScoped<IPersonalReminderRepository, PersonalReminderRepository>();
        services.AddScoped<IAppSuggestionRepository, AppSuggestionRepository>();
        services.AddScoped<IPublicEntityRepository, PublicEntityRepository>();
        services.AddScoped<IPortalRepository, PortalRepository>();
        services.AddScoped<IReviewLetterRepository, ReviewLetterRepository>();
        services.AddScoped<ICorrespondenceRepository, CorrespondenceRepository>();
        services.AddScoped<IForumRepository, ForumRepository>();
        services.AddScoped<IDelegationRepository, DelegationRepository>();
        services.AddScoped<IDelegationReservationRepository, DelegationReservationRepository>();
        // المصنِّف بلا حالة (بلا حقول ولا تبعيات) ⇒ يُسجَّل كـsingleton ليبتلعه
        // مشغّل صندوق الصادر (BackgroundService = singleton) دون «اعتماد محتجز».
        services.AddSingleton<IDbExceptionClassifier, DbExceptionClassifier>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IAppealRepository, AppealRepository>();
        services.AddScoped<IStatisticsRepository, StatisticsRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<ILoginRateLimiter, DbLoginRateLimiter>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<ITransactionRunner, TransactionRunner>();
        // حالة المعاملة: نفس مُنفِّذ ITransactionRunner (نفس النطاق ونفس DbContext)
        // فيرى الحارس المعاملة النشطة فعلًا.
        services.AddScoped<ITransactionContext>(sp => (ITransactionContext)sp.GetRequiredService<ITransactionRunner>());
        services.AddScoped<IHeadAlertOutboxStore, HeadAlertOutboxStore>();
        services.AddSingleton(sp => new HeadAlertOutboxLock(connectionString, usePostgres, sp.GetRequiredService<ILogger<HeadAlertOutboxLock>>()));
        services.AddScoped<ITokenService, TokenService>();
        return services;
    }

    public static IServiceCollection AddJwtOptions(this IServiceCollection services, JwtOptions options)
    {
        services.AddSingleton(options);
        return services;
    }
}
