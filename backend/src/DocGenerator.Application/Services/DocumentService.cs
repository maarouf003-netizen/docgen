using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IDocumentService
{
    Task<DocumentResponse?> GetAsync(int documentId, CancellationToken ct = default);
    /// <summary>
    /// إسقاط خفيف (F-01) لفحص الوصول في نقاط المتدخلين: لا يحمّل شجرة المستند الكاملة.
    /// </summary>
    Task<DocumentAccessInfo?> GetAccessInfoAsync(int documentId, CancellationToken ct = default);
    Task<DocumentResponse?> GetDeletedAsync(int documentId, CancellationToken ct = default);
    Task<DocumentResponse> CreateAsync(DocumentUpsertRequest request, int userId, string? actorName, int? branchId, CancellationToken ct = default, string? idempotencyKey = null);
    Task<DocumentResponse> TransferAsync(int documentId, int targetLawyerId, string? actorName, CancellationToken ct = default, int? actorUserId = null);
    /// <summary>عدد ملفات المحامي غير المحذوفة (للمعاينة قبل النقل الجماعي).</summary>
    Task<int> CountFilesByOwnerAsync(int ownerId, int? scopeBranchId, CancellationToken ct = default, int? ownerSectionId = null);
    /// <summary>
    /// نقل كامل ملفات محامٍ إلى محامٍ آخر بجميع الحالات — رئيس القسم (ضمن فرعه) فقط.
    /// scopeBranchId يُقيّد النطاق بفرع رئيس القسم ويُرجع عدد الملفات المنقولة.
    /// </summary>
    Task<int> TransferAllAsync(int sourceLawyerId, int targetLawyerId, int? scopeBranchId, string? actorName, CancellationToken ct = default, string? idempotencyKey = null, int? ownerSectionId = null, int? actorUserId = null);
    /// <summary>جدول تدوير أرقام الأساس للمحامي (ترحّلي): ملفاته المؤهلة مع رقم أساس السنة الحالية إن وُجد.</summary>
    Task<PagedResult<RotationDocumentDto>> GetRotationListAsync(int userId, int page, int perPage, CancellationToken ct = default);
    /// <summary>
    /// تاريخ أرقام الأساس للملف (سنة + رقم) مرتبًا تنازليًا بالسنوات — لعرضه عند الضغط على رقم الملف.
    /// </summary>
    Task<List<BaseNumberHistoryDto>> GetBaseNumberHistoryAsync(int documentId, CancellationToken ct = default);
    /// <summary>سجل أرقام ملف محذوف (IgnoreQueryFilters) — لصفحة المحذوفات.</summary>
    Task<List<BaseNumberHistoryDto>> GetDeletedBaseNumberHistoryAsync(int documentId, CancellationToken ct = default);
    /// <summary>تصحيح رقم أساس سنة محددة موضعيًا (لا حذف).</summary>
    Task CorrectBaseNumberAsync(int documentId, int year, CorrectBaseNumberRequest request, int userId, string? actorName, CancellationToken ct = default);
    /// <summary>التراجع عن تدوير السنة الحالية (VoidedAt + بقاء الصف).</summary>
    Task UndoRotationAsync(int documentId, int year, int userId, string? actorName, CancellationToken ct = default);
    /// <summary>
    /// حفظ أرقام أساس السنة الحالية ذرّيًا: رقم جديد يُنشأ (صف تدوير)، والإدخال الفارغ لا يغيّر
    /// شيئًا. يتحقق من ملكية المحامي وأهلية الملف (مقيد وغير منفَّذ وغير محذوف)، ويطبّق
    /// طبقتَي التكرار (حجب صلب/تحذير بـ warningToken).
    /// </summary>
    Task SaveBaseNumbersAsync(int userId, List<BaseNumberEntry> entries, string? actorName, string? warningToken = null, CancellationToken ct = default);
}

public sealed partial class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IRepository<DocumentBaseNumber> _baseNumbers;
    private readonly IRepository<DocumentRegistrationDate> _registrationDates;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IExecutionActionService _actionService;
    private readonly IDocumentDelegationService _delegationService;
    private readonly IDocumentStatusService _statusService;
    private readonly IDocumentWriteGuard _writeGuard;
    private readonly IDocumentNumberRegistry _numberRegistry;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IIdempotencyStore? _idempotency;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IExecutionCircuitService? _executionCircuits;

    public DocumentService(
        IDocumentRepository documents,
        IUserRepository users,
        IRepository<DocumentBaseNumber> baseNumbers,
        IRepository<DocumentRegistrationDate> registrationDates,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IExecutionActionService actionService,
        IDocumentDelegationService delegationService,
        IDocumentStatusService statusService,
        IDocumentWriteGuard writeGuard,
        IDocumentNumberRegistry numberRegistry,
        IIdempotencyStore? idempotency = null,
        IRepository<ExecutionCircuit>? circuits = null,
        IExecutionCircuitService? executionCircuits = null)
    {
        _circuits = circuits ?? new UnconfiguredCircuitRepository();
        _executionCircuits = executionCircuits;
        _documents = documents;
        _users = users;
        _baseNumbers = baseNumbers;
        _registrationDates = registrationDates;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _actionService = actionService;
        _delegationService = delegationService;
        _statusService = statusService;
        _writeGuard = writeGuard;
        _numberRegistry = numberRegistry;
        _clock = clock;
        _timeZone = timeZone;
        _idempotency = idempotency;
    }

    public async Task<DocumentResponse?> GetAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        return doc is null ? null : DocumentResponse.FromEntity(doc, ServerClock.CurrentYear(_clock, _timeZone));
    }

    public Task<DocumentAccessInfo?> GetAccessInfoAsync(int documentId, CancellationToken ct = default)
        => _documents.GetAccessInfoAsync(documentId, ct);

    public async Task<DocumentResponse?> GetDeletedAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetDeletedByIdAsync(documentId, ct);
        return doc is null || !doc.IsDeleted ? null : DocumentResponse.FromEntity(doc, ServerClock.CurrentYear(_clock, _timeZone));
    }

    /// <summary>سنة «قرار السنة» الحالية من ساعة النظام ومنطقته — مصدر واحد لا تكرار قرار.</summary>
    private int CurrentYear() => ServerClock.CurrentYear(_clock, _timeZone);
}
