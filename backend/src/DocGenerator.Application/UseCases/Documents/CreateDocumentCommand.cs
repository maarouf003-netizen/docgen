using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// إنشاء ملف جديد: الأمر يحمل سياق الفاعل ومفتاح عدم التكرار، والمعالج يفوض
/// `IDocumentService` (المالك الحالي للمعاملة والتدقيق) بلا تغيير سلوكي.
/// </summary>
public sealed record CreateDocumentCommand(
    DocumentUpsertRequest Request,
    int UserId,
    string? ActorName,
    int? BranchId,
    string? IdempotencyKey) : ICommand<DocumentResponse>;

public sealed class CreateDocumentCommandHandler : CommandHandlerBase<CreateDocumentCommand, DocumentResponse>
{
    private readonly IDocumentService _documents;

    public CreateDocumentCommandHandler(
        IDocumentService documents,
        IEnumerable<IPipelineBehavior<CreateDocumentCommand, DocumentResponse>> behaviors)
        : base(behaviors)
        => _documents = documents;

    protected override Task<DocumentResponse> HandleCoreAsync(CreateDocumentCommand command, CancellationToken ct)
        => _documents.CreateAsync(command.Request, command.UserId, command.ActorName, command.BranchId, ct,
            command.IdempotencyKey);
}
