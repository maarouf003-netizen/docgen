using DocGenerator.Application.Common;

namespace DocGenerator.Application.Services;

/// <summary>
/// RF-009: حراس وحدانية رقم الأساس الفعّال (الدائرة + الرقم + النوع + السنة).
/// الفحص الخدمي المبكر برسالة ودية + التقاط تعارض القيد عند السباق (409) — دفاع عمق
/// مع القيد الفريد الجزئي في Configurations (المحذوف والمسودات خارج القيدين).
/// </summary>
public sealed partial class DocumentService
{
    // ملاحظة: التوقيع النصي القديم EnsureNumberUniqueAsync(court, …) أُسقط بعد ترحيل
    // المواضع الستة إلى التوقيع الدائري (M12 — بلا منادٍ). تاريخه في Git فقط.
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
