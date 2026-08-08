namespace DocGenerator.Domain.Entities;

/// <summary>
/// الجهة العامة المنفذ عليها في وضع «منفذ عليه» — الجهة المطلوب التنفيذ عليها
/// (اسم الجهة + فرع الجهة). لا تُتطلب بيانات هوية شخصية لأنها كيان اعتباري.
/// </summary>
public class ExecutedPublicEntity
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string? EntityName { get; set; }
    public string? EntityBranch { get; set; }

    public Document Document { get; set; } = null!;
}
