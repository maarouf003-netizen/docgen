namespace DocGenerator.Application.Common.Interfaces;

public record WordGenerationResult(byte[] Bytes, string FileName);

public interface IWordDocumentGenerator
{
    Task<WordGenerationResult> GenerateAsync(
        int documentId,
        string templateCode,
        int recipient = 0,
        int[]? estateIds = null,
        int heirId = 0,
        CancellationToken ct = default);
}
