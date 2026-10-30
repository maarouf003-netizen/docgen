namespace DocGenerator.Application.Common;

/// <summary>
/// عنوان الملف المعروض في ملخصات/تفاصيل التنبيهات — المصدر الوحيد لبنائه
/// (ورقة إصلاح PR-4 §6.1ب): يجمع اسم المستفيد واسم الأب والعائلة، ثم نوع الملف،
/// ثم رقم المستند كحلٍّ أخير.
///
/// <para>العنوان يُبنى من حقول مسطّحة فقط، فلا يعتمد على كيان <c>Document</c> محمَّل،
/// ويصلح للاستخدام داخل إسقاط SQL وفي الذاكرة على السواء. وجود الملف يُمرَّر صراحةً
/// (<paramref name="documentExists"/>) لأن استنتاج «الحذف» من فراغ حقول العرض خطأ:
/// الملف القائم قد تكون كل حقوله المعروضة فارغة (صفوف قديمة/مستوردة) فيُخلط بينه
/// وبين المحذوف.</para>
/// </summary>
public static class HeadAlertTitle
{
    /// <summary>عنوان بديل للملف المحذوف/غير المرئي مع بقاء معرّفه على التنبيه.</summary>
    private const string DeletedDocumentMarker = "مستند محذوف";

    /// <summary>
    /// يبني عنوان الملف بالترتيب: أسماء المستفيد المفردة (مع تجاهل الفراغات)، أو نوع
    /// الملف، أو <c>مستند {documentId}</c> — وعند غياب مرجع الملف نص فارغ، وعند
    /// تعذّر رؤية الملف «مستند محذوف».
    /// </summary>
    /// <param name="documentId">معرّف الملف المرتبط بالتنبيه (إن وُجد).</param>
    /// <param name="documentExists">
    /// هل الملف المرتبط مرئي/قائم؟ <c>false</c> مع <paramref name="documentId"/> غير
    /// فارغ تعني حذفًا منطقيًا أو فقدانًا للصف.
    /// </param>
    public static string Build(
        int? documentId,
        bool documentExists,
        string? borrowerName,
        string? borrowerFather,
        string? borrowerFamily,
        string? documentType)
    {
        if (documentId is null)
            return string.Empty;

        if (!documentExists)
            return DeletedDocumentMarker;

        var parts = new[] { borrowerName, borrowerFather, borrowerFamily }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var name = parts.Any() ? string.Join(" ", parts) : documentType;
        return string.IsNullOrWhiteSpace(name) ? $"مستند {documentId}" : name!;
    }
}
