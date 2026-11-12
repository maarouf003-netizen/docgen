using System.Text.Json;
using DocGenerator.Api.Controllers;
using DocGenerator.Application.Common.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// مفتاح تعطيل توليد Word: عند `Enabled=false` يرفض المتحكم الطلب قبل لمس أي خدمة
/// (503 + رسالة مفهومة)، فلا يمكن تجاوز التعطيل من الواجهة أو الـAPI.
/// </summary>
public class DocumentGenerationDisabledTests
{
    private static DocumentsController ControllerWithGeneration(bool enabled) =>
        new(
            null!, null!, null!, null!, null!,
            Options.Create(new DocumentGenerationOptions
            {
                Enabled = enabled,
                DisabledMessage = "الميزة معطلة حاليًا لحين توحيد النماذج المولدة",
            }),
            null!, null!, null!, null!, null!, null!, null!);

    [Fact]
    public async Task Generate_WhenDisabled_Returns503WithConfiguredMessage()
    {
        var controller = ControllerWithGeneration(enabled: false);

        var result = await controller.Generate(1, "004");

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);

        var json = JsonSerializer.Serialize(objectResult.Value);
        using var body = JsonDocument.Parse(json);
        Assert.Equal(
            "الميزة معطلة حاليًا لحين توحيد النماذج المولدة",
            body.RootElement.GetProperty("message").GetString());
    }
}
