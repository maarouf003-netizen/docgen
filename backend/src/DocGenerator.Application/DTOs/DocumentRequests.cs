using DocGenerator.Application.Common;
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
/// نقل تذكير إجراء إلى يوم استحقاق جديد (`POST .../actions/{id}/reschedule` من نافذة
/// اليوم): المدخل هو يوم الاستحقاق نفسه، والخلفية تعيد حساب `ActionDate` خلفيًا
/// (`newDueDate − DurationDays`) مع بقاء المدة — لا مساس ببقية حقول الإجراء.
/// </summary>
public class RescheduleActionReminderRequest
{
    public string? NewDueDate { get; set; }
}

/// <summary>
/// بيان تجديد ملف مشطوب عند إعادته إلى المتداول (فك الشطب مع رقم ملف جديد لسنة الإعادة).
/// الحقول المركبة تُرسَل نصوصًا حرة (مثال: 1/8/2026) وتُفسَّر وتُخزَّن زمنيًا كباقي تواريخ
/// «منفذ عليه». «رقم الملف الجديد» إلزامي، وما عداها اختياري.
/// </summary>
public class RenewalRequest
{
    /// <summary>
    /// عدّاد التزامن المتفائل (RF-010): قيمة `Version` كما قُرئت عند الجلب.
    /// اختياري للتوافق (غيابه = قبول بلا فحص مبكر)؛ القديم يُرفض `409`.
    /// موروث في `DocumentUpsertRequest` و`ReturnReferredToStartRequest` و`ExecutedStatusRequest`.
    /// </summary>
    public long? Version { get; set; }

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

/// <summary>
/// بيان العودة من «محال الى البداية» (نقطة «return-referred-to-start») — بنتيجتين:
/// استعادة «منفذ جبريا» (منفذ جزئيا) حسب اللازمة وإلا «متداول». حقول التجديد موحّدة في
/// المسارين (رقم الملف الجديد يفعّل التجديد ويلزم السنة 1900–2100 وتطابقها مع سنة التاريخ)،
/// و«تاريخ الشطب» خاص بنتيجة-متداول فقط ويُتجاهل في عودة-جزئيا. كلها نصوص حرة (مثال: 1/8/2026)
/// تُفسَّر وتُخزَّن زمنيًا كباقي تواريخ النظام.
/// </summary>
public class ReturnReferredToStartRequest : RenewalRequest
{
    /// <summary>تاريخ شطب الملف (اختياري — لعودة-متداول فقط، ويُتجاهل في عودة-جزئيا).</summary>
    public string? StruckOffDate { get; set; }
}

/// <summary>
/// تعيين حالة وضع «الجهة العامة منفذ عليها» (متداول/منفذ/مشطوب) من صفحة التفاصيل.
/// عند الانتقال إلى «منفذ» تُحفظ حقوله (المبلغ/كيفية التنفيذ/تاريخ الإيداع)، وعند الانتقال
/// إلى «مشطوب» يُحفظ تاريخ الشطب (نص حر؛ إن غاب يُثبَّت توقيت الانتقال)، وعند الإعادة إلى
/// متداول تُطبَّق حقول التجديد الموروثة (رقم الملف الجديد إلزامي عند إعادة الملف المشطوب).
/// </summary>
public class ExecutedStatusRequest : RenewalRequest
{
    /// <summary>الحالة المستهدفة: متداول (سلسلة فارغة) / منفذ / مشطوب.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>تاريخ الشطب عند الانتقال إلى «مشطوب» (نص حر؛ إن غاب يُحفظ توقيت الانتقال).</summary>
    public string? StruckOffDate { get; set; }

    /// <summary>المبلغ الذي دفعته الجهة العامة (صفة «منفذ عليها») أو المبلغ المودع (صفة «عرض وايداع»).</summary>
    public decimal? ExecutedPaidAmount { get; set; }

    /// <summary>عملة المبلغ المدفوع الأول (افتراضيًا ليرة سورية).</summary>
    public string? ExecutedPaidCurrency { get; set; }

    /// <summary>المبلغ المدفوع الثاني (اختياري) في وضع «منفذ عليه» / «عرض وايداع»، بعملته الخاصة.</summary>
    public decimal? ExecutedPaidAmount2 { get; set; }

    /// <summary>عملة المبلغ المدفوع الثاني (افتراضيًا ليرة سورية).</summary>
    public string? ExecutedPaidCurrency2 { get; set; }

    /// <summary>المبلغ المدفوع الثالث (اختياري) في وضع «منفذ عليه» / «عرض وايداع»، بعملته الخاصة.</summary>
    public decimal? ExecutedPaidAmount3 { get; set; }

    /// <summary>عملة المبلغ المدفوع الثالث (افتراضيًا ليرة سورية).</summary>
    public string? ExecutedPaidCurrency3 { get; set; }

    /// <summary>كيفية تنفيذ الملف (صفة «الجهة العامة منفذ عليها» فقط).</summary>
    public string? ExecutedDescription { get; set; }

    /// <summary>تاريخ ايداعه حساب الجهة العامة (صفة «عرض وايداع» فقط).</summary>
    public string? ExecutedDepositDate { get; set; }

    /// <summary>تاريخ التنفيذ (صفة «الجهة العامة منفذ عليها» فقط، نص حر يُفسَّر بصيغ «1/8/2026»).</summary>
    public string? ExecutedExecutionDate { get; set; }

    /// <summary>رقم كتاب الجهة العامة بالسير بالملف عند إرجاع «عرض وايداع» من «منفذ» إلى «متداول» (إلزامي).</summary>
    public string? SayerNumber { get; set; }

    /// <summary>تاريخ كتاب الجهة العامة بالسير بالملف عند الإرجاع من «منفذ» إلى «متداول» (إلزامي).</summary>
    public string? SayerDate { get; set; }

    /// <summary>رقم ورود كتاب بالسير بالملف عند الإرجاع من «منفذ» إلى «متداول» (إلزامي).</summary>
    public string? SayerRegNumber { get; set; }

    /// <summary>تاريخ ورود كتاب بالسير بالملف عند الإرجاع من «منفذ» إلى «متداول» (إلزامي).</summary>
    public string? SayerRegDate { get; set; }
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
    public string? AnnexType { get; set; }
    public string? AnnexNumber { get; set; }
    public string? AnnexDate { get; set; }
    public string? InclusionText { get; set; }
    /// <summary>������� ����� �������� (�������� ������) ��� �������� � �� �� �������.</summary>
    public string? InclusionNotes { get; set; }

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
    /// <summary>
    /// دائرة التنفيذ (سجل الدوائر): إلزامية للجديد عدا ملفات المناب الخارجي
    /// (IsExternal=true). الخادم يشتق Court/CourtNorm من الدائرة ويتجاهل نص العميل دائمًا.
    /// </summary>
    public int? ExecutionCircuitId { get; set; }
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
    public List<AssetDto> Assets { get; set; } = new();

    /// <summary>ورثة المقترض المتوفى (إن وُجدوا)؛ الصفوف بلا اسم تُتجاهل عند الحفظ.</summary>
    public List<HeirDto> BorrowerHeirs { get; set; } = new();

    /// <summary>
    /// صفة الملف (GeneralEntitySideCatalog): تُثبَّت عند الإنشاء ولا تُغيّر عند التعديل.
    /// في وضع «منفذ عليه» يُشترط عادي فقط (لا مصرفي) وتُلغى بيانات المقترض والكفلاء والأموال.
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

    /// <summary>المبلغ الذي دفعته الجهة العامة في وضع «منفذ عليه» / المبلغ المودع في «عرض وايداع».</summary>
    public decimal? ExecutedPaidAmount { get; set; }

    /// <summary>عملة المبلغ المدفوع الأول (افتراضيًا ليرة سورية).</summary>
    public string? ExecutedPaidCurrency { get; set; }

    /// <summary>المبلغ المدفوع الثاني (اختياري) في وضع «منفذ عليه» / «عرض وايداع».</summary>
    public decimal? ExecutedPaidAmount2 { get; set; }

    /// <summary>عملة المبلغ المدفوع الثاني (افتراضيًا ليرة سورية).</summary>
    public string? ExecutedPaidCurrency2 { get; set; }

    /// <summary>المبلغ المدفوع الثالث (اختياري) في وضع «منفذ عليه» / «عرض وايداع».</summary>
    public decimal? ExecutedPaidAmount3 { get; set; }

    /// <summary>عملة المبلغ المدفوع الثالث (افتراضيًا ليرة سورية).</summary>
    public string? ExecutedPaidCurrency3 { get; set; }

    /// <summary>تاريخ ايداعه حساب الجهة العامة في وضع «عرض وايداع» (نص حر يُفسَّر بصيغ «1/8/2026»).</summary>
    public string? ExecutedDepositDate { get; set; }

    /// <summary>تاريخ التنفيذ في وضع «الجهة العامة منفذ عليها» (نص حر يُفسَّر بصيغ «1/8/2026»).</summary>
    public string? ExecutedExecutionDate { get; set; }

    /// <summary>تاريخ قرار الإحالة القطعية في «منفذ جبريا» (نص حر).</summary>
    public string? ForcedExecutionDate { get; set; }

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
