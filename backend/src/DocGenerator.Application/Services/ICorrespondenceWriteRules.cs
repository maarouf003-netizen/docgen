using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// مساعدات كتابة المراسلات المشتركة بين الخدمة وأوامر `P4-01bt`: الأهلية والنطاق
/// والترقيم والتوثيق والإسقاط — بلا فتح معاملة.
/// </summary>
internal interface ICorrespondenceWriteRules
{
    bool CanWrite(UserRole role);
    string RequireBodyText(string bodyHtml, string emptyMessage, string kindLabel);
    string NextMessageNumber(Correspondence letter);
    Task<string> GenerateUniqueNumberAsync(string code, DateTime at, CancellationToken ct);
    Task<bool> MayAttachAsync(Document document, int actorUserId, UserRole role,
        int? actorBranchId, int? actorSectionId, CancellationToken ct);
    Task<bool> IsEligibleTargetAsync(Document document, UserRole role, int targetUserId, CancellationToken ct);
    Task<int?> ResolveHeadRecipientSectionAsync(int? recipientSectionId, User target, Document? document,
        UserRole role, int? actorBranchId, CancellationToken ct);
    Task<(int? BranchId, string Governorate, string Prefix)> ResolveScopeAsync(
        Document? document, UserRole role, int actorUserId, int? actorBranchId, CancellationToken ct);
    CorrespondenceMessageDto ToMessageDto(CorrespondenceMessage message);
    CorrespondenceDto ToDto(Correspondence letter, int readerUserId);
    Task<bool> CanViewAsync(Correspondence letter, int actorUserId, UserRole role,
        int? actorBranchId, int? actorSectionId, CancellationToken ct);
}
