using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Documents;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيع <c>SaveBaseNumbersAsync</c> القديم فوق معالج الأمر
/// <see cref="SaveDocumentBaseNumbersCommand"/> — منطق الاختبارات كما هو.
/// </summary>
public static class DocumentBaseNumberCommandExtensions
{
    public static Task<bool> SaveBaseNumbersAsync(
        this ICommandHandler<SaveDocumentBaseNumbersCommand, bool> handler,
        int userId,
        List<BaseNumberEntry> entries,
        string? actorName,
        string? warningToken = null,
        CancellationToken ct = default)
        => handler.HandleAsync(
            new SaveDocumentBaseNumbersCommand(userId, entries, actorName, warningToken), ct);
}
