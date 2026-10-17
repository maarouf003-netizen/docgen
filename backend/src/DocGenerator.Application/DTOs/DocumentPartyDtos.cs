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
/// وريث لمنفذ عليه متوفى. القيمة الفارغة في الحقلين (AddressType أو Address) تُلغى
/// السابقة «عنوانه/يمثله» في المستندات فيُذكر الاسم الثلاثي للوريث فقط.
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
/// مال مرهون ضمن قائمة الأموال المرهونة (منقول أو غير منقول). الملاك قائمة أسماء (واحد أو أكثر)
/// بترتيب الاختيار، وتُستخدم في النصوص والتوليد مجمعةً، ويُوجَّه توليد 005/006 لكل وريثٍ من الورثة.
/// الحقول غير المعنية بنوع الأصل تبقى فارغة.
/// </summary>
public record AssetDto(
    int? Id,
    string? AssetKind,
    List<string>? Owners,
    string? ShareType,
    // العقار
    string? Property,
    string? PropertyNumber,
    string? PropertyDistrict,
    string? LandRegistry,
    // المركبة
    string? VehicleType,
    string? VehicleClass,
    string? PlateNumber,
    string? VehicleGovernorate,
    // المتجر المسجل
    string? RegisterNumber,
    /// <summary>تاريخ تسجيل المتجر (نص حر يُفسَّر بصيغ «1/8/2026» فيُخزَّن زمنيًا: مثل بقية تواريخ الملفات).</summary>
    string? RegistrationDate,
    string? ShopGovernorate,
    string? ShopDescription,
    string? ShopLocation,
    // كفالة الرواتب
    string? PublicEntity,
    // المتجر غير المسجل
    string? LicenseNumber,
    /// <summary>تاريخ الترخيص (نص حر يُفسَّر بصيغ «1/8/2026» فيُخزَّن زمنيًا: مثل بقية تواريخ الملفات).</summary>
    string? LicenseDate,
    string? LicenseIssuer,
    // الملاحظات
    string? Notes,
    /// <summary>تاريخ القاء الحجز على الأصل (نص حر يُفسَّر زمنيًا ويُستبدل في قوالب الوورد).</summary>
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
/// وريث لمورثٍ متوفى في وضع «منفذ عليه» (اسم ثلاثي). القيمة الفارغة في الحقلين
/// (AddressType أو HeirAddress) تعني عدم وجود عنوان/وكيل فيُذكر الاسم الثلاثي للوريث فقط.
/// </summary>
public record ExecutedHeirDto(
    int? Id,
    string? HeirName,
    string? HeirFather,
    string? HeirFamily,
    string? AddressType,
    string? HeirAddress);

/// <summary>
/// طالب التنفيذ في وضع «منفذ عليه» مع ورثة مورثه المتوفى إن اختير «إضافة لتركة».
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
    /// <summary>معرّف قيد طالب التنفيذ الاعتباري (الجهة العامة) في السجل المرجعي (اختياري — من نافذة الاختيار).</summary>
    int? RegistryId = null) : IExportApplicant;

/// <summary>الجهة العامة أو الشخص الاعتباري المنفذ عليه في وضع «منفذ عليه».</summary>
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
    /// <summary>معرّف قيد الجهة في السجل المرجعي (اختياري — من نافذة الاختيار).</summary>
    int? RegistryId = null) : IExportPublicEntity;

/// <summary>الجهة العامة طالبة التنفيذ في وضع «الجهة العامة طالبة تنفيذ» (اسم الجهة + فرعها + محافظتها).</summary>
public record ApplicantPublicEntityDto(
    int? Id,
    string? Name,
    string? Branch,
    string? Governorate = null,
    /// <summary>معرّف قيد الجهة في السجل المرجعي (اختياري — من نافذة الاختيار).</summary>
    int? RegistryId = null);

/// <summary>سجل تعاقب محامٍ على الملف: منشئ الملف (create) أو إحالة (transfer).</summary>
public record DocumentAssignmentDto(
    int Id,
    string Kind,
    string? LawyerName,
    string? AssignedByName,
    DateTime? AssignedAt);

/// <summary>
/// الشخص الطبيعي المنفذ عليه في وضع «منفذ عليه» مع ورثة مورثه المتوفى إن اختير «إضافة لتركة».
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
