using DocGenerator.Application.Common;
using System.Text.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

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
    string? AddressType,
    string? RepresentativeName,
    string? RepresentativeFather,
    string? RepresentativeFamily,
    string? RepresentativeCapacity,
    string? RepresentativeAddressType,
    string? RepresentativeAddress,
    List<HeirDto>? Heirs = null,
    string? Nature = PartyNatureCatalog.Natural,
    string? RegistrationNumber = null,
    string? RepresentedBy = null);

/// <summary>
/// ظˆط±ظٹط« ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ ظ…طھظˆظپظ‰. ط§ظ„ظ‚ظٹظ…ط© ط§ظ„ظپط§ط±ط؛ط© ظپظٹ ط§ظ„ط­ظ‚ظ„ظٹظ† (AddressType ط£ظˆ Address) طھظڈظ„ط؛ظ‰
/// ط§ظ„ط³ط§ط¨ظ‚ط© آ«ط¹ظ†ظˆط§ظ†ظ‡/ظٹظ…ط«ظ„ظ‡آ» ظپظٹ ط§ظ„ظ…ط³طھظ†ط¯ط§طھ ظپظٹظڈط°ظƒط± ط§ظ„ط§ط³ظ… ط§ظ„ط«ظ„ط§ط«ظٹ ظ„ظ„ظˆط±ظٹط« ظپظ‚ط·.
/// </summary>
public record HeirDto(
    int? Id,
    string? Name,
    string? Father,
    string? Family,
    string? Capacity,
    string? AddressType,
    string? Address);

/// <summary>
/// ظ…ط§ظ„ ظ…ط±ظ‡ظˆظ† ط¶ظ…ظ† ظ‚ط§ط¦ظ…ط© ط§ظ„ط£ظ…ظˆط§ظ„ ط§ظ„ظ…ط±ظ‡ظˆظ†ط© (ظ…ظ†ظ‚ظˆظ„ ط£ظˆ ط؛ظٹط± ظ…ظ†ظ‚ظˆظ„). ط§ظ„ظ…ظ„ط§ظƒ ظ‚ط§ط¦ظ…ط© ط£ط³ظ…ط§ط، (ظˆط§ط­ط¯ ط£ظˆ ط£ظƒط«ط±)
/// ط¨طھط±طھظٹط¨ ط§ظ„ط§ط®طھظٹط§ط±طŒ ظˆطھظڈط³طھط®ط¯ظ… ظپظٹ ط§ظ„ظ†طµظˆطµ ظˆط§ظ„طھظˆظ„ظٹط¯ ظ…ط¬ظ…ط¹ط©ظ‹طŒ ظˆظٹظڈظˆط¬ظژظ‘ظ‡ طھظˆظ„ظٹط¯ 005/006 ظ„ظƒظ„ ظˆط±ظٹط«ظچ ظ…ظ† ط§ظ„ظˆط±ط«ط©.
/// ط§ظ„ط­ظ‚ظˆظ„ ط؛ظٹط± ط§ظ„ظ…ط¹ظ†ظٹط© ط¨ظ†ظˆط¹ ط§ظ„ط£طµظ„ طھط¨ظ‚ظ‰ ظپط§ط±ط؛ط©.
/// </summary>
public record AssetDto(
    int? Id,
    string? AssetKind,
    List<string>? Owners,
    string? ShareType,
    // ط§ظ„ط¹ظ‚ط§ط±
    string? Property,
    string? PropertyNumber,
    string? PropertyDistrict,
    string? LandRegistry,
    // ط§ظ„ظ…ط±ظƒط¨ط©
    string? VehicleType,
    string? VehicleClass,
    string? PlateNumber,
    string? VehicleGovernorate,
    // ط§ظ„ظ…طھط¬ط± ط§ظ„ظ…ط³ط¬ظ„
    string? RegisterNumber,
    /// <summary>طھط§ط±ظٹط® طھط³ط¬ظٹظ„ ط§ظ„ظ…طھط¬ط± (ظ†طµ ط­ط± ظٹظڈظپط³ظژظ‘ط± ط¨طµظٹط؛ آ«1/8/2026آ» ظپظٹظڈط®ط²ظژظ‘ظ† ط²ظ…ظ†ظٹظ‹ط§: ظ…ط«ظ„ ط¨ظ‚ظٹط© طھظˆط§ط±ظٹط® ط§ظ„ظ…ظ„ظپط§طھ).</summary>
    string? RegistrationDate,
    string? ShopGovernorate,
    string? ShopDescription,
    string? ShopLocation,
    // ظƒظپط§ظ„ط© ط§ظ„ط±ظˆط§طھط¨
    string? PublicEntity,
    // ط§ظ„ظ…طھط¬ط± ط؛ظٹط± ط§ظ„ظ…ط³ط¬ظ„
    string? LicenseNumber,
    /// <summary>طھط§ط±ظٹط® ط§ظ„طھط±ط®ظٹطµ (ظ†طµ ط­ط± ظٹظڈظپط³ظژظ‘ط± ط¨طµظٹط؛ آ«1/8/2026آ» ظپظٹظڈط®ط²ظژظ‘ظ† ط²ظ…ظ†ظٹظ‹ط§: ظ…ط«ظ„ ط¨ظ‚ظٹط© طھظˆط§ط±ظٹط® ط§ظ„ظ…ظ„ظپط§طھ).</summary>
    string? LicenseDate,
    string? LicenseIssuer,
    // ط§ظ„ظ…ظ„ط§ط­ط¸ط§طھ
    string? Notes,
    /// <summary>طھط§ط±ظٹط® ط§ظ„ظ‚ط§ط، ط§ظ„ط­ط¬ط² ط¹ظ„ظ‰ ط§ظ„ط£طµظ„ (ظ†طµ ط­ط± ظٹظڈظپط³ظژظ‘ط± ط²ظ…ظ†ظٹظ‹ط§ ظˆظٹظڈط³طھط¨ط¯ظ„ ظپظٹ ظ‚ظˆط§ظ„ط¨ ط§ظ„ظˆظˆط±ط¯).</summary>
    string? SeizureDate);

public record ExecutionActionDto(
    int Id,
    string Type,
    string Text,
    string? ActionDate,
    string? ReminderDuration,
    string? ReminderColor,
    string? CreatedByName,
    DateTime CreatedAt) : IExportAction;

/// <summary>
/// ظˆط±ظٹط« ظ„ظ…ظˆط±ط«ظچ ظ…طھظˆظپظ‰ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» (ط§ط³ظ… ط«ظ„ط§ط«ظٹ). ط§ظ„ظ‚ظٹظ…ط© ط§ظ„ظپط§ط±ط؛ط© ظپظٹ ط§ظ„ط­ظ‚ظ„ظٹظ†
/// (AddressType ط£ظˆ HeirAddress) طھط¹ظ†ظٹ ط¹ط¯ظ… ظˆط¬ظˆط¯ ط¹ظ†ظˆط§ظ†/ظˆظƒظٹظ„ ظپظٹظڈط°ظƒط± ط§ظ„ط§ط³ظ… ط§ظ„ط«ظ„ط§ط«ظٹ ظ„ظ„ظˆط±ظٹط« ظپظ‚ط·.
/// </summary>
public record ExecutedHeirDto(
    int? Id,
    string? HeirName,
    string? HeirFather,
    string? HeirFamily,
    string? AddressType,
    string? HeirAddress);

/// <summary>
/// ط·ط§ظ„ط¨ ط§ظ„طھظ†ظپظٹط° ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» ظ…ط¹ ظˆط±ط«ط© ظ…ظˆط±ط«ظ‡ ط§ظ„ظ…طھظˆظپظ‰ ط¥ظ† ط§ط®طھظٹط± آ«ط¥ط¶ط§ظپط© ظ„طھط±ظƒط©آ».
/// </summary>
public record ExecutionApplicantDto(
    int? Id,
    string? Name,
    string? Father,
    string? Family,
    string? LegalRepresentative,
    string? RepresentationType,
    string? DeceasedName,
    string? DeceasedFather,
    string? DeceasedFamily,
    string? RepresentativeName,
    string? RepresentativeFather,
    string? RepresentativeFamily,
    string? RepresentativeCapacity,
    string? RepresentativeLegalRepresentative,
    List<ExecutedHeirDto>? Heirs = null,
    string? Nature = PartyNatureCatalog.Natural,
    string? RegistrationNumber = null,
    string? RepresentedBy = null,
    string? AddressType = null,
    string? Address = null,
    /// <summary>ظ…ط¹ط±ظ‘ظپ ظ‚ظٹط¯ ط·ط§ظ„ط¨ ط§ظ„طھظ†ظپظٹط° ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ (ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط©) ظپظٹ ط§ظ„ط³ط¬ظ„ ط§ظ„ظ…ط±ط¬ط¹ظٹ (ط§ط®طھظٹط§ط±ظٹ â€” ظ…ظ† ظ†ط§ظپط°ط© ط§ظ„ط§ط®طھظٹط§ط±).</summary>
    int? RegistryId = null) : IExportApplicant;

/// <summary>ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط£ظˆ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط§ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ».</summary>
public record ExecutedPublicEntityDto(
    int? Id,
    string? EntityName,
    string? EntityBranch,
    string? Nature = PartyNatureCatalog.PublicEntity,
    string? RegistrationNumber = null,
    string? RepresentedBy = null,
    string? AddressType = null,
    string? Address = null,
    string? Governorate = null,
    /// <summary>ظ…ط¹ط±ظ‘ظپ ظ‚ظٹط¯ ط§ظ„ط¬ظ‡ط© ظپظٹ ط§ظ„ط³ط¬ظ„ ط§ظ„ظ…ط±ط¬ط¹ظٹ (ط§ط®طھظٹط§ط±ظٹ â€” ظ…ظ† ظ†ط§ظپط°ط© ط§ظ„ط§ط®طھظٹط§ط±).</summary>
    int? RegistryId = null) : IExportPublicEntity;

/// <summary>ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© ط§ظ„طھظ†ظپظٹط° ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ط§ط³ظ… ط§ظ„ط¬ظ‡ط© + ظپط±ط¹ظ‡ط§ + ظ…ط­ط§ظپط¸طھظ‡ط§).</summary>
public record ApplicantPublicEntityDto(
    int? Id,
    string? Name,
    string? Branch,
    string? Governorate = null,
    /// <summary>ظ…ط¹ط±ظ‘ظپ ظ‚ظٹط¯ ط§ظ„ط¬ظ‡ط© ظپظٹ ط§ظ„ط³ط¬ظ„ ط§ظ„ظ…ط±ط¬ط¹ظٹ (ط§ط®طھظٹط§ط±ظٹ â€” ظ…ظ† ظ†ط§ظپط°ط© ط§ظ„ط§ط®طھظٹط§ط±).</summary>
    int? RegistryId = null);

/// <summary>ط³ط¬ظ„ طھط¹ط§ظ‚ط¨ ظ…ط­ط§ظ…ظچ ط¹ظ„ظ‰ ط§ظ„ظ…ظ„ظپ: ظ…ظ†ط´ط¦ ط§ظ„ظ…ظ„ظپ (create) ط£ظˆ ط¥ط­ط§ظ„ط© (transfer).</summary>
public record DocumentAssignmentDto(
    int Id,
    string Kind,
    string? LawyerName,
    string? AssignedByName,
    DateTime? AssignedAt);

/// <summary>
/// ط§ظ„ط´ط®طµ ط§ظ„ط·ط¨ظٹط¹ظٹ ط§ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» ظ…ط¹ ظˆط±ط«ط© ظ…ظˆط±ط«ظ‡ ط§ظ„ظ…طھظˆظپظ‰ ط¥ظ† ط§ط®طھظٹط± آ«ط¥ط¶ط§ظپط© ظ„طھط±ظƒط©آ».
/// </summary>
public record ExecutedNaturalPersonDto(
    int? Id,
    string? Name,
    string? Father,
    string? Family,
    string? AddressType,
    string? AddressOrRepresentative,
    string? RepresentationType,
    string? DeceasedName,
    string? DeceasedFather,
    string? DeceasedFamily,
    string? RepresentativeName,
    string? RepresentativeFather,
    string? RepresentativeFamily,
    string? RepresentativeCapacity,
    string? RepresentativeAddressType,
    string? RepresentativeAddress,
    List<ExecutedHeirDto>? Heirs = null) : IExportNaturalPerson;
