using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>قبول/رفض اقتراح تعديل الجهة الأم — المدير/المشرف فقط.</summary>
public sealed record ReviewParentEditSuggestionCommand(
    int SuggestionId,
    ReviewParentEditSuggestionRequest Request,
    EntityRegistryActor Actor) : ICommand<ParentEditSuggestionDto?>;

internal sealed class ReviewParentEditSuggestionCommandHandler
    : CommandHandlerBase<ReviewParentEditSuggestionCommand, ParentEditSuggestionDto?>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<ParentEditSuggestion> _suggestions;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public ReviewParentEditSuggestionCommandHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IRepository<ParentEditSuggestion> suggestions,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<ReviewParentEditSuggestionCommand, ParentEditSuggestionDto?>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
        _suggestions = suggestions;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<ParentEditSuggestionDto?> HandleCoreAsync(
        ReviewParentEditSuggestionCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actor = command.Actor;
        if (actor.Role is not (UserRole.Manager or UserRole.Admin))
            throw new UnauthorizedAccessException("القرار على اقتراحات الجهة الأم متاح للمدير/المشرف فقط");

        var suggestion = await _suggestions.GetByIdAsync(command.SuggestionId, ct);
        if (suggestion is null)
            return null;
        if (suggestion.Status != ParentEditSuggestionStatusCatalog.Pending)
            throw new ArgumentException("الاقتراح لم يعد معلّقًا");

        var status = _rules.Required(request.Status, "الحالة مطلوبة", 20);
        if (status is not (ParentEditSuggestionStatusCatalog.Approved or ParentEditSuggestionStatusCatalog.Rejected))
            throw new ArgumentException("الحالة يجب أن تكون approved أو rejected");
        var reviewReason = _rules.NormalizeOptional(request.ReviewReason);
        if (status == ParentEditSuggestionStatusCatalog.Rejected)
            reviewReason = _rules.Required(reviewReason, "سبب الرفض مطلوب", 500);

        // حوكمة S1: لا يُعتمد اسم مقترح لم يُطبَّق بعد — مدراء/مشرفون يجيزون التغيير المنجز
        // فعليًا على الأرض، والواجهة تطبّقه قبل المراجعة (needsRename)؛ المسار المباشر يُجبَر هنا.
        var proposedName = _rules.NormalizeOptional(suggestion.ProposedCanonicalName);
        if (status == ParentEditSuggestionStatusCatalog.Approved && proposedName is not null)
        {
            var liveGroup = await _entities.GetGroupAsync(suggestion.GroupId, ct)
                ?? throw new ArgumentException("المجموعة غير موجودة");
            if (ArabicNameNormalizer.Normalize(proposedName) != ArabicNameNormalizer.Normalize(liveGroup.CanonicalName))
                throw new ArgumentException("اعتماد اقتراح باسم غير مُطبَّق مرفوض — طبّق إعادة التسمية أولًا ثم أعد المراجعة");
        }

        suggestion.Status = status;
        suggestion.ReviewedById = actor.UserId;
        suggestion.ReviewReason = reviewReason;
        suggestion.ReviewedAtUtc = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actor.Name, "review_parent_edit_suggestion",
                details: $"{(status == ParentEditSuggestionStatusCatalog.Approved ? "قبل" : "رفض")} اقتراح تعديل الجهة الأم «{suggestion.Group?.CanonicalName ?? suggestion.Entry?.Group?.CanonicalName}»{($" — {reviewReason}")}",
                ct: token);
        }, ct);

        return _rules.ToParentEditSuggestionDto(suggestion);
    }
}
