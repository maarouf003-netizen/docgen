using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common;

/// <summary>
/// فحص مبكر موحّد لعدّاد التزامن المتفائل (P0-06): يقارن النسخة التي قرأها العميل
/// بالنسخة المحمّلة، ويرفض القديمة بـ<c>409</c> ورسالة عربية ودية قبل أي عمل.
/// </summary>
/// <remarks>
/// <para>
/// هذا الفحص <b>اختياري</b>: غياب النسخة يعني قبولًا بلا فحص مبكر (توافق خلفي كامل)،
/// لكن شبكة الأمان البنيوية تبقى ناشطة دائمًا عبر <c>IsConcurrencyToken</c> والزيادة
/// المركزية في <c>SaveChanges</c> — فأي سباق بين القراءة والحفظ يُرَدّ <c>409</c> أيضًا.
/// </para>
/// <para>
/// لا يزيد العدّاد هنا: الزيادة مسؤولية <c>DocGeneratorDbContext.SaveChanges</c>
/// (أو مسار خدمي يزيد صراحةً كما في حارس الملفات) — فلا ازدواج ولا اعتماد على الترتيب.
/// </para>
/// </remarks>
public static class OptimisticConcurrencyGuard
{
    /// <summary>
    /// يرفض الطلب إن كانت <paramref name="expectedVersion"/> محددة ولا تساوي النسخة الحالية.
    /// </summary>
    /// <param name="entity">الكيان المحمّل من القاعدة.</param>
    /// <param name="expectedVersion">النسخة القادمة من العميل (قد تكون غائبة).</param>
    /// <param name="subject">اسم الكيان بالعربية للرسالة (مثل «الفرع» أو «الشعبة»).</param>
    public static void EnsureCurrent(IVersionedEntity entity, long? expectedVersion, string subject)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (expectedVersion is null)
            return;

        if (entity.Version != expectedVersion.Value)
            throw new DocumentConflictException(
                $"{subject} تغيّر أثناء التحرير من مستخدم آخر — أعد التحميل وحاول مجددًا");
    }
}
