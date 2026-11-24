using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// مساعدات كتابة الرد/اللاحق على كتب المطالعة المشتركة بين الخدمة وأوامر `P4-01bl`:
/// حارس المالك، توليد الرقم الفريد، وتنبيه صاحب الكتاب — بلا فتح معاملة.
/// </summary>
internal interface IReviewLetterReplyWriteRules
{
    void EnsureLetterOwner(UserRole role, int? actorSectionId, int? ownerSectionId);
    Task<int?> OwnerSectionOfAsync(ReviewLetter letter, CancellationToken ct);
    Task<string> ResolveBranchCodeAsync(int branchId, CancellationToken ct);
    Task<string> GenerateUniqueNumberAsync(string branchCode, DateTime at, CancellationToken ct);
    Task StageReplyAlertAsync(
        ReviewLetter letter, ReviewLetterMessage reply, int headUserId, string? headName, CancellationToken token);
    ReviewLetterMessageDto ToMessageDto(ReviewLetterMessage message);
}
