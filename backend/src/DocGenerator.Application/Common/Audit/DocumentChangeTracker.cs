using System.Globalization;
using System.Reflection;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Audit;

/// <summary>
/// محرك تتبع تغيّرات الملف التنفيذي على مستوى الحقل: يلتقط لقطة القيم القابلة
/// للتتبع قبل التعديل، ثم يقارنها بعد الحفظ ليُنتج صفوف «حقل / قيمة قبل / قيمة بعد».
/// يشمل كل الحقول العددية للكيان، ويتابع أيضًا تركيب المجموعات (كفالات، ورثة، أصول…)
/// بتوقيعات نصية مكثفة. الاستثناءات التقنية فقط (أعداد مشاهدة/طوابع زمنية/حقول مشتقة).
/// </summary>
public static class DocumentChangeTracker
{
    /// <summary>حقول تقنية مستثناة من التتبع (تُدار بإجراءاتها الخاصة أو مشتقة داخليًا).</summary>
    private static readonly HashSet<string> ExcludedFields = new()
    {
        nameof(Document.Id), nameof(Document.CreatedAt), nameof(Document.UpdatedAt),
        nameof(Document.CreatedById), nameof(Document.BranchId), nameof(Document.IsDeleted),
        nameof(Document.DeletedAt), nameof(Document.ViewCount), nameof(Document.PrintCount),
        nameof(Document.FullData), nameof(Document.SearchText), nameof(Document.FilePath),
        nameof(Document.SourceDelegationId),
    };

    /// <summary>الخصائص العددية المتتبَّعة — تُحسب مرة واحدة ويُعاد استخدامها.</summary>
    private static readonly PropertyInfo[] TrackedProperties = typeof(Document)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanRead && p.CanWrite && IsTrackedType(p.PropertyType))
        .Where(p => !ExcludedFields.Contains(p.Name))
        .OrderBy(p => p.Name, StringComparer.Ordinal)
        .ToArray();

    /// <summary>
    /// تسميات الحقول العربية المعروضة — تُجمَّد في صفوف السجل وقت الكتابة
    /// كي لا يتأثر الأرشيف بتطور التسميات لاحقًا.
    /// </summary>
    private static readonly Dictionary<string, string> FieldLabels = new(StringComparer.Ordinal)
    {
        [nameof(Document.DocumentType)] = "نوع المستند",
        [nameof(Document.IsDraft)] = "مسودة",
        [nameof(Document.BorrowerName)] = "اسم المنفذ عليه",
        [nameof(Document.BorrowerFather)] = "اسم الأب",
        [nameof(Document.BorrowerFamily)] = "اسم العائلة",
        [nameof(Document.BorrowerMother)] = "اسم الأم",
        [nameof(Document.BorrowerBirth)] = "مكان وتاريخ الولادة",
        [nameof(Document.BorrowerRegister)] = "مكان ورقم القيد",
        [nameof(Document.BorrowerNationalId)] = "الرقم الوطني",
        [nameof(Document.BorrowerAddress)] = "العنوان",
        [nameof(Document.BorrowerAddressType)] = "نوع العنوان",
        [nameof(Document.BorrowerRepresentativeName)] = "اسم الوكيل",
        [nameof(Document.BorrowerRepresentativeFather)] = "اسم والد الوكيل",
        [nameof(Document.BorrowerRepresentativeFamily)] = "عائلة الوكيل",
        [nameof(Document.BorrowerRepresentativeCapacity)] = "صفة الوكيل",
        [nameof(Document.BorrowerRepresentativeAddressType)] = "نوع عنوان الوكيل",
        [nameof(Document.BorrowerRepresentativeAddress)] = "عنوان الوكيل",
        [nameof(Document.BorrowerNature)] = "طبيعة المنفذ عليه",
        [nameof(Document.BorrowerRegistrationNumber)] = "رقم القيد (كيان)",
        [nameof(Document.BorrowerRepresentedBy)] = "يمثله",
        [nameof(Document.ContractType)] = "نوع العقد",
        [nameof(Document.ContractTypeSelector)] = "محدد نوع العقد",
        [nameof(Document.ContractNumber)] = "رقم العقد",
        [nameof(Document.ContractDate)] = "تاريخ العقد",
        [nameof(Document.AnnexType)] = "نوع الملحق",
        [nameof(Document.AnnexNumber)] = "رقم الملحق",
        [nameof(Document.AnnexDate)] = "تاريخ الملحق",
        [nameof(Document.InclusionText)] = "نص التضمين",
        [nameof(Document.AmountNumeric)] = "المبلغ",
        [nameof(Document.AmountWords)] = "المبلغ كتابةً",
        [nameof(Document.Currency)] = "عملة المبلغ",
        [nameof(Document.Amount2Numeric)] = "المبلغ الثاني",
        [nameof(Document.Amount2Words)] = "المبلغ الثاني كتابةً",
        [nameof(Document.Currency2)] = "عملة المبلغ الثاني",
        [nameof(Document.Amount3Numeric)] = "المبلغ الثالث",
        [nameof(Document.Amount3Words)] = "المبلغ الثالث كتابةً",
        [nameof(Document.Currency3)] = "عملة المبلغ الثالث",
        [nameof(Document.InclusionAmountNumeric)] = "مبلغ التضمين",
        [nameof(Document.InclusionAmountWords)] = "مبلغ التضمين كتابةً",
        [nameof(Document.InclusionCurrency)] = "عملة التضمين",
        [nameof(Document.InclusionAmount2Numeric)] = "مبلغ التضمين الثاني",
        [nameof(Document.InclusionAmount2Words)] = "مبلغ التضمين الثاني كتابةً",
        [nameof(Document.InclusionCurrency2)] = "عملة التضمين الثاني",
        [nameof(Document.InclusionAmount3Numeric)] = "مبلغ التضمين الثالث",
        [nameof(Document.InclusionAmount3Words)] = "مبلغ التضمين الثالث كتابةً",
        [nameof(Document.InclusionCurrency3)] = "عملة التضمين الثالث",
        [nameof(Document.Court)] = "دائرة التنفيذ",
        [nameof(Document.Applicant)] = "طالب التنفيذ",
        [nameof(Document.ApplicantRegistryId)] = "ربط جهة الطالب بالسجل المرجعي",
        [nameof(Document.Lawyer)] = "المحامي",
        [nameof(Document.ReferredFromLawyer)] = "محامي الإحالة",
        [nameof(Document.ReferredAt)] = "تاريخ الإحالة",
        [nameof(Document.FileNumber)] = "رقم الملف",
        [nameof(Document.FileType)] = "نوع الملف",
        [nameof(Document.FileYear)] = "سنة الملف",
        [nameof(Document.FileIncoming)] = "رقم الصادر",
        [nameof(Document.FileIncomingDate)] = "تاريخ الصادر",
        [nameof(Document.UnderFilingNumber)] = "رقم تحت الإيداع",
        [nameof(Document.FileArrivalNumber)] = "رقم ورود الملف",
        [nameof(Document.FileArrivalDate)] = "تاريخ ورود الملف",
        [nameof(Document.BranchName)] = "فرع الجهة العامة",
        [nameof(Document.ExecStatus)] = "الحالة التنفيذية",
        [nameof(Document.ExecSubStatus)] = "الحالة الفرعية",
        [nameof(Document.CollectedAmount)] = "المبلغ المقبوض",
        [nameof(Document.CollectedAmount2)] = "المبلغ المقبوض الثاني",
        [nameof(Document.CollectedAmount3)] = "المبلغ المقبوض الثالث",
        [nameof(Document.CollectedCurrency)] = "عملة المقبوض",
        [nameof(Document.CollectedCurrency2)] = "عملة المقبوض الثاني",
        [nameof(Document.CollectedCurrency3)] = "عملة المقبوض الثالث",
        [nameof(Document.GeneralEntitySide)] = "الجهة العامة",
        [nameof(Document.ExecutedStatus)] = "حالة الوضع",
        [nameof(Document.WasDepositExecuted)] = "سبق تنفيذه (وايداع)",
        [nameof(Document.ExecutedDescription)] = "وصف المنفذ عليه",
        [nameof(Document.FileReceiptDate)] = "تاريخ ورود الاخطار",
        [nameof(Document.FileReceiptNumber)] = "رقم ورود الاخطار",
        [nameof(Document.ExecutedRequiredAmount)] = "المبلغ المطلوب تنفيذه",
        [nameof(Document.ExecutedRequiredCurrency)] = "عملة المطلوب",
        [nameof(Document.ExecutedRequiredAmount2)] = "المبلغ المطلوب الثاني",
        [nameof(Document.ExecutedRequiredCurrency2)] = "عملة المطلوب الثاني",
        [nameof(Document.ExecutedRequiredAmount3)] = "المبلغ المطلوب الثالث",
        [nameof(Document.ExecutedRequiredCurrency3)] = "عملة المطلوب الثالث",
        [nameof(Document.ExecutedPaidAmount)] = "المبلغ المدفوع",
        [nameof(Document.ExecutedPaidCurrency)] = "عملة المدفوع",
        [nameof(Document.ExecutedPaidAmount2)] = "المبلغ المدفوع الثاني",
        [nameof(Document.ExecutedPaidCurrency2)] = "عملة المدفوع الثاني",
        [nameof(Document.ExecutedPaidAmount3)] = "المبلغ المدفوع الثالث",
        [nameof(Document.ExecutedPaidCurrency3)] = "عملة المدفوع الثالث",
        [nameof(Document.ExecutedDepositDate)] = "تاريخ الإيداع",
        [nameof(Document.ExecutedExecutionDate)] = "تاريخ التنفيذ",
        [nameof(Document.StruckOffDate)] = "تاريخ الشطب",
        [nameof(Document.RenewalFileReceiptNumber)] = "رقم اخطار تجديد الملف",
        [nameof(Document.RenewalFileReceiptDate)] = "تاريخ اخطار التجديد",
        [nameof(Document.RenewalFileNumber)] = "رقم ملف التجديد",
        [nameof(Document.RenewalFileType)] = "نوع ملف التجديد",
        [nameof(Document.RenewalDate)] = "تاريخ التجديد",
        [nameof(Document.BaraetNumber)] = "رقم البراءة",
        [nameof(Document.BaraetDate)] = "تاريخ البراءة",
        [nameof(Document.BaraetRegNumber)] = "رقم قيد البراءة",
        [nameof(Document.BaraetRegDate)] = "تاريخ قيد البراءة",
        [nameof(Document.ForcedExecutionDate)] = "تاريخ التنفيذ الجبري",
        [nameof(Document.ForcibleTransferDate)] = "تاريخ تحويل بدل المبيع",
        [nameof(Document.ForcibleTransferNoticeNumber)] = "رقم إشعار بدل المبيع",
        [nameof(Document.TarithNumber)] = "رقم كتاب التريث",
        [nameof(Document.TarithDate)] = "تاريخ كتاب التريث",
        [nameof(Document.TarithRegNumber)] = "رقم قيد التريث",
        [nameof(Document.TarithRegDate)] = "تاريخ قيد التريث",
        [nameof(Document.SayerNumber)] = "رقم كتاب السير بالملف",
        [nameof(Document.SayerDate)] = "تاريخ كتاب السير بالملف",
        [nameof(Document.SayerRegNumber)] = "رقم ورود كتاب السير بالملف",
        [nameof(Document.SayerRegDate)] = "تاريخ ورود كتاب السير بالملف",
        [nameof(Document.SoldAssetIds)] = "معرفات الأموال المبيعة",
        [nameof(Document.SeizureDate)] = "تاريخ الحجز",
        [nameof(Document.ImmediateActions)] = "الإجراءات الفورية",
        [nameof(Document.Notes)] = "الملاحظات",

        // حقول المجموعات (توقيعات التركيب)
        ["__Col_Guarantors"] = "الكفلاء",
        ["__Col_Heirs"] = "ورثة المنفذ عليه",
        ["__Col_Assets"] = "الأموال المحجوزة",
        ["__Col_ExecutionApplicants"] = "طالبو التنفيذ",
        ["__Col_ExecutedPublicEntities"] = "الجهات العامة المنفذ عليها",
        ["__Col_ExecutedNaturalPersons"] = "الأشخاص الطبيعيون المنفذ عليهم",
        ["__Col_ExecutedHeirs"] = "ورثة الجهات المنفذ عليها",
    };

    /// <summary>خرائط القيم المرمزة إلى عبارات عربية للعرض المؤسسي.</summary>
    private static readonly Dictionary<string, Dictionary<string, string>> ValueMaps =
        new(StringComparer.Ordinal)
        {
            [nameof(Document.GeneralEntitySide)] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["applicant"] = "الجهة العامة طالب التنفيذ",
                ["executed"] = "الجهة العامة منفذ عليها",
                ["deposit"] = "عرض وايداع",
            },
            [nameof(Document.BorrowerNature)] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["natural"] = "شخص طبيعي",
                ["publicentity"] = "كيان عام",
            },
        };

    /// <summary>التقط لقطة القيم المتتبعة للملف قبل تعديله.</summary>
    public static Dictionary<string, string?> Capture(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var snapshot = new Dictionary<string, string?>(TrackedProperties.Length + 7, StringComparer.Ordinal);
        foreach (var property in TrackedProperties)
            snapshot[property.Name] = Format(property.GetValue(document), property.Name);
        foreach (var collection in CollectionSignatures)
            snapshot[collection.Key] = collection.Value(document);
        return snapshot;
    }

    /// <summary>
    /// يقارن اللقطة بالحالة الراهنة بعد الحفظ ويُنتج صفوف التغييرات مرتبة باسم الحقل.
    /// القائمة فارغة حين لا يتغير شيء تتبعه.
    /// </summary>
    public static List<DocumentFieldChange> Diff(
        IReadOnlyDictionary<string, string?> before, Document after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var current = Capture(after);
        var changes = new List<DocumentFieldChange>();

        foreach (var (key, oldValue) in before.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!current.TryGetValue(key, out var newValue))
                continue;
            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
                continue;

            changes.Add(new DocumentFieldChange
            {
                DocumentId = after.Id,
                FieldKey = key,
                FieldLabel = ResolveLabel(key),
                OldValue = oldValue,
                NewValue = newValue,
            });
        }

        return changes;
    }

    private static bool IsTrackedType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(string)
            || underlying == typeof(bool)
            || underlying == typeof(int)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime);
    }

    private static string? Format(object? value, string fieldKey)
    {
        if (value is null)
            return null;

        // الخرائط المرمزة تُطبَّق أولًا (القيم النصية للكتالوجات) قبل التنسيق العام.
        var rawValue = value.ToString() ?? string.Empty;
        if (rawValue.Length > 0
            && ValueMaps.TryGetValue(fieldKey, out var map)
            && map.TryGetValue(rawValue, out var mapped))
            return mapped;

        switch (value)
        {
            case string text:
                var trimmed = text.Trim();
                return trimmed.Length == 0 ? null : trimmed;

            case bool flag:
                return flag ? "نعم" : "لا";

            case DateTime date:
                return date.TimeOfDay == TimeSpan.Zero
                    ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            case decimal number:
                return number.ToString("0.########", CultureInfo.InvariantCulture);

            case int integer:
                return integer.ToString(CultureInfo.InvariantCulture);
        }

        return rawValue.Length == 0 ? null : rawValue;
    }

    private static string ResolveLabel(string key)
        => FieldLabels.TryGetValue(key, out var label) ? label : key;

    /// <summary>
    /// موقّعات المجموعات: قائمة أوصاف مختصرة لكل مجموعة؛ أي اختلاف في الترتيب
    /// أو العدد أو المحتوى يظهر كتغيير واحد مفهوم مؤسسيًا.
    /// </summary>
    private static readonly Dictionary<string, Func<Document, string?>> CollectionSignatures =
        new(StringComparer.Ordinal)
        {
            ["__Col_Guarantors"] = d => Signature(d.Guarantors,
                g => Join(g.GuarantorName, g.GuarantorFather, g.GuarantorFamily)),
            ["__Col_Heirs"] = d => Signature(d.Heirs,
                h => Join(h.HeirName, h.HeirFather, h.HeirFamily)),
            ["__Col_Assets"] = d => Signature(d.Assets,
                a => Join(a.AssetKind,
                    FirstNonEmpty(a.Property, a.PlateNumber, a.RegisterNumber, a.LicenseNumber, a.ShopDescription))),
            ["__Col_ExecutionApplicants"] = d => Signature(d.ExecutionApplicants,
                e => Join(e.Name, e.Father, e.Family)),
            ["__Col_ExecutedPublicEntities"] = d => Signature(d.ExecutedPublicEntities,
                e => Join(e.EntityName, e.EntityBranch)),
            ["__Col_ExecutedNaturalPersons"] = d => Signature(d.ExecutedNaturalPersons,
                e => Join(e.Name, e.Family)),
            ["__Col_ExecutedHeirs"] = d => Signature(d.ExecutedHeirs,
                e => Join(e.HeirName, e.HeirFamily)),
        };

    private static string? Signature<T>(IEnumerable<T>? items, Func<T, string> describe)
    {
        var descriptors = (items ?? Enumerable.Empty<T>())
            .Select(describe)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        return descriptors.Count == 0 ? null : string.Join("؛ ", descriptors);
    }

    private static string Join(params string?[] parts)
        => string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
