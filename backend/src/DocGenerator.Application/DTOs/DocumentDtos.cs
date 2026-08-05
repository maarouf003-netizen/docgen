using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.DTOs;

public record GuarantorDto(
    int? Id,
    int GuarantorNumber,
    string? Name,
    string? Father,
    string? Family,
    string? Mother,
    string? Birth,
    string? Register,
    string? NationalId,
    string? Address,
    string? AddressType);

public record RealEstateDto(
    int? Id,
    string? Owner,
    string? Property,
    string? PropertyNumber,
    string? PropertyDistrict,
    string? LandRegistry,
    string? ShareType);

public record ExecutionActionDto(
    int Id,
    string Type,
    string Text,
    string? ActionDate,
    string? ReminderDuration,
    string? ReminderColor,
    string? CreatedByName,
    DateTime CreatedAt);

public class AddExecutionActionRequest
{
    public string Type { get; set; } = "action";
    public string Text { get; set; } = string.Empty;
    public string? ActionDate { get; set; }
    public string? ReminderDuration { get; set; }
    public string? ReminderColor { get; set; }
}

public class UpdateExecutionActionRequest
{
    public string Type { get; set; } = "action";
    public string Text { get; set; } = string.Empty;
    public string? ActionDate { get; set; }
    public string? ReminderDuration { get; set; }
    public string? ReminderColor { get; set; }
}

public class DocumentUpsertRequest
{
    public string? DocumentType { get; set; }
    public string? BorrowerName { get; set; }
    public string? BorrowerFather { get; set; }
    public string? BorrowerFamily { get; set; }
    public string? BorrowerMother { get; set; }
    public string? BorrowerBirth { get; set; }
    public string? BorrowerRegister { get; set; }
    public string? BorrowerNationalId { get; set; }
    public string? BorrowerAddress { get; set; }
    public string? BorrowerAddressType { get; set; } = "موطن مختار";

    public string? ContractType { get; set; }
    public string? ContractTypeSelector { get; set; } = "مصرفي";
    public string? ContractNumber { get; set; }
    public string? ContractDate { get; set; }
    public string? InclusionText { get; set; }

    public decimal? AmountNumeric { get; set; }
    public string? AmountWords { get; set; }
    public string? Currency { get; set; } = "ليرة سورية";
    public decimal? Amount2Numeric { get; set; }
    public string? Amount2Words { get; set; }
    public string? Currency2 { get; set; } = "دولار أمريكي";
    public decimal? InclusionAmountNumeric { get; set; }
    public string? InclusionAmountWords { get; set; }
    public string? InclusionCurrency { get; set; } = "ليرة سورية";

    public string? Court { get; set; }
    public string? Applicant { get; set; }
    public string? Lawyer { get; set; }

    public string? FileNumber { get; set; }
    public string? FileType { get; set; }
    public string? FileYear { get; set; }
    public string? FileIncoming { get; set; }
    public string? FileIncomingDate { get; set; }
    public string? UnderFilingNumber { get; set; }
    public string? FileRegistrationDate { get; set; }
    public string? BranchName { get; set; }

    public string? SeizureDate { get; set; }
    public string? ImmediateActions { get; set; }
    public string? Notes { get; set; }

    public List<GuarantorDto> Guarantors { get; set; } = new();
    public List<RealEstateDto> RealEstates { get; set; } = new();
}

public class DocumentResponse
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int CreatedById { get; set; }
    public int? BranchId { get; set; }
    public string? DocumentType { get; set; }
    public bool IsDraft { get; set; }
    public string? BorrowerName { get; set; }
    public string? BorrowerFather { get; set; }
    public string? BorrowerFamily { get; set; }
    public string? BorrowerMother { get; set; }
    public string? BorrowerBirth { get; set; }
    public string? BorrowerRegister { get; set; }
    public string? BorrowerNationalId { get; set; }
    public string? BorrowerAddress { get; set; }
    public string? BorrowerAddressType { get; set; }
    public string? ContractType { get; set; }
    public string? ContractTypeSelector { get; set; }
    public string? ContractNumber { get; set; }
    public string? ContractDate { get; set; }
    public string? InclusionText { get; set; }
    public decimal AmountNumeric { get; set; }
    public string? AmountWords { get; set; }
    public string? Currency { get; set; }
    public decimal Amount2Numeric { get; set; }
    public string? Amount2Words { get; set; }
    public string? Currency2 { get; set; }
    public decimal InclusionAmountNumeric { get; set; }
    public string? InclusionAmountWords { get; set; }
    public string? InclusionCurrency { get; set; }
    public string? Court { get; set; }
    public string? Applicant { get; set; }
    public string? Lawyer { get; set; }
    public string? FileNumber { get; set; }
    public string? FileType { get; set; }
    public string? FileYear { get; set; }
    public string? FileIncoming { get; set; }
    public string? FileIncomingDate { get; set; }
    public string? UnderFilingNumber { get; set; }
    public string? FileRegistrationDate { get; set; }
    public string? BranchName { get; set; }
    /// <summary>اسم الفرع الإداري للملف (فرع المستخدم الذي أدخله) عبر BranchId.</summary>
    public string? AdministrativeBranchName { get; set; }
    public string? ExecStatus { get; set; }
    public string? ExecSubStatus { get; set; }
    public decimal? CollectedAmount { get; set; }
    public string? BaraetNumber { get; set; }
    public string? BaraetDate { get; set; }
    public string? BaraetRegNumber { get; set; }
    public string? BaraetRegDate { get; set; }
    public string? TarithNumber { get; set; }
    public string? TarithDate { get; set; }
    public string? TarithRegNumber { get; set; }
    public string? TarithRegDate { get; set; }
    public string? SeizureDate { get; set; }
    public string? ImmediateActions { get; set; }
    public string? Notes { get; set; }
    public int ViewCount { get; set; }
    public int PrintCount { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<GuarantorDto> Guarantors { get; set; } = new();
    public List<RealEstateDto> RealEstates { get; set; } = new();
    public List<ExecutionActionDto> ExecutionActions { get; set; } = new();

    public static DocumentResponse FromEntity(Document d) => new()
    {
        Id = d.Id,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
        CreatedById = d.CreatedById,
        BranchId = d.BranchId,
        DocumentType = d.DocumentType,
        IsDraft = d.IsDraft,
        BorrowerName = d.BorrowerName,
        BorrowerFather = d.BorrowerFather,
        BorrowerFamily = d.BorrowerFamily,
        BorrowerMother = d.BorrowerMother,
        BorrowerBirth = d.BorrowerBirth,
        BorrowerRegister = d.BorrowerRegister,
        BorrowerNationalId = d.BorrowerNationalId,
        BorrowerAddress = d.BorrowerAddress,
        BorrowerAddressType = d.BorrowerAddressType,
        ContractType = d.ContractType,
        ContractTypeSelector = d.ContractTypeSelector,
        ContractNumber = d.ContractNumber,
        ContractDate = d.ContractDate,
        InclusionText = d.InclusionText,
        AmountNumeric = d.AmountNumeric,
        AmountWords = d.AmountWords,
        Currency = d.Currency,
        Amount2Numeric = d.Amount2Numeric,
        Amount2Words = d.Amount2Words,
        Currency2 = d.Currency2,
        InclusionAmountNumeric = d.InclusionAmountNumeric,
        InclusionAmountWords = d.InclusionAmountWords,
        InclusionCurrency = d.InclusionCurrency,
        Court = d.Court,
        Applicant = d.Applicant,
        Lawyer = d.Lawyer,
        FileNumber = d.FileNumber,
        FileType = d.FileType,
        FileYear = d.FileYear,
        FileIncoming = d.FileIncoming,
        FileIncomingDate = d.FileIncomingDate,
        UnderFilingNumber = d.UnderFilingNumber,
        FileRegistrationDate = d.RegistrationDate?.Date,
        BranchName = d.BranchName,
        AdministrativeBranchName = d.Branch?.Name,
        ExecStatus = d.ExecStatus,
        ExecSubStatus = d.ExecSubStatus,
        CollectedAmount = d.CollectedAmount,
        BaraetNumber = d.BaraetNumber,
        BaraetDate = d.BaraetDate,
        BaraetRegNumber = d.BaraetRegNumber,
        BaraetRegDate = d.BaraetRegDate,
        TarithNumber = d.TarithNumber,
        TarithDate = d.TarithDate,
        TarithRegNumber = d.TarithRegNumber,
        TarithRegDate = d.TarithRegDate,
        SeizureDate = d.SeizureDate,
        ImmediateActions = d.ImmediateActions,
        Notes = d.Notes,
        ViewCount = d.ViewCount,
        PrintCount = d.PrintCount,
        CreatedByName = d.CreatedBy?.FullName,
        DeletedAt = d.DeletedAt,
        Guarantors = d.Guarantors
            .OrderBy(g => g.GuarantorNumber)
            .Select(g => new GuarantorDto(g.Id, g.GuarantorNumber, g.GuarantorName, g.GuarantorFather,
                g.GuarantorFamily, g.GuarantorMother, g.GuarantorBirth, g.GuarantorRegister,
                g.GuarantorNationalId, g.GuarantorAddress, g.AddressType))
            .ToList(),
        RealEstates = d.RealEstates
            .Select(r => new RealEstateDto(r.Id, r.Owner, r.Property, r.PropertyNumber,
                r.PropertyDistrict, r.LandRegistry, r.ShareType))
            .ToList(),
        ExecutionActions = d.ExecutionActions
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ExecutionActionDto(a.Id, a.Type, a.Text, a.ActionDate,
                a.ReminderDuration, a.ReminderColor, a.CreatedBy?.FullName, a.CreatedAt))
            .ToList(),
    };
}
