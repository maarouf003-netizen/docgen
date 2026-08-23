using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

/// <summary>
/// استئناف يقع على الملف التنفيذي (DocumentAppeal): متابعة قرار رئيس التنفيذ أمام
/// محكمة الاستئناف المدنية الغرفة الناظرة بالقضايا التنفيذية، إما مستأنِفين (نحن) أو
/// مستأنف علينا (أحد أطراف الملف الآخرين هو المستأنف). يُنشأ من محامي الملف المالك،
/// ويُسند لمحامٍ للمتابعة من رئيس القسم، وتنتهي دورته بـ«محسوم» أو «مشطوب».
/// أطراف الاستئناف (المستأنف/المستأنف عليهم) تُثبَّت لقطةً نصية عند الإنشاء حتى لا
/// تتأثر بأي تعديل لاحق لأطراف الملف الأساس.
/// يمكن أن يقع أكثر من استئناف على ذات الملف، وبشكل مستقل تمامًا بين الملف المنيب والمناب.
/// </summary>
public class DocumentAppeal
{
    public int Id { get; set; }

    /// <summary>الملف التنفيذي الذي وقع عليه الاستئناف.</summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// اتجاه الاستئناف (AppealDirectionCatalog): "appellants" (مستأنِفين)
    /// أو "against-us" (مستأنف علينا). يُثبَّت عند الإنشاء ولا يتغير.
    /// </summary>
    public string Direction { get; set; } = AppealDirectionCatalog.Appellants;

    /// <summary>حالة الاستئناف (AppealStatusCatalog): منظور / محسوم / مشطوب.</summary>
    public string Status { get; set; } = AppealStatusCatalog.Pending;

    /// <summary>نوع الاستئناف نص حر (مصرفي / جمركي / عادي...) — اختياري.</summary>
    public string? AppealTypeLabel { get; set; }

    /// <summary>
    /// المستأنفون: لقطة JSON للأطراف التي تقدمت بالاستئناف عند اتجاه «مستأنِفين»،
    /// أو المنفذ عليهم المختارين عند اتجاه «مستأنف علينا».
    /// كل عنصر { kind, partyId, name }.
    /// </summary>
    public string AppellantsJson { get; set; } = "[]";

    /// <summary>
    /// المستأنف عليهم: لقطة JSON لباقي أطراف الملف كافة عدا المستأنفين وقت الإنشاء.
    /// كل عنصر { kind, partyId, name }.
    /// </summary>
    public string AppelleesJson { get; set; } = "[]";

    // ── حقول اتجاه «مستأنِفين» ─────────────────────────────────────────────

    /// <summary>نص القرار المطلوب استئنافه (قرار رئيس التنفيذ).</summary>
    public string? AppealedDecisionText { get; set; }

    /// <summary>ملخص القرار المطلوب استئنافه.</summary>
    public string? AppealedDecisionSummary { get; set; }

    /// <summary>تاريخ قرار رئيس التنفيذ المطلوب استئنافه.</summary>
    public DateTime? AppealedDecisionDate { get; set; }

    /// <summary>رقم كتاب المطالعة وإيداع الملف رئيس القسم.</summary>
    public string? InspectionBookNumber { get; set; }

    /// <summary>تاريخ كتاب المطالعة وإيداع الملف رئيس القسم.</summary>
    public DateTime? InspectionBookDate { get; set; }

    /// <summary>ملخص كتاب المطالعة المتضمن موجبات الاستئناف.</summary>
    public string? GroundsSummary { get; set; }

    // ── حقول اتجاه «مستأنف علينا» ──────────────────────────────────────────

    /// <summary>رقم ورود سند تبليغ الاستئناف.</summary>
    public string? NoticeNumber { get; set; }

    /// <summary>تاريخ ورود سند تبليغ الاستئناف.</summary>
    public DateTime? NoticeDate { get; set; }

    /// <summary>محكمة الاستئناف التنفيذية المختصة.</summary>
    public string? AppellateCourt { get; set; }

    /// <summary>رقم الأساس الاستئنافي (يُدوَّر سنويًا عبر AppealBaseNumber).</summary>
    public string? AppealBaseNumber { get; set; }

    /// <summary>سنة رقم الأساس الاستئنافي («لعام»).</summary>
    public string? AppealYear { get; set; }

    /// <summary>رقم كتاب إيداع الملف رئيس القسم.</summary>
    public string? DepositBookNumber { get; set; }

    /// <summary>تاريخ كتاب إيداع الملف رئيس القسم.</summary>
    public DateTime? DepositBookDate { get; set; }

    /// <summary>رأي المحامي المتابع للملف بأسباب الاستئناف.</summary>
    public string? DefenseOpinion { get; set; }

    // ── حقول القيد اللاحق (تاريخ إقرار الاستئناف) ──────────────────────────

    /// <summary>تاريخ إقرار الاستئناف (قيده أمام محكمة الاستئناف برقم الأساس).</summary>
    public DateTime? RegistrationDate { get; set; }

    // ── حقول الحسم (محسوم) ────────────────────────────────────────────────

    /// <summary>رقم قرار الحسم.</summary>
    public string? DecisionNumber { get; set; }

    /// <summary>تاريخ قرار الحسم.</summary>
    public DateTime? DecisionDate { get; set; }

    /// <summary>منطوق القرار (نص قرار الحسم).</summary>
    public string? DecisionRuling { get; set; }

    /// <summary>نتيجة الاستئناف (AppealOutcomeCatalog): للصالح / للضد.</summary>
    public string? Outcome { get; set; }

    // ── حقول الشطب (مشطوب) ────────────────────────────────────────────────

    /// <summary>تاريخ الشطب.</summary>
    public DateTime? StruckOffDate { get; set; }

    /// <summary>رقم قرار الشطب.</summary>
    public string? StruckOffDecisionNumber { get; set; }

    // ── عام ───────────────────────────────────────────────────────────────

    /// <summary>ملاحظات حرة على الاستئناف.</summary>
    public string? Notes { get; set; }

    /// <summary>المحامي المختص الذي يتابع الاستئناف (يختاره رئيس القسم).</summary>
    public int? AssignedLawyerId { get; set; }

    /// <summary>لحظة إسناد الاستئناف للمحامي المختص (UTC).</summary>
    public DateTime? AssignedAt { get; set; }

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Document Document { get; set; } = null!;
    public User? AssignedLawyer { get; set; }
    public User CreatedBy { get; set; } = null!;
    public ICollection<AppealAction> Actions { get; set; } = new List<AppealAction>();
    public ICollection<AppealBaseNumber> BaseNumbers { get; set; } = new List<AppealBaseNumber>();
}
