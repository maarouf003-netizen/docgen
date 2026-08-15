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

    /// <summary>
    /// الممثل الشرعي للمقترض (ولي/وصي/قيم) إن وُجد: اسمه الثلاثي وصفته وعنوانه.
    /// عند وجوده يصبح عنوان الممثل هو المعتبر ويُخفى عنوان المقترض نفسه.
    /// </summary>
    public string? BorrowerRepresentativeName { get; set; }
    public string? BorrowerRepresentativeFather { get; set; }
    public string? BorrowerRepresentativeFamily { get; set; }
    /// <summary>صفة الممثل الشرعي: ولي / وصي / قيم.</summary>
    public string? BorrowerRepresentativeCapacity { get; set; }
    /// <summary>نوع عنوان الممثل الشرعي: موطن مختار / عنوان / وكيل قانوني.</summary>
    public string? BorrowerRepresentativeAddressType { get; set; }
    /// <summary>عنوان الممثل الشرعي أو وكيله القانوني حسب نوع العنوان.</summary>
    public string? BorrowerRepresentativeAddress { get; set; }

    /// <summary>
    /// طبيعة المقترض/المنفذ عليه: شخص طبيعي (natural) أو شخص اعتباري (legal).
    /// عند الاعتباري يحمل BorrowerName اسم الشخص الاعتباري، وتُصفَّر حقول الهوية الطبيعية.
    /// </summary>
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

    public decimal AmountNumeric { get; set; }
    public string? AmountWords { get; set; }
    public string? Currency { get; set; } = "ليرة سورية";
    public decimal Amount2Numeric { get; set; }
    public string? Amount2Words { get; set; }
    public string? Currency2 { get; set; } = "دولار أمريكي";
    public decimal Amount3Numeric { get; set; }
    public string? Amount3Words { get; set; }
    public string? Currency3 { get; set; } = "ليرة سورية";
    public decimal InclusionAmountNumeric { get; set; }
    public string? InclusionAmountWords { get; set; }
    public string? InclusionCurrency { get; set; } = "ليرة سورية";
    public decimal InclusionAmount2Numeric { get; set; }
    public string? InclusionAmount2Words { get; set; }
    public string? InclusionCurrency2 { get; set; } = "ليرة سورية";
    public decimal InclusionAmount3Numeric { get; set; }
    public string? InclusionAmount3Words { get; set; }
    public string? InclusionCurrency3 { get; set; } = "ليرة سورية";

    public string? Court { get; set; }
    public string? Applicant { get; set; }
    public string? Lawyer { get; set; }

    /// <summary>
    /// اسم المحامي الذي أُحيل إليه الملف الذي نقله تلقائيًا باسمه عند النقل؛
    /// يُخزَّن من مصدر النقل (المحامي الأصلي) حتى يعرض المحامي الجديد ملاحظة
    /// «أُحيل لك هذا الملف من المحامي فلان الفلاني» في تفاصيل الملف.
    /// </summary>
    public string? ReferredFromLawyer { get; set; }

    /// <summary>لحظة إحالة الملف إلى المحامي الحالي (UTC)، تُثبَّت عند كل نقل.</summary>
    public DateTime? ReferredAt { get; set; }

    public string? FileNumber { get; set; }
    public string? FileType { get; set; }
    public string? FileYear { get; set; }
    public string? FileIncoming { get; set; }
    public string? FileIncomingDate { get; set; }
    public string? UnderFilingNumber { get; set; }

    /// <summary>رقم ورود الملف في وضع «الجهة العامة طالبة تنفيذ» (اختياري).</summary>
    public string? FileArrivalNumber { get; set; }

    /// <summary>تاريخ ورود الملف في وضع «الجهة العامة طالبة تنفيذ» (نص حر).</summary>
    public string? FileArrivalDate { get; set; }
    public string? BranchName { get; set; }

    public string? ExecStatus { get; set; } = string.Empty;
    public string? ExecSubStatus { get; set; }

    /// <summary>
    /// المبالغ المحصَّلة (حتى ثلاثة بعملاتها) في حالتي «منفذ بالتسوية» و«منفذ جبريا»:
    /// ليرة سورية افتراضيًا للأولى ثم دولار أمريكي ثم يورو (قاعدة «لا تكرار العملة»).
    /// </summary>
    public decimal? CollectedAmount { get; set; }
    public decimal? CollectedAmount2 { get; set; }
    public decimal? CollectedAmount3 { get; set; }
    public string? CollectedCurrency { get; set; } = "ليرة سورية";
    public string? CollectedCurrency2 { get; set; } = "دولار أمريكي";
    public string? CollectedCurrency3 { get; set; } = "يورو";

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
    /// تاريخ ورود الاخطار في وضع «منفذ عليه»: لحظة إعلام الجهة (المحامي) بالملف للدفاع
    /// عنها. عمود مستقل يغذي فترة إحصائية «متداول للضد»، فلا يُستخدم تاريخ قيد الملف هنا
    /// لأن الملف يقيده الخصم وليس محامي الدولة.
    /// </summary>
    public DateTime? FileReceiptDate { get; set; }

    /// <summary>رقم ورود الإخطار التنفيذي في وضع «منفذ عليه» (يُعرض قبل تاريخ ورود الاخطار).</summary>
    public string? FileReceiptNumber { get; set; }

    /// <summary>المبلغ المطلوب دفعه من الجهة العامة في وضع «منفذ عليه» (يغذي إحصائية «متداول للضد»).</summary>
    public decimal? ExecutedRequiredAmount { get; set; }

    /// <summary>عملة المبلغ المطلوب الأول في وضع «منفذ عليه» (افتراضيًا ليرة سورية).</summary>
    public string? ExecutedRequiredCurrency { get; set; } = "ليرة سورية";

    /// <summary>المبلغ المطلوب الثاني (اختياري) في وضع «منفذ عليه»، بعملته الخاصة.</summary>
    public decimal? ExecutedRequiredAmount2 { get; set; }

    /// <summary>عملة المبلغ المطلوب الثاني في وضع «منفذ عليه» (افتراضيًا ليرة سورية).</summary>
    public string? ExecutedRequiredCurrency2 { get; set; } = "ليرة سورية";

    /// <summary>المبلغ المطلوب الثالث (اختياري) في وضع «منفذ عليه»، بعملته الخاصة.</summary>
    public decimal? ExecutedRequiredAmount3 { get; set; }

    /// <summary>عملة المبلغ المطلوب الثالث في وضع «منفذ عليه» (افتراضيًا ليرة سورية).</summary>
    public string? ExecutedRequiredCurrency3 { get; set; } = "ليرة سورية";

    /// <summary>المبلغ الذي دفعته الجهة العامة في وضع «منفذ عليه» (يغذي إحصائية «منفذ للضد»).</summary>
    public decimal? ExecutedPaidAmount { get; set; }

    /// <summary>
    /// تاريخ ايداعه حساب الجهة العامة في وضع «عرض وايداع» (Deposit): لحظة إيداع المبلغ
    /// المعروض في حساب الجهة، يظهر عند الحالة «منفذ». يخص صفة العرض وحدها دون منفذ عليها.
    /// </summary>
    public DateTime? ExecutedDepositDate { get; set; }

    /// <summary>
    /// لحظة الشطب (UTC) في وضع «منفذ عليه»: المشطوب يُخفى من القوائم والتصدير
    /// ويظهر في صفحة «الملفات المشطوبة» (StruckOffDocuments). يبقى التاريخ محفوظًا
    /// حتى بعد إعادة الملف إلى المتداول ليبقى سجل الشطب ظاهرًا في تفاصيل الملف.
    /// </summary>
    public DateTime? StruckOffDate { get; set; }

    /// <summary>
    /// رقم ورود اخطار التجديد عند إعادة ملف مشطوب إلى المتداول (تجديد الملف). اختياري.
    /// </summary>
    public string? RenewalFileReceiptNumber { get; set; }

    /// <summary>تاريخ ورود اخطار التجديد عند إعادة الملف المشطوب (اختياري).</summary>
    public DateTime? RenewalFileReceiptDate { get; set; }

    /// <summary>
    /// رقم الملف الجديد عند إعادة الملف المشطوب (إلزامي): يدخله المحامي يدويًا ويُسجّل
    /// رقم أساس لسنة الإعادة الحالية فيُعرض كرقم الملف من بعد الإعادة.
    /// </summary>
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

    /// <summary>
    /// حقول «كتاب الجهة العامة بالسير بالملف» عند التراجع عن تريث أو عن التنفيذ
    /// (منفذ بالتسوية/جبريا): رقم وتاريخ الكتاب + رقم وتاريخ وروده.
    /// </summary>
    public string? SayerNumber { get; set; }
    public string? SayerDate { get; set; }
    public string? SayerRegNumber { get; set; }
    public string? SayerRegDate { get; set; }

    /// <summary>
    /// معرّفات العقارات المباعة بالمزاد العلني في حالة «منفذ جبريا» (JSON لقائمة int) —
    /// تُتحقق من عقارات الملف نفسه، وتُمسح عند التراجع.
    /// </summary>
    public string? SoldEstateIds { get; set; }

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
    /// <summary>الجهات العامة طالبة التنفيذ في وضع «الجهة العامة طالبة تنفيذ» (واحدة أو أكثر).</summary>
    public ICollection<ApplicantPublicEntity> ApplicantPublicEntities { get; set; } = new List<ApplicantPublicEntity>();
    /// <summary>سجل تعاقب المحامين على الملف (منشئ الملف + كل المحامين المتعاقبين مع تواريخ الإحالة).</summary>
    public ICollection<DocumentAssignment> Assignments { get; set; } = new List<DocumentAssignment>();
    /// <summary>الأشخاص الطبيعيون المنفذ عليهم في وضع «منفذ عليه» (واحد أو أكثر).</summary>
    public ICollection<ExecutedNaturalPerson> ExecutedNaturalPersons { get; set; } = new List<ExecutedNaturalPerson>();
    /// <summary>ورثة المورثين المتوفين في وضع «منفذ عليه» (مرتبطون بالملف والمورث).</summary>
    public ICollection<ExecutedHeir> ExecutedHeirs { get; set; } = new List<ExecutedHeir>();

    /// <summary>
    /// وقوعات الملف في وضع «منفذ عليه»/«عرض وايداع»: سجل زمني لكل شطب وتجديد
    /// (يسمح بتكرارهما عدة مرات)، مرتبة زمنيًا حسب تاريخ الحدث عند العرض.
    /// </summary>
    public ICollection<DocumentOccurrence> Occurrences { get; set; } = new List<DocumentOccurrence>();
}
