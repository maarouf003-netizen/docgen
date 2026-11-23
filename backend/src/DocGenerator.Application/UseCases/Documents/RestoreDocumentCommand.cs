using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// استعادة ملف محذوف منطقيًا = إعادة قيد برقم جديد: تُفرَّغ حقول الرقم الجارية
/// ويُرفع `NeedsRegistration`، وتبقى الصفوف الملغاة توثيقًا. يملك معاملته وتدقيقه
/// (`restore`)، ويحوّل انتهاك القيد الفريد إلى `DocumentConflictException` كما الأصل.
/// </summary>
public sealed record RestoreDocumentCommand(
    int DocumentId,
    string? ActorName) : ICommand<bool>, ITransactionalRequest;

public sealed class RestoreDocumentCommandHandler
    : CommandHandlerBase<RestoreDocumentCommand, bool>
{
    private readonly IDocumentRepository _documents;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IDocumentWriteGuard _writeGuard;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public RestoreDocumentCommandHandler(
        IDocumentRepository documents,
        IRepository<ExecutionCircuit> circuits,
        IDocumentWriteGuard writeGuard,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<RestoreDocumentCommand, bool>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _circuits = circuits;
        _writeGuard = writeGuard;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(
        RestoreDocumentCommand command, CancellationToken ct)
    {
        var doc = await _documents.GetDeletedByIdAsync(command.DocumentId, ct);
        if (doc is null || !doc.IsDeleted)
            return false;

        // الاستعادة = إعادة قيد برقم جديد: تُفرَّغ حقول الرقم الجارية ويُرفع
        // NeedsRegistration، وتبقى الصفوف الملغاة توثيقًا (لا إحياء لصف ملغى).
        // لا فحص تفرد هنا: لا رقم بعد الاستعادة حتى يسجّل المحامي رقمه الجديد.
        return await _writeGuard.WithNumberGuardAsync(async () =>
        {
            // الاستعادة تعيد اشتقاق الدائرة من السجل (M3): التسمية الجماعية أثناء
            // الحذف المنطقي لا تمسّ المحذوف، فتُزامَن هنا وإلا عُرض الاسم القديم.
            if (doc.ExecutionCircuitId is not null)
            {
                var circuit = await _circuits.GetByIdAsync(doc.ExecutionCircuitId.Value, ct);
                if (circuit is not null)
                {
                    doc.Court = circuit.Name;
                    doc.CourtNorm = ArabicNameNormalizer.Normalize(circuit.Name);
                    doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                    doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
                }
            }
            doc.IsDeleted = false;
            doc.DeletedAt = null;
            doc.NeedsRegistration = true;
            doc.FileNumber = null;
            doc.FileYear = null;
            // لا تُعاد حقول التجديد الجارية مع ملف مستعاد بلا رقم: التاريخ محفوظ في الوقوعات.
            doc.RenewalFileNumber = null;
            doc.RenewalFileType = null;
            doc.RenewalFileReceiptNumber = null;
            doc.RenewalFileReceiptDate = null;
            doc.RenewalDate = null;
            doc.SearchText = DocumentSearchTextBuilder.Build(doc);
            doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
            doc.UpdatedAt = DateTime.UtcNow;
            doc.Version++;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(ct);
            await _audit.LogAsync(command.ActorName, "restore", command.DocumentId, doc.DocumentType,
                DocumentText.AuditWithActor($"استعادة المستند (رقم {command.DocumentId})", doc), ct);
            return true;
        }, doc.FileNumber);
    }
}
