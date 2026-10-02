using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// RF-010: حراس التزامن المتفائل (عدّاد `Version`) للتحرير الطويل والحالة.
/// الفحص الخدمي المبكر برسالة ودية + زيادة العدّاد وتثبيت `OriginalValue` لشبكة
/// السباق (`409`) — دفاع عمق مع عمود `Version` في Configurations (إضافة خالصة
/// بلا مساس بالصفوف القائمة، ومحمولة `SQLite`/`Postgres` بلا توليد مخزني).
/// كشف السباق عبر `IDbExceptionClassifier` (نمط RF-009 — لا مرجع `EF` هنا).
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

}
