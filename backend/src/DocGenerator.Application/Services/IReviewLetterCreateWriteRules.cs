using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// مساعدات تسطير كتاب المطالعة المشتركة بين الخدمة وأمر `P4-01bm`: المتابعة،
/// ومستلم الكتاب، وتوليد الرقم، والإسقاط — بلا فتح معاملة.
/// </summary>
internal interface IReviewLetterCreateWriteRules
{
    Task<bool> FollowsDocumentAsync(int documentId, int userId, CancellationToken ct);
    Task<int?> FileOwnerSectionAsync(Document document, CancellationToken ct);
    Task<Section?> ResolveGeneralRecipientSectionAsync(
        int? recipientSectionId, int actorBranchId, CancellationToken ct);
    Task<string> GenerateUniqueNumberAsync(string branchCode, DateTime at, CancellationToken ct);
    ReviewLetterDto ToDto(ReviewLetter letter);
}
