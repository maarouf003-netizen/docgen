using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة الحالات (بقايا عقد مساعدات فقط بعد `P4-01bd`): انتقالات الحالة صارت أوامر
/// تملك معاملاتها، وبقي هنا كاتبا الوقوعات/التجديد اللذان يُستدعيان داخل معاملة
/// مستدعٍ خارجي (`UpdateDocumentCommand`/`DocumentService`) — لا يفتحان معاملة.
/// التنفيذ الفعلي في `DocumentStatusRules` (مصدر واحد بلا تكرار).
/// </summary>
public sealed class DocumentStatusService : IDocumentStatusService
{
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IRepository<DocumentBaseNumber> _baseNumbers;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IDocumentNumberRegistry? _numberRegistry;

    public DocumentStatusService(
        IRepository<DocumentOccurrence> occurrences,
        IRepository<DocumentBaseNumber> baseNumbers,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IDocumentNumberRegistry? numberRegistry = null)
    {
        _occurrences = occurrences;
        _baseNumbers = baseNumbers;
        _clock = clock;
        _timeZone = timeZone;
        _numberRegistry = numberRegistry;
    }

    public Task ApplyRenewalAsync(Document doc, RenewalRequest? renewal, bool executedLike, int? userId, CancellationToken ct)
        => DocumentStatusRules.ApplyRenewalAsync(
            doc, renewal, executedLike, userId, _baseNumbers, _occurrences, _numberRegistry, _clock, _timeZone, ct);

    public Task AddStruckOffOccurrenceAsync(Document doc, int? userId, CancellationToken ct)
        => DocumentStatusRules.AddStruckOffOccurrenceAsync(doc, userId, _occurrences, _clock, _timeZone, ct);
}
