namespace DocGenerator.Domain.Entities;

/// <summary>
/// وريث متدخلٍ متوفى (شخص طبيعي): الاسم الثلاثي والصفة ونوع العنوان/قيمته،
/// وممثله الشرعي (ولي/وصي/قيم) إن وُجد. مرتبط بالمتدخل مباشرة، ويُحذف معه.
/// </summary>
public class DocumentInterventionHeir
{
    public int Id { get; set; }
    public int DocumentInterventionId { get; set; }

    public string? HeirName { get; set; }
    public string? HeirFather { get; set; }
    public string? HeirFamily { get; set; }

    /// <summary>صفة الوريث: أصالة / إضافة لتركة / أصالة وإضافة.</summary>
    public string? Capacity { get; set; } = "أصالة";

    /// <summary>نوع العنوان: عنوان / موطن مختار / وكيل (تُعرض «وكيله القانوني»).</summary>
    public string? AddressType { get; set; } = "عنوان";

    public string? HeirAddress { get; set; }

    public string? RepresentativeName { get; set; }
    public string? RepresentativeFather { get; set; }
    public string? RepresentativeFamily { get; set; }
    /// <summary>صفة الممثل الشرعي: ولي / وصي / قيم.</summary>
    public string? RepresentativeCapacity { get; set; }
    /// <summary>نوع عنوان الممثل الشرعي: موطن مختار / عنوان / وكيل قانوني.</summary>
    public string? RepresentativeAddressType { get; set; }
    public string? RepresentativeAddress { get; set; }

    public DocumentIntervention DocumentIntervention { get; set; } = null!;
}
