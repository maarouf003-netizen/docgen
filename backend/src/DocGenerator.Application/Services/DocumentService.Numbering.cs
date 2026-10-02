using DocGenerator.Application.Common;

namespace DocGenerator.Application.Services;

/// <summary>
/// RF-009: حراس وحدانية رقم الأساس الفعّال (الدائرة + الرقم + النوع + السنة).
/// الفحص الخدمي المبكر برسالة ودية + التقاط تعارض القيد عند السباق (409) — دفاع عمق
/// مع القيد الفريد الجزئي في Configurations (المحذوف والمسودات خارج القيدين).
/// </summary>
public sealed partial class DocumentService
{
    /// <summary>
    /// فحص مبكر: هل يحمل ملف ظاهر آخر نفس المفتاح الفعّال؟ الرقم/السنة الفارغان = لا فحص.
    /// </summary>
    private async Task EnsureNumberUniqueAsync(
        int? excludeDocumentId,
        string? court,
        string? number,
        string? type,
        string? year,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(year))
            return;
        if (await _documents.ExistsActiveWithNumberAsync(excludeDocumentId, court, number, type, year, ct))
            throw new DocumentConflictException(
                $"رقم الأساس {number.Trim()} مكرر في {court?.Trim()} لسنة {year.Trim()} — تحقق من الدائرة والرقم والنوع");
    }

    /// <summary>
    /// شبكة السباق: تعارض القيد الفريد عند الحفظ المتزامن يُترجَم 409 وديًا بدل 500.
    /// تُلفّ حول استدعاء _tx.RunAsync كاملًا (تعديل سطرين لكل موقع).
    /// </summary>
    private async Task<T> WithNumberGuardAsync<T>(Func<Task<T>> action, string? number)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            throw new DocumentConflictException(
                $"رقم الأساس {number?.Trim()} مكرر — أُدخل أثناء الحفظ من مستخدم آخر، أعد المحاولة برقم مختلف", ex);
        }
    }

    /// <summary>نسخة الإجراءات بلا قيمة راجعة (تدوير الأرقام).</summary>
    private async Task WithNumberGuardAsync(Func<Task> action, string? number)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            throw new DocumentConflictException(
                $"رقم الأساس {number?.Trim()} مكرر — أُدخل أثناء الحفظ من مستخدم آخر، أعد المحاولة برقم مختلف", ex);
        }
    }
}
