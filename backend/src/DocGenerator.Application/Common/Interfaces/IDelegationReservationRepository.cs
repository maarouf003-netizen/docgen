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
    /// حذف الحجوزات اليتيمة لمنيب (صفوف ليست لإنابة حاجبة) — حذف مباشر بلا تتبع
    /// (آمن مع السياقات طويلة العمر: لا يتعارض مع نسخ متتبعة بالمعرف نفسه).
    /// </summary>
    Task DeleteExceptAsync(int sourceDocumentId, List<int> keepDelegationIds, CancellationToken ct = default);
}
