using DocGenerator.Application.Common.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// تصنيف أخطاء المزودين (B3): تعارض القيد الفريد يُكتشف من رموز المزود نفسه —
/// SQLite (19 قيْد / 2067 فريد موسّع) وPostgreSQL (23505 unique_violation) —
/// عبر كامل سلسلة الأسباب الداخلية لـ <see cref="DbUpdateException"/>.
/// </summary>
public class DbExceptionClassifier : IDbExceptionClassifier
{
    public bool IsUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqlite
                && (sqlite.SqliteErrorCode == 19 || sqlite.SqliteExtendedErrorCode == 2067))
                return true;
            if (current is PostgresException postgres && postgres.SqlState == "23505")
                return true;
        }
        return false;
    }

    /// <summary>
    /// تعارض التزامن المتفائل (RF-010): `EF` ترفع `DbUpdateConcurrencyException` عند
    /// فشل `WHERE` رمز التزامن (`Document.Version`) — تُكتشَف عبر كامل السلسلة.
    /// </summary>
    public bool IsConcurrencyViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
                return true;
        }
        return false;
    }
}
