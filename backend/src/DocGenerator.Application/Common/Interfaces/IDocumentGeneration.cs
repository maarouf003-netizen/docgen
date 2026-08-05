namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// يبني قاموس سياق القالب لمستند docx (المكافئ لـ prepare_docxtpl_context + build_document_context في التطبيق الأصلي).
/// </summary>
public interface IDocumentContextBuilder
{
    Task<Dictionary<string, object>> BuildContextAsync(
        int documentId,
        string templateCode,
        int recipient = 0,
        int[]? estateIds = null,
        CancellationToken ct = default);
}

/// <summary>
/// يعرض سياق القالب على ملف Word نهائي (قابل للطباعة/التنزيل).
/// </summary>
public interface IDocumentRenderer
{
    Task<byte[]> RenderAsync(Dictionary<string, object> context, string templateCode, CancellationToken ct = default);
}
