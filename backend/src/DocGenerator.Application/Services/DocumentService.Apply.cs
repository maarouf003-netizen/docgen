using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentService
{
    private static void ClearBaraetFields(Document doc)
    {
        doc.BaraetNumber = null;
        doc.BaraetDate = null;
        doc.BaraetRegNumber = null;
        doc.BaraetRegDate = null;
    }

    private static void ClearForcedExecutionField(Document doc)
    {
        doc.ForcedExecutionDate = null;
    }

    /// <summary>
    /// تطهير حقلي «تحويل بدل المبيع» (تاريخ التحويل ورقم الإشعار) عند ترك حالة «منفذ جبريا»
    /// (تسوية/تريث/شطب/تراجع) — يخصان «منفذ جبريا» حصرًا، فيُصفَّران بنفس منطق نظيرهما.
    /// </summary>
    private static void ClearForcibleTransferFields(Document doc)
    {
        doc.ForcibleTransferDate = null;
        doc.ForcibleTransferNoticeNumber = null;
    }

    /// <summary>
    /// مزامنة لقطات أصول الإنابات غير المنفذة للملف المنيب بعد تعديل أصوله: أصول الملف
    /// تُعاد بناؤها بمعرفات جديدة عند كل تعديل، فتُطابَق اللقطات مع الأصول الحالية بالنوع
    /// ثم الوصف — الأصل المطابق تمامًا (نوع + وصف) يبقى بلا تغيير، والأصل الذي تغيّر وصفه
    /// من النوع نفسه تُحدَّث لقطته وتُعلَّم بأن بياناته عُدّلت بعد التسطير (يظهر تنبيه في
    /// بطاقة «تشعبات الملف»). الإنابات المنفذة سجل نهائي بالبدل فلا تُمسّ لقطاتها.
    /// </summary>
    private async Task SyncDelegationSnapshotsForDocumentAsync(Document doc, CancellationToken token)
    {
        var delegations = await _delegations.ListBySourceAsync(doc.Id, token);
        var pending = delegations.Where(d => d.Status != DelegationStatusCatalog.Executed).ToList();
        if (pending.Count == 0)
            return;

        var remaining = doc.Assets.ToList();
        var changed = false;
        foreach (var snapshot in pending.SelectMany(d => d.Assets))
        {
            // تطابق تام (نوع + وصف): لم تتغير بيانات الأصل — تُستهلك وتُترك بلا تغيير.
            var exact = remaining.FindIndex(a => a.AssetKind == snapshot.AssetKind && AssetDisplay.Label(a) == snapshot.AssetLabel);
            if (exact >= 0)
            {
                remaining.RemoveAt(exact);
                continue;
            }
            // تعديل وصف أصل من النوع نفسه بعد التسطير: تُحدَّث اللقطة ويُعلَّم التعديل.
            var sameKind = remaining.FindIndex(a => a.AssetKind == snapshot.AssetKind);
            if (sameKind >= 0)
            {
                var assetLabel = AssetDisplay.Label(remaining[sameKind]);
                remaining.RemoveAt(sameKind);
                if (snapshot.AssetLabel != assetLabel)
                {
                    snapshot.AssetLabel = assetLabel;
                    snapshot.SnapshotAdjusted = true;
                    changed = true;
                }
            }
            // بلا أصل من النوع نفسه في الملف الحالي: تبقى اللقطة كما سُطّرت (سجل التسطير).
        }

        if (changed)
            await _uow.SaveChangesAsync(token);
    }

    private static void ClearTarithFields(Document doc)
    {
        doc.TarithNumber = null;
        doc.TarithDate = null;
        doc.TarithRegNumber = null;
        doc.TarithRegDate = null;
    }

    private static decimal? ParseCollectedAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        // يوحّد الأرقام العربية/الفارسية ثم فواصل الأرقام العربية (فاصل عشري ٫ وألوف ٬)
        // إلى ما يقبله التحليل؛ فلا يكسر ما يكتب بالأرقام ASCII (يمر كما هو).
        raw = ArabicDigitNormalizer.Normalize(raw)
            .Replace('\u066B', '.')   // ٫ الفاصل العشري العربي
            .Replace('\u066C', ',');  // ٬ فاصل الألوف العربي
        if (decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            || decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out parsed))
        {
            if (parsed < 0)
                throw new ArgumentException("المبلغ المحصل لا يمكن أن يكون سالباً");
            return parsed;
        }
        throw new ArgumentException("المبلغ المحصل غير صالح");
    }

    /// <summary>تطبيق المبالغ المحصلة (حتى ثلاثة بعملاتها) من حقول الطلب على المستند وسجل الوقعة.</summary>
    private static void ApplyCollectedAmounts(Document doc, Dictionary<string, string?> fields, Dictionary<string, string> details)
    {
        doc.CollectedAmount = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount"));
        doc.CollectedAmount2 = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount2"));
        doc.CollectedAmount3 = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount3"));
        var currency = fields.GetValueOrDefault("collectedCurrency");
        var currency2 = fields.GetValueOrDefault("collectedCurrency2");
        var currency3 = fields.GetValueOrDefault("collectedCurrency3");
        doc.CollectedCurrency = string.IsNullOrWhiteSpace(currency) ? "ليرة سورية" : currency.Trim();
        doc.CollectedCurrency2 = string.IsNullOrWhiteSpace(currency2) ? "دولار أمريكي" : currency2.Trim();
        doc.CollectedCurrency3 = string.IsNullOrWhiteSpace(currency3) ? "يورو" : currency3.Trim();
        if (doc.CollectedAmount.HasValue)
        {
            details["collectedAmount"] = doc.CollectedAmount.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            details["collectedCurrency"] = doc.CollectedCurrency;
        }
        if (doc.CollectedAmount2.HasValue)
        {
            details["collectedAmount2"] = doc.CollectedAmount2.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            details["collectedCurrency2"] = doc.CollectedCurrency2;
        }
        if (doc.CollectedAmount3.HasValue)
        {
            details["collectedAmount3"] = doc.CollectedAmount3.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            details["collectedCurrency3"] = doc.CollectedCurrency3;
        }
    }

    /// <summary>
    /// تطبيق الأموال المباعة بالمزاد العلني (إلزامية في «منفذ جبريا»): تُتحقق المعرّفات
    /// من أموال الملف نفسه (عدا كفالة الرواتب)، وتُخزَّن JSON، وتُضمَّن أسماؤها في سجل الوقعة للعرض.
    /// </summary>
    private static void ApplySoldAssets(Document doc, Dictionary<string, string?> fields, Dictionary<string, string> details)
    {
        var raw = (fields.GetValueOrDefault("soldAssetIds") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("اختر الأموال التي جرى بيعها بالمزاد العلني على الأقل");

        var ids = new List<int>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var id))
                throw new ArgumentException("معرّف مال مباع غير صالح");
            ids.Add(id);
        }
        var ownedIds = new HashSet<int>(doc.Assets
            .Where(a => AssetKindCatalog.IsAuctionable(a.AssetKind))
            .Select(a => a.Id));
        if (ids.Any(id => !ownedIds.Contains(id)))
            throw new ArgumentException("الأموال المختارة ليست من أموال الملف");

        doc.SoldAssetIds = SerializeJson(ids);
        details["soldAssetIds"] = string.Join(",", ids);
        var soldNames = doc.Assets
            .Where(a => ids.Contains(a.Id))
            .Select(AssetDisplayName)
            .Where(v => !string.IsNullOrWhiteSpace(v));
        details["soldAssetNames"] = string.Join("، ", soldNames);
    }

    /// <summary>تسمية قراءة للأصل (تُستخدم في «منفذ جبريا» وفي قوائم العرض).</summary>
    private static string AssetDisplayName(Asset a) => AssetDisplay.Label(a);

    private static void ClearSayerFields(Document doc)
    {
        doc.SayerNumber = null;
        doc.SayerDate = null;
        doc.SayerRegNumber = null;
        doc.SayerRegDate = null;
    }

    private static void ClearCollectedFields(Document doc)
    {
        doc.CollectedAmount = null;
        doc.CollectedAmount2 = null;
        doc.CollectedAmount3 = null;
    }

    private static void CopyDetail(Dictionary<string, string> details, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            details[key] = value;
    }

    private static string SerializeDetails(Dictionary<string, string> details) =>
        JsonSerializer.Serialize(details);

    private static string SerializeJson<T>(T value) => JsonSerializer.Serialize(value);

    private static void ApplyRequest(Document doc, DocumentUpsertRequest r)
    {
        // صفة الملف تُثبَّت عند الإنشاء ولا تُغيَّر عند التعديل (الموافقة المعتمدة):
        // على التعديل يبقى جانب الملف كما هو مهما أُرسل في الطلب.
        // (ValidateSide تطبَّعت القيمة مسبقًا إلى قيمة صالحة غير فارغة).
        if (doc.Id == 0 || doc.GeneralEntitySide == r.GeneralEntitySide)
            doc.GeneralEntitySide = r.GeneralEntitySide!;

        doc.DocumentType = r.DocumentType;
        doc.BorrowerName = r.BorrowerName;
        doc.BorrowerFather = r.BorrowerFather;
        doc.BorrowerFamily = r.BorrowerFamily;
        doc.BorrowerMother = r.BorrowerMother;
        doc.BorrowerBirth = r.BorrowerBirth;
        doc.BorrowerRegister = r.BorrowerRegister;
        doc.BorrowerNationalId = r.BorrowerNationalId;
        doc.BorrowerAddress = r.BorrowerAddress;
        doc.BorrowerAddressType = r.BorrowerAddressType;

        var borrowerHasRep = !IsEmptyRepresentative(r.BorrowerRepresentativeName, r.BorrowerRepresentativeFather, r.BorrowerRepresentativeFamily);
        doc.BorrowerRepresentativeName = borrowerHasRep ? (r.BorrowerRepresentativeName ?? string.Empty).Trim() : null;
        doc.BorrowerRepresentativeFather = borrowerHasRep ? (r.BorrowerRepresentativeFather ?? string.Empty).Trim() : null;
        doc.BorrowerRepresentativeFamily = borrowerHasRep ? (r.BorrowerRepresentativeFamily ?? string.Empty).Trim() : null;
        doc.BorrowerRepresentativeCapacity = borrowerHasRep ? NormalizeRepresentativeCapacity(r.BorrowerRepresentativeCapacity) : null;
        doc.BorrowerRepresentativeAddressType = borrowerHasRep ? NormalizeRepresentativeAddressType(r.BorrowerRepresentativeAddressType) : null;
        doc.BorrowerRepresentativeAddress = borrowerHasRep ? (r.BorrowerRepresentativeAddress ?? string.Empty).Trim() : null;

        // طبيعة المقترض: الاعتباري يحمل اسم الشخص الاعتباري في BorrowerName وتُصفَّر حقول الهوية
        // الطبيعية والممثل الشرعي والورثة (مفاهيم تخص الشخص الطبيعي)، ويُحتفظ برقم التسجيل ومن يمثله.
        // الطبيعي يُصفِّر الحقول الاعتبارية.
        doc.BorrowerNature = NormalizePartyNature(r.BorrowerNature);
        if (PartyNatureCatalog.IsLegal(doc.BorrowerNature))
        {
            doc.BorrowerFather = null;
            doc.BorrowerFamily = null;
            doc.BorrowerMother = null;
            doc.BorrowerBirth = null;
            doc.BorrowerRegister = null;
            doc.BorrowerNationalId = null;
            doc.BorrowerRegistrationNumber = (r.BorrowerRegistrationNumber ?? string.Empty).Trim();
            doc.BorrowerRepresentedBy = (r.BorrowerRepresentedBy ?? string.Empty).Trim();
            doc.BorrowerRepresentativeName = null;
            doc.BorrowerRepresentativeFather = null;
            doc.BorrowerRepresentativeFamily = null;
            doc.BorrowerRepresentativeCapacity = null;
            doc.BorrowerRepresentativeAddressType = null;
            doc.BorrowerRepresentativeAddress = null;
        }
        else
        {
            doc.BorrowerRegistrationNumber = null;
            doc.BorrowerRepresentedBy = null;
        }

        doc.ContractType = r.ContractType;
        doc.ContractTypeSelector = r.ContractTypeSelector;
        doc.ContractNumber = r.ContractNumber;
        doc.ContractDate = r.ContractDate;
        doc.AnnexType = r.AnnexType;
        doc.AnnexNumber = r.AnnexNumber;
        doc.AnnexDate = r.AnnexDate;
        doc.InclusionText = r.InclusionText;
        doc.AmountNumeric = r.AmountNumeric ?? 0;
        doc.AmountWords = r.AmountWords;
        doc.Currency = r.Currency;
        doc.Amount2Numeric = r.Amount2Numeric ?? 0;
        doc.Amount2Words = r.Amount2Words;
        doc.Currency2 = r.Currency2;
        doc.Amount3Numeric = r.Amount3Numeric ?? 0;
        doc.Amount3Words = r.Amount3Words;
        doc.Currency3 = r.Currency3;
        doc.InclusionAmountNumeric = r.InclusionAmountNumeric ?? 0;
        doc.InclusionAmountWords = r.InclusionAmountWords;
        doc.InclusionCurrency = r.InclusionCurrency;
        doc.InclusionAmount2Numeric = r.InclusionAmount2Numeric ?? 0;
        doc.InclusionAmount2Words = r.InclusionAmount2Words;
        doc.InclusionCurrency2 = r.InclusionCurrency2;
        doc.InclusionAmount3Numeric = r.InclusionAmount3Numeric ?? 0;
        doc.InclusionAmount3Words = r.InclusionAmount3Words;
        doc.InclusionCurrency3 = r.InclusionCurrency3;
        doc.Court = r.Court;
        // «طالب التنفيذ» في وضع «طالبة تنفيذ» يُشتق من قائمة الجهات (ApplicantPublicEntities)
        // في FillDerivedFields؛ ولا يُؤخذ نصيًا من الطلب بعد الآن.
        doc.FileNumber = r.FileNumber;
        doc.FileType = r.FileType;
        doc.FileYear = r.FileYear;
        doc.FileIncoming = r.FileIncoming;
        doc.FileIncomingDate = r.FileIncomingDate;
        doc.UnderFilingNumber = r.UnderFilingNumber;
        doc.BranchName = r.BranchName;
        doc.SeizureDate = r.SeizureDate;
        doc.ImmediateActions = r.ImmediateActions;
        doc.Notes = r.Notes;

        // حقلا ورود الملف خاصان بوضع «طالبة تنفيذ» فقط ويُصفَّران بغيرها.
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            doc.FileArrivalNumber = null;
            doc.FileArrivalDate = null;
        }
        else
        {
            var arrivalNumber = (r.FileArrivalNumber ?? string.Empty).Trim();
            var arrivalDate = (r.FileArrivalDate ?? string.Empty).Trim();
            doc.FileArrivalNumber = string.IsNullOrEmpty(arrivalNumber) ? null : arrivalNumber;
            doc.FileArrivalDate = string.IsNullOrEmpty(arrivalDate) ? null : arrivalDate;
        }

        // حقول عائلة وضع «منفذ عليه» (Executed + Deposit): تُطبَّق على ملفات هذه الصفة فقط،
        // وتُصفَّر خارجها. صفة العرض لا تحمل وصفًا إضافيًا (ExecutedDescription) بل تاريخ إيداع.
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            var executedStatus = string.IsNullOrWhiteSpace(r.ExecutedStatus)
                ? ExecutedStatusCatalog.None
                : r.ExecutedStatus.Trim();
            if (!ExecutedStatusCatalog.ValidStatuses.Contains(executedStatus))
                throw new ArgumentException("حالة وضع (متداول/منفذ/مشطوب) غير صالحة");

            doc.ExecutedStatus = ExecutedStatusCatalog.IsStored(executedStatus) ? executedStatus : ExecutedStatusCatalog.None;
            if (doc.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
            {
                var submitted = DocumentValidator.ParseDateTime(r.StruckOffDate, "تاريخ الشطب");
                doc.StruckOffDate = submitted ?? doc.StruckOffDate ?? DateTime.UtcNow;
            }
            doc.ExecutedDescription = doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                ? (r.ExecutedDescription ?? string.Empty).Trim()
                : null;
            doc.FileReceiptDate = DocumentValidator.ParseDateTime(r.FileReceiptDate, "تاريخ ورود الاخطار");
            doc.FileReceiptNumber = (r.FileReceiptNumber ?? string.Empty).Trim();
            doc.ExecutedRequiredAmount = r.ExecutedRequiredAmount;
            doc.ExecutedRequiredCurrency = r.ExecutedRequiredCurrency;
            doc.ExecutedRequiredAmount2 = r.ExecutedRequiredAmount2;
            doc.ExecutedRequiredCurrency2 = r.ExecutedRequiredCurrency2;
            doc.ExecutedRequiredAmount3 = r.ExecutedRequiredAmount3;
            doc.ExecutedRequiredCurrency3 = r.ExecutedRequiredCurrency3;
            doc.ExecutedPaidAmount = r.ExecutedPaidAmount;
            doc.ExecutedPaidCurrency = r.ExecutedPaidCurrency;
            doc.ExecutedPaidAmount2 = r.ExecutedPaidAmount2;
            doc.ExecutedPaidCurrency2 = r.ExecutedPaidCurrency2;
            doc.ExecutedPaidAmount3 = r.ExecutedPaidAmount3;
            doc.ExecutedPaidCurrency3 = r.ExecutedPaidCurrency3;
            doc.ExecutedDepositDate = doc.GeneralEntitySide == GeneralEntitySideCatalog.Deposit
                ? DocumentValidator.ParseDateTime(r.ExecutedDepositDate, "تاريخ ايداعه حساب الجهة العامة")
                : null;
            doc.ExecutedExecutionDate = doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                ? DocumentValidator.ParseDateTime(r.ExecutedExecutionDate, "تاريخ التنفيذ")
                : null;
        }
        else
        {
            doc.ExecutedStatus = ExecutedStatusCatalog.None;
            doc.ExecutedDescription = null;
            doc.FileReceiptDate = null;
            doc.FileReceiptNumber = null;
            doc.ExecutedRequiredAmount = null;
            doc.ExecutedRequiredCurrency = null;
            doc.ExecutedRequiredAmount2 = null;
            doc.ExecutedRequiredCurrency2 = null;
            doc.ExecutedRequiredAmount3 = null;
            doc.ExecutedRequiredCurrency3 = null;
            doc.ExecutedPaidAmount = null;
            doc.ExecutedPaidCurrency = null;
            doc.ExecutedPaidAmount2 = null;
            doc.ExecutedPaidCurrency2 = null;
            doc.ExecutedPaidAmount3 = null;
            doc.ExecutedPaidCurrency3 = null;
            doc.ExecutedDepositDate = null;
            doc.ExecutedExecutionDate = null;
            doc.StruckOffDate = null;
        }

        doc.ExecutionApplicants.Clear();
        foreach (var a in NormalizeExecutionApplicants(r.ExecutionApplicants))
        {
            doc.ExecutionApplicants.Add(a);
            // ربط الورثة بالملف مباشرة (DocumentId) وبمورثهم (ExecutionApplicantId):
            // EF يرتب كل مفتاح أجنبي عبر مجموعة المورث ومجموعة الملف معًا.
            foreach (var heir in a.Heirs)
                doc.ExecutedHeirs.Add(heir);
        }

        doc.ExecutedPublicEntities.Clear();
        foreach (var e in NormalizeExecutedPublicEntities(r.ExecutedPublicEntities))
            doc.ExecutedPublicEntities.Add(e);

        // قائمة الجهات طالبة التنفيذ: تخص وضع «طالبة تنفيذ» فقط وتُصفَّر بغيره.
        doc.ApplicantPublicEntities.Clear();
        if (!GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            var applicantList = r.ApplicantPublicEntities;
            // توافق مع الطلبات القديمة: نص «طالب التنفيذ» المرسل بلا قائمة يُعامَل كجهة واحدة.
            if ((applicantList is null || applicantList.Count == 0) && !string.IsNullOrWhiteSpace(r.Applicant))
                applicantList = new List<ApplicantPublicEntityDto> { new(null, r.Applicant, null) };
            foreach (var a in NormalizeApplicantPublicEntities(applicantList))
                doc.ApplicantPublicEntities.Add(a);
        }

        doc.ExecutedNaturalPersons.Clear();
        foreach (var p in NormalizeExecutedNaturalPersons(r.ExecutedNaturalPersons))
        {
            doc.ExecutedNaturalPersons.Add(p);
            foreach (var heir in p.Heirs)
                doc.ExecutedHeirs.Add(heir);
        }

        doc.Guarantors.Clear();
        foreach (var g in r.Guarantors.OrderBy(g => g.GuarantorNumber))
        {
            var nature = NormalizePartyNature(g.Nature);
            var isLegalGuarantor = PartyNatureCatalog.IsLegal(nature);
            var hasRep = !isLegalGuarantor
                && !IsEmptyRepresentative(g.RepresentativeName, g.RepresentativeFather, g.RepresentativeFamily);
            doc.Guarantors.Add(new Guarantor
            {
                GuarantorNumber = g.GuarantorNumber,
                GuarantorName = g.Name,
                GuarantorFather = isLegalGuarantor ? null : g.Father,
                GuarantorFamily = isLegalGuarantor ? null : g.Family,
                GuarantorMother = isLegalGuarantor ? null : g.Mother,
                GuarantorBirth = isLegalGuarantor ? null : g.Birth,
                GuarantorRegister = isLegalGuarantor ? null : g.Register,
                GuarantorNationalId = isLegalGuarantor ? null : g.NationalId,
                GuarantorAddress = g.Address,
                AddressType = g.AddressType,
                GuarantorNature = nature,
                GuarantorRegistrationNumber = isLegalGuarantor ? (g.RegistrationNumber ?? string.Empty).Trim() : null,
                GuarantorRepresentedBy = isLegalGuarantor ? (g.RepresentedBy ?? string.Empty).Trim() : null,
                RepresentativeName = hasRep ? (g.RepresentativeName ?? string.Empty).Trim() : null,
                RepresentativeFather = hasRep ? (g.RepresentativeFather ?? string.Empty).Trim() : null,
                RepresentativeFamily = hasRep ? (g.RepresentativeFamily ?? string.Empty).Trim() : null,
                RepresentativeCapacity = hasRep ? NormalizeRepresentativeCapacity(g.RepresentativeCapacity) : null,
                RepresentativeAddressType = hasRep ? NormalizeRepresentativeAddressType(g.RepresentativeAddressType) : null,
                RepresentativeAddress = hasRep ? (g.RepresentativeAddress ?? string.Empty).Trim() : null,
            });
        }

        // الورثة: صفوف بلا اسم ثلاثي تُتجاهل، ونوع العنوان غير الصالح يُعيَّر إلى «عنوان».
        // لا ورثة لشخص اعتباري (ورثة تخص الشخص الطبيعي المتوفى فقط).
        doc.Heirs.Clear();
        if (!PartyNatureCatalog.IsLegal(doc.BorrowerNature))
            foreach (var h in NormalizeHeirs(r.BorrowerHeirs, null))
                doc.Heirs.Add(h);
        foreach (var g in r.Guarantors)
            if (!PartyNatureCatalog.IsLegal(NormalizePartyNature(g.Nature)))
                foreach (var h in NormalizeHeirs(g.Heirs, g.GuarantorNumber))
                    doc.Heirs.Add(h);

        doc.Assets.Clear();
        foreach (var re in r.Assets)
        {
            var kind = (re.AssetKind ?? string.Empty).Trim();
            if (!AssetKindCatalog.IsValid(kind))
                throw new ArgumentException($"نوع الأصل غير صالح: {kind}");

            var asset = new Asset
            {
                AssetKind = kind,
                ShareType = re.ShareType,
                Property = re.Property,
                PropertyNumber = re.PropertyNumber,
                PropertyDistrict = re.PropertyDistrict,
                LandRegistry = re.LandRegistry,
                VehicleType = re.VehicleType,
                VehicleClass = re.VehicleClass,
                PlateNumber = re.PlateNumber,
                VehicleGovernorate = re.VehicleGovernorate,
                RegisterNumber = re.RegisterNumber,
                RegistrationDate = DocumentValidator.ParseDateTime(re.RegistrationDate, "تاريخ تسجيل المتجر"),
                ShopGovernorate = re.ShopGovernorate,
                ShopDescription = re.ShopDescription,
                ShopLocation = re.ShopLocation,
                PublicEntity = re.PublicEntity,
                LicenseNumber = re.LicenseNumber,
                LicenseDate = DocumentValidator.ParseDateTime(re.LicenseDate, "تاريخ الترخيص"),
                LicenseIssuer = re.LicenseIssuer,
                Notes = re.Notes,
                SeizureDate = DocumentValidator.ParseDateTime(re.SeizureDate, "تاريخ القاء الحجز"),
            };
            asset.Owners = AssetMapper.NormalizeOwners(re.Owners);
            // تمام الأصل لا يكون إلا لمالك واحد؛ عند تعدد الملاك تُفرض الحصة السهمية
            // حتى لو أُرسل نوع حصة آخر (حماية البيانات على مستوى الخدمة).
            // الأنواع غير الحصصية (كفالة الرواتب والمتجر غير المسجل) لا تحمل مقدار حصة.
            if (AssetKindCatalog.HasShare(kind))
            {
                if (asset.Owners.Count > 1)
                    asset.ShareType = "حصة سهمية";
                else if (string.IsNullOrWhiteSpace(asset.ShareType))
                    asset.ShareType = AssetKindCatalog.FullShareLabel(kind);
            }
            else
            {
                asset.ShareType = null;
            }
            doc.Assets.Add(asset);
        }
    }

    /// <summary>
    /// تطبيع قائمة ملاك الأصل: يُتجاهل الاسم الفارغ، ويُقصّ الاسم من الطرفين،
    /// وتُلغى التكرارات مع الحفاظ على ترتيب الاختيار الأصلي.
    /// </summary>
    private static List<Heir> NormalizeHeirs(IEnumerable<HeirDto>? heirs, int? guarantorNumber)
    {
        var result = new List<Heir>();
        if (heirs is null)
            return result;

        foreach (var h in heirs)
        {
            var name = (h.Name ?? string.Empty).Trim();
            var father = (h.Father ?? string.Empty).Trim();
            var family = (h.Family ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(father) && string.IsNullOrWhiteSpace(family))
                continue;

            var addressType = (h.AddressType ?? string.Empty).Trim();
            if (addressType != "عنوان" && addressType != "وكيل" && addressType != "موطن مختار")
                addressType = "عنوان";

            var capacity = (h.Capacity ?? string.Empty).Trim();
            if (capacity != "إضافة لتركة" && capacity != "أصالة وإضافة")
                capacity = "أصالة";

            result.Add(new Heir
            {
                GuarantorNumber = guarantorNumber,
                HeirName = name,
                HeirFather = father,
                HeirFamily = family,
                HeirCapacity = capacity,
                AddressType = addressType,
                HeirAddress = (h.Address ?? string.Empty).Trim(),
            });
        }

        return result;
    }

    /// <summary>
    /// تطبيع طلبات التنفيذ: يُتجاهل الطلب بلا اسم ثلاثي، ويُقيَّد نوع التمثيل بالقيم المسموح بها
    /// («أصالة»/«إضافة لتركة»/«أصالة وإضافة») مع معاملة أي قيمة أخرى أو فارغة كـ«أصالة»، ويُقصّ
    /// الاسم الثلاثي للمورث إن لم يُحدَّد مع «إضافة لتركة» أو «أصالة وإضافة». وترتبط ورثة كل مورث
    /// بمجموعته مباشرة، ويُطبَّع الممثل الشرعي (إن وُجد بغير اسم ثلاثي فارغ) حقولَه فيُصفَّر
    /// عند الغياب.
    /// </summary>
    private static List<ExecutionApplicant> NormalizeExecutionApplicants(IEnumerable<ExecutionApplicantDto>? applicants)
    {
        var result = new List<ExecutionApplicant>();
        if (applicants is null)
            return result;

        foreach (var a in applicants)
        {
            var name = (a.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var nature = NormalizePartyNature(a.Nature);
            var isLegal = PartyNatureCatalog.IsLegal(nature);

            // الشخص الاعتباري بلا تمثيل بالتركة ولا ممثل شرعي: تُصفَّر حقول الهوية الطبيعية
            // ويُحتفظ برقم التسجيل ومن يمثلها ونوع العنوان وعنوانه.
            var representationType = isLegal
                ? "أصالة"
                : NormalizeApplicantRepresentation(a.RepresentationType);
            var hasEstate = !isLegal && representationType is "إضافة لتركة" or "أصالة وإضافة";
            var hasRep = !isLegal && !IsEmptyRepresentative(a.RepresentativeName, a.RepresentativeFather, a.RepresentativeFamily);

            var applicant = new ExecutionApplicant
            {
                Name = name,
                ApplicantNature = nature,
                ApplicantRegistrationNumber = isLegal ? (a.RegistrationNumber ?? string.Empty).Trim() : null,
                ApplicantRepresentedBy = isLegal ? (a.RepresentedBy ?? string.Empty).Trim() : null,
                ApplicantAddressType = isLegal ? (a.AddressType ?? string.Empty).Trim() : null,
                ApplicantAddress = isLegal ? (a.Address ?? string.Empty).Trim() : null,
                Father = isLegal ? null : (a.Father ?? string.Empty).Trim(),
                Family = isLegal ? null : (a.Family ?? string.Empty).Trim(),
                LegalRepresentative = isLegal ? null : (a.LegalRepresentative ?? string.Empty).Trim(),
                RepresentationType = representationType,
                DeceasedName = hasEstate ? (a.DeceasedName ?? string.Empty).Trim() : null,
                DeceasedFather = hasEstate ? (a.DeceasedFather ?? string.Empty).Trim() : null,
                DeceasedFamily = hasEstate ? (a.DeceasedFamily ?? string.Empty).Trim() : null,
                RepresentativeName = hasRep ? (a.RepresentativeName ?? string.Empty).Trim() : null,
                RepresentativeFather = hasRep ? (a.RepresentativeFather ?? string.Empty).Trim() : null,
                RepresentativeFamily = hasRep ? (a.RepresentativeFamily ?? string.Empty).Trim() : null,
                RepresentativeCapacity = hasRep ? NormalizeRepresentativeCapacity(a.RepresentativeCapacity) : null,
                RepresentativeLegalRepresentative = hasRep ? (a.RepresentativeLegalRepresentative ?? string.Empty).Trim() : null,
            };
            if (!isLegal)
            {
                foreach (var heir in NormalizeExecutedHeirs(a.Heirs))
                    applicant.Heirs.Add(heir);
            }
            result.Add(applicant);
        }

        return result;
    }

    /// <summary>
    /// تطبيع المنفذ عليهم الاعتباريين (جهة عامة أو شخص اعتباري): يُتجاهل ما بلا اسم، ويُقصّ
    /// اسمه وفرعه. عند الطبيعة (legal) تُعبَّأ حقول الشخص الاعتباري (رقم التسجيل/من يمثلها/العنوان)
    /// ويُصفَّر فرع الجهة العامة؛ وعند (public) تُصفَّر حقول الشخص الاعتباري.
    /// </summary>
    private static List<ExecutedPublicEntity> NormalizeExecutedPublicEntities(IEnumerable<ExecutedPublicEntityDto>? entities)
    {
        var result = new List<ExecutedPublicEntity>();
        if (entities is null)
            return result;

        foreach (var e in entities)
        {
            var name = (e.EntityName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var nature = NormalizeEntityNature(e.Nature);
            var isLegal = PartyNatureCatalog.IsLegal(nature);
            result.Add(new ExecutedPublicEntity
            {
                EntityName = name,
                EntityBranch = isLegal ? null : (e.EntityBranch ?? string.Empty).Trim(),
                Governorate = (e.Governorate ?? string.Empty).Trim(),
                // ربط السجل المرجعي خاص بجهات الدولة العامة لا بالأشخاص الاعتباريين.
                RegistryId = isLegal ? null : e.RegistryId,
                EntityNature = nature,
                RegistrationNumber = isLegal ? (e.RegistrationNumber ?? string.Empty).Trim() : null,
                RepresentedBy = isLegal ? (e.RepresentedBy ?? string.Empty).Trim() : null,
                AddressType = isLegal ? (e.AddressType ?? string.Empty).Trim() : null,
                Address = isLegal ? (e.Address ?? string.Empty).Trim() : null,
            });
        }

        return result;
    }

    /// <summary>
    /// تطبيع قائمة الجهات طالبة التنفيذ في وضع «طالبة تنفيذ»: يُتجاهل ما بلا اسم جهة،
    /// ويُقصّ اسم الجهة وفرعها ومحافظتها.
    /// </summary>
    private static List<ApplicantPublicEntity> NormalizeApplicantPublicEntities(IEnumerable<ApplicantPublicEntityDto>? entities)
    {
        var result = new List<ApplicantPublicEntity>();
        if (entities is null)
            return result;

        foreach (var e in entities)
        {
            var name = (e.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            result.Add(new ApplicantPublicEntity
            {
                Name = name,
                Branch = (e.Branch ?? string.Empty).Trim(),
                Governorate = (e.Governorate ?? string.Empty).Trim(),
                RegistryId = e.RegistryId,
            });
        }

        return result;
    }

    /// <summary>
    /// تطبيع الأشخاص الطبيعيين المنفذ عليهم: يُتجاهل ما بلا اسم ثلاثي، ويُقيَّد نوع العنوان
    /// («عنوان»/«وكيل») مع معاملة أي قيمة أخرى كـ«عنوان»، ونوع التمثيل («أصالة»/«إضافة لتركة»/
    /// «أصالة وإضافة») مع معاملة أي قيمة أخرى كـ«أصالة». وترتبط ورثة كل مورث بمجموعته مباشرة،
    /// ويُطبَّع الممثل الشرعي (إن وُجد) حقولَه فيُصفَّر عند الغياب.
    /// </summary>
    private static List<ExecutedNaturalPerson> NormalizeExecutedNaturalPersons(IEnumerable<ExecutedNaturalPersonDto>? persons)
    {
        var result = new List<ExecutedNaturalPerson>();
        if (persons is null)
            return result;

        foreach (var p in persons)
        {
            var name = (p.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var addressType = (p.AddressType ?? string.Empty).Trim();
            if (addressType != "وكيل")
                addressType = "عنوان";

            var representationType = (p.RepresentationType ?? string.Empty).Trim();
            if (representationType != "إضافة لتركة" && representationType != "أصالة وإضافة")
                representationType = "أصالة";

            var hasEstate = representationType is "إضافة لتركة" or "أصالة وإضافة";
            var hasRep = !IsEmptyRepresentative(p.RepresentativeName, p.RepresentativeFather, p.RepresentativeFamily);

            var person = new ExecutedNaturalPerson
            {
                Name = name,
                Father = (p.Father ?? string.Empty).Trim(),
                Family = (p.Family ?? string.Empty).Trim(),
                AddressType = addressType,
                AddressOrRepresentative = (p.AddressOrRepresentative ?? string.Empty).Trim(),
                RepresentationType = representationType,
                DeceasedName = hasEstate ? (p.DeceasedName ?? string.Empty).Trim() : null,
                DeceasedFather = hasEstate ? (p.DeceasedFather ?? string.Empty).Trim() : null,
                DeceasedFamily = hasEstate ? (p.DeceasedFamily ?? string.Empty).Trim() : null,
                RepresentativeName = hasRep ? (p.RepresentativeName ?? string.Empty).Trim() : null,
                RepresentativeFather = hasRep ? (p.RepresentativeFather ?? string.Empty).Trim() : null,
                RepresentativeFamily = hasRep ? (p.RepresentativeFamily ?? string.Empty).Trim() : null,
                RepresentativeCapacity = hasRep ? NormalizeRepresentativeCapacity(p.RepresentativeCapacity) : null,
                RepresentativeAddressType = hasRep ? NormalizeRepresentativeAddressType(p.RepresentativeAddressType) : null,
                RepresentativeAddress = hasRep ? (p.RepresentativeAddress ?? string.Empty).Trim() : null,
            };
            foreach (var heir in NormalizeExecutedHeirs(p.Heirs))
                person.Heirs.Add(heir);
            result.Add(person);
        }

        return result;
    }

    /// <summary>
    /// تصفية صفوف الورثة الصالحة: يُتجاهل الوريث بلا اسم ثلاثي، ويُقيَّد نوع العنوان
    /// («عنوان»/«وكيل») مع معاملة أي قيمة أخرى أو فارغة كـ«عنوان».
    /// </summary>
    private static List<ExecutedHeir> NormalizeExecutedHeirs(IEnumerable<ExecutedHeirDto>? heirs)
    {
        var result = new List<ExecutedHeir>();
        if (heirs is null)
            return result;

        foreach (var h in heirs)
        {
            var name = (h.HeirName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var addressType = (h.AddressType ?? string.Empty).Trim();
            if (addressType != "عنوان" && addressType != "وكيل")
                addressType = "عنوان";

            result.Add(new ExecutedHeir
            {
                HeirName = name,
                HeirFather = (h.HeirFather ?? string.Empty).Trim(),
                HeirFamily = (h.HeirFamily ?? string.Empty).Trim(),
                AddressType = addressType,
                HeirAddress = (h.HeirAddress ?? string.Empty).Trim(),
            });
        }

        return result;
    }

    /// <summary>
    /// هل الممثل الشرعي غائب (اسمه الثلاثي فارغ كاملًا)؟ تُعدّ الحقول فارغة فلا يُخزَّن ممثل.
    /// </summary>
    private static bool IsEmptyRepresentative(string? name, string? father, string? family) =>
        string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(father) && string.IsNullOrWhiteSpace(family);

    /// <summary>
    /// صفة الممثل الشرعي المقبولة: ولي / وصي / قيم؛ أي قيمة أخرى أو فارغة تُعاد فارغة.
    /// </summary>
    private static string NormalizeRepresentativeCapacity(string? capacity)
    {
        var value = (capacity ?? string.Empty).Trim();
        return value is "ولي" or "وصي" or "قيم" ? value : string.Empty;
    }

    /// <summary>
    /// نوع عنوان الممثل الشرعي المقبول: موطن مختار / عنوان / وكيل قانوني؛ أي قيمة أخرى أو فارغة
    /// تُعيَّر إلى «عنوان».
    /// </summary>
    private static string NormalizeRepresentativeAddressType(string? addressType)
    {
        var value = (addressType ?? string.Empty).Trim();
        return value is "موطن مختار" or "عنوان" or "وكيل قانوني" ? value : "عنوان";
    }

    /// <summary>
    /// طبيعة الطرف المقبولة (مقترض/كفيل/طالب تنفيذ): شخص طبيعي (natural) أو شخص اعتباري (legal)؛
    /// أي قيمة أخرى أو فارغة تُعيَّر إلى «شخص طبيعي».
    /// </summary>
    private static string NormalizePartyNature(string? nature)
    {
        var value = (nature ?? string.Empty).Trim();
        return PartyNatureCatalog.ValidNatures.Contains(value) ? value : PartyNatureCatalog.Natural;
    }

    /// <summary>
    /// طبيعة المنفذ عليه الاعتباري في وضع «منفذ عليه»: جهة عامة (public) أو شخص اعتباري (legal)؛
    /// أي قيمة أخرى أو فارغة تُعيَّر إلى «جهة عامة».
    /// </summary>
    private static string NormalizeEntityNature(string? nature)
    {
        var value = (nature ?? string.Empty).Trim();
        return PartyNatureCatalog.ValidEntityNatures.Contains(value) ? value : PartyNatureCatalog.PublicEntity;
    }

    /// <summary>
    /// نوع تمثيل طالب التنفيذ المقبول: أصالة / إضافة لتركة / أصالة وإضافة؛ أي قيمة أخرى أو فارغة
    /// تُعيَّر إلى «أصالة».
    /// </summary>
    private static string NormalizeApplicantRepresentation(string? representationType)
    {
        var value = (representationType ?? string.Empty).Trim();
        return value is "إضافة لتركة" or "أصالة وإضافة" ? value : "أصالة";
    }

    private void ApplyRegistrationDate(Document doc, string? value)
    {
        var date = value?.Trim();
        if (string.IsNullOrWhiteSpace(date))
        {
            if (doc.RegistrationDate is not null)
            {
                _registrationDates.Remove(doc.RegistrationDate);
                doc.RegistrationDate = null;
            }
            return;
        }

        if (doc.RegistrationDate is null)
            doc.RegistrationDate = new DocumentRegistrationDate { Date = date };
        else
            doc.RegistrationDate.Date = date;

        doc.RegistrationDate.DateParsed = ActionDateParser.TryParse(date);
    }

    private static void FillDerivedFields(Document doc)
    {
        if (doc.AmountNumeric > 0 && string.IsNullOrWhiteSpace(doc.AmountWords))
            doc.AmountWords = FormatAmountWords(doc.AmountNumeric, doc.Currency);
        if (doc.Amount2Numeric > 0 && string.IsNullOrWhiteSpace(doc.Amount2Words))
            doc.Amount2Words = FormatAmountWords(doc.Amount2Numeric, doc.Currency2);
        if (doc.Amount3Numeric > 0 && string.IsNullOrWhiteSpace(doc.Amount3Words))
            doc.Amount3Words = FormatAmountWords(doc.Amount3Numeric, doc.Currency3);
        if (doc.InclusionAmountNumeric > 0 && string.IsNullOrWhiteSpace(doc.InclusionAmountWords))
            doc.InclusionAmountWords = FormatAmountWords(doc.InclusionAmountNumeric, doc.InclusionCurrency);
        if (doc.InclusionAmount2Numeric > 0 && string.IsNullOrWhiteSpace(doc.InclusionAmount2Words))
            doc.InclusionAmount2Words = FormatAmountWords(doc.InclusionAmount2Numeric, doc.InclusionCurrency2);
        if (doc.InclusionAmount3Numeric > 0 && string.IsNullOrWhiteSpace(doc.InclusionAmount3Words))
            doc.InclusionAmount3Words = FormatAmountWords(doc.InclusionAmount3Numeric, doc.InclusionCurrency3);

        doc.IsDraft = string.IsNullOrWhiteSpace(doc.FileNumber) || string.IsNullOrWhiteSpace(doc.FileYear);
        var label = doc.IsDraft ? ExecutionStatusCatalog.DraftFilter : "متداول";
        var borrower = (doc.BorrowerName ?? string.Empty).Trim();
        doc.DocumentType = string.IsNullOrWhiteSpace(borrower) ? label : $"{label} - {borrower}";

        // «طالب التنفيذ» في وضع «طالبة تنفيذ» يُشتق من قائمة الجهات (اسم + فرع بين قوسين)،
        // فتُوحَّد طريقة التخزين ويبقى النص متوافقًا مع البحث والتصدير والتوليد. وإن كانت
        // القائمة فارغة مع وجود نص قديم محفوظ يُحافظ عليه (توافق مع الطلبات القديمة).
        var applicantText = BuildApplicantText(doc.ApplicantPublicEntities);
        if (!string.IsNullOrWhiteSpace(applicantText) || string.IsNullOrWhiteSpace(doc.Applicant))
            doc.Applicant = applicantText;

        // نسخة تسريع لفلترة جهة الطالب في البوابة: أول ربط سجلي غير فارغ بين صفوف الجهات،
        // وتُصفَّر تلقائيًا حين تفرغ القائمة أو يزول الربط.
        doc.ApplicantRegistryId = doc.ApplicantPublicEntities
            .Select(a => a.RegistryId)
            .FirstOrDefault(id => id.HasValue);

        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            // ملف «منفذ عليه»/«عرض وايداع»: مقيد دائمًا، والعنوان يعتمد على حالة الوضع،
            // واسم البحث يضم أسماء طلبات التنفيذ/العرض والجهات/الأشخاص المنفذ عليهم.
            doc.IsDraft = false;
            doc.DocumentType = $"{ExecutedStatusCatalog.ToLabel(doc.ExecutedStatus ?? ExecutedStatusCatalog.None)}";
        }

        doc.SearchText = Common.DocumentSearchTextBuilder.Build(doc);

        doc.FullData = Common.DocumentSearchTextBuilder.BuildFullData(doc);
    }

    private static string FormatAmountWords(decimal amount, string? currency)
    {
        var words = NumberToWords.Convert((long)amount);
        return string.IsNullOrWhiteSpace(words)
            ? string.Empty
            : $"{words} {currency} فقط لا غير".Trim();
    }

    /// <summary>
    /// النص الموحّد لطالب التنفيذ في وضع «طالبة تنفيذ» من قائمة الجهات:
    /// «الجهة - محافظة X و الجهة - محافظة Y» — يُشتق ليغذي البحث والتصدير والتوليد.
    /// الفرع لا يُضمّن هنا؛ يُعرض ويُفلتر عبر حقل الفرع المستقل في ApplicantPublicEntities.Branch.
    /// </summary>
    private static string BuildApplicantText(IEnumerable<ApplicantPublicEntity> entities) =>
        Common.ApplicantTextBuilder.Build(entities);
}
