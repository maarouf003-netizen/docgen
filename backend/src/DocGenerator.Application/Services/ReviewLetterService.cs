using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IReviewLetterService
{
    /// <summary>
    /// قائمة كتب المطالعة بحسب الدور: المحامي كتبه، ورئيس القسم كتب قسمه
    /// (دوائر القسم وبلا دائرة)، ورئيس الشعبة كتب شعبته، والمدير/المشرف كتب
    /// فرع الإدارة المنتقى (لا عرض قبل اختيار الفرع).
    /// </summary>
    Task<PagedResult<ReviewLetterListItemDto>> SearchAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, int page, int perPage,
        string? administrativeBranch, CancellationToken ct = default, int? actorSectionId = null);

    /// <summary>كتاب مطالعة برسائله — بعد التحقق من حق الوصول (نطاق المالك §10).</summary>
    Task<ReviewLetterDto> GetByIdAsync(int id, int actorUserId, UserRole role, int? actorBranchId,
        CancellationToken ct = default, int? actorSectionId = null);

    /// <summary>عدد كتب النطاق بانتظار الرد (جرس المالك الأحمر §10).</summary>
    Task<int> CountPendingForHeadAsync(int branchId, int? ownerSectionId = null, CancellationToken ct = default);

    /// <summary>
    /// عدد كتب المحامي التي فيها ردّ لم يطّلع عليه بعد — شارة بند المطالعات.
    /// </summary>
    Task<int> CountUnseenRepliesForLawyerAsync(int actorUserId, CancellationToken ct = default);

    /// <summary>
    /// كتب ملف محدد — لمالك الملف ومتابعيه (إحالة/إنابة/استئناف) والمدير/المشرف،
    /// ولرئيس نطاق الملف (قسمه لدوائر القسم وبلا دائرة، وشعبته لدوائر شعبته).
    /// </summary>
    Task<List<ReviewLetterListItemDto>> ListByDocumentAsync(int documentId, int actorUserId,
        UserRole role, int? actorBranchId, CancellationToken ct = default, int? actorSectionId = null);

    /// <summary>أسماء فروع الإدارة المميزة لفلتر المدير/المشرف.</summary>
    Task<List<string>> GetAdministrativeBranchesAsync(CancellationToken ct = default);
}

/// <summary>
/// كتب المطالعة: مراسلات رسمية بين المحامي ورئيس القسم. النص المرسل غير قابل للتعديل أو الحذف،
/// والتسلسل الزمني للرسائل هو المرجع، والكتابة ضمن معاملات مع سجل التدقيق.
/// </summary>
public sealed partial class ReviewLetterService : IReviewLetterService, IReviewLetterReplyWriteRules, IReviewLetterCreateWriteRules
{
    private const int NumberRandomDigits = 4;
    private const int MaxNumberGenerationAttempts = 20;

    private readonly IReviewLetterRepository _letters;
    private readonly IDocumentRepository _documents;
    private readonly IBranchRepository _branches;
    private readonly IUserRepository _users;
    private readonly IRepository<Section> _sections;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IAppealRepository _appeals;
    private readonly IDelegationRepository _delegations;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IEntropySource _entropy;

    public ReviewLetterService(
        IReviewLetterRepository letters,
        IDocumentRepository documents,
        IBranchRepository branches,
        IAppealRepository appeals,
        IDelegationRepository delegations,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IUserRepository users,
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
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
        _entropy = entropy ?? SystemEntropySource.Instance;
    }

    // ── تنفيذ عقد مساعدات الرد/اللاحق المشتركة (P4-01bl) ─────────────────

    void IReviewLetterReplyWriteRules.EnsureLetterOwner(UserRole role, int? actorSectionId, int? ownerSectionId)
        => EnsureLetterOwner(role, actorSectionId, ownerSectionId);

    Task<int?> IReviewLetterReplyWriteRules.OwnerSectionOfAsync(ReviewLetter letter, CancellationToken ct)
        => OwnerSectionOfAsync(letter, ct);

    Task<string> IReviewLetterReplyWriteRules.ResolveBranchCodeAsync(int branchId, CancellationToken ct)
        => ResolveBranchCodeAsync(branchId, ct);

    Task<string> IReviewLetterReplyWriteRules.GenerateUniqueNumberAsync(string branchCode, DateTime at, CancellationToken ct)
        => GenerateUniqueNumberAsync(branchCode, at, ct);

    Task IReviewLetterReplyWriteRules.StageReplyAlertAsync(
        ReviewLetter letter, ReviewLetterMessage reply, int headUserId, string? headName, CancellationToken token)
        => StageReplyAlertAsync(letter, reply, headUserId, headName, token);

    ReviewLetterMessageDto IReviewLetterReplyWriteRules.ToMessageDto(ReviewLetterMessage message)
        => ToMessageDto(message);

    // ── تنفيذ عقد مساعدات تسطير الكتاب (P4-01bm) ──────────────────────────

    Task<bool> IReviewLetterCreateWriteRules.FollowsDocumentAsync(int documentId, int userId, CancellationToken ct)
        => FollowsDocumentAsync(documentId, userId, ct);

    Task<int?> IReviewLetterCreateWriteRules.FileOwnerSectionAsync(Document document, CancellationToken ct)
        => FileOwnerSectionAsync(document, ct);

    Task<Section?> IReviewLetterCreateWriteRules.ResolveGeneralRecipientSectionAsync(
        int? recipientSectionId, int actorBranchId, CancellationToken ct)
        => ResolveGeneralRecipientSectionAsync(recipientSectionId, actorBranchId, ct);

    Task<string> IReviewLetterCreateWriteRules.GenerateUniqueNumberAsync(
        string branchCode, DateTime at, CancellationToken ct)
        => GenerateUniqueNumberAsync(branchCode, at, ct);

    ReviewLetterDto IReviewLetterCreateWriteRules.ToDto(ReviewLetter letter)
        => ToDto(letter);
}
