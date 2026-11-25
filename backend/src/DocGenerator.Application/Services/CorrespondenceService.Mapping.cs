using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

public sealed partial class CorrespondenceService
{
    internal static CorrespondenceMessageDto ToMessageDto(CorrespondenceMessage m) => new(
        m.Id,
        m.Kind,
        m.BodyHtml,
        m.MessageNumber,
        m.MessageDate,
        m.AuthorId,
        m.AuthorName,
        m.AuthorRole);

    internal static CorrespondenceFileContextDto? FileContextOf(Correspondence letter,
        TimeProvider clock, TimeZoneInfo timeZone)
    {
        var doc = letter.Document;
        if (doc is null)
            return null;

        var name = string.Join(' ', new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(name))
            name = doc.DocumentType ?? string.Empty;

        var currentYear = ServerClock.CurrentYear(clock, timeZone);
        return new CorrespondenceFileContextDto(
            name,
            EffectiveFileIdentity.Number(doc, currentYear),
            doc.FileType,
            EffectiveFileIdentity.Year(doc, currentYear),
            doc.Court);
    }

    // التوثيق شأن المستلم وحده: يُعرض سجلّه وحده ويقرأه كل قارئ مصرَّح له
    // (المدير/رئيس القسم) بحالة واحدة مشتركة بدل «هل شاهدها كل واحد» المتناقضة.
    // وحقّ التوثيق قرار خادم يُشتقّ من هوية القارئ هنا، لا مقارنة في الواجهة.
    internal static CorrespondenceDto ToDto(Correspondence letter, int readerUserId,
        TimeProvider clock, TimeZoneInfo timeZone)
    {
        var messages = letter.Messages.OrderBy(m => m.Id).ToList();
        var receipts = letter.Receipts
            .Where(r => r.UserId == letter.TargetUserId)
            .OrderBy(r => r.SeenAt)
            .ToList();
        return new CorrespondenceDto(
            letter.Id,
            letter.CorrespondenceNumber,
            letter.CorrespondenceDate,
            letter.Importance,
            letter.DocumentId,
            FileContextOf(letter, clock, timeZone),
            letter.BranchId,
            letter.Governorate,
            letter.Branch?.Name,
            letter.CreatedById,
            letter.CreatedBy?.FullName ?? string.Empty,
            letter.CreatedBy?.Role.ToString().ToLowerInvariant() ?? string.Empty,
            letter.TargetUserId,
            letter.TargetUser?.FullName ?? string.Empty,
            letter.TargetUser?.Role.ToString().ToLowerInvariant() ?? string.Empty,
            receipts.Count > 0 ? Correspondence.ViewStatusSeen : Correspondence.ViewStatusPending,
            letter.TargetUserId == readerUserId,
            // حق الرد للمستلم وحده أيضًا — يُحسب هنا لا في الواجهة، ومستقل عن حق التوثيق.
            letter.TargetUserId == readerUserId,
            messages.Select(ToMessageDto).ToList(),
            receipts.Select(r => new CorrespondenceReceiptDto(r.UserId, r.UserName, r.SeenAt)).ToList(),
            letter.CreatedAt,
            letter.RecipientSectionId,
            letter.Version);
    }

    internal static CorrespondenceListItemDto ToListItem(Correspondence letter, int viewerUserId,
        TimeProvider clock, TimeZoneInfo timeZone)
    {
        var messages = letter.Messages.OrderBy(m => m.Id).ToList();
        var snippet = messages.FirstOrDefault(m => m.Kind == CorrespondenceMessage.KindLetter)?.BodyPlainText
            ?? messages.FirstOrDefault()?.BodyPlainText
            ?? string.Empty;
        var lastKind = messages.Count > 0 ? messages[^1].Kind : CorrespondenceMessage.KindLetter;
        // شارة الاطلاع تُقرأ من توثيق المستلم وحده، وحقّ التوثيق قرار خادم:
        // «يمكنني أن أوثّق» (المستلم) منفصل عن «هل وُثّقت» (كل القراء).
        var canMarkSeen = letter.TargetUserId == viewerUserId;
        var seen = letter.Receipts.Any(r => r.UserId == letter.TargetUserId);

        return new CorrespondenceListItemDto(
            letter.Id,
            letter.CorrespondenceNumber,
            letter.CorrespondenceDate,
            letter.Importance,
            letter.DocumentId,
            FileContextOf(letter, clock, timeZone),
            letter.CreatedBy?.FullName ?? string.Empty,
            letter.TargetUser?.FullName ?? string.Empty,
            snippet.Length > 160 ? snippet[..160] + "…" : snippet,
            lastKind,
            seen ? Correspondence.ViewStatusSeen : Correspondence.ViewStatusPending,
            canMarkSeen,
            // حق الرد للمستلم وحده — قرار خادم مستقل عن حق التوثيق.
            letter.TargetUserId == viewerUserId,
            messages.Count,
            letter.Branch?.Name,
            letter.Governorate,
            letter.UpdatedAt);
    }
}
