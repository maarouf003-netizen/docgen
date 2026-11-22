using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.DTOs;

/// <summary>
/// صف قائمة ملفات مُسقَط (بلا كيان <see cref="Document"/> كامل): الحقول التي تحتاجها
/// بطاقة القائمة + ملخصات الأطراف وآخر إجراء، لتُغذّي `DocumentResponse.FromEntity`
/// على كيان مُجمَّع في الذاكرة — فتتطابق قواعد العرض المشتقة حرفيًا.
/// </summary>
public sealed class DocumentListRow : IDocumentExecutionState
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? DocumentType { get; set; }
    public bool IsDraft { get; set; }
    public string? BorrowerName { get; set; }
    public string? BorrowerFather { get; set; }
    public string? BorrowerFamily { get; set; }
    public string? Applicant { get; set; }
    public string? Lawyer { get; set; }
    public string? Court { get; set; }
    public int? ExecutionCircuitId { get; set; }
    public string? GeneralEntitySide { get; set; }
    public string? ExecStatus { get; set; }
    public string? ExecSubStatus { get; set; }
    public string? ExecutedStatus { get; set; }
    public string? FileNumber { get; set; }
    public string? FileType { get; set; }
    public string? FileYear { get; set; }
    public string? BranchName { get; set; }
    public int? SourceDelegationId { get; set; }
    public string? AdministrativeBranchName { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public DateTime? DelegationReturnDate { get; set; }

    public List<DocumentBaseNumber> BaseNumbers { get; set; } = new();
    public List<ExecutionAction> ExecutionActions { get; set; } = new();
    public List<ExecutedNaturalPerson> ExecutedNaturalPersons { get; set; } = new();
    public List<ExecutedPublicEntity> ExecutedPublicEntities { get; set; } = new();
    public List<ExecutionApplicant> ExecutionApplicants { get; set; } = new();
    public List<ApplicantPublicEntity> ApplicantPublicEntities { get; set; } = new();
}

/// <summary>
/// عنصر قائمة الملفات: الحقول التي تستهلكها بطاقات القائمة فقط (سطحية + ملخصات أطراف
/// + آخر إجراء)، مع الحقول المشتقة المحسوبة خادميًا من المصدر الوحيد نفسه.
/// </summary>
public sealed class DocumentListItemDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? DocumentType { get; set; }
    public bool IsDraft { get; set; }
    public string? BorrowerName { get; set; }
    public string? BorrowerFather { get; set; }
    public string? BorrowerFamily { get; set; }
    public string? Applicant { get; set; }
    public string? GeneralEntitySide { get; set; }
    public string? FileNumber { get; set; }
    public string? DisplayFileNumber { get; set; }
    public string? DisplayFileYear { get; set; }
    public string? FileType { get; set; }
    public string? FileYear { get; set; }
    public string? Court { get; set; }
    public string? Lawyer { get; set; }
    public string? BranchName { get; set; }
    public string? AdministrativeBranchName { get; set; }
    public string? ExecStatus { get; set; }
    public string? ExecSubStatus { get; set; }
    public string? ExecutedStatus { get; set; }
    public string? DisplayStatus { get; set; }
    public bool NeedsRotation { get; set; }
    public bool HasAppeals { get; set; }
    public int? MatchedAppealId { get; set; }
    public int? SourceDelegationId { get; set; }
    public string? DelegationReturnDate { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public string? LastActivitySource { get; set; }
    public List<ExecutionActionDto> ExecutionActions { get; set; } = new();
    public List<ExecutedNaturalPersonDto> ExecutedNaturalPersons { get; set; } = new();
    public List<ExecutedPublicEntityDto> ExecutedPublicEntities { get; set; } = new();
    public List<ExecutionApplicantDto> ExecutionApplicants { get; set; } = new();
    public List<ApplicantPublicEntityDto> ApplicantPublicEntities { get; set; } = new();
}
