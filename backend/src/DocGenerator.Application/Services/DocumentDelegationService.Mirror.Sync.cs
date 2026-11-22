using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentDelegationService
{
    // ── المزامنة: منيب → مناب (نسخ قيمي بلا حذف إلا استثناء الكفيل المحذوف) ──────────

    /// <summary>
    /// يُستدعى داخل معاملة التعديل بعد حفظ المنيب — داخل معاملة المستدعي حصرًا
    /// (لا يفتح معاملة خاصة). تنسخ المرآة الحقول المقفولة إلى كل
    /// منابٍ لإضافةٍ معلّقة (غير منفذة)، وتُعيد اشتقاق بيانات المناب (FillDerivedFields)،
    /// وتُعيد نوايا التنبيه المُتحدِدة لتُطلق بعد نجاح المعاملة (نقطة خروج واحدة).
    /// </summary>
    public async Task<List<MirrorNotification>> MirrorSourceEditsAsync(Document source, CancellationToken token)
    {
        var delegations = await _delegations.ListPendingBySourceWithTargetsAsync(source.Id, token);
        if (delegations.Count == 0)
            return new List<MirrorNotification>();

        var intents = new List<MirrorNotification>();
        foreach (var delegation in delegations)
        {
            var target = delegation.TargetDocument;
            if (target is null)
                continue;

            var labels = new List<string>();
            MirrorBorrowerInto(source, target, labels);
            MirrorBondInto(source, target, labels);
            MirrorBooksInto(source, target, labels);
            MirrorEntitiesInto(source, target, labels);
            MirrorGuarantorsInto(source, target, labels);
            MirrorBorrowerHeirsInto(source, target, labels);
            // الأصول غير مُزامَنة: ملك العرض/البيع المحلي للمناب، والحارس يرفض تغييرها قيميًا.

            if (labels.Count == 0)
                continue;

            DocumentText.FillDerivedFields(target);
            await _uow.SaveChangesAsync(token);

            var recipientBranch = target.BranchId ?? source.BranchId ?? delegation.ExternalBranchId;
            intents.Add(new MirrorNotification(
                delegation.Id,
                target.CreatedById,
                recipientBranch ?? 0,
                source.CreatedById,
                HeadAlertMessages.DelegationMirrorUpdated(new(string.Join("، ", labels))),
                HeadAlertKindCatalog.DelegationMirrorUpdated));
        }

        return intents;
    }

    private static void MirrorBorrowerInto(Document source, Document target, List<string> labels)
    {
        // عنوان المقترض/نوعه لا يُكتبان على منابٍ له ورثة أو ممثل محلي (الفراغ مرافق مقصود للثابت
        // «ورثة/ممثل ⟺ عنوان فارغ») — التخطي صامت بلا تنبيه (قرار 17).
        var targetHasLocalFamily = target.Heirs.Any(h => h.GuarantorNumber is null)
            || !DocumentText.IsEmptyRepresentative(target.BorrowerRepresentativeName, target.BorrowerRepresentativeFather, target.BorrowerRepresentativeFamily);

        var changed = false;
        changed |= CopyField(source.BorrowerName, target.BorrowerName, v => target.BorrowerName = v);
        changed |= CopyField(source.BorrowerFather, target.BorrowerFather, v => target.BorrowerFather = v);
        changed |= CopyField(source.BorrowerFamily, target.BorrowerFamily, v => target.BorrowerFamily = v);
        changed |= CopyField(source.BorrowerMother, target.BorrowerMother, v => target.BorrowerMother = v);
        changed |= CopyField(source.BorrowerBirth, target.BorrowerBirth, v => target.BorrowerBirth = v);
        changed |= CopyField(source.BorrowerRegister, target.BorrowerRegister, v => target.BorrowerRegister = v);
        changed |= CopyField(source.BorrowerNationalId, target.BorrowerNationalId, v => target.BorrowerNationalId = v);
        if (!targetHasLocalFamily)
        {
            changed |= CopyField(source.BorrowerAddress, target.BorrowerAddress, v => target.BorrowerAddress = v);
            changed |= CopyField(source.BorrowerAddressType, target.BorrowerAddressType, v => target.BorrowerAddressType = v);
        }
        changed |= CopyField(source.BorrowerNature, target.BorrowerNature,
            v => target.BorrowerNature = v ?? PartyNatureCatalog.Natural);
        changed |= CopyField(source.BorrowerRegistrationNumber, target.BorrowerRegistrationNumber, v => target.BorrowerRegistrationNumber = v);
        changed |= CopyField(source.BorrowerRepresentedBy, target.BorrowerRepresentedBy, v => target.BorrowerRepresentedBy = v);
        if (changed)
            labels.Add("بيانات المقترض");

        // الممثل الشرعي للمقترض: تعبئة عند الفراغ فقط — لا مساس بالمضبوط محليًا،
        // ولا يُنسخ إذا كان على المناب ورثة محليون (لا يجتمع الممثل مع الورثة).
        var targetHasBorrowerHeirs = target.Heirs.Any(h => h.GuarantorNumber is null);
        if (!targetHasBorrowerHeirs
            && DocumentText.IsEmptyRepresentative(target.BorrowerRepresentativeName, target.BorrowerRepresentativeFather, target.BorrowerRepresentativeFamily)
            && !DocumentText.IsEmptyRepresentative(source.BorrowerRepresentativeName, source.BorrowerRepresentativeFather, source.BorrowerRepresentativeFamily))
        {
            target.BorrowerRepresentativeName = source.BorrowerRepresentativeName;
            target.BorrowerRepresentativeFather = source.BorrowerRepresentativeFather;
            target.BorrowerRepresentativeFamily = source.BorrowerRepresentativeFamily;
            target.BorrowerRepresentativeCapacity = source.BorrowerRepresentativeCapacity;
            target.BorrowerRepresentativeAddressType = source.BorrowerRepresentativeAddressType;
            target.BorrowerRepresentativeAddress = source.BorrowerRepresentativeAddress;
            labels.Add("الممثل الشرعي للمقترض");
        }
    }

    private static void MirrorBondInto(Document source, Document target, List<string> labels)
    {
        var changed = false;
        changed |= CopyField(source.ContractType, target.ContractType, v => target.ContractType = v);
        changed |= CopyField(source.ContractTypeSelector, target.ContractTypeSelector, v => target.ContractTypeSelector = v);
        changed |= CopyField(source.ContractNumber, target.ContractNumber, v => target.ContractNumber = v);
        changed |= CopyField(source.ContractDate, target.ContractDate, v => target.ContractDate = v);
        var sourceAnnexes = source.Annexes.OrderBy(a => a.Sequence).ToList();
        if (!DocumentAnnexText.ListsEqual(sourceAnnexes, target.Annexes.OrderBy(a => a.Sequence).ToList()))
        {
            target.Annexes.Clear();
            foreach (var annex in sourceAnnexes)
            {
                target.Annexes.Add(new DocumentAnnex
                {
                    Sequence = annex.Sequence,
                    AnnexType = annex.AnnexType,
                    AnnexNumber = annex.AnnexNumber,
                    AnnexDate = annex.AnnexDate,
                });
            }
            changed = true;
        }
        changed |= CopyField(source.InclusionText, target.InclusionText, v => target.InclusionText = v);
        changed |= CopyField(source.InclusionNotes, target.InclusionNotes, v => target.InclusionNotes = v);
        changed |= CopyNumeric(source.AmountNumeric, target.AmountNumeric, v => target.AmountNumeric = v);
        changed |= CopyField(source.AmountWords, target.AmountWords, v => target.AmountWords = v);
        changed |= CopyField(source.Currency, target.Currency, v => target.Currency = v);
        changed |= CopyNumeric(source.Amount2Numeric, target.Amount2Numeric, v => target.Amount2Numeric = v);
        changed |= CopyField(source.Amount2Words, target.Amount2Words, v => target.Amount2Words = v);
        changed |= CopyField(source.Currency2, target.Currency2, v => target.Currency2 = v);
        changed |= CopyNumeric(source.Amount3Numeric, target.Amount3Numeric, v => target.Amount3Numeric = v);
        changed |= CopyField(source.Amount3Words, target.Amount3Words, v => target.Amount3Words = v);
        changed |= CopyField(source.Currency3, target.Currency3, v => target.Currency3 = v);
        changed |= CopyNumeric(source.InclusionAmountNumeric, target.InclusionAmountNumeric, v => target.InclusionAmountNumeric = v);
        changed |= CopyField(source.InclusionAmountWords, target.InclusionAmountWords, v => target.InclusionAmountWords = v);
        changed |= CopyField(source.InclusionCurrency, target.InclusionCurrency, v => target.InclusionCurrency = v);
        changed |= CopyNumeric(source.InclusionAmount2Numeric, target.InclusionAmount2Numeric, v => target.InclusionAmount2Numeric = v);
        changed |= CopyField(source.InclusionAmount2Words, target.InclusionAmount2Words, v => target.InclusionAmount2Words = v);
        changed |= CopyField(source.InclusionCurrency2, target.InclusionCurrency2, v => target.InclusionCurrency2 = v);
        changed |= CopyNumeric(source.InclusionAmount3Numeric, target.InclusionAmount3Numeric, v => target.InclusionAmount3Numeric = v);
        changed |= CopyField(source.InclusionAmount3Words, target.InclusionAmount3Words, v => target.InclusionAmount3Words = v);
        changed |= CopyField(source.InclusionCurrency3, target.InclusionCurrency3, v => target.InclusionCurrency3 = v);
        // الدائرة لا تُزامَن من المنيب: هوية مستقلة للمناب (الدائرة المنابة).
        changed |= CopyField(source.Applicant, target.Applicant, v => target.Applicant = v);
        if (changed)
            labels.Add("السند التنفيذي");
    }

    private static void MirrorBooksInto(Document source, Document target, List<string> labels)
    {
        var changed = false;
        changed |= CopyField(source.FileArrivalNumber, target.FileArrivalNumber, v => target.FileArrivalNumber = v);
        changed |= CopyField(source.FileArrivalDate, target.FileArrivalDate, v => target.FileArrivalDate = v);
        changed |= CopyField(source.FileIncoming, target.FileIncoming, v => target.FileIncoming = v);
        changed |= CopyField(source.FileIncomingDate, target.FileIncomingDate, v => target.FileIncomingDate = v);
        changed |= CopyField(source.UnderFilingNumber, target.UnderFilingNumber, v => target.UnderFilingNumber = v);
        // حقلّا «ورود الإخطار التنفيذي» (FileReceiptNumber/FileReceiptDate) خارج عقد المرآة (B6):
        // خاصان بوضع «منفذ عليه» ويُصفَّران على طالبة تنفيذ (DocumentService.Apply.cs)، فلا
        // يُنسخان هنا كما لا يُفحصان في الحارس.
        changed |= CopyField(source.SeizureDate, target.SeizureDate, v => target.SeizureDate = v);
        if (changed)
            labels.Add("الكتب");
    }

    private static void MirrorEntitiesInto(Document source, Document target, List<string> labels)
    {
        var changed = false;
        var currentEntities = target.ApplicantPublicEntities.ToList();
        var consumed = new bool[currentEntities.Count];
        var next = new List<ApplicantPublicEntity>();
        foreach (var entity in source.ApplicantPublicEntities)
        {
            var matchIndex = -1;
            for (var i = 0; i < currentEntities.Count; i++)
            {
                if (consumed[i])
                    continue;
                var candidate = currentEntities[i];
                if (ApplicantEntitySameIdentity(entity, candidate))
                {
                    consumed[i] = true;
                    matchIndex = i;
                    break;
                }
            }
            if (matchIndex >= 0)
            {
                var current = currentEntities[matchIndex];
                changed |= CopyField(entity.Name, current.Name, v => current.Name = v);
                changed |= CopyField(entity.Branch, current.Branch, v => current.Branch = v);
                changed |= CopyField(entity.Governorate, current.Governorate, v => current.Governorate = v);
                if (current.RegistryId != entity.RegistryId)
                {
                    current.RegistryId = entity.RegistryId;
                    changed = true;
                }
                next.Add(current);
            }
            else
            {
                next.Add(new ApplicantPublicEntity
                {
                    Name = entity.Name,
                    Branch = entity.Branch,
                    Governorate = entity.Governorate,
                    RegistryId = entity.RegistryId,
                });
                changed = true;
            }
        }
        if (next.Count != currentEntities.Count)
            changed = true;

        target.ApplicantPublicEntities.Clear();
        foreach (var entity in next)
            target.ApplicantPublicEntities.Add(entity);
        if (changed)
            labels.Add("الجهات العامة طالبة التنفيذ");
    }

    /// <summary>هوية الجهة للمطابقة: RegistryId المتساوي أولاً ثم الاسم.</summary>
    private static bool ApplicantEntitySameIdentity(ApplicantPublicEntity a, ApplicantPublicEntity b) =>
        (a.RegistryId is not null && b.RegistryId is not null && a.RegistryId == b.RegistryId)
        || string.Equals(DocumentText.Trimmed(a.Name), DocumentText.Trimmed(b.Name), StringComparison.Ordinal);

    private static void MirrorGuarantorsInto(Document source, Document target, List<string> labels)
    {
        var changed = false;
        var deletedNames = new List<string>();
        var sourceNumbers = source.Guarantors.Select(g => g.GuarantorNumber).ToHashSet();

        // الكفلاء الحاليون على المنيب: تحديث أو إضافة (بالرقم المرجعي المستقر — لا يُعاد ترقيمه).
        foreach (var g in source.Guarantors.OrderBy(g => g.GuarantorNumber))
        {
            var current = target.Guarantors.FirstOrDefault(t => t.GuarantorNumber == g.GuarantorNumber);
            if (current is not null)
            {
                // عنوان الكفيل/نوعه لا يُكتبان على منابٍ له ورثة أو ممثل محلي لهذا الكفيل (فراغ
                // مرافق مقصود — التخطي صامت بلا تنبيه).
                var targetHasLocalHeirs = target.Heirs.Any(h => h.GuarantorNumber == g.GuarantorNumber);
                var currentHasLocalFamily = targetHasLocalHeirs
                    || !DocumentText.IsEmptyRepresentative(current.RepresentativeName, current.RepresentativeFather, current.RepresentativeFamily);
                changed |= CopyGuarantorCoreFields(g, current, currentHasLocalFamily);
                // الممثل الشرعي للكفيل: تعبئة عند الفراغ فقط (لئلا تُطمس الإضافة المحلية)،
                // ولا يُنسخ إذا كان على المناب ورثة محليون لهذا الكفيل (لا يجتمعان).
                if (!targetHasLocalHeirs
                    && DocumentText.IsEmptyRepresentative(current.RepresentativeName, current.RepresentativeFather, current.RepresentativeFamily)
                    && !DocumentText.IsEmptyRepresentative(g.RepresentativeName, g.RepresentativeFather, g.RepresentativeFamily))
                {
                    current.RepresentativeName = g.RepresentativeName;
                    current.RepresentativeFather = g.RepresentativeFather;
                    current.RepresentativeFamily = g.RepresentativeFamily;
                    current.RepresentativeCapacity = g.RepresentativeCapacity;
                    current.RepresentativeAddressType = g.RepresentativeAddressType;
                    current.RepresentativeAddress = g.RepresentativeAddress;
                    changed = true;
                }
                // ورثة الكفيل القائم: إضافة الناقص بلا حذف (الإضافات المحلية محفوظة).
                changed |= MirrorHeirsAddOnly(source, target, g.GuarantorNumber);
                continue;
            }

            // كفيل جديد على المنيب: يُنسخ كاملًا مع ورثته المرتبطة برقمه.
            target.Guarantors.Add(DocumentDelegationService.CopyGuarantor(g));
            foreach (var h in source.Heirs.Where(h => h.GuarantorNumber == g.GuarantorNumber))
                target.Heirs.Add(DocumentDelegationService.CopyHeir(h));
            changed = true;
        }

        // الكفلاء المحذوفون من المنيب: حذف من المناب مع ورثتهم المرتبطة برقمهم
        // (الاستثناء الموثق الوحيد لقاعدة «بلا حذف» — وإلا علقت صفوف يتيمة).
        foreach (var stale in target.Guarantors.Where(g => !sourceNumbers.Contains(g.GuarantorNumber)).ToList())
        {
            var removedHeirs = target.Heirs.Where(h => h.GuarantorNumber == stale.GuarantorNumber).ToList();
            foreach (var h in removedHeirs)
                target.Heirs.Remove(h);
            target.Guarantors.Remove(stale);
            changed = true;

            // تُسمَّى الورثة المحذوفون مع الكفيل المحذوف (قرار 8): بالأسماء أولاً وإلا بالعدد.
            var label = GuarantorFullName(stale);
            if (removedHeirs.Count > 0)
            {
                var heirNames = removedHeirs
                    .Select(h => string.Join(' ', new[] { h.HeirName, h.HeirFather, h.HeirFamily }.Where(v => !string.IsNullOrWhiteSpace(v))))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
                label += heirNames.Count > 0
                    ? $" وورثته ({string.Join("، ", heirNames)})"
                    : $" وورثته ({removedHeirs.Count})";
            }
            deletedNames.Add(label);
        }

        if (changed)
            labels.Add(deletedNames.Count > 0
                ? $"الكفلاء (حُذف من المنيب: {string.Join("، ", deletedNames)})"
                : "الكفلاء");
    }

    private static bool MirrorHeirsAddOnly(Document source, Document target, int? guarantorNumber)
    {
        // لا تُضاف ورثة المنيب إلى طرفٍ على المناب له ممثل شرعي محلي (لا يجتمعان).
        var partyHasLocalRep = guarantorNumber is null
            ? !DocumentText.IsEmptyRepresentative(
                target.BorrowerRepresentativeName, target.BorrowerRepresentativeFather, target.BorrowerRepresentativeFamily)
            : target.Guarantors.FirstOrDefault(g => g.GuarantorNumber == guarantorNumber) is { } targetGuarantor
                && !DocumentText.IsEmptyRepresentative(
                    targetGuarantor.RepresentativeName, targetGuarantor.RepresentativeFather, targetGuarantor.RepresentativeFamily);
        if (partyHasLocalRep)
            return false;

        var added = false;
        var existingKeys = target.Heirs
            .Where(h => h.GuarantorNumber == guarantorNumber)
            .Select(HeirIdentityKey)
            .ToHashSet();
        foreach (var h in source.Heirs.Where(h => h.GuarantorNumber == guarantorNumber))
        {
            if (existingKeys.Contains(HeirIdentityKey(h)))
                continue;
            target.Heirs.Add(DocumentDelegationService.CopyHeir(h));
            existingKeys.Add(HeirIdentityKey(h));
            added = true;
        }
        return added;
    }

    private static void MirrorBorrowerHeirsInto(Document source, Document target, List<string> labels)
    {
        // لا تُضاف ورثة المنيب إلى مقترضٍ على المناب له ممثل شرعي محلي (لا يجتمعان).
        if (!DocumentText.IsEmptyRepresentative(
                target.BorrowerRepresentativeName, target.BorrowerRepresentativeFather, target.BorrowerRepresentativeFamily))
            return;

        // ورثة المنيب (دون رقم كفيل): إضافة الناقص بالمطابقة القيمية (الرقم + الاسم الثلاثي) بلا حذف
        // — التعديل/الحذف على المنيب لا يمس المناب (قرار 8) ويُخطر المُناب عبر التنبيه المدموج.
        var added = false;
        var existingKeys = target.Heirs.Where(h => h.GuarantorNumber is null).Select(HeirIdentityKey).ToHashSet();
        foreach (var h in source.Heirs.Where(h => h.GuarantorNumber is null))
        {
            if (existingKeys.Contains(HeirIdentityKey(h)))
                continue;
            target.Heirs.Add(DocumentDelegationService.CopyHeir(h));
            existingKeys.Add(HeirIdentityKey(h));
            added = true;
        }
        if (added)
            labels.Add("ورثة المقترض");
    }

    /// <summary>نسخ قيم الكفيل غير الممثلة (الدمج بالرقم المرجعي لا يمسّ الممثل المضبوط محليًا).
    /// عند `skipAddress` (ورثة/ممثل محليون على الكفيل) لا يُكتب عنوانه ونوعه (فراغ مرافق مقصود).</summary>
    private static bool CopyGuarantorCoreFields(Guarantor source, Guarantor target, bool skipAddress)
    {
        var changed = false;
        changed |= CopyField(source.GuarantorName, target.GuarantorName, v => target.GuarantorName = v);
        changed |= CopyField(source.GuarantorFather, target.GuarantorFather, v => target.GuarantorFather = v);
        changed |= CopyField(source.GuarantorFamily, target.GuarantorFamily, v => target.GuarantorFamily = v);
        changed |= CopyField(source.GuarantorMother, target.GuarantorMother, v => target.GuarantorMother = v);
        changed |= CopyField(source.GuarantorBirth, target.GuarantorBirth, v => target.GuarantorBirth = v);
        changed |= CopyField(source.GuarantorRegister, target.GuarantorRegister, v => target.GuarantorRegister = v);
        changed |= CopyField(source.GuarantorNationalId, target.GuarantorNationalId, v => target.GuarantorNationalId = v);
        if (!skipAddress)
        {
            changed |= CopyField(source.GuarantorAddress, target.GuarantorAddress, v => target.GuarantorAddress = v);
            changed |= CopyField(source.AddressType, target.AddressType, v => target.AddressType = v);
        }
        changed |= CopyField(source.GuarantorNature, target.GuarantorNature,
            v => target.GuarantorNature = v ?? PartyNatureCatalog.Natural);
        changed |= CopyField(source.GuarantorRegistrationNumber, target.GuarantorRegistrationNumber, v => target.GuarantorRegistrationNumber = v);
        changed |= CopyField(source.GuarantorRepresentedBy, target.GuarantorRepresentedBy, v => target.GuarantorRepresentedBy = v);
        return changed;
    }

    private static bool CopyField(string? source, string? current, Action<string?> assign)
    {
        if (string.Equals(DocumentText.Trimmed(source), DocumentText.Trimmed(current), StringComparison.Ordinal))
            return false;
        assign(DocumentText.Trimmed(source));
        return true;
    }

    private static bool CopyNumeric(decimal source, decimal current, Action<decimal> assign)
    {
        if (source == current)
            return false;
        assign(source);
        return true;
    }
}
