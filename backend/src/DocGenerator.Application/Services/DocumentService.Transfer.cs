using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentService : IDocumentService
{
    public async Task<int> CountFilesByOwnerAsync(int ownerId, int? scopeBranchId, CancellationToken ct = default, int? ownerSectionId = null)
    {
        var owner = await _users.GetByIdAsync(ownerId, ct);
        if (owner is null || owner.Role != UserRole.Lawyer)
            throw new ArgumentException("المحامي غير موجود");
        if (scopeBranchId.HasValue && owner.BranchId != scopeBranchId)
            throw new ArgumentException("لا يمكن عرض ملفات محامٍ من فرع آخر");
        // المعاينة بالنطاق نفسه (§5.5): تطابق المنقول فعلًا.
        return await _documents.CountByOwnerInScopeAsync(ownerId, scopeBranchId, ownerSectionId, ct);
    }
}
