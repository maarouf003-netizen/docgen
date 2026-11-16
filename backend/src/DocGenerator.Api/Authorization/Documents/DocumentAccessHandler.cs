using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace DocGenerator.Api.Authorization.Documents;

/// <summary>
/// معالج سياسة الوصول إلى المستند (`P2-01a`) — النقل الحرفي لقاعدة
/// <c>DocumentsController.CanAccessAsync</c> السابقة مع إضافة قدرة الكتالوج
/// الخاصة بكل عملية.
///
/// <para>الترتيب مقصود: القدرة أولًا (بلا أي قراءة) ثم النطاق:</para>
/// <list type="number">
/// <item>قدرة العملية من الكتالوج المركزي — المدير/المشرف ليسا محاميَين، فلا يعدّلان
/// ملفًا حتى لو كانا يريانه؛</item>
/// <item>الوصول الكامل (مدير/مشرف) يرى كل الفروع بلا كتابة؛</item>
/// <item>المحامي: ملفاته التي أنشأها فقط (الملكية)،</item>
/// <item>الرئيس/رئيس الشعبة: نفس الفرع، ثم استثناء الإحالة (رئيس القسم) أو الشعبة،</item>
/// <item><c>ReadOrFollow</c> تضيف متابع الاستئناف المُسند إلى الاستئنافات لقراءة الملف.</item>
/// </list>
///
/// <para>
/// لا يُعيد النجاح عند نقص قدرة أو نطاق: الفشل افتراضي، والمتحكم يترجم
/// <c>NotFound</c>/<c>Forbidden</c> بعد حلّ المورد (ترتيب `ADR-0007`).
/// </para>
/// </summary>
public sealed class DocumentAccessHandler(
    IUserCapabilities capabilities,
    IDocumentAccessReader reader)
    : AuthorizationHandler<DocumentAccessRequirement, DocumentAccessContext>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DocumentAccessRequirement requirement,
        DocumentAccessContext resource)
    {
        var user = context.User;

        if (RequiredCapability(requirement.Action) is { } capability
            && !capabilities.Has(user, capability))
        {
            return;
        }

        var document = resource.Document;
        var allowed = await HasScopeAccessAsync(user, document, resource.CancellationToken);

        if (!allowed && requirement.Action == DocumentAction.ReadOrFollow)
        {
            allowed = await reader.IsAssignedFollowerAsync(
                document.Id, user.GetUserId(), resource.CancellationToken);
        }

        if (allowed)
        {
            context.Succeed(requirement);
        }
    }

    /// <summary>قدرة كل عملية؛ `null` للقراءة (نطاق فقط).</summary>
    private static UserCapability? RequiredCapability(DocumentAction action) => action switch
    {
        DocumentAction.Read or DocumentAction.ReadOrFollow => null,
        DocumentAction.Edit => UserCapability.CanEditDocuments,
        DocumentAction.ChangeStatus => UserCapability.CanChangeDocumentStatus,
        // الاستعادة وإعادة المشطوب بقدرة الحذف نفسها (سلوك `CanDelete` القائم).
        DocumentAction.Delete or DocumentAction.Restore => UserCapability.CanDeleteDocuments,
        DocumentAction.Rotate => UserCapability.CanRotate,
        DocumentAction.ManageActions => UserCapability.CanManageExecutionActions,
        DocumentAction.Transfer => UserCapability.CanTransferDocuments,
        // إغلاق الفشل: عملية جديدة بلا قاعدة لا تُمنح افتراضيًا.
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "عملية مستند غير معرّفة"),
    };

    private async Task<bool> HasScopeAccessAsync(
        System.Security.Claims.ClaimsPrincipal user,
        DocumentAccessInfo document,
        CancellationToken ct)
    {
        var role = user.GetRoleEnum();

        if (capabilities.Has(user, UserCapability.HasFullAccess))
        {
            return true;
        }

        if (role == UserRole.Lawyer)
        {
            return document.CreatedById == user.GetUserId();
        }

        if (document.BranchId != user.GetBranchId())
        {
            return false;
        }

        if (role == UserRole.Head)
        {
            // ملفات الدوائر (ذات شعبة) ليست لرئيس القسم إلا إن كانت محالة إليه حتى الحسم
            // (الاستثناء القرائي 22′)؛ وملفات بلا دائرة/بلا شعبة تبقى له.
            return document.ExecutionCircuitId is null
                || !document.SectionId.HasValue
                || await reader.HasForwardedAppealAsync(document.Id, ct);
        }

        if (role == UserRole.SubHead)
        {
            return document.SectionId.HasValue && document.SectionId == user.GetSectionId();
        }

        return false;
    }
}
