using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// كتب المطالعة: مراسلات رسمية بين المحامي ورئيس القسم. النص المرسل غير قابل للتعديل أو الحذف،
/// والتسلسل الزمني للرسائل هو المرجع، والكتابة ضمن معاملات مع سجل التدقيق.
/// </summary>
public sealed partial class ReviewLetterService : IReviewLetterReplyWriteRules, IReviewLetterCreateWriteRules
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
        _clock = clock;
        _timeZone = timeZone;
        _entropy = entropy ?? SystemEntropySource.Instance;
    }

    // ── تنفيذ عقد مساعدات الرد/اللاحق المشتركة (P4-01bl) ─────────────────

    void IReviewLetterReplyWriteRules.EnsureLetterOwner(UserRole role, int? actorSectionId, int? ownerSectionId)
        => EnsureLetterOwner(role, actorSectionId, ownerSectionId);

    Task<int?> IReviewLetterReplyWriteRules.OwnerSectionOfAsync(ReviewLetter letter, CancellationToken ct)
        => OwnerSectionOfAsync(letter, _documents, _circuits, ct);

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
        => FollowsDocumentAsync(_appeals, _delegations, documentId, userId, ct);

    Task<int?> IReviewLetterCreateWriteRules.FileOwnerSectionAsync(Document document, CancellationToken ct)
        => FileOwnerSectionAsync(document, _circuits, ct);

    Task<Section?> IReviewLetterCreateWriteRules.ResolveGeneralRecipientSectionAsync(
        int? recipientSectionId, int actorBranchId, CancellationToken ct)
        => ResolveGeneralRecipientSectionAsync(recipientSectionId, actorBranchId, ct);

    Task<string> IReviewLetterCreateWriteRules.GenerateUniqueNumberAsync(
        string branchCode, DateTime at, CancellationToken ct)
        => GenerateUniqueNumberAsync(branchCode, at, ct);

    ReviewLetterDto IReviewLetterCreateWriteRules.ToDto(ReviewLetter letter)
        => ToDto(letter, _clock, _timeZone);
}
