using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DocGenerator.Api.Tests.TestServices;

/// <summary>
/// P3-08 — عدّاد أوامر قاعدة البيانات لاختبارات الميزانية (حارس N+1).
/// <para>
/// يُسجَّل Singleton داخل حاوية <see cref="ApiFactory"/> (انظر
/// <see cref="ApiFactory.ConfigureDatabaseServices"/>) ويُحقن في خيارات <c>DbContext</c>
/// كمُعترِض، فيعدّ كل أمر يُنفَّذ على قاعدة الاختبار. الاختبارات تُصفّره قبل الطلب ثم
/// تقارن العدد بسقف ثابت، فأي انحدار N+1 (استعلام لكل صف) يفشل صراحةً.
/// </para>
/// <para>
/// ليست أداة إنتاج: تعيش في مشروع الاختبارات فقط وتُبنى على قاعدة `PostgreSQL` الحقيقية.
/// </para>
/// </summary>
public sealed class QueryCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    private void CountCommand() => Interlocked.Increment(ref _count);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        CountCommand();
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        CountCommand();
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        CountCommand();
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        CountCommand();
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        CountCommand();
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CountCommand();
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}
