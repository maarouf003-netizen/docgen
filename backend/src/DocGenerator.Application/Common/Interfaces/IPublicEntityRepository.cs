using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// مستودع السجل المرجعي للجهات العامة: قيود المحافظة/الفرع وهوياتها الأم
/// وأسماؤها البديلة واقتراحات الاعتماد، مع استعلامات الاستيراد ومزامنة النصوص.
/// </summary>
public interface IPublicEntityRepository
{
    /// <summary>كل الهويات الأم مع قيودها وأسمائها البديلة (للبحث والتطبيع في الذاكرة).</summary>
    Task<List<PublicEntityGroup>> ListGroupsWithEntriesAsync(CancellationToken ct = default);

    /// <summary>الهويات الأم متتبَّعة (لإعادة استخدامها عند إنشاء قيد دون تعارض تتبع).</summary>
    Task<List<PublicEntityGroup>> ListGroupsTrackedAsync(CancellationToken ct = default);

    Task<PublicEntity?> GetEntryAsync(int entryId, CancellationToken ct = default);

    /// <summary>قيدٌ مع هويته الأم وأسمائه البديلة ومنشئه — لعرض التفاصيل والتعديل.</summary>
    Task<PublicEntity?> GetEntryWithDetailsAsync(int entryId, CancellationToken ct = default);

    Task<PublicEntityGroup?> GetGroupAsync(int groupId, CancellationToken ct = default);

    /// <summary>هل يوجد قيد بنفس (الهوية، المحافظة، الفرع)؟ يمنع تكرار القيد نفسه.</summary>
    Task<bool> EntryExistsAsync(int groupId, string governorate, string branchName, CancellationToken ct = default);

    Task AddGroupAsync(PublicEntityGroup group, CancellationToken ct = default);
    Task AddEntryAsync(PublicEntity entry, CancellationToken ct = default);

    // ── الاقتراحات ── (أُلغي نموذج الاقتراحات — استُبدل بالمراجعة اللاحقة)

    /// <summary>رؤساء الأقسام النشطون الذين تتبع فروعهم محافظة محددة — لتوجيه تنبيه المراجعة.</summary>
    Task<List<User>> ListActiveHeadsByGovernorateAsync(string governorate, CancellationToken ct = default);

    /// <summary>رؤساء الأقسام النشطون لفرع محدد — لتوجيه تنبيه المراجعة إلى رئيس فرع المُدخِل.</summary>
    Task<List<User>> ListActiveHeadsByBranchAsync(int branchId, CancellationToken ct = default);

    // ── الاستيراد (د12) ──

    /// <summary>نصوص الجهات طالبة التنفيذ المتمايزة مع محافظتها وعدّاد ملفاتها.</summary>
    Task<List<(string Name, string? Governorate, int DocumentCount)>> ListDistinctApplicantTextsAsync(CancellationToken ct = default);

    /// <summary>نصوص الجهات المنفذ عليها (جهة عامة فقط) المتمايزة مع محافظتها وعدّاد ملفاتها.</summary>
    Task<List<(string EntityName, string? Governorate, int DocumentCount)>> ListDistinctExecutedTextsAsync(CancellationToken ct = default);

    /// <summary>نصوص طالبي التنفيذ الاعتباريين المربوطين جهة عامة (RegistryId != null) المتمايزة.</summary>
    Task<List<(string Name, int DocumentCount)>> ListDistinctExecutionApplicantTextsAsync(CancellationToken ct = default);

    // ── مزامنة النصوص عند إعادة التسمية (د5) ──

    /// <summary>
    /// صفوف طالب التنفيذ المطابقة لأحد الأسماء المعطاة، مع ملفها محمّلًا بكامل
    /// المجموعات التي يقرؤها بناء نص البحث (ورثة/كفلاء/طالبو تنفيذ/منفذ عليهم)
    /// حتى لا تُفقَد توكنات غير متأثرة عند إعادة بناء SearchText.
    /// </summary>
    Task<List<ApplicantPublicEntity>> ListApplicantRowsByNamesAsync(IReadOnlyCollection<string> names, CancellationToken ct = default);

    /// <summary>
    /// صفوف المنفذ عليه (جهة عامة فقط) المطابقة لأحد الأسماء المعطاة، مع ملفها
    /// محمّلًا بكامل مجموعات نص البحث كما في الطرف المقابل.
    /// </summary>
    Task<List<ExecutedPublicEntity>> ListExecutedRowsByNamesAsync(IReadOnlyCollection<string> names, CancellationToken ct = default);

    /// <summary>
    /// صفوف طالب التنفيذ الاعتباري المربوطة جهة عامة (RegistryId != null) المطابقة لأحد
    /// الأسماء، مع ملفها محمّلًا بكامل مجموعات نص البحث حتى لا تُفقَد توكنات غير متأثرة.
    /// </summary>
    Task<List<ExecutionApplicant>> ListExecutionApplicantRowsByNamesAsync(IReadOnlyCollection<string> names, CancellationToken ct = default);

    // ── نقل القيد (د3) ──

    /// <summary>
    /// الملفات المربوطة بقيد معين عبر ApplicantPublicEntity.RegistryId أو ExecutedPublicEntity.RegistryId
    /// مع تحميل المجموعات الفرعية اللازمة لمزامنة نص البحث.
    /// </summary>
    Task<List<Document>> ListDocumentsLinkedToEntryAsync(int entryId, CancellationToken ct = default);

    /// <summary>إيجاد قيد في هوية أم محددة بنفس المحافظة والفرع.</summary>
    Task<PublicEntity?> FindEntryInGroupAsync(int groupId, string governorate, string branchName, CancellationToken ct = default);

    // ── الدمج (د5 §4) ──

    /// <summary>كل القيود في مجموعة معينة (متتبعة للتعديل).</summary>
    Task<List<PublicEntity>> ListEntriesByGroupAsync(int groupId, CancellationToken ct = default);

    /// <summary>سجل تغييرات الجهات مع الفاعل والقيد/الهوية — للمراقبة والتصدير.</summary>
    Task<List<PublicEntityChangeEvent>> ListChangeEventsAsync(CancellationToken ct = default);
}
