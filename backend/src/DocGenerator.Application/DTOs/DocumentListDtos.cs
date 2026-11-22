using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.DTOs;

/// <summary>
/// صف قائمة ملفات مُسقَط (بلا كيان <see cref="Document"/> كامل): الحقول التي تحتاجها
/// بطاقة القائمة + ملخصات الأطراف وآخر إجراء، لتُغذّي `DocumentResponse.FromEntity`
/// على كيان مُجمَّع في الذاكرة — فتتطابق قواعد العرض المشتقة حرفيًا.
/// </summary>
public sealed class DocumentListRow : IDocumentExecutionState
{
    public int Id { get; set; }
    public long Version { get; set; }
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
    // حقول قوائم الأرشيف الخمس (محذوفة/مشطوبة/منفذة/محالة للبداية/تذكيرات الشطب):
    // تواريخ العرض الخاصة بكل قائمة — تُسقَط من نفس الأعمدة بلا شجرة تحميل كاملة.
    public DateTime? DeletedAt { get; set; }
    public DateTime? StruckOffDate { get; set; }
    public DateTime? ExecutedExecutionDate { get; set; }
    public DateTime? ExecutedDepositDate { get; set; }
    public string? ForcedExecutionDate { get; set; }
    public string? BaraetDate { get; set; }
    public DateTime? StartReferralDate { get; set; }

    public List<DocumentBaseNumber> BaseNumbers { get; set; } = new();
    public List<ExecutionAction> ExecutionActions { get; set; } = new();
    public List<ExecutedNaturalPerson> ExecutedNaturalPersons { get; set; } = new();
    public List<ExecutedPublicEntity> ExecutedPublicEntities { get; set; } = new();
    public List<ExecutionApplicant> ExecutionApplicants { get; set; } = new();
    public List<ApplicantPublicEntity> ApplicantPublicEntities { get; set; } = new();

    /// <summary>
    /// كيان مؤقت (غير متتبَّع) يُجمَّع في الذاكرة من الحقول المسقطة + العناصر الدفعية —
    /// يُمرَّر إلى <see cref="DocumentResponse.FromEntity"/> فيبقى مصدر قواعد العرض واحدًا.
    /// مصدر مشترك لقوائم الملفات (`P3-04b`/`P3-04c`) وتصدير البوابة (`P3-04d`).
    /// </summary>
    public Document ToDocumentStub() => new()
    {
        Id = Id,
        Version = Version,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        DocumentType = DocumentType,
        IsDraft = IsDraft,
        BorrowerName = BorrowerName,
        BorrowerFather = BorrowerFather,
        BorrowerFamily = BorrowerFamily,
        Applicant = Applicant,
        Lawyer = Lawyer,
        Court = Court,
        ExecutionCircuitId = ExecutionCircuitId,
        GeneralEntitySide = GeneralEntitySide ?? GeneralEntitySideCatalog.Applicant,
        ExecStatus = ExecStatus,
        ExecSubStatus = ExecSubStatus,
        ExecutedStatus = ExecutedStatus,
        FileNumber = FileNumber,
        FileType = FileType,
        FileYear = FileYear,
        BranchName = BranchName,
        SourceDelegationId = SourceDelegationId,
        Branch = AdministrativeBranchName is null
            ? null
            : new Branch { Name = AdministrativeBranchName },
        RegistrationDate = RegistrationDate is null
            ? null
            : new DocumentRegistrationDate { DateParsed = RegistrationDate },
        SourceDelegation = DelegationReturnDate is null
            ? null
            : new DocumentDelegation { ReturnDate = DelegationReturnDate },
        DeletedAt = DeletedAt,
        StruckOffDate = StruckOffDate,
        ExecutedExecutionDate = ExecutedExecutionDate,
        ExecutedDepositDate = ExecutedDepositDate,
        ForcedExecutionDate = ForcedExecutionDate,
        BaraetDate = BaraetDate,
        StartReferralDate = StartReferralDate,
        BaseNumbers = BaseNumbers,
        ExecutionActions = ExecutionActions,
        ExecutedNaturalPersons = ExecutedNaturalPersons,
        ExecutedPublicEntities = ExecutedPublicEntities,
        ExecutionApplicants = ExecutionApplicants,
        ApplicantPublicEntities = ApplicantPublicEntities,
    };
}

/// <summary>
/// عنصر قائمة الملفات: الحقول التي تستهلكها بطاقات القائمة فقط (سطحية + ملخصات أطراف
/// + آخر إجراء)، مع الحقول المشتقة المحسوبة خادميًا من المصدر الوحيد نفسه.
/// </summary>
public sealed class DocumentListItemDto
{
    public int Id { get; set; }
    /// <summary>عدّاد التزامن المتفائل (RF-010) — تُرسله إعادة الملف المشطوب مع طلبها.</summary>
    public long Version { get; set; }
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
    // حقول قوائم الأرشيف الخمس (P3-04c) — نفس دلالات `DocumentResponse` حرفيًا.
    public DateTime? DeletedAt { get; set; }
    public DateTime? StruckOffDate { get; set; }
    public DateTime? ExecutedExecutionDate { get; set; }
    public DateTime? ExecutedDepositDate { get; set; }
    public string? ForcedExecutionDate { get; set; }
    public string? BaraetDate { get; set; }
    public string? StartReferralDate { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public string? LastActivitySource { get; set; }
    public List<ExecutionActionDto> ExecutionActions { get; set; } = new();
    public List<ExecutedNaturalPersonDto> ExecutedNaturalPersons { get; set; } = new();
    public List<ExecutedPublicEntityDto> ExecutedPublicEntities { get; set; } = new();
    public List<ExecutionApplicantDto> ExecutionApplicants { get; set; } = new();
    public List<ApplicantPublicEntityDto> ApplicantPublicEntities { get; set; } = new();

    /// <summary>
    /// تحويل صف مُسقَط إلى عنصر قائمة: يُجمَّع كيان مؤقت في الذاكرة (غير متتبَّع) من
    /// الحقول المسقطة + العناصر الدفعية، ثم يُمرَّر إلى <see cref="DocumentResponse.FromEntity"/>
    /// — المصدر الوحيد لقواعد العرض (الحالة/الرقم الفعّال/التدوير/آخر نشاط)، فلا تُنسخ
    /// القواعد في قائمة ثانية. مصدر مشترك لكل قوائم الملفات (P3-04b/P3-04c).
    /// </summary>
    public static DocumentListItemDto FromRow(DocumentListRow row, int currentYear)
    {
        var full = DocumentResponse.FromEntity(row.ToDocumentStub(), currentYear);
        return new DocumentListItemDto
        {
            Id = full.Id,
            Version = full.Version,
            CreatedAt = full.CreatedAt,
            UpdatedAt = full.UpdatedAt,
            DocumentType = full.DocumentType,
            IsDraft = full.IsDraft,
            BorrowerName = full.BorrowerName,
            BorrowerFather = full.BorrowerFather,
            BorrowerFamily = full.BorrowerFamily,
            Applicant = full.Applicant,
            GeneralEntitySide = full.GeneralEntitySide,
            FileNumber = full.FileNumber,
            DisplayFileNumber = full.DisplayFileNumber,
            DisplayFileYear = full.DisplayFileYear,
            FileType = full.FileType,
            FileYear = full.FileYear,
            Court = full.Court,
            Lawyer = full.Lawyer,
            BranchName = full.BranchName,
            AdministrativeBranchName = full.AdministrativeBranchName,
            ExecStatus = full.ExecStatus,
            ExecSubStatus = full.ExecSubStatus,
            ExecutedStatus = full.ExecutedStatus,
            DisplayStatus = full.DisplayStatus,
            NeedsRotation = full.NeedsRotation,
            SourceDelegationId = full.SourceDelegationId,
            DelegationReturnDate = full.DelegationReturnDate,
            DeletedAt = full.DeletedAt,
            StruckOffDate = full.StruckOffDate,
            ExecutedExecutionDate = full.ExecutedExecutionDate,
            ExecutedDepositDate = full.ExecutedDepositDate,
            ForcedExecutionDate = full.ForcedExecutionDate,
            BaraetDate = full.BaraetDate,
            StartReferralDate = full.StartReferralDate,
            LastActivityDate = full.LastActivityDate,
            LastActivitySource = full.LastActivitySource,
            ExecutionActions = full.ExecutionActions,
            ExecutedNaturalPersons = full.ExecutedNaturalPersons,
            ExecutedPublicEntities = full.ExecutedPublicEntities,
            ExecutionApplicants = full.ExecutionApplicants,
            ApplicantPublicEntities = full.ApplicantPublicEntities,
        };
    }
}
