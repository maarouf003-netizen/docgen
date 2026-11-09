using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اسم العرض الموحّد: يغطي عائلة «طالبة تنفيذ» (المقترض) وعائلة «منفذ عليها/عرض وايداع»
/// (المنفذ عليه/طالب التنفيذ) فلا يسقط التذكير إلى نوع المستند أو حالته.
/// </summary>
public class DocumentDisplayNameTests
{
    [Fact]
    public void ApplicantFile_UsesBorrowerTriple()
    {
        var doc = NewDocument(GeneralEntitySideCatalog.Applicant);
        doc.BorrowerName = "أحمد";
        doc.BorrowerFather = "خالد";
        doc.BorrowerFamily = "الخطيب";

        Assert.Equal("أحمد خالد الخطيب", DocumentDisplayName.Build(doc));
    }

    [Fact]
    public void ExecutedFile_PrefersFirstExecutedNaturalPerson()
    {
        var doc = NewDocument(GeneralEntitySideCatalog.Executed);
        doc.ExecutedNaturalPersons.Add(new ExecutedNaturalPerson
        {
            Name = "سامر",
            Father = "نبيل",
            Family = "الحلبي",
        });
        doc.ExecutedPublicEntities.Add(new ExecutedPublicEntity
        {
            EntityName = "شركة بركات التجارية",
            EntityNature = PartyNatureCatalog.Legal,
        });

        Assert.Equal("سامر نبيل الحلبي", DocumentDisplayName.Build(doc));
    }

    [Fact]
    public void ExecutedFile_UsesLegalExecutedEntityName()
    {
        var doc = NewDocument(GeneralEntitySideCatalog.Executed);
        doc.ExecutedPublicEntities.Add(new ExecutedPublicEntity
        {
            EntityName = "شركة بركات التجارية",
            EntityNature = PartyNatureCatalog.Legal,
        });

        Assert.Equal("شركة بركات التجارية", DocumentDisplayName.Build(doc));
    }

    [Fact]
    public void ExecutedFile_UsesLegalExecutionApplicantName()
    {
        var doc = NewDocument(GeneralEntitySideCatalog.Deposit);
        doc.ExecutionApplicants.Add(new ExecutionApplicant
        {
            Name = "شركة بركات التجارية",
            ApplicantNature = PartyNatureCatalog.Legal,
        });

        Assert.Equal("شركة بركات التجارية", DocumentDisplayName.Build(doc));
    }

    [Fact]
    public void EmptyExecutedFile_FallsBackToSideLabel()
    {
        var doc = NewDocument(GeneralEntitySideCatalog.Executed);

        Assert.Equal("الجهة العامة منفذ عليها", DocumentDisplayName.Build(doc));
    }

    private static Document NewDocument(string side) => new()
    {
        Id = 77,
        GeneralEntitySide = side,
        DocumentType = "متداول",
    };
}
