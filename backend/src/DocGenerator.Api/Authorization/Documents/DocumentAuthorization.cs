using System.Security.Claims;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace DocGenerator.Api.Authorization.Documents;

/// <inheritdoc/>
public sealed class DocumentAuthorization(
    IAuthorizationService authorization,
    IUserCapabilities capabilities,
    IDocumentAccessReader reader) : IDocumentAuthorization
{
    public async Task<DocumentAccessOutcome> AuthorizeAsync(
        ClaimsPrincipal user, int documentId, DocumentAction action, CancellationToken ct = default)
    {
        var document = await reader.GetAccessInfoAsync(documentId, ct);
        if (document is null)
        {
            return DocumentAccessOutcome.NotFound;
        }

        return await EvaluateAsync(user, document, action, ct);
    }

    public Task<DocumentAccessOutcome> AuthorizeAsync(
        ClaimsPrincipal user, DocumentResponse document, DocumentAction action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        // نفس الحقول الخمسة التي يعتمد عليها فحص الوصول (F-01) — حمل الاستجابة الثقيل
        // يُحوَّل إلى الإسقاط ولا تُنسخ القاعدة.
        var access = new DocumentAccessInfo(
            document.Id,
            document.CreatedById,
            document.BranchId,
            document.ExecutionCircuitId,
            document.SectionId);

        return EvaluateAsync(user, access, action, ct);
    }

    public bool Has(ClaimsPrincipal user, UserCapability capability)
        => capabilities.Has(user, capability);

    public DocumentListScope? ResolveListScope(ClaimsPrincipal user)
    {
        var fullAccess = capabilities.Has(user, UserCapability.HasFullAccess);
        var headOrSubHead = capabilities.Has(user, UserCapability.IsHeadOrSubHead);

        int? ownerSectionId = null;
        if (user.GetRoleEnum() == UserRole.SubHead)
        {
            // رئيس شعبة برمز بلا شعبة مرفوض (أعد الدخول) بدل التدهور لنطاق القسم.
            ownerSectionId = user.GetSectionId();
            if (ownerSectionId is null)
            {
                return null;
            }
        }

        return new DocumentListScope(
            VisibleBranchId: fullAccess ? null : user.GetBranchId(),
            VisibleUserId: fullAccess || headOrSubHead ? null : user.GetUserId(),
            OwnerSectionId: ownerSectionId);
    }

    private async Task<DocumentAccessOutcome> EvaluateAsync(
        ClaimsPrincipal user,
        DocumentAccessInfo document,
        DocumentAction action,
        CancellationToken ct)
    {
        var result = await authorization.AuthorizeAsync(
            user,
            new DocumentAccessContext(document, ct),
            new DocumentAccessRequirement(action));

        return result.Succeeded ? DocumentAccessOutcome.Allowed : DocumentAccessOutcome.Forbidden;
    }
}
