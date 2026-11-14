using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.DTOs;

/// <summary>
/// طلب إضافة/تعديل متدخل في الملف. الصفة تحدد مجموعة الحقول المعتمدة وتُصفَّر
/// حقول الصفات الأخرى، والتواريخ نصوص حرة (قاعدة حقول التواريخ) تُفسَّر خادميًا.
/// </summary>
public class DocumentInterventionUpsertRequest
{
    /// <summary>عدّاد التزامن المتفائل (P0-06) — يُفحص في التعديل فقط؛ غيابه = فحص القاعدة.</summary>
    public long? Version { get; set; }

    public string? Nature { get; set; } = PartyNatureCatalog.Natural;

    /// <summary>رقم ورود طلب التدخل (اختياري).</summary>
    public string? ReceiptNumber { get; set; }

    /// <summary>تاريخ ورود طلب التدخل (نص حر، إلزامي فعليًا — مثال: 1/8/2026).</summary>
    public string? ReceiptDate { get; set; }

    /// <summary>تاريخ قبول طلب التدخل (نص حر اختياري، ويسبق تاريخ ورود الطلب دائمًا).</summary>
    public string? AcceptanceDate { get; set; }

    /// <summary>موضوع التدخل (نص حر متعدد الأسطر).</summary>
    public string? Subject { get; set; }

    /// <summary>ملاحظات التدخل (نص حر متعدد الأسطر).</summary>
    public string? Notes { get; set; }

    // ── الشخص الطبيعي ───────────────────────────────────────────────────
    public string? Name { get; set; }
    public string? Father { get; set; }
    public string? Family { get; set; }
    public string? Mother { get; set; }
    public string? Birth { get; set; }
    public string? Register { get; set; }
    public string? NationalId { get; set; }
    public string? AddressType { get; set; }
    public string? Address { get; set; }

    public string? RepresentativeName { get; set; }
    public string? RepresentativeFather { get; set; }
    public string? RepresentativeFamily { get; set; }
    public string? RepresentativeCapacity { get; set; }
    public string? RepresentativeAddressType { get; set; }
    public string? RepresentativeAddress { get; set; }

    /// <summary>ورثة المتدخل الطبيعي (نفس عقد ورثة الأطراف القائم).</summary>
    public List<HeirDto> Heirs { get; set; } = new();

    // ── الشخص الاعتباري والجهة العامة ──────────────────────────────────
    public string? EntityName { get; set; }
    public string? EntityBranch { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? RepresentedBy { get; set; }
    public string? Governorate { get; set; }

    /// <summary>معرّف قيد الجهة في السجل المرجعي — إلزامي للجهة العامة.</summary>
    public int? RegistryId { get; set; }
}

/// <summary>
/// متدخل في استجابة الملف: يحمل الحقول كافةً ليملأ نافذة التعديل ونافذة التفاصيل،
/// والتواريخ تُعاد نصًا `yyyy-MM-dd` وفق عقد حقول التواريخ.
/// </summary>
public record DocumentInterventionDto(
    int Id,
    string Nature,
    string NatureLabel,
    string DisplayName,
    string? ReceiptNumber,
    string? ReceiptDate,
    string? AcceptanceDate,
    string? Subject,
    string? Notes,
    string? Name,
    string? Father,
    string? Family,
    string? Mother,
    string? Birth,
    string? Register,
    string? NationalId,
    string? AddressType,
    string? Address,
    string? RepresentativeName,
    string? RepresentativeFather,
    string? RepresentativeFamily,
    string? RepresentativeCapacity,
    string? RepresentativeAddressType,
    string? RepresentativeAddress,
    string? EntityName,
    string? EntityBranch,
    string? RegistrationNumber,
    string? RepresentedBy,
    string? Governorate,
    int? RegistryId,
    List<HeirDto> Heirs,
    /// <summary>عدّاد التزامن المتفائل (P0-06) — يُعاد للعميل ليُرسله في التعديل.</summary>
    long Version = 0);
