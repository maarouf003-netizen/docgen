namespace DocGenerator.Domain.Entities;

/// <summary>
/// توثيق مشاهدة مراسلة: زر «تمت المشاهدة» الصريح يسجّل (من شاهد؟ متى؟) لكل مطّلع،
/// فيبقى إثبات الاطلاع في السجل الرسمي بدل الاعتماد على فتح الصفحة ضمنيًا.
/// </summary>
public class CorrespondenceReceipt
{
    public int Id { get; set; }

    public int CorrespondenceId { get; set; }

    /// <summary>المطّلع الذي أكّد المشاهدة.</summary>
    public int UserId { get; set; }

    /// <summary>اسم المطّلع لحظة التأكيد (لقطة للسجل).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>لحظة تأكيد المشاهدة (UTC).</summary>
    public DateTime SeenAt { get; set; } = DateTime.UtcNow;

    public Correspondence Correspondence { get; set; } = null!;
}
