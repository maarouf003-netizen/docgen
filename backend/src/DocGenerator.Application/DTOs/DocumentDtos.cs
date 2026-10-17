using DocGenerator.Application.Common;
using System.Text.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.DTOs;

public class DocumentResponse : IDocumentSheetFields
{
    // تحقيق صريح لحقول القوائم في `IDocumentSheetFields`: `List<T>` لا يطابق
    // `IReadOnlyList<I>` في توقيع التنفيذ (CS0738) رغم التحويل الضمني — وهذه
    // الأعضاء الصريحة غير مرئية للتسلسل `JSON` فلا يتغير عقد الواجهة.
    IReadOnlyList<IExportApplicant> IDocumentSheetFields.ExecutionApplicants => ExecutionApplicants;
    IReadOnlyList<IExportPublicEntity> IDocumentSheetFields.ExecutedPublicEntities => ExecutedPublicEntities;
    IReadOnlyList<IExportNaturalPerson> IDocumentSheetFields.ExecutedNaturalPersons => ExecutedNaturalPersons;
    IReadOnlyList<IExportAction> IDocumentSheetFields.ExecutionActions => ExecutionActions;

    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    /// <summary>عدّاد التزامن المتفائل (RF-010) — يُعاد إرساله في `Version` عند الحفظ.</summary>
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
    public string? AnnexType { get; set; }
    public string? AnnexNumber { get; set; }
    public string? AnnexDate { get; set; }
    public string? InclusionText { get; set; }
    /// <summary>������� ����� �������� (�������� ������) ��� �������� � �� �� �������.</summary>
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
    /// <summary>دائرة التنفيذ المرجعية (سجل الدوائر) — تُعرض نصًا عبر Court للتوافق.</summary>
    public int? ExecutionCircuitId { get; set; }
    /// <summary>شعبة الدائرة المالكة (`null` = ملك القسم) — للنطاق والعرض.</summary>
    public int? SectionId { get; set; }
    /// <summary>اسم الشعبة المالكة — للعرض فقط.</summary>
    public string? SectionName { get; set; }
    /// <summary>علم «بانتظار إعادة القيد» — بانتظار المحامي لإدخال الرقم الجديد.</summary>
    public bool NeedsRegistration { get; set; }
    public string? Applicant { get; set; }
    /// <summary>نسخة تسريع: معرّف قيد أول جهة طالب مرتبطة بالسجل (للفلترة في البوابة).</summary>
    public int? ApplicantRegistryId { get; set; }
    public string? Lawyer { get; set; }
    public string? ReferredFromLawyer { get; set; }
    public DateTime? ReferredAt { get; set; }
    public string? FileNumber { get; set; }
    /// <summary>
    /// الرقم الظاهر للمستخدم: آخر رقم أساس ≤ سنة اليوم (إن وُجد)، وإلا رقم الملف الأصلي.
    /// (رقم الملف الأصلي يبقى في FileNumber للتحرير والسجل التاريخي.)
    /// </summary>
    public string? DisplayFileNumber { get; set; }
    /// <summary>سنة الرقم الفعّال المرافقة لـ DisplayFileNumber — يُحسبان معًا من السجل نفسه، فلا يُعرض أحدهما مع سنة/رقم الزوج الآخر.</summary>
    public string? DisplayFileYear { get; set; }
    public string? FileType { get; set; }
    public string? FileYear { get; set; }
    public string? FileIncoming { get; set; }
    public string? FileIncomingDate { get; set; }
    public string? UnderFilingNumber { get; set; }
    public string? FileRegistrationDate { get; set; }
    public string? BranchName { get; set; }
    /// <summary>اسم الفرع الإداري للملف (فرع المستخدم الذي أدخله) عبر BranchId.</summary>
    public string? AdministrativeBranchName { get; set; }
    /// <summary>حالة العرض الموحدة المشتقة من الخلفية (المصدر الوحيد للقواعد) — تعرضها الواجهة كما هي.</summary>
    public string DisplayStatus { get; set; } = string.Empty;

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
    public string? ExecutedPaidCurrency { get; set; }
    public decimal? ExecutedPaidAmount2 { get; set; }
    public string? ExecutedPaidCurrency2 { get; set; }
    public decimal? ExecutedPaidAmount3 { get; set; }
    public string? ExecutedPaidCurrency3 { get; set; }
    /// <summary>تاريخ ايداعه حساب الجهة العامة في وضع «عرض وايداع».</summary>
    public DateTime? ExecutedDepositDate { get; set; }
    /// <summary>تاريخ التنفيذ في وضع «الجهة العامة منفذ عليها».</summary>
    public DateTime? ExecutedExecutionDate { get; set; }
    /// <summary>تاريخ قرار الإحالة القطعية في «منفذ جبريا» (نص حر).</summary>
    public string? ForcedExecutionDate { get; set; }
    /// <summary>تاريخ تحويل بدل المبيع للجهة العامة عند «اعتبار الملف منفذًا كاملًا بهذا البيع» — نص حر يُعرَض «yyyy-MM-dd» (مثل بقية حقول التخزين الزمني الحر).</summary>
    public string? ForcibleTransferDate { get; set; }
    /// <summary>رقم الإشعار (اختياري) عند تحويل بدل المبيع للجهة العامة.</summary>
    public string? ForcibleTransferNoticeNumber { get; set; }
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
    /// <summary>رقم كتاب المطالعة بعدم وجود أموال للتنفيذ عليها في «محال الى البداية».</summary>
    public string? NoFundsDemandNumber { get; set; }
    /// <summary>تاريخ كتاب المطالعة بعدم وجود أموال للتنفيذ عليها — نص حر يُعرَض «yyyy-MM-dd» (مثل بقية حقول التخزين الزمني الحر).</summary>
    public string? NoFundsDemandDate { get; set; }
    /// <summary>رقم كتاب الإحالة في «محال الى البداية».</summary>
    public string? StartReferralNumber { get; set; }
    /// <summary>تاريخ كتاب الإحالة — نص حر يُعرَض «yyyy-MM-dd» (مثل بقية حقول التخزين الزمني الحر).</summary>
    public string? StartReferralDate { get; set; }
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
    /// <summary>معرّفات الأموال المباعة بالمزاد العلني في «منفذ جبريا» (من أموال الملف، عدا كفالة الرواتب).</summary>
    public List<int> SoldAssetIds { get; set; } = new();
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
    /// <summary>
    /// صحيح إذا كان على الملف استئناف منظور واحد على الأقل — تُعرض بجانب نتيجة البحث
    /// شارة «استئناف»، ويفتح النقر عليها تفاصيل الاستئناف (MatchedAppealId).
    /// المحسوم والمشطوب لا يُظهران الشارة.
    /// </summary>
    public bool HasAppeals { get; set; }
    /// <summary>معرف أول استئناف منظور على الملف عند وجوده (للانتقال إلى تفاصيل الاستئناف).</summary>
    public int? MatchedAppealId { get; set; }
    /// <summary>
    /// معرف الإنابة التي صدر عنها هذا الملف كمناب (غير فارغ ⇒ الملف «مرآة» للمنيب):
    /// تُقفَل حقول المزامنة أماميًا وخلفيًا، وتُحفظ الإضافات المحلية (وريث/ممثل) مع تنبيه للمنيب.
    /// </summary>
    public int? SourceDelegationId { get; set; }
    public List<GuarantorDto> Guarantors { get; set; } = new();
    public List<AssetDto> Assets { get; set; } = new();
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

    /// <summary>فكّ قائمة معرّفات الأموال المباعة من JSON المخزن (أو قائمة فارغة عند العطب).</summary>
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
    /// قاعدة اللون الأحمر المعتمدة: يُلوَّن رقم الملف فقط إذا كان الملف مقيدًا
    /// (ليس تحت رفع)، غير منفَّذ، وسبق أن دُوِّر في سنة سابقة، ولم يُدوَّر في السنة الحالية.
    /// ولعائلتي وضع «منفذ عليه» (Executed + Deposit) يُؤخذ «غير منفَّذ» بمعناه العائلي:
    /// متداولة فقط (لا منفذ ولا مشطوب) — مطابقًا لشرط أهليتهما في قائمة التدوير والحفظ.
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
