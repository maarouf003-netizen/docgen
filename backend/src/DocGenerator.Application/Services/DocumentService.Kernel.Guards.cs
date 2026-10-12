using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// نواة الحراس المشتركة داخل الصنف (المرحلة 0 — بلا `DI` ولا تغيير باني ولا توقيع عام):
/// نقل ميكانيكي حرفي من `DocumentService.Concurrency.cs` (حراس RF-010: الفحص المبكر
/// + شبكة السباق) و`DocumentService.Numbering.cs` (شبكتا RF-009: تعارض القيد الفريد
/// يُترجَم `409` وديًا) وحارس المعلق الصريح من `DocumentService.Circuits.cs` —
/// بلا أي تغيير سلوكي.
/// البيوت النهائية (تُطبَّق من المرحلة 1 فصاعدًا): الحراس ذات الاعتمادات
/// (`EnsureConcurrency`، `WithConcurrencyGuardAsync`، `WithNumberGuardAsync`)
/// ← متعاون نحيف محقون `IDocumentWriteGuard`، وحارس المعلق (خالص)
/// ← صنف `static` مشترك `Common/DocumentText.cs`.
/// </summary>
public sealed partial class DocumentService
{
    /// <summary>
    /// الفحص المبكر: النسخة المرسَلة تطابق المقروءة؛ وإلا `409` ودي.
    /// ثم يزيد العدّاد ويثبّت `OriginalValue` لتكشف `EF` السباق بين القراءة والحفظ.
    /// الغياب = توافق (قبول بلا فحص مبكر، لكن الزيادة والشبكة تنشطان).
    /// </summary>
    private void EnsureConcurrency(Document doc, long? version)
    {
        if (version is not null && doc.Version != version.Value)
            throw new DocumentConflictException(
                "تغيّر الملف أثناء التحرير من مستخدم آخر — أعد تحميل الملف وحاول مجددًا");
        doc.Version++;
        if (version is not null)
            _documents.SetVersionOriginal(doc, version.Value);
    }

    /// <summary>
    /// شبكة السباق: تعارض التزامن عند الحفظ المتزامن يُترجَم `409` وديًا بدل `500`.
    /// تُلفّ حول استدعاء `_tx.RunAsync` كاملًا (نمط `WithNumberGuardAsync` في RF-009).
    /// </summary>
    private async Task<T> WithConcurrencyGuardAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (_dbErrors.IsConcurrencyViolation(ex))
        {
            throw new DocumentConflictException(
                "تغيّر الملف أثناء الحفظ من مستخدم آخر — أعد تحميل الملف وحاول مجددًا", ex);
        }
    }

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

    /// <summary>حارس المعلق الصريح: تُمنع عليه العمليات التي تلزم الهوية الرقمية.</summary>
    private static void EnsureNotPendingRegistration(Document doc, string actionLabel)
    {
        if (doc.NeedsRegistration)
            throw new ArgumentException($"لا يمكن {actionLabel} على ملف بانتظار إعادة القيد — أدخل رقمه الجديد أولًا");
    }
}
