using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// عقد الاستثناءات: كيان غير موجود (`مراسلة`/`ملف`) ← `KeyNotFoundException`
/// (يترجم إلى 404)، ومدخل تحقق خاطئ ← `ArgumentException` (يترجم إلى 400) —
/// لا يُرمى أحدهما مكان الآخر في أي مسار.
/// </summary>
public interface ICorrespondenceService
{
    /// <summary>
    /// قائمة المراسلات بحسب الدور: الطرف (منشئ/مستلم) مراسلاته، ورئيس القسم مراسلات
    /// محافظته (فرعه + العامة من مندوبيها)، ورئيس الشعبة مراسلات نطاقه (طرفٌ فيها
    /// أو ملك دائرة شعبته)، والمدير/المشرف بمحافظة منتقاة إجباريًا.
    /// </summary>
    Task<PagedResult<CorrespondenceListItemDto>> SearchAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, string? governorate,
        string? importance, int page, int perPage, CancellationToken ct = default,
        int? actorSectionId = null);

    /// <summary>مراسلة برسائلها وتوثيق مشاهداتها — بعد التحقق من حق الوصول (نطاق الشعبة §10).</summary>
    Task<CorrespondenceDto> GetByIdAsync(int id, int actorUserId, UserRole role,
        int? actorBranchId, CancellationToken ct = default, int? actorSectionId = null);

    /// <summary>تسطير مراسلة (مربوطة بملف أو عامة) لطرف معيَّن بالاسم، برقم وتاريخ تلقائيين.</summary>
    Task<CorrespondenceDto> CreateAsync(CreateCorrespondenceRequest request, int actorUserId,
        string? actorName, UserRole role, int? actorBranchId, CancellationToken ct = default,
        int? actorSectionId = null);

    /// <summary>إضافة لاحق إلى مراسلة — منشئ المراسلة نفسه فقط.</summary>
    Task<CorrespondenceMessageDto> AddAddendumAsync(int correspondenceId,
        AddCorrespondenceAddendumRequest request, int actorUserId, string? actorName,
        CancellationToken ct = default);

    /// <summary>
    /// رد الطرف المستلم على المراسلة — الطرف المستلم فقط. الرد يُوثّق مشاهدته
    /// تلقائيًا (من؟ متى؟) في المعاملة نفسها إن لم يكن موثَّقًا سابقًا.
    /// </summary>
    Task<CorrespondenceMessageDto> ReplyAsync(int correspondenceId,
        ReplyCorrespondenceRequest request, int actorUserId, string? actorName,
        CancellationToken ct = default);
    /// <summary>
    /// تأكيد المشاهدة الصريح (زر «تمت المشاهدة») — يسجّل (من؟ متى؟) مرة واحدة؛
    /// التكرار يُبقي أول توثيق ولا يجدّده. مقصور على الطرف المستلم معيَّنًا بالاسم:
    /// من يقرأ المراسلة (المرسل/رئيس/المدير) يرى حالتها ولا يُنشئ توثيقًا.
    /// قرار أرشيف مقصود: توثيقات قديمة أنشأها غير المستلم (حين كان الحق مفتوحًا)
    /// تبقى في القاعدة ولا تُعرض في الواجهة، والمراسلة تُقرأ «بانتظار» حتى يوثّق
    /// المستلم — فالتوثيق المرئي هو توثيق المستلم وحده دائمًا.
    /// </summary>
    Task<CorrespondenceReceiptDto> MarkSeenAsync(int correspondenceId, int actorUserId,
        string? actorName, UserRole role, int? actorBranchId,
        CancellationToken ct = default, int? actorSectionId = null);

    /// <summary>عدد المراسلات غير المؤكَّدة المشاهدة (كمستلم) — عدّاد الجرس والشارة.</summary>
    Task<int> CountUnseenAsync(int actorUserId, CancellationToken ct = default);

    /// <summary>
    /// مراسلات ملف محدد: لمالك الملف ورئيس نطاقه (قسمه لدوائر القسم وبلا دائرة،
    /// وشعبته لدوائر شعبته) والمدير/المشرف ومتابعيه (إحالة/إنابة/استئناف)،
    /// ولمندوب الجهة المراسلات التي هو طرف فيها على هذا الملف.
    /// </summary>
    Task<List<CorrespondenceListItemDto>> ListByDocumentAsync(int documentId, int actorUserId,
        UserRole role, int? actorBranchId, CancellationToken ct = default, int? actorSectionId = null);

    /// <summary>المحافظات المميزة لفلتر المدير/المشرف.</summary>
    Task<List<string>> GetGovernoratesAsync(CancellationToken ct = default);

    /// <summary>
    /// مرشحو الاستلام بالاسم — محامٍ/رئيس قسم/مندوب جهة نشط، بلا المنشئ نفسه.
    /// مع documentId تُقيَّد الأهلية بنوع المراسلة: مندوب الجهة ← محامو الملف (مالكه
    /// ومتابعوه) حصرًا؛ المحامي/رئيس القسم ← مناديب نطاق الملف فقط. عامة بلا ملف = بحث حر.
    /// </summary>
    Task<List<CorrespondenceTargetDto>> SearchTargetsAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, int? documentId,
        CancellationToken ct = default, int? actorSectionId = null);
}

/// <summary>
/// المراسلات: تواصل رسمي ثنائي بين مندوب الجهة العامة والمحامي/رئيس القسم (أو بينهما
/// لمندوبٍ معيَّن بالاسم). النص المرسل غير قابل للتعديل أو الحذف، والتسلسل الزمني
/// للرسائل مع توثيق المشاهدات هو المرجع، والكتابة ضمن معاملات مع سجل التدقيق.
/// كتابة المندوب تمر حصرًا عبر مسارات البوابة المخصصة (لا تُضعِف حارس العزل البنيوي).
/// </summary>
public sealed partial class CorrespondenceService : ICorrespondenceService
{
    private const int NumberRandomDigits = 5;
    private const int MaxNumberGenerationAttempts = 20;
    private const int TargetsLimit = 20;

    /// <summary>
    /// حدّ النص الصافي للمراسلة (بعد التعقيم): 10000 حرف — يمنع الرسائل شاذة
    /// الحجم (تكلفة التعقيم والعرض والتخزين) دون أن يمسّ أي مراسلة رسمية
    /// مشروعة. المقياس أحرف النص المرئي المطبّع (`ToPlainText`) لا وسوم HTML
    /// ولا كلمات — وهو نفسه مقياس عدّاد الواجهة (`correspondencePlainText`).
    /// يُفرض هنا (400) لا في الواجهة وحدها. بلا هجرة: الأعمدة `TEXT` مفتوحة
    /// والصفوف القائمة تُقرأ كما هي.
    /// </summary>
    private const int MaxBodyPlainTextLength = 10_000;

    private readonly ICorrespondenceRepository _letters;
    private readonly IDocumentRepository _documents;
    private readonly IBranchRepository _branches;
    private readonly IUserRepository _users;
    private readonly IRepository<Section> _sections;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IAppealRepository _appeals;
    private readonly IDelegationRepository _delegations;
    private readonly IPortalRepository _portal;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _errors;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IEntropySource _entropy;

    public CorrespondenceService(
        ICorrespondenceRepository letters,
        IDocumentRepository documents,
        IBranchRepository branches,
        IUserRepository users,
        IAppealRepository appeals,
        IDelegationRepository delegations,
        IPortalRepository portal,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier errors,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IRepository<Section> sections,
        IRepository<ExecutionCircuit> circuits,
        IEntropySource? entropy = null)
    {
        _letters = letters;
        _documents = documents;
        _branches = branches;
        _users = users;
        _sections = sections;
        _circuits = circuits;
        _appeals = appeals;
        _delegations = delegations;
        _portal = portal;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _errors = errors;
        _clock = clock;
        _timeZone = timeZone;
        _entropy = entropy ?? SystemEntropySource.Instance;
    }
}
