using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// محوّل قيمة موحّد لجميع خصائص <c>DateTime</c> في النموذج: يفرض أن المخزّن في القاعدة
/// <c>UTC</c> دائمًا (SQLite <c>TEXT</c> و Postgres <c>timestamp with time zone</c>).
/// القراءة تعيد تسمية الـ <c>Kind</c> إلى <c>Utc</c> دون تغيير الـ <c>Ticks</c>، والكتابة
/// تُوحِّد أي قيمة (Local/Unspecified) إلى <c>Utc</c> — بسياق مرة تطبيق عبر
/// <c>ConfigureConventions</c> (خطة <c>docs/timezone-fix-plan.md</c>).
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            // الكتابة: المخزّن UTC حصرًا — Local تُحوَّل، وUnspecified تُفسَّر كـ UTC
            // (قاعدة المشروع الثابتة: كل المخزّن UTC، وسابقة PortalService.cs:547).
            v => v.Kind == DateTimeKind.Local
                ? v.ToUniversalTime()
                : v.Kind == DateTimeKind.Utc
                    ? v
                    : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            // القراءة: تُستعاد كـ UTC بلا تغيير الـ Ticks (تُصدرها System.Text.Json بإزاحة Z).
            v => new DateTime(v.Ticks, DateTimeKind.Utc))
    {
    }
}