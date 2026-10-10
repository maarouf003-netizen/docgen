using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات منتدى المحامين المنفّذة على مستوى قاعدة البيانات: ترقيم بمفتاح `Id`
/// (لا إزاحة — التيار حيّ)، وعدّادات القراءة باستعلام تجميع واحد، وحذف الاحتفاظ
/// الصريح على خطوتين.
/// </summary>
public interface IForumRepository : IRepository<ForumMessage>
{
    /// <summary>
    /// شريحة التيار تصاعديًا (`CreatedAt`, `Id`): بلا مؤشر = أحدث `limit`؛
    /// `before` = أحدث `limit` قبل المعرف؛ `after` = أول `limit` بعده.
    /// مع `q` يُتجاهل المؤشران وتُعاد أحدث المطابقات (بحث النص).
    /// </summary>
    Task<List<ForumMessage>> ListSliceAsync(int limit, int? before, int? after, string? q, CancellationToken ct = default);

    /// <summary>هل توجد رسائل أقدم من `oldestId` (مع نفس فلتر البحث)؟</summary>
    Task<bool> HasOlderAsync(int oldestId, string? q, CancellationToken ct = default);

    /// <summary>الرسالة المثبّتة الحالية (`null` عند عدمها) — للشريط.</summary>
    Task<ForumMessage?> GetPinnedAsync(CancellationToken ct = default);

    /// <summary>المثبّتة متتبَّعة لتبديل التثبيت الذري.</summary>
    Task<ForumMessage?> GetTrackedPinnedAsync(CancellationToken ct = default);

    /// <summary>رسالة متتبَّعة للتعديل/الحذف/التثبيت.</summary>
    Task<ForumMessage?> GetTrackedByIdAsync(int id, CancellationToken ct = default);

    /// <summary>عدد الإيصالات لكل رسالة من المعطاة — استعلام تجميع واحد (لا `N+1`).</summary>
    Task<Dictionary<int, int>> CountReadsAsync(IReadOnlyCollection<int> messageIds, CancellationToken ct = default);

    /// <summary>أعلى `MessageId` موثّق لهذا المستخدم (0 عند غيابه) — أساس العدّاد.</summary>
    Task<int> MaxReadMessageIdAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// معرفات رسائل الغير القائمة ضمن (`fromExclusive`, `toInclusive`] — للتوثيق
    /// الدفعي. رسائل الكاتب نفسه مستثناة دائمًا: قراءة الذات لا توثَّق (فلا تضخم
    /// عدّاد ✓✓ ولا تظهر في «شوهدت بواسطة»).
    /// </summary>
    Task<List<int>> ExistingIdsInRangeAsync(int userId, int fromExclusive, int toInclusive, CancellationToken ct = default);

    /// <summary>معرفات الرسائل الموثّقة سلفًا لهذا المستخدم من المعطاة.</summary>
    Task<List<int>> ReadIdsAsync(int userId, IReadOnlyCollection<int> messageIds, CancellationToken ct = default);

    /// <summary>
    /// عدد رسائل الغير الأحدث من `maxReadId` — العدّاد (شارة الأيقونة).
    /// رسائل الكاتب نفسه مستثناة دائمًا (لا شارة لرسائلي).
    /// </summary>
    Task<int> CountUnreadAsync(int userId, int maxReadId, CancellationToken ct = default);

    /// <summary>إيصالات رسالة مرتبة زمنيًا — نافذة «شوهدت بواسطة».</summary>
    Task<List<ForumMessageRead>> ListReadersAsync(int messageId, CancellationToken ct = default);

    /// <summary>
    /// حذف الاحتفاظ الصلب: تصفير مراجع الاقتباس إلى المستهدفة (تبقى اللقطات) ثم
    /// إيصالات الرسائل الأقدم من `cutoff` (عدا المثبّتة) ثم الرسائل نفسها —
    /// يُعيد عدد الرسائل المحذوفة.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default);

    /// <summary>
    /// حذف صلب مرتب لرسالة واحدة: تصفير مراجع اقتباسها (تبقى اللقطات) ثم
    /// إيصالاتها ثم هي — بلا اعتماد على تتالي المزود. يُعيد `true` عند وجودها وحذفها.
    /// </summary>
    Task<bool> DeleteMessageAsync(int messageId, CancellationToken ct = default);
}
