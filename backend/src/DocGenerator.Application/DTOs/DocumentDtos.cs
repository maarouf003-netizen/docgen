using DocGenerator.Application.Common;
using System.Text.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.DTOs;

public class DocumentResponse : IDocumentSheetFields
{
    // طھط­ظ‚ظٹظ‚ طµط±ظٹط­ ظ„ط­ظ‚ظˆظ„ ط§ظ„ظ‚ظˆط§ط¦ظ… ظپظٹ `IDocumentSheetFields`: `List<T>` ظ„ط§ ظٹط·ط§ط¨ظ‚
    // `IReadOnlyList<I>` ظپظٹ طھظˆظ‚ظٹط¹ ط§ظ„طھظ†ظپظٹط° (CS0738) ط±ط؛ظ… ط§ظ„طھط­ظˆظٹظ„ ط§ظ„ط¶ظ…ظ†ظٹ â€” ظˆظ‡ط°ظ‡
    // ط§ظ„ط£ط¹ط¶ط§ط، ط§ظ„طµط±ظٹط­ط© ط؛ظٹط± ظ…ط±ط¦ظٹط© ظ„ظ„طھط³ظ„ط³ظ„ `JSON` ظپظ„ط§ ظٹطھط؛ظٹط± ط¹ظ‚ط¯ ط§ظ„ظˆط§ط¬ظ‡ط©.
    IReadOnlyList<IExportApplicant> IDocumentSheetFields.ExecutionApplicants => ExecutionApplicants;
    IReadOnlyList<IExportPublicEntity> IDocumentSheetFields.ExecutedPublicEntities => ExecutedPublicEntities;
    IReadOnlyList<IExportNaturalPerson> IDocumentSheetFields.ExecutedNaturalPersons => ExecutedNaturalPersons;
    IReadOnlyList<IExportAction> IDocumentSheetFields.ExecutionActions => ExecutionActions;

    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    /// <summary>ط¹ط¯ظ‘ط§ط¯ ط§ظ„طھط²ط§ظ…ظ† ط§ظ„ظ…طھظپط§ط¦ظ„ (RF-010) â€” ظٹظڈط¹ط§ط¯ ط¥ط±ط³ط§ظ„ظ‡ ظپظٹ `Version` ط¹ظ†ط¯ ط§ظ„ط­ظپط¸.</summary>
    public long Version { get; set; }
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
    /// <summary>ط§ظ„ظ…ظ…ط«ظ„ ط§ظ„ط´ط±ط¹ظٹ ظ„ظ„ظ…ظ‚طھط±ط¶ (ظˆظ„ظٹ/ظˆطµظٹ/ظ‚ظٹظ…) ط¥ظ† ظˆظڈط¬ط¯: ط§ط³ظ…ظ‡ ط§ظ„ط«ظ„ط§ط«ظٹ ظˆطµظپطھظ‡ ظˆط¹ظ†ظˆط§ظ†ظ‡.</summary>
    public string? BorrowerRepresentativeName { get; set; }
    public string? BorrowerRepresentativeFather { get; set; }
    public string? BorrowerRepresentativeFamily { get; set; }
    public string? BorrowerRepresentativeCapacity { get; set; }
    public string? BorrowerRepresentativeAddressType { get; set; }
    public string? BorrowerRepresentativeAddress { get; set; }
    /// <summary>ط·ط¨ظٹط¹ط© ط§ظ„ظ…ظ‚طھط±ط¶/ط§ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡: ط´ط®طµ ط·ط¨ظٹط¹ظٹ (natural) ط£ظˆ ط´ط®طµ ط§ط¹طھط¨ط§ط±ظٹ (legal).</summary>
    public string BorrowerNature { get; set; } = PartyNatureCatalog.Natural;
    /// <summary>ط±ظ‚ظ… طھط³ط¬ظٹظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© ط§ظ„ط§ط¹طھط¨ط§ط±ظٹط© (ط§ط®طھظٹط§ط±ظٹ).</summary>
    public string? BorrowerRegistrationNumber { get; set; }
    /// <summary>ظ…ظ† ظٹظ…ط«ظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© ط§ظ„ط§ط¹طھط¨ط§ط±ظٹط© (ط§ط®طھظٹط§ط±ظٹ).</summary>
    public string? BorrowerRepresentedBy { get; set; }
    public string? ContractType { get; set; }
    public string? ContractTypeSelector { get; set; }
    public string? ContractNumber { get; set; }
    public string? ContractDate { get; set; }
    public string? AnnexType { get; set; }
    public string? AnnexNumber { get; set; }
    public string? AnnexDate { get; set; }
    public string? InclusionText { get; set; }
    /// <summary>ملاحظات السند التنفيذي (القرارات الأخرى) تحت «المتضمن» — نص حر اختياري.</summary>
    public string? InclusionNotes { get; set; }
    public decimal AmountNumeric { get; set; }
    public string? AmountWords { get; set; }
    public string? Currency { get; set; }
    public decimal Amount2Numeric { get; set; }
    public string? Amount2Words { get; set; }
    public string? Currency2 { get; set; }
    public decimal Amount3Numeric { get; set; }
    public string? Amount3Words { get; set; }
    public string? Currency3 { get; set; }
    public decimal InclusionAmountNumeric { get; set; }
    public string? InclusionAmountWords { get; set; }
    public string? InclusionCurrency { get; set; }
    public decimal InclusionAmount2Numeric { get; set; }
    public string? InclusionAmount2Words { get; set; }
    public string? InclusionCurrency2 { get; set; }
    public decimal InclusionAmount3Numeric { get; set; }
    public string? InclusionAmount3Words { get; set; }
    public string? InclusionCurrency3 { get; set; }
    public string? Court { get; set; }
    /// <summary>ط¯ط§ط¦ط±ط© ط§ظ„طھظ†ظپظٹط° ط§ظ„ظ…ط±ط¬ط¹ظٹط© (ط³ط¬ظ„ ط§ظ„ط¯ظˆط§ط¦ط±) â€” طھظڈط¹ط±ط¶ ظ†طµظ‹ط§ ط¹ط¨ط± Court ظ„ظ„طھظˆط§ظپظ‚.</summary>
    public int? ExecutionCircuitId { get; set; }
    /// <summary>ط´ط¹ط¨ط© ط§ظ„ط¯ط§ط¦ط±ط© ط§ظ„ظ…ط§ظ„ظƒط© (`null` = ظ…ظ„ظƒ ط§ظ„ظ‚ط³ظ…) â€” ظ„ظ„ظ†ط·ط§ظ‚ ظˆط§ظ„ط¹ط±ط¶.</summary>
    public int? SectionId { get; set; }
    /// <summary>ط§ط³ظ… ط§ظ„ط´ط¹ط¨ط© ط§ظ„ظ…ط§ظ„ظƒط© â€” ظ„ظ„ط¹ط±ط¶ ظپظ‚ط·.</summary>
    public string? SectionName { get; set; }
    /// <summary>ط¹ظ„ظ… آ«ط¨ط§ظ†طھط¸ط§ط± ط¥ط¹ط§ط¯ط© ط§ظ„ظ‚ظٹط¯آ» â€” ط¨ط§ظ†طھط¸ط§ط± ط§ظ„ظ…ط­ط§ظ…ظٹ ظ„ط¥ط¯ط®ط§ظ„ ط§ظ„ط±ظ‚ظ… ط§ظ„ط¬ط¯ظٹط¯.</summary>
    public bool NeedsRegistration { get; set; }
    public string? Applicant { get; set; }
    /// <summary>ظ†ط³ط®ط© طھط³ط±ظٹط¹: ظ…ط¹ط±ظ‘ظپ ظ‚ظٹط¯ ط£ظˆظ„ ط¬ظ‡ط© ط·ط§ظ„ط¨ ظ…ط±طھط¨ط·ط© ط¨ط§ظ„ط³ط¬ظ„ (ظ„ظ„ظپظ„طھط±ط© ظپظٹ ط§ظ„ط¨ظˆط§ط¨ط©).</summary>
    public int? ApplicantRegistryId { get; set; }
    public string? Lawyer { get; set; }
    public string? ReferredFromLawyer { get; set; }
    public DateTime? ReferredAt { get; set; }
    public string? FileNumber { get; set; }
    /// <summary>
    /// ط§ظ„ط±ظ‚ظ… ط§ظ„ط¸ط§ظ‡ط± ظ„ظ„ظ…ط³طھط®ط¯ظ…: ط¢ط®ط± ط±ظ‚ظ… ط£ط³ط§ط³ â‰¤ ط³ظ†ط© ط§ظ„ظٹظˆظ… (ط¥ظ† ظˆظڈط¬ط¯)طŒ ظˆط¥ظ„ط§ ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ ط§ظ„ط£طµظ„ظٹ.
    /// (ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ ط§ظ„ط£طµظ„ظٹ ظٹط¨ظ‚ظ‰ ظپظٹ FileNumber ظ„ظ„طھط­ط±ظٹط± ظˆط§ظ„ط³ط¬ظ„ ط§ظ„طھط§ط±ظٹط®ظٹ.)
    /// </summary>
    public string? DisplayFileNumber { get; set; }
    /// <summary>ط³ظ†ط© ط§ظ„ط±ظ‚ظ… ط§ظ„ظپط¹ظ‘ط§ظ„ ط§ظ„ظ…ط±ط§ظپظ‚ط© ظ„ظ€ DisplayFileNumber â€” ظٹظڈط­ط³ط¨ط§ظ† ظ…ط¹ظ‹ط§ ظ…ظ† ط§ظ„ط³ط¬ظ„ ظ†ظپط³ظ‡طŒ ظپظ„ط§ ظٹظڈط¹ط±ط¶ ط£ط­ط¯ظ‡ظ…ط§ ظ…ط¹ ط³ظ†ط©/ط±ظ‚ظ… ط§ظ„ط²ظˆط¬ ط§ظ„ط¢ط®ط±.</summary>
    public string? DisplayFileYear { get; set; }
    public string? FileType { get; set; }
    public string? FileYear { get; set; }
    public string? FileIncoming { get; set; }
    public string? FileIncomingDate { get; set; }
    public string? UnderFilingNumber { get; set; }
    public string? FileRegistrationDate { get; set; }
    public string? BranchName { get; set; }
    /// <summary>ط§ط³ظ… ط§ظ„ظپط±ط¹ ط§ظ„ط¥ط¯ط§ط±ظٹ ظ„ظ„ظ…ظ„ظپ (ظپط±ط¹ ط§ظ„ظ…ط³طھط®ط¯ظ… ط§ظ„ط°ظٹ ط£ط¯ط®ظ„ظ‡) ط¹ط¨ط± BranchId.</summary>
    public string? AdministrativeBranchName { get; set; }
    /// <summary>ط­ط§ظ„ط© ط§ظ„ط¹ط±ط¶ ط§ظ„ظ…ظˆط­ط¯ط© ط§ظ„ظ…ط´طھظ‚ط© ظ…ظ† ط§ظ„ط®ظ„ظپظٹط© (ط§ظ„ظ…طµط¯ط± ط§ظ„ظˆط­ظٹط¯ ظ„ظ„ظ‚ظˆط§ط¹ط¯) â€” طھط¹ط±ط¶ظ‡ط§ ط§ظ„ظˆط§ط¬ظ‡ط© ظƒظ…ط§ ظ‡ظٹ.</summary>
    public string DisplayStatus { get; set; } = string.Empty;

    public string? ExecStatus { get; set; }
    public string? ExecSubStatus { get; set; }
    /// <summary>ط§ظ„ظ…ط¨ط§ظ„ط؛ ط§ظ„ظ…ط­طµظژظ‘ظ„ط© (ط­طھظ‰ ط«ظ„ط§ط«ط© ط¨ط¹ظ…ظ„ط§طھظ‡ط§) ظپظٹ آ«ظ…ظ†ظپط° ط¨ط§ظ„طھط³ظˆظٹط©آ»/آ«ظ…ظ†ظپط° ط¬ط¨ط±ظٹط§آ».</summary>
    public decimal? CollectedAmount { get; set; }
    public decimal? CollectedAmount2 { get; set; }
    public decimal? CollectedAmount3 { get; set; }
    public string? CollectedCurrency { get; set; }
    public string? CollectedCurrency2 { get; set; }
    public string? CollectedCurrency3 { get; set; }
    /// <summary>طµظپط© ط§ظ„ظ…ظ„ظپ (GeneralEntitySideCatalog): applicant = ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°طŒ executed = ظ…ظ†ظپط° ط¹ظ„ظٹظ‡طŒ deposit = ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹.</summary>
    public string? GeneralEntitySide { get; set; }
    /// <summary>ط§ظ„طھط³ظ…ظٹط© ط§ظ„ط¹ط±ط¨ظٹط© ظ„ظ„طµظپط©.</summary>
    public string? GeneralEntitySideLabel { get; set; }
    public string? ExecutedStatus { get; set; }
    public string? ExecutedDescription { get; set; }
    public DateTime? FileReceiptDate { get; set; }
    /// <summary>ط±ظ‚ظ… ظˆط±ظˆط¯ ط§ظ„ط¥ط®ط·ط§ط± ط§ظ„طھظ†ظپظٹط°ظٹ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ».</summary>
    public string? FileReceiptNumber { get; set; }
    public decimal? ExecutedRequiredAmount { get; set; }
    public string? ExecutedRequiredCurrency { get; set; }
    public decimal? ExecutedRequiredAmount2 { get; set; }
    public string? ExecutedRequiredCurrency2 { get; set; }
    public decimal? ExecutedRequiredAmount3 { get; set; }
    public string? ExecutedRequiredCurrency3 { get; set; }
    public decimal? ExecutedPaidAmount { get; set; }
    public string? ExecutedPaidCurrency { get; set; }
    public decimal? ExecutedPaidAmount2 { get; set; }
    public string? ExecutedPaidCurrency2 { get; set; }
    public decimal? ExecutedPaidAmount3 { get; set; }
    public string? ExecutedPaidCurrency3 { get; set; }
    /// <summary>طھط§ط±ظٹط® ط§ظٹط¯ط§ط¹ظ‡ ط­ط³ط§ط¨ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظپظٹ ظˆط¶ط¹ آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ».</summary>
    public DateTime? ExecutedDepositDate { get; set; }
    /// <summary>طھط§ط±ظٹط® ط§ظ„طھظ†ظپظٹط° ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ».</summary>
    public DateTime? ExecutedExecutionDate { get; set; }
    /// <summary>طھط§ط±ظٹط® ظ‚ط±ط§ط± ط§ظ„ط¥ط­ط§ظ„ط© ط§ظ„ظ‚ط·ط¹ظٹط© ظپظٹ آ«ظ…ظ†ظپط° ط¬ط¨ط±ظٹط§آ» (ظ†طµ ط­ط±).</summary>
    public string? ForcedExecutionDate { get; set; }
    /// <summary>طھط§ط±ظٹط® طھط­ظˆظٹظ„ ط¨ط¯ظ„ ط§ظ„ظ…ط¨ظٹط¹ ظ„ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط¹ظ†ط¯ آ«ط§ط¹طھط¨ط§ط± ط§ظ„ظ…ظ„ظپ ظ…ظ†ظپط°ظ‹ط§ ظƒط§ظ…ظ„ظ‹ط§ ط¨ظ‡ط°ط§ ط§ظ„ط¨ظٹط¹آ» â€” ظ†طµ ط­ط± ظٹظڈط¹ط±ظژط¶ آ«yyyy-MM-ddآ» (ظ…ط«ظ„ ط¨ظ‚ظٹط© ط­ظ‚ظˆظ„ ط§ظ„طھط®ط²ظٹظ† ط§ظ„ط²ظ…ظ†ظٹ ط§ظ„ط­ط±).</summary>
    public string? ForcibleTransferDate { get; set; }
    /// <summary>ط±ظ‚ظ… ط§ظ„ط¥ط´ط¹ط§ط± (ط§ط®طھظٹط§ط±ظٹ) ط¹ظ†ط¯ طھط­ظˆظٹظ„ ط¨ط¯ظ„ ط§ظ„ظ…ط¨ظٹط¹ ظ„ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط©.</summary>
    public string? ForcibleTransferNoticeNumber { get; set; }
    public DateTime? StruckOffDate { get; set; }
    /// <summary>ط±ظ‚ظ… ظˆط±ظˆط¯ ط§ط®ط·ط§ط± ط§ظ„طھط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ظ…ظ„ظپ ظ…ط´ط·ظˆط¨ ط¥ظ„ظ‰ ط§ظ„ظ…طھط¯ط§ظˆظ„ (ط§ط®طھظٹط§ط±ظٹ).</summary>
    public string? RenewalFileReceiptNumber { get; set; }
    /// <summary>طھط§ط±ظٹط® ظˆط±ظˆط¯ ط§ط®ط·ط§ط± ط§ظ„طھط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط§ط®طھظٹط§ط±ظٹ).</summary>
    public DateTime? RenewalFileReceiptDate { get; set; }
    /// <summary>ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ ط§ظ„ط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط¥ظ„ط²ط§ظ…ظٹ) â€” ظٹط¹ظˆط¯ ط§ظ„ظ…ظ„ظپ ط¨ظ‡ ظ„ط³ظ†ط© ط§ظ„ط¥ط¹ط§ط¯ط©.</summary>
    public string? RenewalFileNumber { get; set; }
    /// <summary>ظ†ظˆط¹ ط§ظ„ظ…ظ„ظپ ط§ظ„ط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط§ط®طھظٹط§ط±ظٹ).</summary>
    public string? RenewalFileType { get; set; }
    /// <summary>طھط§ط±ظٹط® ط§ظ„طھط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط§ط®طھظٹط§ط±ظٹ).</summary>
    public DateTime? RenewalDate { get; set; }
    /// <summary>ط±ظ‚ظ… ظƒطھط§ط¨ ط§ظ„ظ…ط·ط§ظ„ط¹ط© ط¨ط¹ط¯ظ… ظˆط¬ظˆط¯ ط£ظ…ظˆط§ظ„ ظ„ظ„طھظ†ظپظٹط° ط¹ظ„ظٹظ‡ط§ ظپظٹ آ«ظ…ط­ط§ظ„ ط§ظ„ظ‰ ط§ظ„ط¨ط¯ط§ظٹط©آ».</summary>
    public string? NoFundsDemandNumber { get; set; }
    /// <summary>طھط§ط±ظٹط® ظƒطھط§ط¨ ط§ظ„ظ…ط·ط§ظ„ط¹ط© ط¨ط¹ط¯ظ… ظˆط¬ظˆط¯ ط£ظ…ظˆط§ظ„ ظ„ظ„طھظ†ظپظٹط° ط¹ظ„ظٹظ‡ط§ â€” ظ†طµ ط­ط± ظٹظڈط¹ط±ظژط¶ آ«yyyy-MM-ddآ» (ظ…ط«ظ„ ط¨ظ‚ظٹط© ط­ظ‚ظˆظ„ ط§ظ„طھط®ط²ظٹظ† ط§ظ„ط²ظ…ظ†ظٹ ط§ظ„ط­ط±).</summary>
    public string? NoFundsDemandDate { get; set; }
    /// <summary>ط±ظ‚ظ… ظƒطھط§ط¨ ط§ظ„ط¥ط­ط§ظ„ط© ظپظٹ آ«ظ…ط­ط§ظ„ ط§ظ„ظ‰ ط§ظ„ط¨ط¯ط§ظٹط©آ».</summary>
    public string? StartReferralNumber { get; set; }
    /// <summary>طھط§ط±ظٹط® ظƒطھط§ط¨ ط§ظ„ط¥ط­ط§ظ„ط© â€” ظ†طµ ط­ط± ظٹظڈط¹ط±ظژط¶ آ«yyyy-MM-ddآ» (ظ…ط«ظ„ ط¨ظ‚ظٹط© ط­ظ‚ظˆظ„ ط§ظ„طھط®ط²ظٹظ† ط§ظ„ط²ظ…ظ†ظٹ ط§ظ„ط­ط±).</summary>
    public string? StartReferralDate { get; set; }
    public string? BaraetNumber { get; set; }
    public string? BaraetDate { get; set; }
    public string? BaraetRegNumber { get; set; }
    public string? BaraetRegDate { get; set; }
    public string? TarithNumber { get; set; }
    public string? TarithDate { get; set; }
    public string? TarithRegNumber { get; set; }
    public string? TarithRegDate { get; set; }
    /// <summary>ط­ظ‚ظˆظ„ ظƒطھط§ط¨ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط¨ط§ظ„ط³ظٹط± ط¨ط§ظ„ظ…ظ„ظپ ط¹ظ†ط¯ ط§ظ„طھط±ط§ط¬ط¹ (ط±ظ‚ظ…/طھط§ط±ظٹط® ط§ظ„ظƒطھط§ط¨ + ظˆط±ظˆط¯ظ‡).</summary>
    public string? SayerNumber { get; set; }
    public string? SayerDate { get; set; }
    public string? SayerRegNumber { get; set; }
    public string? SayerRegDate { get; set; }
    /// <summary>ظ…ط¹ط±ظ‘ظپط§طھ ط§ظ„ط£ظ…ظˆط§ظ„ ط§ظ„ظ…ط¨ط§ط¹ط© ط¨ط§ظ„ظ…ط²ط§ط¯ ط§ظ„ط¹ظ„ظ†ظٹ ظپظٹ آ«ظ…ظ†ظپط° ط¬ط¨ط±ظٹط§آ» (ظ…ظ† ط£ظ…ظˆط§ظ„ ط§ظ„ظ…ظ„ظپطŒ ط¹ط¯ط§ ظƒظپط§ظ„ط© ط§ظ„ط±ظˆط§طھط¨).</summary>
    public List<int> SoldAssetIds { get; set; } = new();
    public string? SeizureDate { get; set; }
    public string? ImmediateActions { get; set; }
    public string? Notes { get; set; }
    public int ViewCount { get; set; }
    public int PrintCount { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? DeletedAt { get; set; }
    /// <summary>
    /// طµط­ظٹط­ ط¥ط°ط§ ظƒط§ظ† ط§ظ„ظ…ظ„ظپ ط¨ط­ط§ط¬ط© ط¥ظ„ظ‰ طھط¯ظˆظٹط± ظ‡ط°ط§ ط§ظ„ط¹ط§ظ…: ظ…ظ‚ظٹط¯ ط¨ط±ظ‚ظ… ظ…ظ„ظپ (ظ„ظٹط³ طھط­طھ ط±ظپط¹)طŒ
    /// ط؛ظٹط± ظ…ظ†ظپظژظ‘ط°طŒ ظ„ط¯ظٹظ‡ ط±ظ‚ظ… ط£ط³ط§ط³ ظ„ط³ظ†ط© ط³ط§ط¨ظ‚ط©طŒ ظˆظ„ط§ ظٹط­ظ…ظ„ ط±ظ‚ظ… ط£ط³ط§ط³ ظ„ظ„ط³ظ†ط© ط§ظ„ط­ط§ظ„ظٹط© â€”
    /// ظپظٹط¸ظ‡ط± ط±ظ‚ظ… ظ…ظ„ظپظ‡ ط¨ط§ظ„ط£ط­ظ…ط± ظپظٹ آ«ط§ظ„ظ…ظ„ظپط§طھ ط§ظ„طھظ†ظپظٹط°ظٹط©آ».
    /// </summary>
    public bool NeedsRotation { get; set; }
    /// <summary>
    /// طµط­ظٹط­ ط¥ط°ط§ ظƒط§ظ† ط¹ظ„ظ‰ ط§ظ„ظ…ظ„ظپ ط§ط³طھط¦ظ†ط§ظپ ظ…ظ†ط¸ظˆط± ظˆط§ط­ط¯ ط¹ظ„ظ‰ ط§ظ„ط£ظ‚ظ„ â€” طھظڈط¹ط±ط¶ ط¨ط¬ط§ظ†ط¨ ظ†طھظٹط¬ط© ط§ظ„ط¨ط­ط«
    /// ط´ط§ط±ط© آ«ط§ط³طھط¦ظ†ط§ظپآ»طŒ ظˆظٹظپطھط­ ط§ظ„ظ†ظ‚ط± ط¹ظ„ظٹظ‡ط§ طھظپط§طµظٹظ„ ط§ظ„ط§ط³طھط¦ظ†ط§ظپ (MatchedAppealId).
    /// ط§ظ„ظ…ط­ط³ظˆظ… ظˆط§ظ„ظ…ط´ط·ظˆط¨ ظ„ط§ ظٹظڈط¸ظ‡ط±ط§ظ† ط§ظ„ط´ط§ط±ط©.
    /// </summary>
    public bool HasAppeals { get; set; }
    /// <summary>ظ…ط¹ط±ظپ ط£ظˆظ„ ط§ط³طھط¦ظ†ط§ظپ ظ…ظ†ط¸ظˆط± ط¹ظ„ظ‰ ط§ظ„ظ…ظ„ظپ ط¹ظ†ط¯ ظˆط¬ظˆط¯ظ‡ (ظ„ظ„ط§ظ†طھظ‚ط§ظ„ ط¥ظ„ظ‰ طھظپط§طµظٹظ„ ط§ظ„ط§ط³طھط¦ظ†ط§ظپ).</summary>
    public int? MatchedAppealId { get; set; }
    /// <summary>
    /// ظ…ط¹ط±ظپ ط§ظ„ط¥ظ†ط§ط¨ط© ط§ظ„طھظٹ طµط¯ط± ط¹ظ†ظ‡ط§ ظ‡ط°ط§ ط§ظ„ظ…ظ„ظپ ظƒظ…ظ†ط§ط¨ (ط؛ظٹط± ظپط§ط±ط؛ â‡’ ط§ظ„ظ…ظ„ظپ آ«ظ…ط±ط¢ط©آ» ظ„ظ„ظ…ظ†ظٹط¨):
    /// طھظڈظ‚ظپظژظ„ ط­ظ‚ظˆظ„ ط§ظ„ظ…ط²ط§ظ…ظ†ط© ط£ظ…ط§ظ…ظٹظ‹ط§ ظˆط®ظ„ظپظٹظ‹ط§طŒ ظˆطھظڈط­ظپط¸ ط§ظ„ط¥ط¶ط§ظپط§طھ ط§ظ„ظ…ط­ظ„ظٹط© (ظˆط±ظٹط«/ظ…ظ…ط«ظ„) ظ…ط¹ طھظ†ط¨ظٹظ‡ ظ„ظ„ظ…ظ†ظٹط¨.
    /// </summary>
    public int? SourceDelegationId { get; set; }
    public List<GuarantorDto> Guarantors { get; set; } = new();
    public List<AssetDto> Assets { get; set; } = new();
    /// <summary>ظˆط±ط«ط© ط§ظ„ظ…ظ‚طھط±ط¶ ط§ظ„ظ…طھظˆظپظ‰ (ط¥ظ† ظˆظڈط¬ط¯ظˆط§).</summary>
    public List<HeirDto> BorrowerHeirs { get; set; } = new();
    public List<ExecutionActionDto> ExecutionActions { get; set; } = new();
    public List<ExecutionApplicantDto> ExecutionApplicants { get; set; } = new();
    public List<ExecutedPublicEntityDto> ExecutedPublicEntities { get; set; } = new();
    public List<ExecutedNaturalPersonDto> ExecutedNaturalPersons { get; set; } = new();
    /// <summary>ط§ظ„ط¬ظ‡ط§طھ ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© ط§ظ„طھظ†ظپظٹط° ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ظˆط§ط­ط¯ط© ط£ظˆ ط£ظƒط«ط±).</summary>
    public List<ApplicantPublicEntityDto> ApplicantPublicEntities { get; set; } = new();
    /// <summary>ط³ط¬ظ„ طھط¹ط§ظ‚ط¨ ط§ظ„ظ…ط­ط§ظ…ظٹظ† ط¹ظ„ظ‰ ط§ظ„ظ…ظ„ظپ (ظ…ظ†ط´ط¦ + ظƒظ„ ط§ظ„ظ…ط­ط§ظ…ظٹظ† ط§ظ„ظ…طھط¹ط§ظ‚ط¨ظٹظ† ظ…ط¹ طھظˆط§ط±ظٹط® ط§ظ„ط¥ط­ط§ظ„ط©).</summary>
    public List<DocumentAssignmentDto> Assignments { get; set; } = new();
    /// <summary>ط±ظ‚ظ… ظˆط±ظˆط¯ ط§ظ„ظ…ظ„ظپ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ط§ط®طھظٹط§ط±ظٹ).</summary>
    public string? FileArrivalNumber { get; set; }
    /// <summary>طھط§ط±ظٹط® ظˆط±ظˆط¯ ط§ظ„ظ…ظ„ظپ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ظ†طµ ط­ط±).</summary>
    public string? FileArrivalDate { get; set; }

    /// <summary>
    /// آ«ظˆظ‚ظˆط¹ط§طھ ط§ظ„ظ…ظ„ظپآ» ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ»/آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ»: ط³ط¬ظ„ ط²ظ…ظ†ظٹ ظ„ظƒظ„ ط´ط·ط¨ ظˆطھط¬ط¯ظٹط¯طŒ
    /// ظ…ط±طھط¨ طھطµط§ط¹ط¯ظٹظ‹ط§ ط­ط³ط¨ طھط§ط±ظٹط® ط§ظ„ظˆظ‚ط¹ط© (ط³ط±ط¯ ط²ظ…ظ†ظٹ ظ…ظ† ط§ظ„ط£ظ‚ط¯ظ… ط¥ظ„ظ‰ ط§ظ„ط£ط­ط¯ط«).
    /// </summary>
    public List<DocumentOccurrenceDto> Occurrences { get; set; } = new();

    public static DocumentResponse FromEntity(Document d, int currentYear) => new()
    {

            DisplayStatus = DocumentStatusResolver.Resolve(d),
        Id = d.Id,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
        Version = d.Version,
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
        BorrowerRepresentativeName = d.BorrowerRepresentativeName,
        BorrowerRepresentativeFather = d.BorrowerRepresentativeFather,
        BorrowerRepresentativeFamily = d.BorrowerRepresentativeFamily,
        BorrowerRepresentativeCapacity = d.BorrowerRepresentativeCapacity,
        BorrowerRepresentativeAddressType = d.BorrowerRepresentativeAddressType,
        BorrowerRepresentativeAddress = d.BorrowerRepresentativeAddress,
        BorrowerNature = d.BorrowerNature,
        BorrowerRegistrationNumber = d.BorrowerRegistrationNumber,
        BorrowerRepresentedBy = d.BorrowerRepresentedBy,
        ContractType = d.ContractType,
        ContractTypeSelector = d.ContractTypeSelector,
        ContractNumber = d.ContractNumber,
        ContractDate = d.ContractDate,
        AnnexType = d.AnnexType,
        AnnexNumber = d.AnnexNumber,
        AnnexDate = d.AnnexDate,
        InclusionText = d.InclusionText,
        InclusionNotes = d.InclusionNotes,
        AmountNumeric = d.AmountNumeric,
        AmountWords = d.AmountWords,
        Currency = d.Currency,
        Amount2Numeric = d.Amount2Numeric,
        Amount2Words = d.Amount2Words,
        Currency2 = d.Currency2,
        Amount3Numeric = d.Amount3Numeric,
        Amount3Words = d.Amount3Words,
        Currency3 = d.Currency3,
        InclusionAmountNumeric = d.InclusionAmountNumeric,
        InclusionAmountWords = d.InclusionAmountWords,
        InclusionCurrency = d.InclusionCurrency,
        InclusionAmount2Numeric = d.InclusionAmount2Numeric,
        InclusionAmount2Words = d.InclusionAmount2Words,
        InclusionCurrency2 = d.InclusionCurrency2,
        InclusionAmount3Numeric = d.InclusionAmount3Numeric,
        InclusionAmount3Words = d.InclusionAmount3Words,
        InclusionCurrency3 = d.InclusionCurrency3,
        Court = d.Court,
        ExecutionCircuitId = d.ExecutionCircuitId,
        SectionId = d.ExecutionCircuit?.SectionId,
        SectionName = d.ExecutionCircuit?.Section?.Name,
        NeedsRegistration = d.NeedsRegistration,
        Applicant = d.Applicant,
        ApplicantRegistryId = d.ApplicantRegistryId,
        Lawyer = d.Lawyer,
        ReferredFromLawyer = d.ReferredFromLawyer,
        ReferredAt = d.ReferredAt,
        FileNumber = d.FileNumber,
        DisplayFileNumber = EffectiveFileIdentity.Number(d, currentYear),
        DisplayFileYear = EffectiveFileIdentity.Year(d, currentYear),
        FileType = d.FileType,
        FileYear = d.FileYear,
        FileIncoming = d.FileIncoming,
        FileIncomingDate = d.FileIncomingDate,
        UnderFilingNumber = d.UnderFilingNumber,
        FileArrivalNumber = d.FileArrivalNumber,
        FileArrivalDate = d.FileArrivalDate,
        FileRegistrationDate = d.RegistrationDate?.Date,
        BranchName = d.BranchName,
        AdministrativeBranchName = d.Branch?.Name,
        ExecStatus = d.ExecStatus,
        ExecSubStatus = d.ExecSubStatus,
        CollectedAmount = d.CollectedAmount,
        CollectedAmount2 = d.CollectedAmount2,
        CollectedAmount3 = d.CollectedAmount3,
        CollectedCurrency = d.CollectedCurrency,
        CollectedCurrency2 = d.CollectedCurrency2,
        CollectedCurrency3 = d.CollectedCurrency3,
        GeneralEntitySide = d.GeneralEntitySide,
        GeneralEntitySideLabel = GeneralEntitySideCatalog.ToLabel(d.GeneralEntitySide),
        ExecutedStatus = d.ExecutedStatus,
        ExecutedDescription = d.ExecutedDescription,
        FileReceiptDate = d.FileReceiptDate,
        FileReceiptNumber = d.FileReceiptNumber,
        ExecutedRequiredAmount = d.ExecutedRequiredAmount,
        ExecutedRequiredCurrency = d.ExecutedRequiredCurrency,
        ExecutedRequiredAmount2 = d.ExecutedRequiredAmount2,
        ExecutedRequiredCurrency2 = d.ExecutedRequiredCurrency2,
        ExecutedRequiredAmount3 = d.ExecutedRequiredAmount3,
        ExecutedRequiredCurrency3 = d.ExecutedRequiredCurrency3,
        ExecutedPaidAmount = d.ExecutedPaidAmount,
        ExecutedPaidCurrency = d.ExecutedPaidCurrency,
        ExecutedPaidAmount2 = d.ExecutedPaidAmount2,
        ExecutedPaidCurrency2 = d.ExecutedPaidCurrency2,
        ExecutedPaidAmount3 = d.ExecutedPaidAmount3,
        ExecutedPaidCurrency3 = d.ExecutedPaidCurrency3,
        ExecutedDepositDate = d.ExecutedDepositDate,
        ExecutedExecutionDate = d.ExecutedExecutionDate,
        StruckOffDate = d.StruckOffDate,
        RenewalFileReceiptNumber = d.RenewalFileReceiptNumber,
        RenewalFileReceiptDate = d.RenewalFileReceiptDate,
        RenewalFileNumber = d.RenewalFileNumber,
        RenewalFileType = d.RenewalFileType,
        RenewalDate = d.RenewalDate,
        NoFundsDemandNumber = d.NoFundsDemandNumber,
        NoFundsDemandDate = d.NoFundsDemandDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        StartReferralNumber = d.StartReferralNumber,
        StartReferralDate = d.StartReferralDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        BaraetNumber = d.BaraetNumber,
        BaraetDate = d.BaraetDate,
        ForcedExecutionDate = d.ForcedExecutionDate,
        ForcibleTransferDate = d.ForcibleTransferDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ForcibleTransferNoticeNumber = d.ForcibleTransferNoticeNumber,
        BaraetRegNumber = d.BaraetRegNumber,
        BaraetRegDate = d.BaraetRegDate,
        TarithNumber = d.TarithNumber,
        TarithDate = d.TarithDate,
        TarithRegNumber = d.TarithRegNumber,
        TarithRegDate = d.TarithRegDate,
        SayerNumber = d.SayerNumber,
        SayerDate = d.SayerDate,
        SayerRegNumber = d.SayerRegNumber,
        SayerRegDate = d.SayerRegDate,
        SoldAssetIds = ParseSoldAssetIds(d.SoldAssetIds),
        SeizureDate = d.SeizureDate,
        ImmediateActions = d.ImmediateActions,
        Notes = d.Notes,
        ViewCount = d.ViewCount,
        PrintCount = d.PrintCount,
        CreatedByName = d.CreatedBy?.FullName,
        DeletedAt = d.DeletedAt,
        SourceDelegationId = d.SourceDelegationId,
        NeedsRotation = NeedsRotationOf(d, currentYear),
        Guarantors = d.Guarantors
            .OrderBy(g => g.GuarantorNumber)
            .Select(g => new GuarantorDto(g.Id, g.GuarantorNumber, g.GuarantorName, g.GuarantorFather,
                g.GuarantorFamily, g.GuarantorMother, g.GuarantorBirth, g.GuarantorRegister,
                g.GuarantorNationalId, g.GuarantorAddress, g.AddressType,
                g.RepresentativeName, g.RepresentativeFather, g.RepresentativeFamily,
                g.RepresentativeCapacity, g.RepresentativeAddressType, g.RepresentativeAddress,
                ToHeirDtos(d.Heirs, g.GuarantorNumber),
                g.GuarantorNature, g.GuarantorRegistrationNumber, g.GuarantorRepresentedBy))
            .ToList(),
        Assets = d.Assets
            .Select(a => new AssetDto(a.Id, a.AssetKind,
                a.Owners.OrderBy(o => o.Order).Select(o => o.Name).ToList(),
                a.ShareType,
                a.Property, a.PropertyNumber, a.PropertyDistrict, a.LandRegistry,
                a.VehicleType, a.VehicleClass, a.PlateNumber, a.VehicleGovernorate,
                a.RegisterNumber, a.RegistrationDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), a.ShopGovernorate, a.ShopDescription, a.ShopLocation,
                a.PublicEntity,
                a.LicenseNumber, a.LicenseDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), a.LicenseIssuer,
                a.Notes,
                a.SeizureDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)))
            .ToList(),
        BorrowerHeirs = ToHeirDtos(d.Heirs, null),
        ExecutionActions = d.ExecutionActions
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ExecutionActionDto(a.Id, a.Type, a.Text, a.ActionDate,
                a.ReminderDuration, a.ReminderColor, a.CreatedBy?.FullName, a.CreatedAt))
            .ToList(),
        ExecutionApplicants = d.ExecutionApplicants
            .Select(a => new ExecutionApplicantDto(a.Id, a.Name, a.Father, a.Family,
                a.LegalRepresentative, a.RepresentationType,
                a.DeceasedName, a.DeceasedFather, a.DeceasedFamily,
                a.RepresentativeName, a.RepresentativeFather, a.RepresentativeFamily,
                a.RepresentativeCapacity, a.RepresentativeLegalRepresentative,
                ToExecutedHeirDtos(d.ExecutedHeirs, a.Id, null),
                a.ApplicantNature, a.ApplicantRegistrationNumber, a.ApplicantRepresentedBy,
                a.ApplicantAddressType, a.ApplicantAddress,
                a.RegistryId))
            .ToList(),
        ExecutedPublicEntities = d.ExecutedPublicEntities
            .Select(e => new ExecutedPublicEntityDto(e.Id, e.EntityName, e.EntityBranch,
                e.EntityNature, e.RegistrationNumber, e.RepresentedBy, e.AddressType, e.Address,
                e.Governorate, e.RegistryId))
            .ToList(),
        ExecutedNaturalPersons = d.ExecutedNaturalPersons
            .Select(p => new ExecutedNaturalPersonDto(p.Id, p.Name, p.Father, p.Family,
                p.AddressType, p.AddressOrRepresentative, p.RepresentationType,
                p.DeceasedName, p.DeceasedFather, p.DeceasedFamily,
                p.RepresentativeName, p.RepresentativeFather, p.RepresentativeFamily,
                p.RepresentativeCapacity, p.RepresentativeAddressType, p.RepresentativeAddress,
                ToExecutedHeirDtos(d.ExecutedHeirs, null, p.Id)))
            .ToList(),
        ApplicantPublicEntities = d.ApplicantPublicEntities
            .Select(e => new ApplicantPublicEntityDto(e.Id, e.Name, e.Branch, e.Governorate, e.RegistryId))
            .ToList(),
        Assignments = d.Assignments
            .OrderBy(a => a.AssignedAt)
            .Select(a => new DocumentAssignmentDto(a.Id, a.Kind, a.LawyerName, a.AssignedByName, a.AssignedAt))
            .ToList(),
        Occurrences = d.Occurrences
            .OrderBy(o => o.EventDate)
            .ThenBy(o => o.Id)
            .Select(ToOccurrenceDto)
            .ToList(),
    };

    private static DocumentOccurrenceDto ToOccurrenceDto(DocGenerator.Domain.Entities.DocumentOccurrence o)
    {
        var (details, detailsText) = OccurrenceDetails.Split(o.Details);
        return new DocumentOccurrenceDto(o.Id, o.OccurrenceType,
            OccurrenceTypeCatalog.ToLabel(o.OccurrenceType), o.EventDate,
            o.FileNumber, o.FileType, o.Year, o.ReceiptNumber, o.ReceiptDate,
            details, o.CreatedBy?.FullName, o.Source, detailsText,
            o.FromCircuitName, o.ToCircuitName);
    }

    /// <summary>ظپظƒظ‘ ظ‚ط§ط¦ظ…ط© ظ…ط¹ط±ظ‘ظپط§طھ ط§ظ„ط£ظ…ظˆط§ظ„ ط§ظ„ظ…ط¨ط§ط¹ط© ظ…ظ† JSON ط§ظ„ظ…ط®ط²ظ† (ط£ظˆ ظ‚ط§ط¦ظ…ط© ظپط§ط±ط؛ط© ط¹ظ†ط¯ ط§ظ„ط¹ط·ط¨).</summary>
    private static List<int> ParseSoldAssetIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<int>>(json) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    /// <summary>
    /// ظ‚ط§ط¹ط¯ط© ط§ظ„ظ„ظˆظ† ط§ظ„ط£ط­ظ…ط± ط§ظ„ظ…ط¹طھظ…ط¯ط©: ظٹظڈظ„ظˆظژظ‘ظ† ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ ظپظ‚ط· ط¥ط°ط§ ظƒط§ظ† ط§ظ„ظ…ظ„ظپ ظ…ظ‚ظٹط¯ظ‹ط§
    /// (ظ„ظٹط³ طھط­طھ ط±ظپط¹)طŒ ط؛ظٹط± ظ…ظ†ظپظژظ‘ط°طŒ ظˆط³ط¨ظ‚ ط£ظ† ط¯ظڈظˆظگظ‘ط± ظپظٹ ط³ظ†ط© ط³ط§ط¨ظ‚ط©طŒ ظˆظ„ظ… ظٹظڈط¯ظˆظژظ‘ط± ظپظٹ ط§ظ„ط³ظ†ط© ط§ظ„ط­ط§ظ„ظٹط©.
    /// ظˆظ„ط¹ط§ط¦ظ„طھظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» (Executed + Deposit) ظٹظڈط¤ط®ط° آ«ط؛ظٹط± ظ…ظ†ظپظژظ‘ط°آ» ط¨ظ…ط¹ظ†ط§ظ‡ ط§ظ„ط¹ط§ط¦ظ„ظٹ:
    /// ظ…طھط¯ط§ظˆظ„ط© ظپظ‚ط· (ظ„ط§ ظ…ظ†ظپط° ظˆظ„ط§ ظ…ط´ط·ظˆط¨) â€” ظ…ط·ط§ط¨ظ‚ظ‹ط§ ظ„ط´ط±ط· ط£ظ‡ظ„ظٹطھظ‡ظ…ط§ ظپظٹ ظ‚ط§ط¦ظ…ط© ط§ظ„طھط¯ظˆظٹط± ظˆط§ظ„ط­ظپط¸.
    /// </summary>
    private static bool NeedsRotationOf(Document d, int currentYear)
    {
        if (GeneralEntitySideCatalog.IsExecutedLike(d.GeneralEntitySide))
            return !d.IsDraft
                && d.ExecutedStatus == ExecutedStatusCatalog.None
                && d.BaseNumbers.Any(b => b.Year < currentYear)
                && !d.BaseNumbers.Any(b => b.Year == currentYear);
        return !d.IsDraft
            && !ExecutionStatusCatalog.IsExecuted(d.ExecStatus, d.ExecSubStatus)
            && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff
            && d.BaseNumbers.Any(b => b.Year < currentYear)
            && !d.BaseNumbers.Any(b => b.Year == currentYear);
    }

    /// <summary>
    /// ظˆط±ط«ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ ظ…ط­ط¯ط¯ (GuarantorNumber = null ظٹط¹ظ†ظٹ ط§ظ„ظ…ظ‚طھط±ط¶)طŒ ظ…ط±طھط¨ط© ط¨طھط±طھظٹط¨ ط§ظ„ط¥ط¯ط®ط§ظ„.
    /// </summary>
    private static List<HeirDto> ToHeirDtos(IEnumerable<Heir> heirs, int? guarantorNumber) =>
        heirs.Where(h => h.GuarantorNumber == guarantorNumber)
            .Select(h => new HeirDto(h.Id, h.HeirName, h.HeirFather, h.HeirFamily, h.HeirCapacity, h.AddressType, h.HeirAddress))
            .ToList();

    /// <summary>
    /// ظˆط±ط«ط© ظ…ظˆط±ط«ظچ ظ…ط­ط¯ط¯ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» (ط·ط§ظ„ط¨ طھظ†ظپظٹط° ظ…طھظˆظپظ‰ ط£ظˆ ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ ط·ط¨ظٹط¹ظٹ ظ…طھظˆظپظ‰)طŒ
    /// ظ…ط±طھط¨ط© ط¨طھط±طھظٹط¨ ط§ظ„ط¥ط¯ط®ط§ظ„.
    /// </summary>
    private static List<ExecutedHeirDto> ToExecutedHeirDtos(
        IEnumerable<ExecutedHeir> heirs, int? applicantId, int? naturalPersonId) =>
        heirs.Where(h => h.ExecutionApplicantId == applicantId && h.ExecutedNaturalPersonId == naturalPersonId)
            .Select(h => new ExecutedHeirDto(h.Id, h.HeirName, h.HeirFather, h.HeirFamily, h.AddressType, h.HeirAddress))
            .ToList();
}
