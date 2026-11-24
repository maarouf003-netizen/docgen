using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Documents;

namespace DocGenerator.Application.Tests;

/// <summary>
/// تثبيت رسائل التحقق لأوامر أرقام الأساس — وحدات صرفة (بلا قاعدة): التحقق يسبق
/// أي مساس بالمستودع، فالاعتماد `null` هنا مُعلَن ومقصود.
/// </summary>
public sealed class DocumentCommandValidationTests
{
    [Fact]
    public async Task CorrectBaseNumber_WithoutNumber_ThrowsExactMessage()
    {
        var handler = new CorrectDocumentBaseNumberCommandHandler(
            null!, Array.Empty<IPipelineBehavior<CorrectDocumentBaseNumberCommand, bool>>());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(
            new CorrectDocumentBaseNumberCommand(
                1, 2026, new CorrectBaseNumberRequest("   "), 1, "lawyer1")));

        Assert.Equal("رقم الأساس مطلوب", ex.Message);
    }
}
