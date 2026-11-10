namespace DocGenerator.Application.Common.Options;

/// <summary>
/// مفتاح تعطيل/تفعيل توليد مستندات Word مؤقتًا. القيمة الافتراضية معطّلة لحين
/// توحيد النماذج المولدة؛ تُفعَّل صراحةً عند اكتمال التوحيد.
/// </summary>
public class DocumentGenerationOptions
{
    public const string SectionName = "DocumentGeneration";

    public bool Enabled { get; set; }

    public string DisabledMessage { get; set; } =
        "الميزة معطلة حاليًا لحين توحيد النماذج المولدة";
}
