using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

/// <summary>
/// اقتراح محامٍ لإضافة جهة عامة جديدة إلى السجل (د4): يدخل بحالة Pending ولا
/// يظهر لبوات المندوبين ولا يُربط نهائيًا حتى اعتماد رئيس القسم (أو رفضه بسبب).
/// نص التحذير والصيغة (د7/د8) تُلتقطان في الواجهة وتُخزَّن قيمهما هنا.
/// </summary>
public class PublicEntityProposal
{
    public int Id { get; set; }

    /// <summary>اسم الجهة كما أدخله المحامي (max 200).</summary>
    public string ProposedName { get; set; } = string.Empty;

    /// <summary>نوع الجهة (كتالوج نصي).</summary>
    public string EntityType { get; set; } = PublicEntityTypeCatalog.Ministry;

    /// <summary>المحافظة (max 100).</summary>
    public string Governorate { get; set; } = string.Empty;

    /// <summary>اسم الفرع (max 200).</summary>
    public string BranchName { get; set; } = string.Empty;

    /// <summary>صيغة المناداة (د8): add-to-job / add-to-position.</summary>
    public string CitationFormula { get; set; } = CitationFormulaCatalog.AddToJob;

    public int ProposedById { get; set; }

    /// <summary>الملف الذي قُدِّر منه الاقتراح (اختياري للسياق) — يُفكّ بحذف الملف.</summary>
    public int? SourceDocumentId { get; set; }

    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;

    /// <summary>من رفض الاقتراح (يُملأ عند الرفض فقط).</summary>
    public int? RejectedById { get; set; }

    /// <summary>سبب الرفض (يُملأ عند الرفض فقط).</summary>
    public string? RejectionReason { get; set; }

    /// <summary>القيد الذي أُنشئ من الاقتراح عند الاعتماد.</summary>
    public int? CreatedPublicEntityId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User ProposedBy { get; set; } = null!;

    public Document? SourceDocument { get; set; }

    public User? RejectedBy { get; set; }

    public PublicEntity? CreatedPublicEntity { get; set; }
}
