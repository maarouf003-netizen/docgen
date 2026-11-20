using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// حجوزات الأصول الصريحة للإنابات الحاجبة (B3): صف لكل أصل فيزيائي محجوز، بقيد فريد
/// على (المنيب + الأصل) — نقطة التسلسل لسباق التسطير المتزامن. القراءات بلا تتبع
/// (لا تُعدَّل بعد قراءتها)؛ الكتابة داخل معاملات مسارات الإنابة حصرًا.
/// </summary>
public interface IDelegationReservationRepository : IRepository<DelegationAssetReservation>
{
    /// <summary>حجوزات منيب معين (بلا تتبع) — يغذي حارس الحجب (اتحادًا مع مسح اللقطات).</summary>
    Task<List<DelegationAssetReservation>> ListBySourceAsync(int sourceDocumentId, CancellationToken ct = default);

    /// <summary>حذف كل حجوزات إنابة (مواءمة التعديل/التحرر) — حذف مباشر بلا تتبع.</summary>
    Task DeleteByDelegationAsync(int delegationId, CancellationToken ct = default);

    /// <summary>
    /// حذف الحجوزات اليتيمة لمنيب (صفوف ليست لإنابة حاجبة) في جملة `DELETE` واحدة
    /// تحكم على حالة الإنابة **وقت التنفيذ** — لا من لقطة سابقة. لقطةٌ أقدم من قراءة
    /// الحجوزات كانت تمحو حجز إنابةٍ التزمت للتوّ في سباق `READ COMMITTED` على `Postgres`
    /// (فيبقى أصلها بلا حجز يقيد تسطيرًا متزامنًا لاحقًا).
    /// </summary>
    Task DeleteStaleAsync(int sourceDocumentId, CancellationToken ct = default);
}
