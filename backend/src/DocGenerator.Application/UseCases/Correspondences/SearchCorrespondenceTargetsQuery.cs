using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Correspondences;

/// <summary>
/// مرشحو الاستلام بالاسم — محامٍ/رئيس قسم/مندوب جهة نشط، بلا المنشئ نفسه.
/// مع documentId تُقيَّد الأهلية بنوع المراسلة: مندوب الجهة ← محامو الملف (مالكه
/// ومتابعوه) حصرًا؛ المحامي/رئيس القسم ← مناديب نطاق الملف فقط. عامة بلا ملف = بحث حر.
/// </summary>
public sealed record SearchCorrespondenceTargetsQuery(
    int ActorUserId,
    UserRole Role,
    int? ActorBranchId,
    string? Q,
    int? DocumentId,
    int? ActorSectionId = null) : IQuery<List<CorrespondenceTargetDto>>;

internal sealed class SearchCorrespondenceTargetsQueryHandler
    : QueryHandlerBase<SearchCorrespondenceTargetsQuery, List<CorrespondenceTargetDto>>
{
    private const int TargetsLimit = 20;

    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IAppealRepository _appeals;
    private readonly IDelegationRepository _delegations;
    private readonly IPortalRepository _portal;

    public SearchCorrespondenceTargetsQueryHandler(
        IDocumentRepository documents,
        IUserRepository users,
        IRepository<ExecutionCircuit> circuits,
        IAppealRepository appeals,
        IDelegationRepository delegations,
        IPortalRepository portal,
        IEnumerable<IPipelineBehavior<SearchCorrespondenceTargetsQuery, List<CorrespondenceTargetDto>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _users = users;
        _circuits = circuits;
        _appeals = appeals;
        _delegations = delegations;
        _portal = portal;
    }

    protected override async Task<List<CorrespondenceTargetDto>> HandleCoreAsync(
        SearchCorrespondenceTargetsQuery query, CancellationToken ct)
    {
        if (!CorrespondenceService.CanWrite(query.Role))
            throw new UnauthorizedAccessException("الدور غير مخوّل لتسطير المراسلات");

        Document? document = null;
        if (query.DocumentId is not null)
        {
            document = await _documents.GetByIdAsync(query.DocumentId.Value, ct)
                ?? throw new ArgumentException("الملف غير موجود");

            // بوابة البحث = صلاحية التسطير على هذا الملف (نفس فحص الإنشاء) فلا نكشف
            // مرشحين لمن لا يملك الكتابة عليه مهما كان دوره.
            if (!await CorrespondenceService.MayAttachAsync(
                    document, query.ActorUserId, query.Role, query.ActorBranchId, query.ActorSectionId,
                    _circuits, _appeals, _delegations, _portal, ct))
                throw new UnauthorizedAccessException("لا تملك صلاحية تسطير مراسلة على هذا الملف");
        }

        return await ResolveEligibleTargetsAsync(query.ActorUserId, query.Role, query.Q, document, ct);
    }

    /// <summary>
    /// مصدر الحقيقة الوحيد لأهلية المستلمين: عامة بلا ملف = البحث الحر المعتاد؛
    /// مربوطة بملف = مندوب←محامو الملف، محامٍ/رئيس←مناديب نطاق الملف.
    /// يُستخدم من البحث والإنشاء معًا فلا يمكن أن يُترشح مستلمٌ ثم يُرفض إنشاؤه أو العكس.
    /// </summary>
    private async Task<List<CorrespondenceTargetDto>> ResolveEligibleTargetsAsync(
        int actorUserId, UserRole role, string? q, Document? document, CancellationToken ct)
    {
        if (document is null)
        {
            var users = await _users.SearchCorrespondenceTargetsAsync(actorUserId, q, TargetsLimit, ct);
            return users.Select(ToTargetDto).ToList();
        }

        if (role == UserRole.EntityManager)
        {
            var lawyers = await _users.SearchDocumentLawyerTargetsAsync(
                document.Id, document.CreatedById, actorUserId, q, TargetsLimit, ct);
            return lawyers.Select(ToTargetDto).ToList();
        }

        // محامٍ/رئيس قسم: مناديب نطاق الملف فقط — لا رؤساء ولا محامين آخرين.
        var scope = await _portal.GetDocumentScopeKeysAsync(document.Id, ct);
        var delegates = await _users.SearchScopeDelegateTargetsAsync(
            scope.EntryIds, scope.GroupIds, actorUserId, q, TargetsLimit, ct);
        return delegates.Select(ToTargetDto).ToList();
    }

    private static CorrespondenceTargetDto ToTargetDto(User u) => new(
        u.Id,
        u.FullName,
        u.Role.ToString().ToLowerInvariant(),
        u.Branch?.Name,
        u.Branch?.Governorate ?? u.PortalEntry?.Governorate);
}
