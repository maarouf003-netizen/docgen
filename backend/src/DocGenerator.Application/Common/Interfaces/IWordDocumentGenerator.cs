namespace DocGenerator.Application.Common.Interfaces;

public record WordGenerationResult(byte[] Bytes, string FileName);

public interface IWordDocumentGenerator
{
    Task<WordGenerationResult> GenerateAsync(
        int documentId,
        string templateCode,
        int recipient = 0,
        int[]? estateIds = null,
        CancellationToken ct = default);
}
