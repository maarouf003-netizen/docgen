using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class ReviewLetterService
{
    /// <summary>
    /// تجهيز تنبيه الرد لصاحب الكتاب (لا لمنفّذ الرد): الجمهور حسب دوره (محامٍ أو رئيس)،
    /// والنص الكامل للرد بلا قصّ، عبر Outbox ليُدمج مع أي تنبيه نشط لنفس الكتاب.
    /// </summary>
    private async Task StageReplyAlertAsync(ReviewLetter letter, ReviewLetterMessage reply,
        int headUserId, string? headName, CancellationToken token)
    {
        var owner = await _users.GetByIdAsync(letter.CreatedById, token);
        var audience = owner?.Role is UserRole.Head or UserRole.SubHead
            ? HeadAlertAudience.SingleHead
            : HeadAlertAudience.SingleLawyer;

        await _alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                HeadAlertKindCatalog.ReviewLetterReply,
                HeadAlertMessages.ReviewLetterReply(new(
                    headName ?? "رئيس القسم", letter.LetterNumber, reply.BodyPlainText)),
                audience,
                letter.BranchId,
                headUserId,
                DocumentId: letter.DocumentId,
                ReviewLetterId: letter.Id,
                RecipientUserId: letter.CreatedById),
            supersedePlan: null,
            occurredAt: DateTime.UtcNow,
            ct: token);
    }
}
