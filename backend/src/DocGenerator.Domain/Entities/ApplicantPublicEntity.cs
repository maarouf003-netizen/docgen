namespace DocGenerator.Domain.Entities;

/// <summary>
/// الجهة العامة طالبة التنفيذ في وضع «الجهة العامة طالبة تنفيذ» (اسم الجهة + فرعها + محافظتها)،
/// واحدة أو أكثر. لا تُتطلب بيانات هوية شخصية لأنها كيان اعتباري.
/// </summary>
public class ApplicantPublicEntity
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string? Name { get; set; }
    public string? Branch { get; set; }

    /// <summary>المحافظة التي تتبع لها الجهة (مثل: دمشق/اللاذقية) — تُملأ تلقائيًا من فرع المحامي وقابلة للتعديل.</summary>
    public string? Governorate { get; set; }

    /// <summary>معرّف قيد هذه الجهة في السجل المرجعي المركزي (اختياري — يُفكّ الارتباط بحذف القيد).</summary>
    public int? RegistryId { get; set; }

    /// <summary>القيد المرجعي المرتبط.</summary>
    public PublicEntity? Registry { get; set; }

    public Document Document { get; set; } = null!;
}
