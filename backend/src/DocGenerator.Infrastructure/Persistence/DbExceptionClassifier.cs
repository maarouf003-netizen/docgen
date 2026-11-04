using DocGenerator.Application.Common.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// تصنيف أخطاء المزودين (B3): تعارض القيد الفريد يُكتشف من رموز المزود نفسه —
/// SQLite (الرمز الموسّع 2067/1555/2579 حصرًا، لا الرمز العام 19) وPostgreSQL
/// (23505 unique_violation) — عبر كامل سلسلة الأسباب الداخلية لـ
/// <see cref="DbUpdateException"/>.
/// </summary>
public class DbExceptionClassifier : IDbExceptionClassifier
{
    public bool IsUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            // الرمز الموسّع وحده يميّز التفرد عن بقية قيود SQLite. الرمز الأساسي 19
            // (`SQLITE_CONSTRAINT`) عام ويشمل NOT NULL (1299) وCHECK (275) والمفتاح
            // الأجنبي (787) — تصنيفها تفردًا يحوّل أعطال بيانات حقيقية إلى 409 مضللة.
            // 2067 = UNIQUE، 1555 = PRIMARYKEY، 2579 = ROWID.
            if (current is SqliteException sqlite
                && sqlite.SqliteExtendedErrorCode is 2067 or 1555 or 2579)
                return true;
            if (current is PostgresException postgres && postgres.SqlState == "23505")
                return true;
        }
        return false;
    }

    /// <summary>
    /// خرق مفتاح أجنبي: SQLite (19 قيْد / 787 FOREIGN KEY موسّع) وPostgreSQL
    /// (23503 foreign_key_violation) — مرجع محذوف بين التسجيل والمعالجة.
    /// </summary>
    public bool IsForeignKeyViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqlite
                && sqlite.SqliteErrorCode == 19
                && sqlite.SqliteExtendedErrorCode == 787)
                return true;
            if (current is PostgresException postgres && postgres.SqlState == "23503")
                return true;
        }
        return false;
    }

    /// <summary>
    /// تعارض التزامن المتفائل (RF-010): `EF` ترفع `DbUpdateConcurrencyException` عند
    /// فشل `WHERE` رمز التزامن (`Document.Version`) — تُكتشَف عبر كامل السلسلة.
    /// إضافة المرحلة 9: انشغال/قفل التخزين المتزامن (SQLite `BUSY/LOCKED` وPostgres
    /// `serialization_failure/deadlock_detected`) — كتابتان متزامنتان حقيقيتان
    /// تتصادمان خارج `WHERE` الرمز، فتُترجمان 409 ودية بدل 500 خام.
    /// </summary>
    public bool IsConcurrencyViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
                return true;
            if (current is SqliteException sqlite
                && (sqlite.SqliteErrorCode == 5 || sqlite.SqliteErrorCode == 6))
                return true;
            if (current is PostgresException postgres
                && (postgres.SqlState is "40001" or "40P01"))
                return true;
        }
        return false;
    }
}
