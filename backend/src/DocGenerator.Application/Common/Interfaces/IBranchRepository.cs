using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات الفروع المنفّذة على مستوى قاعدة البيانات:
/// التفرّد (الاسم/الكود) وفحص الاستخدام قبل الحذف، وعدّادات الاستخدام للعرض.
/// </summary>
public interface IBranchRepository : IRepository<Branch>
{
    /// <summary>تحقق من تفرّد اسم الفرع (يستثني فرعاً معيناً عند التحديث).</summary>
    Task<bool> NameExistsAsync(string name, int? excludeBranchId, CancellationToken ct = default);

    /// <summary>تحقق من تفرّد كود الفرع (يستثني فرعاً معيناً عند التحديث).</summary>
    Task<bool> CodeExistsAsync(string code, int? excludeBranchId, CancellationToken ct = default);

    /// <summary>هل يوجد مستخدمون مرتبطون بالفرع؟ (يمنع حذفه نهائياً).</summary>
    Task<bool> HasUsersAsync(int branchId, CancellationToken ct = default);

    /// <summary>هل توجد مستندات مرتبطة بالفرع؟ (يمنع حذفه نهائياً).</summary>
    Task<bool> HasDocumentsAsync(int branchId, CancellationToken ct = default);

    /// <summary>عدد المستخدمين لكل فرع — مفتاحه معرّف الفرع.</summary>
    Task<Dictionary<int, int>> CountUsersByBranchAsync(CancellationToken ct = default);

    /// <summary>عدد المستندات لكل فرع — مفتاحه معرّف الفرع.</summary>
    Task<Dictionary<int, int>> CountDocumentsByBranchAsync(CancellationToken ct = default);
}
