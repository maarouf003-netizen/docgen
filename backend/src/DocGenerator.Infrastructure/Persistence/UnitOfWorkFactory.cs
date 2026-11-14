using DocGenerator.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// ينفّذ عملية كتابة في نطاق DI جديد لكل محاولة (P0-03b). الإستراتيجية تُنشأ من
/// سياق نطاق منفصل، والـdelegate يُنفَّذ في نطاق آخر جديد في كل محاولة — فلا تبقى
/// كيانات/تغييرات المحاولة الفاشلة في السياق التالي.
/// </summary>
public sealed class UnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public UnitOfWorkFactory(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<T> ExecuteAsync<T>(
        Func<IUnitOfWork, CancellationToken, Task<T>> action,
        bool retryable,
        CancellationToken ct = default)
    {
        if (!retryable)
            return await ExecuteOnceAsync(action, ct).ConfigureAwait(false);

        // سياق الإستراتيجية فقط: لا يشارك السياق/الكيانات مع محاولات التنفيذ.
        await using var strategyScope = _scopeFactory.CreateAsyncScope();
        var strategyDb = strategyScope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var strategy = strategyDb.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            action,
            async (_, capturedAction, token) =>
                await ExecuteOnceAsync(capturedAction, token).ConfigureAwait(false),
            verifySucceeded: null,
            ct).ConfigureAwait(false);
    }

    private async Task<T> ExecuteOnceAsync<T>(
        Func<IUnitOfWork, CancellationToken, Task<T>> action,
        CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var result = await action(uow, ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return result;
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                // أفضل جهد: الاستثناء الأصلي هو المهم، والتفريغ يغلق المعاملة.
            }

            throw;
        }
    }
}
