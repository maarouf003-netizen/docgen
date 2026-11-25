using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Delegations;

/// <summary>إنابات ملف (المنيب: المصدر؛ أو المناب: إنابته) — بطاقة «تشعبات الملف».</summary>
public sealed record GetDocumentDelegationsQuery(int DocumentId) : IQuery<List<DelegationDto>>;

internal sealed class GetDocumentDelegationsQueryHandler
    : QueryHandlerBase<GetDocumentDelegationsQuery, List<DelegationDto>>
{
    private readonly IDocumentRepository _documents;
    private readonly IDelegationRepository _delegations;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public GetDocumentDelegationsQueryHandler(
        IDocumentRepository documents,
        IDelegationRepository delegations,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<GetDocumentDelegationsQuery, List<DelegationDto>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _delegations = delegations;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<List<DelegationDto>> HandleCoreAsync(
        GetDocumentDelegationsQuery query, CancellationToken ct)
    {
        var documentId = query.DocumentId;
        var doc = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new ArgumentException("الملف غير موجود");

        // الملف المناب: يُعرض «تشعباته» من إنابته (المصدر)، والملف المنيب: إناباته الصادرة.
        var delegations = doc.SourceDelegationId is not null
            ? await ListOfTargetAsync(doc.Id, ct)
            : await _delegations.ListBySourceAsync(doc.Id, ct);

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        // ملاحظة الإضافات المحلية تُقرأ من صف الإنابة نفسه (سجل دائم) لا من تنبيهات المرآة
        // النشطة — فأرشفة التنبيه أو تنظيفه لا تمحو أثر الإضافة من «تشعبات الملف».
        return delegations
            .Select(d => DocumentDelegationService.ToDto(d, d.SourceDocument, currentYear))
            .ToList();
    }

    private async Task<List<DocumentDelegation>> ListOfTargetAsync(int targetDocumentId, CancellationToken ct)
    {
        var delegation = await _delegations.FindByTargetAsync(targetDocumentId, ct);
        return delegation is null ? new List<DocumentDelegation>() : new List<DocumentDelegation> { delegation };
    }
}
