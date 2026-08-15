using System.Text.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.DTOs;

/// <summary>
/// خيارات فلترة «الملفات التنفيذية» بأسلوب إكسل.
/// كل قائمة مُقيدة بباقي الفلاتر النشطة (ما عدا فلتر الحقل نفسه)
/// فيلتزم الاختيار اللاحق بنتائج الفلتر السابق تلقائيًا.
/// </summary>
public record DocumentFilterOptions(
    List<string> Applicants,
    List<string> Courts,
    List<string> Lawyers,
    List<string> AdministrativeBranches,
    List<string> Branches,
    List<string> ExecutedEntities,
    List<string> PublicEntityBranches);

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
/// عقار ضمن قائمة العقارات المرهونة. الملاك قائمة أسماء (واحد أو أكثر) بترتيب الاختيار،
/// وتُستخدم في النصوص والتوليد مجمعةً، ويُوجَّه توليد 005/006 لكل وريثٍ من الورثة.
/// </summary>
public record RealEstateDto(
    int? Id,
    List<string>? Owners,
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
    string? Address = null);

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
    string? Governorate = null);

/// <summary>الجهة العامة طالبة التنفيذ في وضع «الجهة العامة طالبة تنفيذ» (اسم الجهة + فرعها + محافظتها).</summary>
public record ApplicantPublicEntityDto(
    int? Id,
    string? Name,
    string? Branch,
    string? Governorate = null);

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
    List<ExecutedHeirDto>? Heirs = null);

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

/// <summary>
/// بيان تجديد ملف مشطوب عند إعادته إلى المتداول (فك الشطب مع رقم ملف جديد لسنة الإعادة).
/// الحقول المركبة تُرسَل نصوصًا حرة (مثال: 1/8/2026) وتُفسَّر وتُخزَّن زمنيًا كباقي تواريخ
/// «منفذ عليه». «رقم الملف الجديد» إلزامي، وما عداها اختياري.
/// </summary>
public class RenewalRequest
{
    /// <summary>رقم ورود اخطار التجديد (اختياري).</summary>
    public string? RenewalFileReceiptNumber { get; set; }

    /// <summary>تاريخ ورود اخطار التجديد (اختياري، نص حر).</summary>
    public string? RenewalFileReceiptDate { get; set; }

    /// <summary>رقم الملف الجديد عند إعادة الملف المشطوب (إلزامي).</summary>
    public string? RenewalFileNumber { get; set; }

    /// <summary>نوع الملف الجديد عند إعادة الملف المشطوب (اختياري).</summary>
    public string? RenewalFileType { get; set; }

    /// <summary>سنة الإعادة (إلزامية في نظام «طالبة تنفيذ»، وتُسجَّل بها سنة رقم الأساس الجديد).</summary>
    public int? RenewalYear { get; set; }

    /// <summary>تاريخ التجديد (اختياري، نص حر).</summary>
    public string? RenewalDate { get; set; }
}

public class DocumentUpsertRequest : RenewalRequest
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

    /// <summary>الممثل الشرعي للمقترض (ولي/وصي/قيم) إن وُجد: اسمه الثلاثي وصفته وعنوانه.</summary>
    public string? BorrowerRepresentativeName { get; set; }
    public string? BorrowerRepresentativeFather { get; set; }
    public string? BorrowerRepresentativeFamily { get; set; }
    public string? BorrowerRepresentativeCapacity { get; set; }
    public string? BorrowerRepresentativeAddressType { get; set; }
    public string? BorrowerRepresentativeAddress { get; set; }

    /// <summary>طبيعة المقترض/المنفذ عليه: شخص طبيعي (natural) أو شخص اعتباري (legal).</summary>
    public string BorrowerNature { get; set; } = PartyNatureCatalog.Natural;
    /// <summary>رقم تسجيل الشخص الاعتباري عند الطبيعة الاعتبارية (اختياري).</summary>
    public string? BorrowerRegistrationNumber { get; set; }
    /// <summary>من يمثل الشخص الاعتباري عند الطبيعة الاعتبارية (اختياري).</summary>
    public string? BorrowerRepresentedBy { get; set; }

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
    public decimal? Amount3Numeric { get; set; }
    public string? Amount3Words { get; set; }
    public string? Currency3 { get; set; } = "ليرة سورية";
    public decimal? InclusionAmountNumeric { get; set; }
    public string? InclusionAmountWords { get; set; }
    public string? InclusionCurrency { get; set; } = "ليرة سورية";
    public decimal? InclusionAmount2Numeric { get; set; }
    public string? InclusionAmount2Words { get; set; }
    public string? InclusionCurrency2 { get; set; } = "ليرة سورية";
    public decimal? InclusionAmount3Numeric { get; set; }
    public string? InclusionAmount3Words { get; set; }
    public string? InclusionCurrency3 { get; set; } = "ليرة سورية";

    public string? Court { get; set; }
    public string? Applicant { get; set; }

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

    /// <summary>ورثة المقترض المتوفى (إن وُجدوا)؛ الصفوف بلا اسم تُتجاهل عند الحفظ.</summary>
    public List<HeirDto> BorrowerHeirs { get; set; } = new();

    /// <summary>
    /// صفة الملف (GeneralEntitySideCatalog): تُثبَّت عند الإنشاء ولا تُغيّر عند التعديل.
    /// في وضع «منفذ عليه» يُشترط عادي فقط (لا مصرفي) وتُلغى بيانات المقترض والكفلاء والعقارات.
    /// </summary>
    public string? GeneralEntitySide { get; set; } = GeneralEntitySideCatalog.Applicant;

    /// <summary>حالة وضع «منفذ عليه» (ExecutedStatusCatalog): متداول/منفذ/مشطوب.</summary>
    public string? ExecutedStatus { get; set; } = string.Empty;

    /// <summary>تاريخ الشطب في وضع «منفذ عليه» (نص حر يُفسَّر بصيغ «1/8/2026» ويُخزَّن DateTime في القاعدة).</summary>
    public string? StruckOffDate { get; set; }

    /// <summary>وصف/بيان إضافي في وضع «منفذ عليه».</summary>
    public string? ExecutedDescription { get; set; }

    /// <summary>تاريخ ورود الاخطار في وضع «منفذ عليه» (نص حر يُفسَّر بصيغ «1/8/2026» ويغذي فترة إحصائية «متداول للضد»).</summary>
    public string? FileReceiptDate { get; set; }

    /// <summary>رقم ورود الإخطار التنفيذي في وضع «منفذ عليه».</summary>
    public string? FileReceiptNumber { get; set; }

    /// <summary>المبلغ المطلوب دفعه من الجهة العامة في وضع «منفذ عليه».</summary>
    public decimal? ExecutedRequiredAmount { get; set; }

    /// <summary>عملة المبلغ المطلوب الأول في وضع «منفذ عليه» (افتراضيًا ليرة سورية).</summary>
    public string? ExecutedRequiredCurrency { get; set; }

    /// <summary>المبلغ المطلوب الثاني (اختياري) في وضع «منفذ عليه».</summary>
    public decimal? ExecutedRequiredAmount2 { get; set; }

    /// <summary>عملة المبلغ المطلوب الثاني في وضع «منفذ عليه» (افتراضيًا ليرة سورية).</summary>
    public string? ExecutedRequiredCurrency2 { get; set; }

    /// <summary>المبلغ المطلوب الثالث (اختياري) في وضع «منفذ عليه».</summary>
    public decimal? ExecutedRequiredAmount3 { get; set; }

    /// <summary>عملة المبلغ المطلوب الثالث في وضع «منفذ عليه» (افتراضيًا ليرة سورية).</summary>
    public string? ExecutedRequiredCurrency3 { get; set; }

    /// <summary>المبلغ الذي دفعته الجهة العامة في وضع «منفذ عليه».</summary>
    public decimal? ExecutedPaidAmount { get; set; }

    /// <summary>تاريخ ايداعه حساب الجهة العامة في وضع «عرض وايداع» (نص حر يُفسَّر بصيغ «1/8/2026»).</summary>
    public string? ExecutedDepositDate { get; set; }

    /// <summary>طالبو التنفيذ في وضع «منفذ عليه».</summary>
    public List<ExecutionApplicantDto> ExecutionApplicants { get; set; } = new();

    /// <summary>الجهات العامة المنفذ عليها في وضع «منفذ عليه».</summary>
    public List<ExecutedPublicEntityDto> ExecutedPublicEntities { get; set; } = new();

    /// <summary>الأشخاص الطبيعيون المنفذ عليهم في وضع «منفذ عليه».</summary>
    public List<ExecutedNaturalPersonDto> ExecutedNaturalPersons { get; set; } = new();

    /// <summary>الجهات العامة طالبة التنفيذ في وضع «الجهة العامة طالبة تنفيذ» (واحدة أو أكثر).</summary>
    public List<ApplicantPublicEntityDto> ApplicantPublicEntities { get; set; } = new();

    /// <summary>رقم ورود الملف في وضع «الجهة العامة طالبة تنفيذ» (اختياري).</summary>
    public string? FileArrivalNumber { get; set; }

    /// <summary>تاريخ ورود الملف في وضع «الجهة العامة طالبة تنفيذ» (نص حر).</summary>
    public string? FileArrivalDate { get; set; }

    /// <summary>
    /// إجراءات وملاحظات تُزرع في جدول «الإجراءات والملاحظات» ذرّيًا مع حفظ المستند.
    /// كل عنصر يمر بنفس دوال التعقيم والتحقق الخاصة بالإجراءات (NormalizeAction/NormalizeReminder)،
    /// وأي عنصر غير صالح يرفض الحفظ كاملًا بنفس المعاملة، ولا يُنشأ أي سجل مكرر عند التعديل.
    /// الحقول المتروكة فارغة تُتجاهل ولا تُسبب فشل الحفظ.
    /// </summary>
    public List<AddExecutionActionRequest> InitialActions { get; set; } = new();
}

/// <summary>
/// صف ملف واحد في جدول تدوير أرقام الأساس — ملفات المحامي المؤهلة للتدوير
/// مع رقم أساس السنة الحالية إن وُجد (وإلا يكون null فيُترك الحقل فارغًا للإدخال).
/// DisplayName هو اسم العرض (اسم المقترض، أو اسم طالب العرض لمن لفائف العائلتين
/// Executed + Deposit اللتين بلا مقترض)، ويعود للواجهة لعرضه عمود الاسم الموحد.
/// </summary>
public record RotationDocumentDto(
    int DocumentId,
    string? Court,
    string? BorrowerName,
    string? BorrowerFather,
    string? BorrowerFamily,
    string? FileNumber,
    string? FileType,
    string? BaseNumber,
    string? DisplayName);

/// <summary>
/// رقم أساس مُدخل لملف واحد في سنة التدوير الحالية. القيمة الفارغة تعني
/// إلغاء رقم أساس السنة الحالية لهذا الملف (مع الاحتفاظ بأرقام السنوات السابقة).
/// </summary>
public record BaseNumberEntry(
    int DocumentId,
    string? BaseNumber);

public class SaveBaseNumbersRequest
{
    public List<BaseNumberEntry> Entries { get; set; } = new();
}

/// <summary>
/// رقم أساس لسنة واحدة في تاريخ تدوير أرقام الأساس للملف (مرتب تنازليًا بالسنوات).
/// </summary>
public record BaseNumberHistoryDto(
    int Year,
    string BaseNumber);

/// <summary>
/// وقعة واحدة من «وقوعات الملف»: شطب/تجديد (وضع «منفذ عليه») أو إجراء تغيير حالة
/// (نظام «طالبة تنفيذ»: تريث/منفذ بالتسوية/منفذ جبريا/تراجع). EventDate/ReceiptDate
/// زمنيّان (تُفسَّر النصوص الحرة في الخدمة وتحوَّل إلى DateTime)، وDetails تحمل حقول
/// إجراءات تغيير الحالة الكاملة (المفاتيح الإنكليزية المعتمدة في الخدمة).
/// </summary>
public record DocumentOccurrenceDto(
    int Id,
    string OccurrenceType,
    string OccurrenceTypeLabel,
    DateTime? EventDate,
    string? FileNumber,
    string? FileType,
    int? Year,
    string? ReceiptNumber,
    DateTime? ReceiptDate,
    IReadOnlyDictionary<string, string>? Details,
    string? CreatedByName);

/// <summary>
/// إضافة/تعديل وقعة «منفذ عليه» يدويًا. التواريخ تُرسَل نصوصًا حرة (مثال: 1/8/2026)
/// وتُفسَّر وتُخزَّن زمنيًا كباقي تواريخ وضع «منفذ عليه» (نفس صيغ RenewalRequest).
/// </summary>
public class UpsertOccurrenceRequest
{
    /// <summary>نوع الوقعة (OccurrenceTypeCatalog): "struck-off" أو "renewal".</summary>
    public string OccurrenceType { get; set; } = OccurrenceTypeCatalog.StruckOff;

    /// <summary>تاريخ الوقعة: تاريخ الشطب أو تاريخ التجديد (نص حر).</summary>
    public string? EventDate { get; set; }

    /// <summary>الرقم المعني بالوقعة: الرقم القديم المُشطوب أو الرقم الجديد للتجديد.</summary>
    public string? FileNumber { get; set; }

    /// <summary>نوع الملف الجديد عند التجديد (اختياري).</summary>
    public string? FileType { get; set; }

    /// <summary>سنة الوقعة: سنة الشطب أو سنة الإعادة للتجديد.</summary>
    public int? Year { get; set; }

    /// <summary>رقم ورود اخطار التجديد عند التجديد (اختياري).</summary>
    public string? ReceiptNumber { get; set; }

    /// <summary>تاريخ ورود اخطار التجديد عند التجديد (اختياري، نص حر).</summary>
    public string? ReceiptDate { get; set; }

    /// <summary>
    /// حقول إجراءات تغيير الحالة (نظام «طالبة تنفيذ»): المفاتيح المعتمدة في الخدمة
    /// (execSubStatus، collectedAmount/2/3 + العملات، baraet*، tarith*، sayer*، soldEstateIds).
    /// </summary>
    public Dictionary<string, string?>? Details { get; set; }
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
    /// <summary>الممثل الشرعي للمقترض (ولي/وصي/قيم) إن وُجد: اسمه الثلاثي وصفته وعنوانه.</summary>
    public string? BorrowerRepresentativeName { get; set; }
    public string? BorrowerRepresentativeFather { get; set; }
    public string? BorrowerRepresentativeFamily { get; set; }
    public string? BorrowerRepresentativeCapacity { get; set; }
    public string? BorrowerRepresentativeAddressType { get; set; }
    public string? BorrowerRepresentativeAddress { get; set; }
    /// <summary>طبيعة المقترض/المنفذ عليه: شخص طبيعي (natural) أو شخص اعتباري (legal).</summary>
    public string BorrowerNature { get; set; } = PartyNatureCatalog.Natural;
    /// <summary>رقم تسجيل الشخص الاعتباري عند الطبيعة الاعتبارية (اختياري).</summary>
    public string? BorrowerRegistrationNumber { get; set; }
    /// <summary>من يمثل الشخص الاعتباري عند الطبيعة الاعتبارية (اختياري).</summary>
    public string? BorrowerRepresentedBy { get; set; }
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
    public string? Applicant { get; set; }
    public string? Lawyer { get; set; }
    public string? ReferredFromLawyer { get; set; }
    public DateTime? ReferredAt { get; set; }
    public string? FileNumber { get; set; }
    /// <summary>
    /// الرقم الظاهر للمستخدم: رقم أساس السنة الحالية إن وُجد، وإلا رقم الملف الأصلي.
    /// (رقم الملف الأصلي يبقى في FileNumber للتحرير والسجل التاريخي.)
    /// </summary>
    public string? DisplayFileNumber { get; set; }
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
    /// <summary>المبالغ المحصَّلة (حتى ثلاثة بعملاتها) في «منفذ بالتسوية»/«منفذ جبريا».</summary>
    public decimal? CollectedAmount { get; set; }
    public decimal? CollectedAmount2 { get; set; }
    public decimal? CollectedAmount3 { get; set; }
    public string? CollectedCurrency { get; set; }
    public string? CollectedCurrency2 { get; set; }
    public string? CollectedCurrency3 { get; set; }
    /// <summary>صفة الملف (GeneralEntitySideCatalog): applicant = طالبة تنفيذ، executed = منفذ عليه، deposit = عرض وايداع.</summary>
    public string? GeneralEntitySide { get; set; }
    /// <summary>التسمية العربية للصفة.</summary>
    public string? GeneralEntitySideLabel { get; set; }
    public string? ExecutedStatus { get; set; }
    public string? ExecutedDescription { get; set; }
    public DateTime? FileReceiptDate { get; set; }
    /// <summary>رقم ورود الإخطار التنفيذي في وضع «منفذ عليه».</summary>
    public string? FileReceiptNumber { get; set; }
    public decimal? ExecutedRequiredAmount { get; set; }
    public string? ExecutedRequiredCurrency { get; set; }
    public decimal? ExecutedRequiredAmount2 { get; set; }
    public string? ExecutedRequiredCurrency2 { get; set; }
    public decimal? ExecutedRequiredAmount3 { get; set; }
    public string? ExecutedRequiredCurrency3 { get; set; }
    public decimal? ExecutedPaidAmount { get; set; }
    /// <summary>تاريخ ايداعه حساب الجهة العامة في وضع «عرض وايداع».</summary>
    public DateTime? ExecutedDepositDate { get; set; }
    public DateTime? StruckOffDate { get; set; }
    /// <summary>رقم ورود اخطار التجديد عند إعادة ملف مشطوب إلى المتداول (اختياري).</summary>
    public string? RenewalFileReceiptNumber { get; set; }
    /// <summary>تاريخ ورود اخطار التجديد عند إعادة الملف المشطوب (اختياري).</summary>
    public DateTime? RenewalFileReceiptDate { get; set; }
    /// <summary>رقم الملف الجديد عند إعادة الملف المشطوب (إلزامي) — يعود الملف به لسنة الإعادة.</summary>
    public string? RenewalFileNumber { get; set; }
    /// <summary>نوع الملف الجديد عند إعادة الملف المشطوب (اختياري).</summary>
    public string? RenewalFileType { get; set; }
    /// <summary>تاريخ التجديد عند إعادة الملف المشطوب (اختياري).</summary>
    public DateTime? RenewalDate { get; set; }
    public string? BaraetNumber { get; set; }
    public string? BaraetDate { get; set; }
    public string? BaraetRegNumber { get; set; }
    public string? BaraetRegDate { get; set; }
    public string? TarithNumber { get; set; }
    public string? TarithDate { get; set; }
    public string? TarithRegNumber { get; set; }
    public string? TarithRegDate { get; set; }
    /// <summary>حقول كتاب الجهة العامة بالسير بالملف عند التراجع (رقم/تاريخ الكتاب + وروده).</summary>
    public string? SayerNumber { get; set; }
    public string? SayerDate { get; set; }
    public string? SayerRegNumber { get; set; }
    public string? SayerRegDate { get; set; }
    /// <summary>معرّفات العقارات المباعة بالمزاد العلني في «منفذ جبريا» (من عقارات الملف).</summary>
    public List<int> SoldEstateIds { get; set; } = new();
    public string? SeizureDate { get; set; }
    public string? ImmediateActions { get; set; }
    public string? Notes { get; set; }
    public int ViewCount { get; set; }
    public int PrintCount { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? DeletedAt { get; set; }
    /// <summary>
    /// صحيح إذا كان الملف بحاجة إلى تدوير هذا العام: مقيد برقم ملف (ليس تحت رفع)،
    /// غير منفَّذ، لديه رقم أساس لسنة سابقة، ولا يحمل رقم أساس للسنة الحالية —
    /// فيظهر رقم ملفه بالأحمر في «الملفات التنفيذية».
    /// </summary>
    public bool NeedsRotation { get; set; }
    public List<GuarantorDto> Guarantors { get; set; } = new();
    public List<RealEstateDto> RealEstates { get; set; } = new();
    /// <summary>ورثة المقترض المتوفى (إن وُجدوا).</summary>
    public List<HeirDto> BorrowerHeirs { get; set; } = new();
    public List<ExecutionActionDto> ExecutionActions { get; set; } = new();
    public List<ExecutionApplicantDto> ExecutionApplicants { get; set; } = new();
    public List<ExecutedPublicEntityDto> ExecutedPublicEntities { get; set; } = new();
    public List<ExecutedNaturalPersonDto> ExecutedNaturalPersons { get; set; } = new();
    /// <summary>الجهات العامة طالبة التنفيذ في وضع «الجهة العامة طالبة تنفيذ» (واحدة أو أكثر).</summary>
    public List<ApplicantPublicEntityDto> ApplicantPublicEntities { get; set; } = new();
    /// <summary>سجل تعاقب المحامين على الملف (منشئ + كل المحامين المتعاقبين مع تواريخ الإحالة).</summary>
    public List<DocumentAssignmentDto> Assignments { get; set; } = new();
    /// <summary>رقم ورود الملف في وضع «الجهة العامة طالبة تنفيذ» (اختياري).</summary>
    public string? FileArrivalNumber { get; set; }
    /// <summary>تاريخ ورود الملف في وضع «الجهة العامة طالبة تنفيذ» (نص حر).</summary>
    public string? FileArrivalDate { get; set; }

    /// <summary>
    /// «وقوعات الملف» في وضع «منفذ عليه»/«عرض وايداع»: سجل زمني لكل شطب وتجديد،
    /// مرتب تصاعديًا حسب تاريخ الوقعة (سرد زمني من الأقدم إلى الأحدث).
    /// </summary>
    public List<DocumentOccurrenceDto> Occurrences { get; set; } = new();

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
        InclusionText = d.InclusionText,
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
        Applicant = d.Applicant,
        Lawyer = d.Lawyer,
        ReferredFromLawyer = d.ReferredFromLawyer,
        ReferredAt = d.ReferredAt,
        FileNumber = d.FileNumber,
        DisplayFileNumber = CurrentBaseNumberOf(d) ?? d.FileNumber,
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
        ExecutedDepositDate = d.ExecutedDepositDate,
        StruckOffDate = d.StruckOffDate,
        RenewalFileReceiptNumber = d.RenewalFileReceiptNumber,
        RenewalFileReceiptDate = d.RenewalFileReceiptDate,
        RenewalFileNumber = d.RenewalFileNumber,
        RenewalFileType = d.RenewalFileType,
        RenewalDate = d.RenewalDate,
        BaraetNumber = d.BaraetNumber,
        BaraetDate = d.BaraetDate,
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
        SoldEstateIds = ParseSoldEstateIds(d.SoldEstateIds),
        SeizureDate = d.SeizureDate,
        ImmediateActions = d.ImmediateActions,
        Notes = d.Notes,
        ViewCount = d.ViewCount,
        PrintCount = d.PrintCount,
        CreatedByName = d.CreatedBy?.FullName,
        DeletedAt = d.DeletedAt,
        NeedsRotation = NeedsRotationOf(d),
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
        RealEstates = d.RealEstates
            .Select(r => new RealEstateDto(r.Id,
                r.Owners.OrderBy(o => o.Order).Select(o => o.Name).ToList(),
                r.Property, r.PropertyNumber,
                r.PropertyDistrict, r.LandRegistry, r.ShareType))
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
                a.ApplicantAddressType, a.ApplicantAddress))
            .ToList(),
        ExecutedPublicEntities = d.ExecutedPublicEntities
            .Select(e => new ExecutedPublicEntityDto(e.Id, e.EntityName, e.EntityBranch,
                e.EntityNature, e.RegistrationNumber, e.RepresentedBy, e.AddressType, e.Address,
                e.Governorate))
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
            .Select(e => new ApplicantPublicEntityDto(e.Id, e.Name, e.Branch, e.Governorate))
            .ToList(),
        Assignments = d.Assignments
            .OrderBy(a => a.AssignedAt)
            .Select(a => new DocumentAssignmentDto(a.Id, a.Kind, a.LawyerName, a.AssignedByName, a.AssignedAt))
            .ToList(),
        Occurrences = d.Occurrences
            .OrderBy(o => o.EventDate)
            .ThenBy(o => o.Id)
            .Select(o => new DocumentOccurrenceDto(o.Id, o.OccurrenceType,
                OccurrenceTypeCatalog.ToLabel(o.OccurrenceType), o.EventDate,
                o.FileNumber, o.FileType, o.Year, o.ReceiptNumber, o.ReceiptDate,
                ParseOccurrenceDetails(o.Details), o.CreatedBy?.FullName))
            .ToList(),
    };

    /// <summary>فكّ قائمة معرّفات العقارات المباعة من JSON المخزن (أو قائمة فارغة عند العطب).</summary>
    private static List<int> ParseSoldEstateIds(string? json)
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

    /// <summary>فكّ حقول الوقعة التفصيلية من JSON المخزن (أو null عند غيابها/عطبها).</summary>
    private static IReadOnlyDictionary<string, string>? ParseOccurrenceDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// رقم أساس السنة الحالية (سنة التدوير) إن وُجد للملف — وإلا null.
    /// </summary>
    private static string? CurrentBaseNumberOf(Document d)
    {
        var currentYear = DateTime.Today.Year;
        return d.BaseNumbers.FirstOrDefault(b => b.Year == currentYear)?.BaseNumber;
    }

    /// <summary>
    /// قاعدة اللون الأحمر المعتمدة: يُلوَّن رقم الملف فقط إذا كان الملف مقيدًا
    /// (ليس تحت رفع)، غير منفَّذ، وسبق أن دُوِّر في سنة سابقة، ولم يُدوَّر في السنة الحالية.
    /// ولعائلتي وضع «منفذ عليه» (Executed + Deposit) يُؤخذ «غير منفَّذ» بمعناه العائلي:
    /// متداولة فقط (لا منفذ ولا مشطوب) — مطابقًا لشرط أهليتهما في قائمة التدوير والحفظ.
    /// </summary>
    private static bool NeedsRotationOf(Document d)
    {
        var currentYear = DateTime.Today.Year;
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
    /// ورثة منفذ عليه محدد (GuarantorNumber = null يعني المقترض)، مرتبة بترتيب الإدخال.
    /// </summary>
    private static List<HeirDto> ToHeirDtos(IEnumerable<Heir> heirs, int? guarantorNumber) =>
        heirs.Where(h => h.GuarantorNumber == guarantorNumber)
            .Select(h => new HeirDto(h.Id, h.HeirName, h.HeirFather, h.HeirFamily, h.HeirCapacity, h.AddressType, h.HeirAddress))
            .ToList();

    /// <summary>
    /// ورثة مورثٍ محدد في وضع «منفذ عليه» (طالب تنفيذ متوفى أو منفذ عليه طبيعي متوفى)،
    /// مرتبة بترتيب الإدخال.
    /// </summary>
    private static List<ExecutedHeirDto> ToExecutedHeirDtos(
        IEnumerable<ExecutedHeir> heirs, int? applicantId, int? naturalPersonId) =>
        heirs.Where(h => h.ExecutionApplicantId == applicantId && h.ExecutedNaturalPersonId == naturalPersonId)
            .Select(h => new ExecutedHeirDto(h.Id, h.HeirName, h.HeirFather, h.HeirFamily, h.AddressType, h.HeirAddress))
            .ToList();
}
