namespace DocGenerator.Application.Common;

/// <summary>
/// عنوان الملف المعروض في ملخصات/تفاصيل التنبيهات — المصدر الوحيد لبنائه
/// (ورقة إصلاح PR-4 §6.1ب): يجمع اسم المستفيد واسم الأب والعائلة، ثم نوع الملف،
/// ثم رقم المستند كحلٍّ أخير، ويميّز «مستند محذوف» عند غياب بيانات الملف مع بقاء المعرّف.
///
/// <para>العنوان يُبنى من حقول مسطّحة فقط، فلا يعتمد على كيان <c>Document</c> محمَّل،
/// ويصلح للاستخدام داخل إسقاط SQL وفي الذاكرة على السواء.</para>
/// </summary>
public static class HeadAlertTitle
{
    /// <summary>
    /// يبني عنوان الملف: أسماء المستفيد المفردة (مع تجاهل الفراغات)، أو نوع الملف،
    /// أو <c>مستند {documentId}</c>، أو نص فارغ عند غياب كل ذلك.
    /// </summary>
    public static string Build(
        int? documentId,
        string? borrowerName,
        string? borrowerFather,
        string? borrowerFamily,
        string? documentType)
    {
        if (borrowerName is null && borrowerFather is null
            && borrowerFamily is null && documentType is null)
            return documentId is null ? string.Empty : "مستند محذوف";

        var parts = new[] { borrowerName, borrowerFather, borrowerFamily }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var name = parts.Any() ? string.Join(" ", parts) : documentType;
        return string.IsNullOrWhiteSpace(name) ? $"مستند {documentId}" : name!;
    }
}
