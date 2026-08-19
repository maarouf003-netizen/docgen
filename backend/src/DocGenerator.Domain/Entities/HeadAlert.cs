using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

/// <summary>
/// تنبيه يصدره رئيس القسم لفرعه، ويصل للمحامين كجهة استلام واحدة أو أكثر:
/// مرتبط بملف، أو رسالة خاصة لمحامٍ، أو تعميم لكل محامي الفرع.
/// الاستلام الفردي لكل محامٍ مسجّل في <see cref="HeadAlertRecipient"/>.
/// </summary>
public class HeadAlert
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int CreatedById { get; set; }
    public HeadAlertTargetType TargetType { get; set; }
    public int? DocumentId { get; set; }
    public int? TargetLawyerId { get; set; }
    /// <summary>رابط تنبيهات دورة حياة الإنابة (بانتظار الاعتماد/الإتمام) بالإنابة نفسها لتصفيتها تلقائيًا.</summary>
    public int? DelegationId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Branch? Branch { get; set; }
    public User? CreatedBy { get; set; }
    public Document? Document { get; set; }
    public User? TargetLawyer { get; set; }
    public ICollection<HeadAlertRecipient> Recipients { get; set; } = new List<HeadAlertRecipient>();
}
