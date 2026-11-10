using System.Globalization;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

/// <summary>
/// قواعد التحقق النقية لطلب إنشاء/تعديل الملف (بلا حالة ولا اعتماديات تخزين).
/// استُخرجت من DocumentService ضمن إعادة الهيكلة المتدرجة — السلوك مطابق حرفيًا.
/// </summary>
public static class DocumentValidator
{

    /// <summary>
    /// صفة الملف تُثبَّت عند الإنشاء: تُقبل القيم الصالحة فقط (applicant/executed)،
    /// والقيمة الفارغة تُفسَّر على أنها «الجهة العامة طالبة التنفيذ» للحفاظ على توافق الطلبات القائمة.
    /// </summary>
    public static void ValidateSide(DocumentUpsertRequest request)
    {
        var side = string.IsNullOrWhiteSpace(request.GeneralEntitySide)
            ? GeneralEntitySideCatalog.Applicant
            : request.GeneralEntitySide.Trim();

        if (!GeneralEntitySideCatalog.ValidSides.Contains(side))
            throw new ArgumentException("صفة الجهة العامة غير صالحة");

        request.GeneralEntitySide = side;
    }

    /// <summary>
    /// قيود عائلة وضع «الجهة العامة منفذ عليها» (Executed + Deposit): عادي فقط (لا مصرفي)،
    /// مقيد (لا مسودة)، وبلا مقترض/كفلاء/أموال. وتُطبق أيضًا على الملفات الحالية التي
    /// تُحرَّر بوضعها الجديد.
    /// </summary>
    public static void ValidateExecutedRequest(DocumentUpsertRequest request)
    {
        if (!GeneralEntitySideCatalog.IsExecutedLike(request.GeneralEntitySide))
            return;

        var sideLabel = GeneralEntitySideCatalog.ToLabel(request.GeneralEntitySide!);

        if (string.IsNullOrWhiteSpace(request.FileNumber) || string.IsNullOrWhiteSpace(request.FileYear))
            throw new ArgumentException($"ملف «{sideLabel}» يجب أن يكون مقيدًا برقم وسنة الملف");

        var selector = string.IsNullOrWhiteSpace(request.ContractTypeSelector)
            ? "عادي"
            : request.ContractTypeSelector.Trim();
        if (selector == "مصرفي")
            throw new ArgumentException($"ملف «{sideLabel}» يكون بعقد عادي فقط (لا مصرفي)");

        if (!string.IsNullOrWhiteSpace(request.BorrowerName)
            || request.Guarantors.Count > 0
            || request.Assets.Count > 0
            || request.BorrowerHeirs.Count > 0)
            throw new ArgumentException($"ملف «{sideLabel}» لا يتضمن مقترضًا أو كفلاء أو أموالًا");
    }

    /// <summary>
    /// تطبيع القوائم القابلة للفراغ قبل أي تحقق: مُهيَّئات `= new()` لا تحمي من
    /// `null` صريح في JSON (ولا من عناصر `null` داخل المصفوفة) فينكسر `.Count`
    /// والترتيب والتكرار بـ `NullReferenceException` (500) بدل رفض 400 — تُستبدل
    /// بالفارغ وتُسقَط العناصر الفارغة (كـ`NormalizeHeirs` وسائر المطبِّعات).
    /// القوائم الأخرى تُستهلَك عبر مطبِّعات آمنة للفراغ فلا حاجة لها هنا.
    /// </summary>
    public static void NormalizeCollections(DocumentUpsertRequest request)
    {
        request.Guarantors = request.Guarantors?.Where(g => g is not null).ToList() ?? new();
        request.Assets = request.Assets?.Where(a => a is not null).ToList() ?? new();
        request.BorrowerHeirs = request.BorrowerHeirs?.Where(h => h is not null).ToList() ?? new();
        request.ExecutionApplicants = request.ExecutionApplicants?.Where(a => a is not null).ToList() ?? new();
        request.ExecutedNaturalPersons = request.ExecutedNaturalPersons?.Where(p => p is not null).ToList() ?? new();
    }

    /// <summary>
    /// قاعدة «لا يجتمع الممثل الشرعي مع الورثة»: الشخص الطبيعي إمّا حيّ له ممثل شرعي،
    /// أو متوفى له ورثة — لا الاثنان. تُطبَّق على كل طرفٍ طبيعي (المقترض/الكفيل/طالب
    /// التنفيذ/المنفذ عليه) وتُرفض المخالفة صراحةً بلا تصفير صامت.
    /// الشخص الاعتباري خارج القاعدة: ممثله «من يمثلها» لا الممثل الشرعي.
    /// </summary>
    public static void ValidateRepresentativeHeirExclusivity(DocumentUpsertRequest request)
    {
        var borrowerNature = DocumentText.NormalizePartyNature(request.BorrowerNature);
        if (!PartyNatureCatalog.IsLegal(borrowerNature)
            && DocumentText.NormalizeHeirs(request.BorrowerHeirs, null).Count > 0
            && !DocumentText.IsEmptyRepresentative(
                request.BorrowerRepresentativeName,
                request.BorrowerRepresentativeFather,
                request.BorrowerRepresentativeFamily))
        {
            throw new ArgumentException(
                "لا يجتمع الممثل الشرعي مع الورثة — المقترض: احذف الممثل الشرعي أو الورثة أولًا");
        }

        foreach (var g in request.Guarantors)
        {
            var nature = DocumentText.NormalizePartyNature(g.Nature);
            if (PartyNatureCatalog.IsLegal(nature)
                || DocumentText.NormalizeHeirs(g.Heirs, g.GuarantorNumber).Count == 0
                || DocumentText.IsEmptyRepresentative(g.RepresentativeName, g.RepresentativeFather, g.RepresentativeFamily))
                continue;

            throw new ArgumentException(
                $"لا يجتمع الممثل الشرعي مع الورثة — الكفيل {g.GuarantorNumber}: احذف الممثل الشرعي أو الورثة أولًا");
        }

        // في وضعي «منفذ عليها/عرض وايداع» يكون طالب التنفيذ والمنفذ عليه الطبيعي حيّين،
        // والممثل الشرعي لهما (لا للمورث المتوفى)، فيجتمع مع ورثة المورث بلا تعارض.
        // القاعدة تبقى في «طالبة تنفيذ» للمقترض/الكفيل حيث الممثل الشرعي للمتوفى.
        if (!GeneralEntitySideCatalog.IsExecutedLike(request.GeneralEntitySide))
        {
            foreach (var a in request.ExecutionApplicants)
            {
                var name = (a.Name ?? string.Empty).Trim();
                var nature = DocumentText.NormalizePartyNature(a.Nature);
                if (name.Length == 0
                    || PartyNatureCatalog.IsLegal(nature)
                    || DocumentText.IsEmptyRepresentative(a.RepresentativeName, a.RepresentativeFather, a.RepresentativeFamily))
                    continue;

                // «إضافة لتركة» تعني متوفى حتى لو لم تُدخَل ورثته بعد؛ فلا يجتمع معها ممثل.
                var representationType = (a.RepresentationType ?? string.Empty).Trim();
                var hasEstate = representationType is "إضافة لتركة" or "أصالة وإضافة";
                if (hasEstate || DocumentText.NormalizeExecutedHeirs(a.Heirs).Count > 0)
                    throw new ArgumentException(
                        $"لا يجتمع الممثل الشرعي مع الورثة — طالب التنفيذ {name}: احذف الممثل الشرعي أو الورثة أولًا");
            }

            foreach (var p in request.ExecutedNaturalPersons)
            {
                var name = (p.Name ?? string.Empty).Trim();
                if (name.Length == 0
                    || DocumentText.IsEmptyRepresentative(p.RepresentativeName, p.RepresentativeFather, p.RepresentativeFamily))
                    continue;

                var representationType = (p.RepresentationType ?? string.Empty).Trim();
                var hasEstate = representationType is "إضافة لتركة" or "أصالة وإضافة";
                if (hasEstate || DocumentText.NormalizeExecutedHeirs(p.Heirs).Count > 0)
                    throw new ArgumentException(
                        $"لا يجتمع الممثل الشرعي مع الورثة — المنفذ عليه {name}: احذف الممثل الشرعي أو الورثة أولًا");
            }
        }
    }

    /// <summary>
    /// سقف ملاحق السند التنفيذي: الصفوف الفارغة تُتجاهل، والحد الأقصى 5. يُقرأ الطلب
    /// القديم (بلا قائمة) كملحق واحد للتوافق الخلفي.
    /// </summary>
    public static void ValidateAnnexes(DocumentUpsertRequest request)
    {
        var rows = DocumentAnnexText.Build(request);
        if (rows.Count > AnnexLimitsCatalog.MaxAnnexes)
            throw new ArgumentException(
                $"عدد ملاحق السند التنفيذي يتجاوز الحد الأقصى ({AnnexLimitsCatalog.MaxAnnexes})");
    }

    /// <summary>
    /// سقف أطراف الملف في صفة «الجهة العامة طالبة التنفيذ» (مصرفي/عادي):
    /// إجمالي المنفذ عليهم 50 شاملًا المقترض، أي حتى 49 صف كفلاء/منفذ عليهم آخرين
    /// (قرار المالك — `PartyLimitsCatalog`). ورثة الكفلاء والممثلون بلا سقف.
    /// رقم الكفيل هوية مستقرة فلا يقبل التكرار (يُفسد ربط الورثة والمقارنات)،
    /// ولا يتجاوز نطاق خانات القالب (1–49) وإلا سقط صامتًا من التوليد.
    /// </summary>
    public static void ValidateGuarantorCount(DocumentUpsertRequest request)
    {
        if (request.Guarantors.Count > PartyLimitsCatalog.MaxGuarantors)
            throw new ArgumentException(
                $"عدد الكفلاء/المنفذ عليهم الآخرين يتجاوز الحد الأقصى ({PartyLimitsCatalog.MaxGuarantors}) — الإجمالي مع المقترض {PartyLimitsCatalog.MaxTotalDebtors}");

        var outOfRange = request.Guarantors
            .Select(g => g.GuarantorNumber)
            .Where(n => n < 1 || n > PartyLimitsCatalog.MaxGuarantors)
            .Distinct()
            .ToList();
        if (outOfRange.Count > 0)
            throw new ArgumentException(
                $"أرقام الكفلاء خارج النطاق (1–{PartyLimitsCatalog.MaxGuarantors}): {string.Join("، ", outOfRange)} — لا خانات لها في القالب");

        var duplicates = request.Guarantors
            .GroupBy(g => g.GuarantorNumber)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
            throw new ArgumentException(
                $"أرقام الكفلاء مكررة: {string.Join("، ", duplicates)} — يجب أن يكون رقم كل كفيل فريدًا");
    }

    /// <summary>
    /// الملف المقيّد (بعد إدخال رقم الملف وسنة الملف) لا بد أن يحمل تاريخ قيد صالحًا،
    /// لأنه المعيار الوحيد في إحصاءات المتداول. وتُستثنى عائلة وضع «الجهة العامة منفذ عليها»
    /// لأن ملفها يقيده الخصم لا محامي الدولة، فتاريخ ورود الاخطار يغني عن تاريخ القيد.
    /// </summary>
    public static void ValidateRegistrationDate(DocumentUpsertRequest request)
    {
        if (GeneralEntitySideCatalog.IsExecutedLike(request.GeneralEntitySide))
            return;

        var hasFileNumber = !string.IsNullOrWhiteSpace(request.FileNumber);
        var hasFileYear = !string.IsNullOrWhiteSpace(request.FileYear);
        if (!hasFileNumber || !hasFileYear)
            return;

        if (string.IsNullOrWhiteSpace(request.FileRegistrationDate))
            throw new ArgumentException("تاريخ قيد الملف مطلوب عند إدخال رقم الملف وسنة الملف");

        if (!TryParseDate(request.FileRegistrationDate, out _))
            throw new ArgumentException("تاريخ قيد الملف غير صالح — استخدم مثال: 1/8/2026");
    }

    public static bool TryParseDate(string? value, out DateTime date)
    {
        var parsed = ActionDateParser.TryParse(value);
        if (parsed is { } result)
        {
            date = result;
            return true;
        }
        date = default;
        return false;
    }

    /// <summary>
    /// قاعدة عامة تسري على كل الكود: سنة الملف (رقم القيد/الأساس/الإعادة) لا تكون
    /// سنة مستقبلية — الحد الأقصى السنة الجارية بتوقيت دمشق. الفارغ مباح (مسودة/
    /// حقل اختياري)، وغير الرقمي يُترك لمساره القائم (لا تشديد جديد هنا).
    /// التطبيع الرقمي (عربية/فارسية ← لاتينية) قبل التحليل ليُمسك «٢٠٢٧» أيضًا.
    /// </summary>
    public static void ValidateFileYearNotFuture(string? fileYear, int currentYear)
    {
        var normalized = DigitNormalizer.NormalizeDigitsOrNull(fileYear);
        if (string.IsNullOrEmpty(normalized))
            return;
        if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)
            && year > currentYear)
            throw new ArgumentException($"سنة الملف ({normalized}) لم تأتِ بعد — الحد الأقصى السنة الجارية ({currentYear})");
    }

    /// <summary>
    /// التاريخ في وضع «منفذ عليه» يُرسَل نصًا حرًا (مثال: 1/8/2026) فيُفسَّر ويُخزَّن زمنيًا
    /// في القاعدة. الفارغ يعني null، وغير الصالح يُرفض برسالة تحمل اسم الحقل.
    /// </summary>
    public static DateTime? ParseDateTime(string? value, string fieldName)
        => FreeDateParser.Parse(value, fieldName);

    public static void RequireField(Dictionary<string, string?> fields, string key, string label)
    {
        if (string.IsNullOrWhiteSpace(fields.GetValueOrDefault(key)))
            throw new ArgumentException($"يجب إدخال {label} على الأقل");
    }
}