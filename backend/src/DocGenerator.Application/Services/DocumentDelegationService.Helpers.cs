using System.Globalization;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentDelegationService : IDocumentDelegationService
{
    // ── أدوات مساعدة ─────────────────────────────────────────────

    internal static void ValidateSourceForDelegation(Document source)
    {
        // حراس NeedsRegistration الصريحة: تُمنع على المعلق — تسطير/اعتماد إنابة حيث تلزم الهوية.
        if (source.NeedsRegistration)
            throw new ArgumentException("لا يمكن تسطير إنابة على ملف بانتظار إعادة القيد — أدخل رقمه الجديد أولًا");
        // الملف تحت رفع (بلا رقم قيد) لا يُسطَّر عليه إنابة — الرقم ضروري لرابطة المصدر/الهدف.
        if (source.IsDraft)
            throw new ArgumentException("لا يمكن تسطير إنابة على ملف تحت رفع");
        // الإنابة على ملفات «طالبة تنفيذ» فقط (البيع بالمزاد والأموال المرهونة موجودة فيها حصرًا).
        if (GeneralEntitySideCatalog.IsExecutedLike(source.GeneralEntitySide))
            throw new ArgumentException("الإنابة تخص ملفات «الجهة العامة طالبة التنفيذ» فقط");
        if (source.ExecStatus == ExecutionStatusCatalog.Deferred)
            throw new ArgumentException("لا يمكن تسطير انابة في ملف تريث");
        if (source.ExecStatus == ExecutionStatusCatalog.ReferredToStart)
            throw new ArgumentException("لا يمكن تسطير انابة في ملف «محال الى البداية»");
        if (ExecutionStatusCatalog.IsExecuted(source.ExecStatus, source.ExecSubStatus)
            || source.ExecStatus == ExecutionStatusCatalog.StateStruckOff)
            throw new ArgumentException("لا يمكن تسطير إنابة على ملف منفَّذ أو مشطوب");
    }

    private void EnsurePendingByOwner(DocumentDelegation delegation, int userId)
        => DocumentDelegationRules.EnsurePendingByOwner(delegation, userId);

    internal static void ApplyDelegationAssets(DocumentDelegation delegation, Document source, List<int>? assetIds)
    {
        var ids = (assetIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new ArgumentException("يجب اختيار الأموال موضوع الإنابة");

        var sourceIds = new HashSet<int>(source.Assets.Select(a => a.Id));
        foreach (var id in ids)
            if (!sourceIds.Contains(id))
                throw new ArgumentException("أصل من الأموال موضوع الإنابة لا يتبع الملف المنيب");

        delegation.Assets.Clear();
        foreach (var asset in source.Assets.Where(a => ids.Contains(a.Id)))
        {
            delegation.Assets.Add(new DelegationAsset
            {
                AssetKind = asset.AssetKind,
                AssetLabel = AssetDisplay.Label(asset),
            });
        }
    }

    /// <summary>
    /// لقطة مجمّدة من الملف المنيب إلى الملف المناب (منفصلة عنه): أطرافه (المقترض والكفلاء
    /// والجهات العامة وورثتهم) وبيانات سنده وكتبه — فلا يظهر أي صف أطراف أو كتاب فارغًا
    /// على الملف المناب رغم وجوده على الملف المنيب. الإنابة تخص ملفات «طالبة تنفيذ» فقط
    /// (ValidateSourceForDelegation)، فأطرافها هي: المقترض والكفلاء والورثة والجهات العامة.
    /// </summary>
    internal static void CopySourcePartiesAndBond(Document source, Document target)
    {
        CopyBorrower(source, target);
        CopyBond(source, target);
        CopyBooks(source, target);

        // الكفلاء.
        foreach (var g in source.Guarantors)
            target.Guarantors.Add(CopyGuarantor(g));

        // ورثة المقترض/الكفلاء.
        foreach (var h in source.Heirs)
            target.Heirs.Add(CopyHeir(h));

        // الجهات العامة طالبة التنفيذ — مع ربط السجل المرجعي ونسخة التسريع للفلترة.
        foreach (var e in source.ApplicantPublicEntities)
            target.ApplicantPublicEntities.Add(new ApplicantPublicEntity
            {
                Name = e.Name,
                Branch = e.Branch,
                Governorate = e.Governorate,
                RegistryId = e.RegistryId,
            });
        target.ApplicantRegistryId = ApplicantRegistryIdDeriver.Derive(target);
    }

    private static void CopyBorrower(Document source, Document target)
    {
        target.BorrowerName = source.BorrowerName;
        target.BorrowerFather = source.BorrowerFather;
        target.BorrowerFamily = source.BorrowerFamily;
        target.BorrowerMother = source.BorrowerMother;
        target.BorrowerBirth = source.BorrowerBirth;
        target.BorrowerRegister = source.BorrowerRegister;
        target.BorrowerNationalId = source.BorrowerNationalId;
        target.BorrowerAddress = source.BorrowerAddress;
        target.BorrowerAddressType = source.BorrowerAddressType;
        target.BorrowerNature = source.BorrowerNature;
        target.BorrowerRegistrationNumber = source.BorrowerRegistrationNumber;
        target.BorrowerRepresentedBy = source.BorrowerRepresentedBy;

        target.BorrowerRepresentativeName = source.BorrowerRepresentativeName;
        target.BorrowerRepresentativeFather = source.BorrowerRepresentativeFather;
        target.BorrowerRepresentativeFamily = source.BorrowerRepresentativeFamily;
        target.BorrowerRepresentativeCapacity = source.BorrowerRepresentativeCapacity;
        target.BorrowerRepresentativeAddressType = source.BorrowerRepresentativeAddressType;
        target.BorrowerRepresentativeAddress = source.BorrowerRepresentativeAddress;
    }

    private static void CopyBond(Document source, Document target)
    {
        target.ContractType = source.ContractType;
        target.ContractTypeSelector = source.ContractTypeSelector;
        target.ContractNumber = source.ContractNumber;
        target.ContractDate = source.ContractDate;
        target.Annexes.Clear();
        foreach (var annex in source.Annexes.OrderBy(a => a.Sequence))
        {
            target.Annexes.Add(new DocumentAnnex
            {
                Sequence = annex.Sequence,
                AnnexType = annex.AnnexType,
                AnnexNumber = annex.AnnexNumber,
                AnnexDate = annex.AnnexDate,
            });
        }
        target.InclusionText = source.InclusionText;
        target.InclusionNotes = source.InclusionNotes;

        target.AmountNumeric = source.AmountNumeric;
        target.AmountWords = source.AmountWords;
        target.Currency = source.Currency;
        target.Amount2Numeric = source.Amount2Numeric;
        target.Amount2Words = source.Amount2Words;
        target.Currency2 = source.Currency2;
        target.Amount3Numeric = source.Amount3Numeric;
        target.Amount3Words = source.Amount3Words;
        target.Currency3 = source.Currency3;
        target.InclusionAmountNumeric = source.InclusionAmountNumeric;
        target.InclusionAmountWords = source.InclusionAmountWords;
        target.InclusionCurrency = source.InclusionCurrency;
        target.InclusionAmount2Numeric = source.InclusionAmount2Numeric;
        target.InclusionAmount2Words = source.InclusionAmount2Words;
        target.InclusionCurrency2 = source.InclusionCurrency2;
        target.InclusionAmount3Numeric = source.InclusionAmount3Numeric;
        target.InclusionAmount3Words = source.InclusionAmount3Words;
        target.InclusionCurrency3 = source.InclusionCurrency3;

        // الدائرة خارج النسخ: دائرة المناب هي الدائرة المنابة المسجَّل فيها (تُضبط عند
        // الاعتماد)، لا دائرة المنيب — هوية مستقلة كباقي حقول الملف (النوع/الرقم).
        target.Applicant = source.Applicant;
    }

    /// <summary>
    /// كتب الملف المنيب التي تنتقل معه إلى الملف المناب: ورود الملف وكتاب الجهة العامة
    /// ورقم تحت رفع وتاريخ إلقاء الحجز. حقلّا «ورود الإخطار التنفيذي»
    /// (FileReceiptNumber/FileReceiptDate) خاصان بوضع «منفذ عليه» ويُصفَّران على طالبة
    /// تنفيذ (DocumentUpsertMapper.cs)، فهما خارج عقد المرآة (B6) ولا يُنسخان هنا.
    /// </summary>
    private static void CopyBooks(Document source, Document target)
    {
        target.FileArrivalNumber = source.FileArrivalNumber;
        target.FileArrivalDate = source.FileArrivalDate;
        target.FileIncoming = source.FileIncoming;
        target.FileIncomingDate = source.FileIncomingDate;
        target.UnderFilingNumber = source.UnderFilingNumber;
        target.SeizureDate = source.SeizureDate;
    }

    /// <summary>نسخ كامِل للكفيل (تُستخدمه المرآة لنسخ الكفيل الجديد على المنيب، وللإنشاء بنفس البنية).</summary>
    internal static Guarantor CopyGuarantor(Guarantor g) => new()
    {
        GuarantorNumber = g.GuarantorNumber,
        GuarantorName = g.GuarantorName,
        GuarantorFather = g.GuarantorFather,
        GuarantorFamily = g.GuarantorFamily,
        GuarantorMother = g.GuarantorMother,
        GuarantorBirth = g.GuarantorBirth,
        GuarantorRegister = g.GuarantorRegister,
        GuarantorNationalId = g.GuarantorNationalId,
        GuarantorAddress = g.GuarantorAddress,
        AddressType = g.AddressType,
        GuarantorNature = g.GuarantorNature,
        GuarantorRegistrationNumber = g.GuarantorRegistrationNumber,
        GuarantorRepresentedBy = g.GuarantorRepresentedBy,
        RepresentativeName = g.RepresentativeName,
        RepresentativeFather = g.RepresentativeFather,
        RepresentativeFamily = g.RepresentativeFamily,
        RepresentativeCapacity = g.RepresentativeCapacity,
        RepresentativeAddressType = g.RepresentativeAddressType,
        RepresentativeAddress = g.RepresentativeAddress,
    };

    /// <summary>نسخ كامِل للوريث (تُستخدمه المرآة لإضافة الوريث الناقص، وللإنشاء بنفس البنية).</summary>
    internal static Heir CopyHeir(Heir h) => new()
    {
        GuarantorNumber = h.GuarantorNumber,
        HeirName = h.HeirName,
        HeirFather = h.HeirFather,
        HeirFamily = h.HeirFamily,
        HeirCapacity = h.HeirCapacity,
        AddressType = h.AddressType,
        HeirAddress = h.HeirAddress,
        RepresentativeName = h.RepresentativeName,
        RepresentativeFather = h.RepresentativeFather,
        RepresentativeFamily = h.RepresentativeFamily,
        RepresentativeCapacity = h.RepresentativeCapacity,
        RepresentativeAddressType = h.RepresentativeAddressType,
        RepresentativeAddress = h.RepresentativeAddress,
    };

    internal static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// تطهير حقول «التريث» و«كتاب السير بالملف» عند التفعيل التلقائي «منفذ جبريا (منفذ جزئيا)»
    /// — بنفس معاملة مسار «تغيير الحالة» اليدوية (منفذ جبريا ينظف حقول التريث والسير).
    /// </summary>
    internal static void ClearTarithAndSayerFields(Document source)
    {
        source.TarithNumber = null;
        source.TarithDate = null;
        source.TarithRegNumber = null;
        source.TarithRegDate = null;
        source.SayerNumber = null;
        source.SayerDate = null;
        source.SayerRegNumber = null;
        source.SayerRegDate = null;
    }

    internal static string SerializeDetails(Dictionary<string, string> details) =>
        JsonSerializer.Serialize(details);

    /// <summary>
    /// RF-017 (INT-005): بدلات المبيع قبل/بعد لكل أصل — تُلحَق بتفاصيل تدقيق الإتمام
    /// (المتتبع الحقلي يغطي كيان المستند لا أصول الإنابة). نفس تنسيق المتتبع الثابت.
    /// </summary>
    internal static string FormatSalePrices(
        DocumentDelegation delegation, Dictionary<int, decimal?> oldPrices)
        => string.Join("؛ ", delegation.Assets
            .OrderBy(a => a.Id)
            .Select(a =>
            {
                var oldText = oldPrices.TryGetValue(a.Id, out var old) && old.HasValue
                    ? old.Value.ToString("0.########", CultureInfo.InvariantCulture)
                    : "بلا";
                var newText = a.SalePrice?.ToString("0.########", CultureInfo.InvariantCulture) ?? "بلا";
                return $"{a.AssetLabel}: {oldText} ← {newText}";
            }));
}
