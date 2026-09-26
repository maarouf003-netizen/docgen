namespace DocGenerator.Domain.Entities;

/// <summary>
/// مراسلة رسمية بين مندوب جهة عامة ومحامٍ/رئيس قسم (أو بين محامٍ ورئيس قسم لمندوبٍ معيَّن بالاسم).
/// مرتبطة بملف تنفيذي أو عامة غير مرتبطة. يُولَّد رقمها تلقائيًا بصيغة {الرمز}-{السنة}-{عشوائي}
/// عند الحفظ، ويؤخذ تاريخها من لحظة الإرسال. المراسلة المرسلة وثيقة رسمية: لا يُعدَّل نصها
/// ولا تُحذف، وتُستكمل بلاحقٍ (من المنشئ) أو ردٍّ (من الطرف المستلم) فقط.
/// </summary>
public class Correspondence
{
    /// <summary>أهمية عادية.</summary>
    public const string ImportanceNormal = "normal";

    /// <summary>مراسلة هامة.</summary>
    public const string ImportanceImportant = "important";

    /// <summary>مراسلة عاجلة — تُظهر جرسًا للطرف الذي لم يؤكد مشاهدتها.</summary>
    public const string ImportanceUrgent = "urgent";

    /// <summary>حالة اطلاع: الطرف المستلم أكّد مشاهدته (توثيق واحد له لا يتجدّد).</summary>
    public const string ViewStatusSeen = "seen";

    /// <summary>حالة اطلاع: لم يؤكّد الطرف المستلم مشاهدته بعد.</summary>
    public const string ViewStatusPending = "pending";

    public int Id { get; set; }

    /// <summary>
    /// فرع المراسلة؛ null للمراسلة العامة المسطَّرة من مندوب جهة (بلا فرع) —
    /// محافظتها عندها من نطاق جهة المندوب.
    /// </summary>
    public int? BranchId { get; set; }

    /// <summary>
    /// محافظة المراسلة (إجبارية): للمرتبطة بملف أو المسطَّرة من محامٍ/رئيس تُؤخذ من الفرع،
    /// وللعامة من مندوب تُؤخذ من نطاق جهته. عليها تُبنى رؤية رئيس القسم وفلتر المدير/المشرف.
    /// </summary>
    public string Governorate { get; set; } = string.Empty;

    /// <summary>منشئ المراسلة (محامٍ أو رئيس قسم أو مندوب جهة).</summary>
    public int CreatedById { get; set; }

    /// <summary>الطرف المستلم المعيَّن بالاسم (محامٍ أو رئيس قسم أو مندوب جهة).</summary>
    public int TargetUserId { get; set; }

    /// <summary>الملف التنفيذي المرتبط؛ null يعني «مراسلة عامة غير مرتبطة بملف».</summary>
    public int? DocumentId { get; set; }

    /// <summary>رقم المراسلة المولد تلقائيًا (فريد).</summary>
    public string CorrespondenceNumber { get; set; } = string.Empty;

    /// <summary>تاريخ إرسال المراسلة (UTC) المعروض في السجلات.</summary>
    public DateTime CorrespondenceDate { get; set; } = DateTime.UtcNow;

    /// <summary>الأهمية: normal / important / urgent — تُحدَّد عند التسطير ولا تتغير بعده.</summary>
    public string Importance { get; set; } = ImportanceNormal;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Branch? Branch { get; set; }
    public User? CreatedBy { get; set; }
    public User? TargetUser { get; set; }
    public Document? Document { get; set; }

    public ICollection<CorrespondenceMessage> Messages { get; set; } = new List<CorrespondenceMessage>();
    public ICollection<CorrespondenceReceipt> Receipts { get; set; } = new List<CorrespondenceReceipt>();

    public static bool IsValidImportance(string? importance)
        => importance is ImportanceNormal or ImportanceImportant or ImportanceUrgent;

    public static bool IsUrgent(string? importance)
        => string.Equals(importance, ImportanceUrgent, StringComparison.Ordinal);
}
