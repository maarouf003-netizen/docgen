using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

/// <summary>
/// المنفذ عليه الاعتباري في وضع «منفذ عليه»: جهة عامة (اسم الجهة + فرعها) أو شخص اعتباري
/// (شركة/مؤسسة) بالاسم الاعتباري ورقم تسجيله ومن يمثلها وعنوانها. لا تُتطلب بيانات هوية
/// شخصية لأن الطرف كيان اعتباري في الحالتين.
/// </summary>
public class ExecutedPublicEntity
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string? EntityName { get; set; }
    public string? EntityBranch { get; set; }

    /// <summary>المحافظة التي تتبع لها الجهة العامة أو الشخص الاعتباري (مثل: دمشق/اللاذقية) — قابلة للتعديل.</summary>
    public string? Governorate { get; set; }

    /// <summary>معرّف قيد هذه الجهة في السجل المرجعي المركزي عند الطبيعة (public) — اختياري ويُفكّ بحذف القيد.</summary>
    public int? RegistryId { get; set; }

    /// <summary>القيد المرجعي المرتبط.</summary>
    public PublicEntity? Registry { get; set; }

    /// <summary>نوع المنفذ عليه الاعتباري: جهة عامة (public) أو شخص اعتباري (legal).</summary>
    public string EntityNature { get; set; } = PartyNatureCatalog.PublicEntity;
    /// <summary>رقم تسجيل الشخص الاعتباري عند الطبيعة (legal) — اختياري.</summary>
    public string? RegistrationNumber { get; set; }
    /// <summary>من يمثل الشخص الاعتباري عند الطبيعة (legal) — اختياري.</summary>
    public string? RepresentedBy { get; set; }
    /// <summary>نوع عنوان الشخص الاعتباري: موطن مختار / عنوان / وكيل قانوني.</summary>
    public string? AddressType { get; set; }
    /// <summary>عنوان الشخص الاعتباري أو وكيله القانوني حسب نوع العنوان.</summary>
    public string? Address { get; set; }

    public Document Document { get; set; } = null!;
}
