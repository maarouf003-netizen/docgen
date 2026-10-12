using DocGenerator.Application.Common.Audit;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// نواة التدقيق والنصوص داخل الصنف (المرحلة 0 — بلا `DI` ولا تغيير باني ولا توقيع عام):
/// نقل ميكانيكي حرفي لمساعد التدقيق العابر `LogDocumentChangesAsync`
/// من `DocumentService.Status.cs` والخالصتين `AuditWithActor` / `ActorFullName`
/// من `DocumentService.cs` وتسمية نوع الإجراء `TypeLabel` من `DocumentService.Actions.cs` —
/// بلا أي تغيير سلوكي.
/// البيوت النهائية (تُطبَّق من المرحلة 1 فصاعدًا): الخالصات (`AuditWithActor`،
/// `ActorFullName`، `TypeLabel`) ← صنف `static` مشترك `Common/DocumentText.cs`،
/// و`LogDocumentChangesAsync` ← المتعاون المحقون `IDocumentWriteGuard`.
/// </summary>
public sealed partial class DocumentService
{
    /// <summary>
    /// تسجيل تدقيق عمليات الملف مع تفاصيل تغيّرات الحقول: حين تُرصد فروقات
    /// تُسجَّل صفوفًا (حقل/قبل/بعد) مرتبطة بالإدخال، وإلا يبقى التسجيل نصيًا كالسابق.
    /// </summary>
    private async Task LogDocumentChangesAsync(
        Dictionary<string, string?> before,
        Document after,
        string? actorName,
        string actionType,
        string baseDetail,
        CancellationToken token)
    {
        var changes = DocumentChangeTracker.Diff(before, after);
        if (changes.Count == 0)
        {
            // لا تغييرات متتبعة: يبقى التسجيل النصي كما كان تاريخيًا.
            await _audit.LogAsync(actorName, actionType, after.Id, after.DocumentType,
                AuditWithActor(baseDetail, after), token);
            return;
        }

        await _audit.LogDocumentChangeAsync(
            actorName, actionType, after.Id, after.DocumentType,
            AuditWithActor($"{baseDetail} — غيّر {changes.Count} حقلًا", after), changes, token);
    }

    /// <summary>
    /// الاسم الثلاثي للمنفذ عليه من بيانات المستند، يُلحق بكل أحداث التدقيق
    /// ليُعرف أي ملف وقع عليه الإجراء دون فتح صفحة المستند.
    /// </summary>
    private static string ActorFullName(Document doc) => string.Join(' ',
        new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }
            .Where(v => !string.IsNullOrWhiteSpace(v)));

    private static string AuditWithActor(string action, Document doc) =>
        string.IsNullOrWhiteSpace(ActorFullName(doc))
            ? action
            : $"{action} — المنفذ عليه: {ActorFullName(doc)}";

    private static string TypeLabel(string type) => type == "note" ? "ملاحظة" : "إجراء";
}
