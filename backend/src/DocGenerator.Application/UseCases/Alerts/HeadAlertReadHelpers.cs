using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Alerts;

/// <summary>
/// مساعدات قراءات التنبيهات المشتركة بين استعلامات `P4-01cw` — نُقلت حرفيًا من
/// خدمة التنبيهات بعد إفراغ سطح قراءتها: بناء الصفحة، وفرع الرئيس الإلزامي،
/// وإسقاط التفاصيل، ونطاق عدّاد المستخدم. مصدر واحد لكل الاستعلامات الستة.
/// </summary>
internal static class HeadAlertReadHelpers
{
    /// <summary>
    /// يبني العقد القائم <see cref="PagedResult{T}"/> بلا حقول جديدة. عند طلب
    /// المؤشر (<paramref name="cursor"/> غير <c>null</c>) لا معنى لرقم الصفحة
    /// المزاح، فيُعاد <c>page = 1</c> صراحةً بدل صدق رقم لا يصف النافذة.
    /// </summary>
    internal static PagedResult<HeadAlertSummaryDto> ToPagedResult(
        HeadAlertSummaryPage page, AlertPageRequest request, AlertCursor? cursor = null) => new()
        {
            Items = page.Items.ToList(),
            Page = cursor is null ? request.Page : 1,
            PerPage = request.PageSize,
            TotalCount = page.TotalCount,
        };

    /// <summary>الفرع إلزامي لرئيس القسم/الشعبة — وإلا فالحساب غير صالح للاستخدام.</summary>
    internal static int RequireBranch(HeadAlertActor actor) =>
        actor.BranchId ?? throw new UnauthorizedAccessException("حساب رئيس القسم/الشعبة غير مرتبط بفرع");

    /// <summary>
    /// `scope=user` (الافتراضي، `D7`): صفوف الفاعل نفسه؛ المحامي بلا قيد فرع،
    /// والرئيس/الشعبة داخل فرعه كي يطابق العدّاد قائمته.
    /// </summary>
    internal static async Task<int> CountUserScopeAsync(
        IHeadAlertRepository alerts, HeadAlertActor actor, CancellationToken ct)
    {
        if (!RolePermissions.CanReceiveAlerts(actor.Role))
            throw new UnauthorizedAccessException("عدّاد غير المقروء مخصص للمستلمين (محامٍ أو رئيس نطاق)");

        var branchId = actor.IsHeadOrSubHead ? RequireBranch(actor) : (int?)null;
        return await alerts.CountUnreadForUserAsync(actor.UserId, branchId, ct);
    }

    /// <summary>
    /// تحويل صف التفاصيل المسقط إلى العقد الكامل (الخطة §9.2): النص الكامل هنا وحده،
    /// و<c>IsRead</c> حالة القارئ نفسه (أو <c>null</c> لغير المستلم)، و<c>CanViewRecipients</c>
    /// قرار خادم — لا يملك العميل تعيينه.
    /// </summary>
    internal static HeadAlertDto ToDetailDto(HeadAlertDetailRow row, bool canViewRecipients) => new(
        row.Id,
        row.Kind,
        row.Message,
        row.TargetType.ToString().ToLowerInvariant(),
        row.DocumentId,
        HeadAlertTitle.Build(
            row.DocumentId,
            row.DocumentExists,
            row.BorrowerName,
            row.BorrowerFather,
            row.BorrowerFamily,
            row.DocumentType),
        row.TargetLawyerId,
        row.TargetLawyerName,
        row.DelegationId,
        row.AppealId,
        row.ReviewLetterId,
        row.PublicEntityId,
        row.ExecutionCircuitId,
        row.CreatedAt,
        row.LastContentChangedAt,
        row.IsArchived,
        row.ArchivedAt,
        row.CreatedByName,
        row.IsRead,
        canViewRecipients);
}
