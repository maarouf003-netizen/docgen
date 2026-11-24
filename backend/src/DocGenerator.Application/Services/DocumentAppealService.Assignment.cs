using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentAppealService : IDocumentAppealService
{
    // ── الإسناد والنقل (رئيس القسم والشعبة بنطاقه) ──────────────────────────

    public async Task<int> CountByAssigneeForHeadAsync(int assigneeId, int? headBranchId, int? ownerSectionId, CancellationToken ct = default)
    {
        if (headBranchId is null)
            throw new ArgumentException("رئيس القسم دون فرع لا يمكنه الاطلاع على الاستئنافات");
        // المعاينة بالنطاق نفسه (§5.5): عدّ قاعدي يطابق المنقول فعلًا.
        return await _appeals.CountPendingByAssigneeInScopeAsync(
            assigneeId, headBranchId.Value, ownerSectionId, ct);
    }
}
