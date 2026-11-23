using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// حذف منطقي لملف — يملك حرّاس الإنابة/الاستئناف ومعاملته والتدقيق (`delete`)
/// وإلغاء صفوف الأرقام عبر الكاتب الوحيد.
/// <para>
/// **حدود المعاملة:** الحرّاس قبل المعاملة (كسائر الحراس)، ثم `_tx.RunAsync` واحدة
/// تُلغي صفوف الأرقام قبل رفع علم الحذف — فلا تداخل ولا تغيير في الترتيب.
/// </para>
/// </summary>
public sealed record DeleteDocumentCommand(
    int DocumentId,
    string? ActorName) : ICommand<bool>;

public sealed class DeleteDocumentCommandHandler
    : CommandHandlerBase<DeleteDocumentCommand, bool>
{
    private readonly IDocumentRepository _documents;
    private readonly IAppealRepository _appeals;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDocumentNumberRegistry _numberRegistry;

    public DeleteDocumentCommandHandler(
        IDocumentRepository documents,
        IAppealRepository appeals,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDocumentNumberRegistry numberRegistry,
        IEnumerable<IPipelineBehavior<DeleteDocumentCommand, bool>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _appeals = appeals;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _numberRegistry = numberRegistry;
    }

    protected override async Task<bool> HandleCoreAsync(
        DeleteDocumentCommand command, CancellationToken ct)
    {
        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            return false;

        // منع حذف ملفٍ مرتبط بإنابة (صادرة عنه أو مناب عنها — بأي حالة) أو باستئناف
        // (بأي حالة: منظور/محسوم/مشطوب): الحذف المنطقي يُخفي الملف عن القوائم بينما تبقى
        // الإنابة/الاستئناف تشير إليه، فيُيتّم سجلات حية. تحقق قبل تغيير وخارج المعاملة كسائر الحراس.
        // (doc.Delegations وSourceDelegationId محمّلتان في GetByIdAsync — صفر استعلامات إضافية.)
        if (doc.Delegations.Count > 0)
            throw new ArgumentException("لا يمكن حذف الملف المنيب لوجود إنابة صادرة عنه");
        if (doc.SourceDelegationId is not null)
            throw new ArgumentException("لا يمكن حذف الملف المناب لوجود إنابة مرتبطة به");
        if ((await _appeals.ListByDocumentAsync(doc.Id, ct)).Count > 0)
            throw new ArgumentException("لا يمكن حذف الملف لوجود استئناف مرتبط به");

        return await _tx.RunAsync(async token =>
        {
            // الكاتب الوحيد: إلغاء صفوف الأرقام (document-deleted) قبل رفع علم الحذف
            // حتى تبقى الصفوف مرئية لاستعلام الإلغاء، وتبقى معروضة في السجل.
            await _numberRegistry.VoidForDocumentDeleteAsync(doc, doc.CreatedById, command.ActorName, token);
            doc.IsDeleted = true;
            doc.DeletedAt = DateTime.UtcNow;
            doc.UpdatedAt = DateTime.UtcNow;
            doc.Version++;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "delete", command.DocumentId, doc.DocumentType,
                DocumentText.AuditWithActor($"حذف المستند (رقم {command.DocumentId})", doc), token);
            return true;
        }, ct);
    }
}
