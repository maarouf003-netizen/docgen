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
    List<string> Branches);

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
    List<HeirDto>? Heirs = null);

/// <summary>
/// وريث لمنفذ عليه متوفى. القيمة الفارغة في الحقلين (AddressType أو Address) تُلغى
/// السابقة «عنوانه/يمثله» في المستندات فيُذكر الاسم الثلاثي للوريث فقط.
/// </summary>
public record HeirDto(
    int? Id,
    string? Name,
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
    List<ExecutedHeirDto>? Heirs = null);

/// <summary>الجهة العامة المنفذ عليها في وضع «منفذ عليه».</summary>
public record ExecutedPublicEntityDto(
    int? Id,
    string? EntityName,
    string? EntityBranch);

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

    /// <summary>تاريخ ورود الملف في وضع «منفذ عليه» (نص حر يُفسَّر بصيغ «1/8/2026» ويغذي فترة إحصائية «متداول للضد»).</summary>
    public string? FileReceiptDate { get; set; }

    /// <summary>المبلغ المطلوب دفعه من الجهة العامة في وضع «منفذ عليه».</summary>
    public decimal? ExecutedRequiredAmount { get; set; }

    /// <summary>المبلغ الذي دفعته الجهة العامة في وضع «منفذ عليه».</summary>
    public decimal? ExecutedPaidAmount { get; set; }

    /// <summary>طالبو التنفيذ في وضع «منفذ عليه».</summary>
    public List<ExecutionApplicantDto> ExecutionApplicants { get; set; } = new();

    /// <summary>الجهات العامة المنفذ عليها في وضع «منفذ عليه».</summary>
    public List<ExecutedPublicEntityDto> ExecutedPublicEntities { get; set; } = new();

    /// <summary>الأشخاص الطبيعيون المنفذ عليهم في وضع «منفذ عليه».</summary>
    public List<ExecutedNaturalPersonDto> ExecutedNaturalPersons { get; set; } = new();

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
/// </summary>
public record RotationDocumentDto(
    int DocumentId,
    string? Court,
    string? BorrowerName,
    string? BorrowerFather,
    string? BorrowerFamily,
    string? FileNumber,
    string? FileType,
    string? BaseNumber);

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
    public decimal? CollectedAmount { get; set; }
    /// <summary>صفة الملف (GeneralEntitySideCatalog): applicant = طالبة تنفيذ، executed = منفذ عليه.</summary>
    public string? GeneralEntitySide { get; set; }
    /// <summary>التسمية العربية للصفة.</summary>
    public string? GeneralEntitySideLabel { get; set; }
    public string? ExecutedStatus { get; set; }
    public string? ExecutedDescription { get; set; }
    public DateTime? FileReceiptDate { get; set; }
    public decimal? ExecutedRequiredAmount { get; set; }
    public decimal? ExecutedPaidAmount { get; set; }
    public DateTime? StruckOffDate { get; set; }
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
        DisplayFileNumber = CurrentBaseNumberOf(d) ?? d.FileNumber,
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
        GeneralEntitySide = d.GeneralEntitySide,
        GeneralEntitySideLabel = GeneralEntitySideCatalog.ToLabel(d.GeneralEntitySide),
        ExecutedStatus = d.ExecutedStatus,
        ExecutedDescription = d.ExecutedDescription,
        FileReceiptDate = d.FileReceiptDate,
        ExecutedRequiredAmount = d.ExecutedRequiredAmount,
        ExecutedPaidAmount = d.ExecutedPaidAmount,
        StruckOffDate = d.StruckOffDate,
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
        NeedsRotation = NeedsRotationOf(d),
        Guarantors = d.Guarantors
            .OrderBy(g => g.GuarantorNumber)
            .Select(g => new GuarantorDto(g.Id, g.GuarantorNumber, g.GuarantorName, g.GuarantorFather,
                g.GuarantorFamily, g.GuarantorMother, g.GuarantorBirth, g.GuarantorRegister,
                g.GuarantorNationalId, g.GuarantorAddress, g.AddressType,
                ToHeirDtos(d.Heirs, g.GuarantorNumber)))
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
                ToExecutedHeirDtos(d.ExecutedHeirs, a.Id, null)))
            .ToList(),
        ExecutedPublicEntities = d.ExecutedPublicEntities
            .Select(e => new ExecutedPublicEntityDto(e.Id, e.EntityName, e.EntityBranch))
            .ToList(),
        ExecutedNaturalPersons = d.ExecutedNaturalPersons
            .Select(p => new ExecutedNaturalPersonDto(p.Id, p.Name, p.Father, p.Family,
                p.AddressType, p.AddressOrRepresentative, p.RepresentationType,
                p.DeceasedName, p.DeceasedFather, p.DeceasedFamily,
                ToExecutedHeirDtos(d.ExecutedHeirs, null, p.Id)))
            .ToList(),
    };

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
    /// </summary>
    private static bool NeedsRotationOf(Document d)
    {
        var currentYear = DateTime.Today.Year;
        return !d.IsDraft
            && !ExecutionStatusCatalog.IsExecuted(d.ExecStatus, d.ExecSubStatus)
            && d.BaseNumbers.Any(b => b.Year < currentYear)
            && !d.BaseNumbers.Any(b => b.Year == currentYear);
    }

    /// <summary>
    /// ورثة منفذ عليه محدد (GuarantorNumber = null يعني المقترض)، مرتبة بترتيب الإدخال.
    /// </summary>
    private static List<HeirDto> ToHeirDtos(IEnumerable<Heir> heirs, int? guarantorNumber) =>
        heirs.Where(h => h.GuarantorNumber == guarantorNumber)
            .Select(h => new HeirDto(h.Id, h.HeirName, h.AddressType, h.HeirAddress))
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
