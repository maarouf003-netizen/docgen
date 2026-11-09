using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Tests;

public class DocumentSearchTextBuilderTests
{
    [Fact]
    public void Build_IncludesOnlyActiveNumbersOfCurrentCircuit()
    {
        var doc = new Document
        {
            Id = 1,
            ExecutionCircuitId = 1,
            BorrowerName = "مقترض",
        };
        doc.BaseNumbers.Add(new DocumentBaseNumber
        {
            Id = 1, DocumentId = 1, ExecutionCircuitId = 1, Year = 2026,
            BaseNumber = "111", CreatedById = 1,
        });
        doc.BaseNumbers.Add(new DocumentBaseNumber
        {
            Id = 2, DocumentId = 1, ExecutionCircuitId = 2, Year = 2026,
            BaseNumber = "222", CreatedById = 1,
        });
        doc.BaseNumbers.Add(new DocumentBaseNumber
        {
            Id = 3, DocumentId = 1, ExecutionCircuitId = 1, Year = 2025,
            BaseNumber = "333", CreatedById = 1, VoidedAt = DateTime.UtcNow,
        });

        var text = DocumentSearchTextBuilder.Build(doc);

        Assert.Contains("111", text);
        Assert.DoesNotContain("222", text);
        Assert.DoesNotContain("333", text);
    }

    [Fact]
    public void Build_CircuitlessDocument_DoesNotLeakOtherCircuitNumbers()
    {
        var doc = new Document { Id = 1, ExecutionCircuitId = null, BorrowerName = "مقترض" };
        doc.BaseNumbers.Add(new DocumentBaseNumber
        {
            Id = 1, DocumentId = 1, ExecutionCircuitId = 9, Year = 2026,
            BaseNumber = "999", CreatedById = 1,
        });

        var text = DocumentSearchTextBuilder.Build(doc);

        Assert.DoesNotContain("999", text);
    }
}
