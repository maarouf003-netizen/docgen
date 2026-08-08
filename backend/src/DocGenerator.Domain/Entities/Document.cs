using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

public class Document
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedById { get; set; }
    public int? BranchId { get; set; }

    /// <summary>
    /// حذف منطقي: يُرفع بدل الحذف الفيزيائي لإبقاء المستند وتوابعه في القاعدة،
    /// ويُستثنى تلقائياً من كل الاستعلامات عبر Query Filter.
    /// </summary>
    public bool IsDeleted { get; set; }
    /// <summary>
    /// لحظة الحذف المنطقي (UTC)، تُصفَّر عند الاستعادة.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    public string? DocumentType { get; set; }
    public bool IsDraft { get; set; } = true;

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

    public decimal AmountNumeric { get; set; }
    public string? AmountWords { get; set; }
    public string? Currency { get; set; } = "ليرة سورية";
    public decimal Amount2Numeric { get; set; }
    public string? Amount2Words { get; set; }
    public string? Currency2 { get; set; } = "دولار أمريكي";
    public decimal InclusionAmountNumeric { get; set; }
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
    public string? BranchName { get; set; }

    public string? ExecStatus { get; set; } = string.Empty;
    public string? ExecSubStatus { get; set; }
    public decimal? CollectedAmount { get; set; }

    /// <summary>
    /// صفة الملف الثابتة عند الإنشاء:
    /// Applicant = ملف «طالبة تنفيذ» (النموذج الحالي)، Executed = وضع «منفذ عليه» الجديد.
    /// تُثبَّت من أول حفظ ولا تتغير أثناء التعديل.
    /// </summary>
    public string GeneralEntitySide { get; set; } = GeneralEntitySideCatalog.Applicant;

    /// <summary>
    /// حالة وضع «منفذ عليه» (Executed): متداول (فارغة = لا حالة)، منفذ، مشطوب.
    /// معزولة تمامًا عن حالة نظام «طالبة تنفيذ» (ExecStatus/ExecSubStatus).
    /// </summary>
    public string? ExecutedStatus { get; set; } = string.Empty;

    /// <summary>وصف/بيان إضافي في وضع «منفذ عليه».</summary>
    public string? ExecutedDescription { get; set; }

    /// <summary>
    /// تاريخ ورود الملف في وضع «منفذ عليه»: لحظة إعلام الجهة (المحامي) بالملف للدفاع
    /// عنها. عمود مستقل يغذي فترة إحصائية «متداول للضد»، فلا يُستخدم تاريخ قيد الملف هنا
    /// لأن الملف يقيده الخصم وليس محامي الدولة.
    /// </summary>
    public DateTime? FileReceiptDate { get; set; }

    /// <summary>المبلغ المطلوب دفعه من الجهة العامة في وضع «منفذ عليه» (يغذي إحصائية «متداول للضد»).</summary>
    public decimal? ExecutedRequiredAmount { get; set; }

    /// <summary>المبلغ الذي دفعته الجهة العامة في وضع «منفذ عليه» (يغذي إحصائية «منفذ للضد»).</summary>
    public decimal? ExecutedPaidAmount { get; set; }

    /// <summary>
    /// لحظة الشطب (UTC) في وضع «منفذ عليه»: المشطوب يُخفى من القوائم والتصدير
    /// ويظهر في صفحة «الملفات المشطوبة» (StruckOffDocuments). يبقى التاريخ محفوظًا
    /// حتى بعد إعادة الملف إلى المتداول ليبقى سجل الشطب ظاهرًا في تفاصيل الملف.
    /// </summary>
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
    public string? FullData { get; set; }
    public string? SearchText { get; set; }
    public string? FilePath { get; set; }

    public int ViewCount { get; set; }
    public int PrintCount { get; set; }

    public User? CreatedBy { get; set; }
    public Branch? Branch { get; set; }
    public ICollection<Guarantor> Guarantors { get; set; } = new List<Guarantor>();
    public ICollection<RealEstate> RealEstates { get; set; } = new List<RealEstate>();
    public ICollection<Heir> Heirs { get; set; } = new List<Heir>();
    public ICollection<ExecutionAction> ExecutionActions { get; set; } = new List<ExecutionAction>();
    public DocumentRegistrationDate? RegistrationDate { get; set; }
    public ICollection<DocumentBaseNumber> BaseNumbers { get; set; } = new List<DocumentBaseNumber>();

    /// <summary>طالبو التنفيذ في وضع «منفذ عليه» (واحد أو أكثر).</summary>
    public ICollection<ExecutionApplicant> ExecutionApplicants { get; set; } = new List<ExecutionApplicant>();
    /// <summary>الجهات العامة المنفذ عليها في وضع «منفذ عليه» (واحد أو أكثر).</summary>
    public ICollection<ExecutedPublicEntity> ExecutedPublicEntities { get; set; } = new List<ExecutedPublicEntity>();
    /// <summary>الأشخاص الطبيعيون المنفذ عليهم في وضع «منفذ عليه» (واحد أو أكثر).</summary>
    public ICollection<ExecutedNaturalPerson> ExecutedNaturalPersons { get; set; } = new List<ExecutedNaturalPerson>();
    /// <summary>ورثة المورثين المتوفين في وضع «منفذ عليه» (مرتبطون بالملف والمورث).</summary>
    public ICollection<ExecutedHeir> ExecutedHeirs { get; set; } = new List<ExecutedHeir>();
}
