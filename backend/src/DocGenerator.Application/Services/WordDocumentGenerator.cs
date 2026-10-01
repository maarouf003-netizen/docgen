using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

public class WordDocumentGenerator : IWordDocumentGenerator
{
    private readonly IDocumentContextBuilder _contextBuilder;
    private readonly IDocumentRenderer _renderer;
    private readonly IRepository<Document> _documents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public WordDocumentGenerator(
        IDocumentContextBuilder contextBuilder,
        IDocumentRenderer renderer,
        IRepository<Document> documents,
        IUnitOfWork unitOfWork,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _contextBuilder = contextBuilder;
        _renderer = renderer;
        _documents = documents;
        _unitOfWork = unitOfWork;
        _tx = tx;
        _audit = audit;
    }

    public async Task<WordGenerationResult> GenerateAsync(
        int documentId,
        string templateCode,
        int recipient = 0,
        int[]? estateIds = null,
        int heirId = 0,
        CancellationToken ct = default,
        string? actorName = null)
    {
        var document = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new KeyNotFoundException($"المستند غير موجود: {documentId}");

        var context = await _contextBuilder.BuildContextAsync(
            documentId, templateCode, recipient, estateIds, heirId, ct);
        var bytes = await _renderer.RenderAsync(context, templateCode, ct);

        // RF-017 (SEC-001): العرض خارج المعاملة عمدًا (بناء + تصيير بلا كتابة)؛ الكتابة
        // (العدّاد) والتدقيق داخل معاملة واحدة — فشل التدقيق يتراجع عن الزيادة.
        await _tx.RunAsync(async token =>
        {
            document.PrintCount++;
            document.UpdatedAt = DateTime.UtcNow;
            _documents.Update(document);
            await _unitOfWork.SaveChangesAsync(token);
            var scope = heirId > 0 ? $" للوريث {heirId}" : estateIds is { Length: > 0 } estates
                ? $" للأصول [{string.Join('،', estates)}]"
                : string.Empty;
            await _audit.LogAsync(actorName, "generate_document", documentId, document.DocumentType,
                $"ولّد مستندًا بالقالب {templateCode} للمستلم {recipient}{scope}", token);
        }, ct);

        var name = string.IsNullOrWhiteSpace(document.BorrowerName)
            ? "مستند"
            : SanitizeFileName(document.BorrowerName);

        return new WordGenerationResult(bytes, $"{name}_{templateCode}.docx");
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray());
    }
}
