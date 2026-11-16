using DocGenerator.Application.Common;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// بوابة قراءة ضيّقة لفحص الوصول إلى المستند (`P2-01`): أسئلة لا تكشف الكيان ولا تسمح
/// بأي كتابة، حتى لا تعتمد سياسات التفويض على مستودع كامل أو على `DbContext`.
///
/// <para>
/// `GetAccessInfoAsync` هو إسقاط F-01 القائم نفسه (خمسة حقول، بلا `Include`)؛
/// و`HasForwardedAppealAsync`/`IsAssignedFollowerAsync` هما سؤالا الاستثناء القرائي
/// والمتابعة المقرران (قرار 22′ وRF-003) — كانتا تُستدعيان من المتحكم مباشرة،
/// والآن يملكهما معالج السياسة.
/// </para>
/// </summary>
public interface IDocumentAccessReader
{
    /// <summary>إسقاط وصول خفيف؛ `null` للمفقود أو المحذوف منطقيًا.</summary>
    Task<DocumentAccessInfo?> GetAccessInfoAsync(int documentId, CancellationToken ct = default);

    /// <summary>هل للمستند استئناف محال إلى رئيس القسم (استثناء القراءة حتى الحسم)؟</summary>
    Task<bool> HasForwardedAppealAsync(int documentId, CancellationToken ct = default);

    /// <summary>هل المستخدم متابع مُسند إلى استئناف هذا المستند (وصول قراءة موسّع)؟</summary>
    Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default);
}
