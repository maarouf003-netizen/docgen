using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// المراسلات: تواصل رسمي ثنائي بين مندوب الجهة العامة والمحامي/رئيس القسم (أو بينهما
/// لمندوبٍ معيَّن بالاسم). النص المرسل غير قابل للتعديل أو الحذف، والتسلسل الزمني
/// للرسائل مع توثيق المشاهدات هو المرجع، والكتابة ضمن معاملات مع سجل التدقيق.
/// كتابة المندوب تمر حصرًا عبر مسارات البوابة المخصصة (لا تُضعِف حارس العزل البنيوي).
/// </summary>
public sealed partial class CorrespondenceService : ICorrespondenceWriteRules
{
    private const int NumberRandomDigits = 5;
    private const int MaxNumberGenerationAttempts = 20;

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
        _clock = clock;
        _timeZone = timeZone;
        _entropy = entropy ?? SystemEntropySource.Instance;
    }

    // ── تنفيذ عقد مساعدات كتابة المراسلات (P4-01bt) ──────────────────────

    bool ICorrespondenceWriteRules.CanWrite(UserRole role) => CanWrite(role);

    string ICorrespondenceWriteRules.RequireBodyText(string bodyHtml, string emptyMessage, string kindLabel)
        => RequireBodyText(bodyHtml, emptyMessage, kindLabel);

    string ICorrespondenceWriteRules.NextMessageNumber(Correspondence letter)
        => NextMessageNumber(letter);

    Task<string> ICorrespondenceWriteRules.GenerateUniqueNumberAsync(string code, DateTime at, CancellationToken ct)
        => GenerateUniqueNumberAsync(code, at, ct);

    Task<bool> ICorrespondenceWriteRules.MayAttachAsync(Document document, int actorUserId, UserRole role,
        int? actorBranchId, int? actorSectionId, CancellationToken ct)
        => MayAttachAsync(document, actorUserId, role, actorBranchId, actorSectionId,
            _circuits, _appeals, _delegations, _portal, ct);

    Task<bool> ICorrespondenceWriteRules.IsEligibleTargetAsync(
        Document document, UserRole role, int targetUserId, CancellationToken ct)
        => IsEligibleTargetAsync(document, role, targetUserId, ct);

    Task<int?> ICorrespondenceWriteRules.ResolveHeadRecipientSectionAsync(
        int? recipientSectionId, User target, Document? document, UserRole role,
        int? actorBranchId, CancellationToken ct)
        => ResolveHeadRecipientSectionAsync(recipientSectionId, target, document, role, actorBranchId, ct);

    Task<(int? BranchId, string Governorate, string Prefix)> ICorrespondenceWriteRules.ResolveScopeAsync(
        Document? document, UserRole role, int actorUserId, int? actorBranchId, CancellationToken ct)
        => ResolveScopeAsync(document, role, actorUserId, actorBranchId, ct);

    CorrespondenceMessageDto ICorrespondenceWriteRules.ToMessageDto(CorrespondenceMessage message)
        => ToMessageDto(message);

    CorrespondenceDto ICorrespondenceWriteRules.ToDto(Correspondence letter, int readerUserId)
        => ToDto(letter, readerUserId, _clock, _timeZone);

    Task<bool> ICorrespondenceWriteRules.CanViewAsync(Correspondence letter, int actorUserId, UserRole role,
        int? actorBranchId, int? actorSectionId, CancellationToken ct)
        => CanViewAsync(letter, actorUserId, role, actorBranchId, actorSectionId,
            _documents, _circuits, _branches, ct);
}
