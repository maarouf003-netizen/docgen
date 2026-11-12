using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

/// <summary>
/// متدخل في ملف «الجهة العامة طالبة التنفيذ» أو «الجهة العامة منفذ عليها»:
/// سجل مستقل قابل للتكرار (أكثر من متدخل في الملف) يحمل بيانات طلب التدخل
/// (رقم/تاريخ الورود، تاريخ القبول، الموضوع، الملاحظات) وهوية الطرف حسب صفته:
/// شخص طبيعي (هوية كاملة + عنوان/وكيل + ممثل شرعي + ورثة)، شخص اعتباري، أو
/// جهة عامة مربوطة بالسجل المرجعي. تُحفظ التواريخ زمنيًا (`DateTime?` بـ`UTC`).
/// </summary>
public class DocumentIntervention
{
    public int Id { get; set; }
    public int DocumentId { get; set; }

    /// <summary>صفة المتدخل: natural / legal / public (PartyNatureCatalog).</summary>
    public string Nature { get; set; } = PartyNatureCatalog.Natural;

    /// <summary>رقم ورود طلب التدخل (نص حر اختياري على نمط أرقام الكتب).</summary>
    public string? ReceiptNumber { get; set; }

    /// <summary>تاريخ ورود طلب التدخل (إلزامي فعليًا — محور وقعة «طلب تدخل»).</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>تاريخ قبول طلب التدخل (اختياري، ويسبق تاريخ ورود الطلب دائمًا).</summary>
    public DateTime? AcceptanceDate { get; set; }

    /// <summary>موضوع التدخل (نص حر متعدد الأسطر).</summary>
    public string? Subject { get; set; }

    /// <summary>ملاحظات التدخل (نص حر متعدد الأسطر).</summary>
    public string? Notes { get; set; }

    // ── الشخص الطبيعي ───────────────────────────────────────────────────
    public string? Name { get; set; }
    public string? Father { get; set; }
    public string? Family { get; set; }
    public string? Mother { get; set; }
    /// <summary>مكان وتاريخ الولادة.</summary>
    public string? Birth { get; set; }
    /// <summary>مكان ورقم القيد.</summary>
    public string? Register { get; set; }
    public string? NationalId { get; set; }

    /// <summary>
    /// نوع العنوان: موطن مختار / عنوان / يمثله (تُعرض «وكيله القانوني»).
    /// يُصفَّر عند وجود ممثل شرعي أو ورثة — عنوان الممثل هو المعتبر، والورثة يعني وفاة المتدخل.
    /// </summary>
    public string? AddressType { get; set; } = "موطن مختار";

    /// <summary>العنوان أو الوكيل القانوني حسب <see cref="AddressType"/> — لا يُستخدم مع الممثل الشرعي أو الورثة.</summary>
    public string? Address { get; set; }

    // ── الممثل الشرعي للشخص الطبيعي ────────────────────────────────────
    public string? RepresentativeName { get; set; }
    public string? RepresentativeFather { get; set; }
    public string? RepresentativeFamily { get; set; }
    /// <summary>صفة الممثل الشرعي: ولي / وصي / قيم.</summary>
    public string? RepresentativeCapacity { get; set; }
    /// <summary>نوع عنوان الممثل الشرعي: موطن مختار / عنوان / وكيل قانوني.</summary>
    public string? RepresentativeAddressType { get; set; }
    public string? RepresentativeAddress { get; set; }

    // ── الشخص الاعتباري والجهة العامة ──────────────────────────────────
    /// <summary>اسم الشخص الاعتباري أو الجهة العامة (للجهة العامة يُؤخذ من السجل المرجعي).</summary>
    public string? EntityName { get; set; }
    /// <summary>فرع الجهة العامة (من السجل المرجعي).</summary>
    public string? EntityBranch { get; set; }
    /// <summary>رقم تسجيل الشخص الاعتباري (اختياري).</summary>
    public string? RegistrationNumber { get; set; }
    /// <summary>من يمثل الشخص الاعتباري (اختياري).</summary>
    public string? RepresentedBy { get; set; }
    /// <summary>المحافظة (للشخص الاعتباري حر، وللجهة العامة من السجل المرجعي).</summary>
    public string? Governorate { get; set; }

    /// <summary>معرّف قيد الجهة في السجل المرجعي — إلزامي للجهة العامة فقط، ويُفكّ بحذف القيد.</summary>
    public int? RegistryId { get; set; }

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Document Document { get; set; } = null!;
    public PublicEntity? Registry { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<DocumentInterventionHeir> Heirs { get; set; } = new List<DocumentInterventionHeir>();
}
