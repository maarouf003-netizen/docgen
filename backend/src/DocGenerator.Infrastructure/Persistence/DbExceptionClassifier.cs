using DocGenerator.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// تصنيف أخطاء المزوّد (‏`ADR-0001`: `PostgreSQL` هو المزوّد الوحيد للتشغيل والاختبار
/// بعد `P1-08`): تعارض القيد الفريد (<c>23505</c>)، خرق المفتاح الأجنبي (<c>23503</c>)،
/// قيد <c>CHECK</c> محدد بالاسم (<c>23514</c>)، وتعارض التزامن
/// (<see cref="DbUpdateConcurrencyException"/> أو <c>40001</c>/<c>40P01</c>) — عبر كامل
/// سلسلة الأسباب الداخلية للاستثناء.
/// </summary>
public class DbExceptionClassifier : IDbExceptionClassifier
{
    public bool IsUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState == "23505")
                return true;
        }
        return false;
    }

    /// <summary>
    /// خرق مفتاح أجنبي (<c>23503</c>) — مرجع محذوف بين التسجيل والمعالجة.
    /// </summary>
    public bool IsForeignKeyViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState == "23503")
                return true;
        }
        return false;
    }

    /// <summary>
    /// انتهاك قيد <c>CK_Documents_FileNumberRequiresCircuit</c> (`PostgreSQL` 23514 مع
    /// اسم القيد) — يُترجم إلى رسالة مجال لا 500.
    /// </summary>
    public bool IsMissingDocumentCircuitViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres
                && postgres.SqlState == "23514"
                && string.Equals(
                    postgres.ConstraintName,
                    "CK_Documents_FileNumberRequiresCircuit",
                    StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// انتهاك قيد مبلغ (<c>23514</c> على قيد يحتوي <c>Amount</c>/<c>NonNegative</c>)
    /// أو تجاوز دقة <c>numeric</c> (<c>22003</c>) — يُترجم 400 لا 500.
    /// </summary>
    public bool IsAmountConstraintViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                if (postgres.SqlState == "22003")
                    return true;
                if (postgres.SqlState == "23514"
                    && postgres.ConstraintName is { } name
                    && name.StartsWith("CK_Documents_", StringComparison.Ordinal)
                    && name.Contains("Amount", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// تعارض التزامن المتفائل (RF-010): `EF` ترفع `DbUpdateConcurrencyException` عند
    /// فشل `WHERE` رمز التزامن (`Version`) — تُكتشَف عبر كامل السلسلة. وتشمل أيضًا
    /// انشغال/قفل التخزين المتزامن (`serialization_failure`/`deadlock_detected`
    /// <c>40001</c>/<c>40P01</c>) — كتابتان متزامنتان حقيقيتان تتصادمان خارج
    /// <c>WHERE</c> الرمز، فتُترجمان 409 ودية بدل 500 خام.
    /// </summary>
    public bool IsConcurrencyViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
                return true;
            if (current is PostgresException postgres
                && postgres.SqlState is "40001" or "40P01")
                return true;
        }
        return false;
    }
}
