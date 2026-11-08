using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات سجل أرقام الأساس — تُبقي تفاصيل EF في البنية التحتية وتمنع تحميل
/// الجدول كاملًا في الذاكرة. كل كتابة تمر عبر <c>IDocumentNumberRegistry</c>.
/// </summary>
public interface IDocumentBaseNumberRepository : IRepository<DocumentBaseNumber>
{
    /// <summary>كل صفوف ملف (بما فيها الملغاة). includeDeletedDocument يتجاوز فلتر الأب المحذوف.</summary>
    Task<List<DocumentBaseNumber>> ListByDocumentAsync(
        int documentId, bool includeDeletedDocument, CancellationToken ct = default);

    /// <summary>الصفوف الفعّالة (غير الملغاة) في دائرة/سنة — لفحص الهوية والتدوير.</summary>
    Task<List<DocumentBaseNumber>> ListActiveByCircuitYearAsync(
        int circuitId, int year, CancellationToken ct = default);

    /// <summary>الصفوف الفعّالة في دائرة الملف كلها (لكل السنوات) — للأهلية/التراجع.</summary>
    Task<List<DocumentBaseNumber>> ListActiveByDocumentCircuitAsync(
        int documentId, int circuitId, CancellationToken ct = default);

    /// <summary>أحدث صف قيد فعّال في الدائرة (أي سنة) — لاستبدال سنة القيد.</summary>
    Task<DocumentBaseNumber?> FindLatestActiveRegistrationAsync(
        int documentId, int circuitId, CancellationToken ct = default);

    /// <summary>صف قيد فعّال في (الملف، الدائرة، السنة) — بلا احتياطي Source.</summary>
    Task<DocumentBaseNumber?> FindActiveRegistrationAsync(
        int documentId, int circuitId, int year, CancellationToken ct = default);

    /// <summary>احتياطي الصفوف القديمة: Source=null و NumberKey مطابق (E16/R10).</summary>
    Task<DocumentBaseNumber?> FindLegacyRegistrationFallbackAsync(
        int documentId, int circuitId, int year, string numberKey, CancellationToken ct = default);

    /// <summary>أحدث صف فعّال لسنة محددة في دائرة الملف (للتصحيح/التراجع).</summary>
    Task<DocumentBaseNumber?> FindActiveRowForYearAsync(
        int documentId, int circuitId, int year, CancellationToken ct = default);

    /// <summary>صف بمعرّفه مع المستند (للتحقق من الملكية/السنة/عدم الإلغاء).</summary>
    Task<DocumentBaseNumber?> FindRowWithDocumentAsync(int rowId, CancellationToken ct = default);

    /// <summary>
    /// مرشحو التعارض: نفس (النوع المطبّع، السنة، الرقم المطبّع) في أي دائرة،
    /// بما فيهم صفوف الملفات المحذوفة (IgnoreQueryFilters) — للتحذير/التدقيق.
    /// </summary>
    Task<List<DocumentBaseNumber>> FindConflictCandidatesAsync(
        string typeKey, int year, string numberKey, CancellationToken ct = default);

    /// <summary>كل الصفوف الفعّالة لملف في دائرته الحالية (للهوية والتراجع).</summary>
    Task<List<DocumentBaseNumber>> ListActiveByDocumentAsync(int documentId, CancellationToken ct = default);
}
