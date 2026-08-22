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