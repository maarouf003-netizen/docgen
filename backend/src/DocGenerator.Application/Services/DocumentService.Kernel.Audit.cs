using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// نواة التدقيق داخل الصنف (المرحلة 1 — بلا `DI` ولا تغيير باني ولا توقيع عام):
/// مساعد التدقيق العابر `LogDocumentChangesAsync` (منقول من `DocumentService.Status.cs`
/// في المرحلة 0) — يستدعي الخالصات عبر البيت المشترك `Common/DocumentText.cs`.
/// البيت النهائي (من المرحلة 1 فصاعدًا): `LogDocumentChangesAsync`
/// ← المتعاون المحقون `IDocumentWriteGuard`.
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
                DocumentText.AuditWithActor(baseDetail, after), token);
            return;
        }

        await _audit.LogDocumentChangeAsync(
            actorName, actionType, after.Id, after.DocumentType,
            DocumentText.AuditWithActor($"{baseDetail} — غيّر {changes.Count} حقلًا", after), changes, token);
    }
}
