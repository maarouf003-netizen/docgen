using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class ReviewLetterService : IReviewLetterService
{
    /// <summary>
    /// الرقم بصيغة {رمز الفرع}-{السنة}-{عشوائي 4 خانات} مع ضمان التفرّد بإعادة المحاولة.
    /// </summary>
    private async Task<string> GenerateUniqueNumberAsync(string branchCode, DateTime at,
        CancellationToken ct)
    {
        var prefix = NormalizePrefix(branchCode);
        var year = at.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);

        for (var attempt = 0; attempt < MaxNumberGenerationAttempts; attempt++)
        {
            var random = _entropy.NextInt64(0, 10_000)
                .ToString(System.Globalization.CultureInfo.InvariantCulture)
                .PadLeft(NumberRandomDigits, '0');
            var candidate = $"{prefix}-{year}-{random}";
            if (!await _letters.NumberExistsAsync(candidate, ct))
                return candidate;
        }

        throw new InvalidOperationException("تعذر توليد رقم فريد لكتاب المطالعة، حاول مجدداً");
    }

    private static string NormalizePrefix(string code)
    {
        var trimmed = code?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return "ML";
        var builder = new System.Text.StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
            builder.Append(char.IsWhiteSpace(ch) ? '-' : ch);
        return builder.ToString().ToUpperInvariant();
    }

    private async Task<string> ResolveBranchCodeAsync(int branchId, CancellationToken ct)
    {
        var branch = await _branches.GetByIdAsync(branchId, ct)
            ?? throw new ArgumentException("الفرع غير موجود");
        return branch.Code;
    }

    private static ReviewLetterMessageDto ToMessageDto(ReviewLetterMessage m) => new(
        m.Id,
        m.Kind,
        m.BodyHtml,
        m.MessageNumber,
        m.MessageDate,
        m.AuthorId,
        m.AuthorName,
        m.AuthorRole);

    private ReviewLetterFileContextDto? FileContextOf(ReviewLetter letter)
    {
        var doc = letter.Document;
        if (doc is null)
            return null;

        var name = string.Join(' ', new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(name))
            name = doc.DocumentType ?? string.Empty;

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        return new ReviewLetterFileContextDto(
            name,
            EffectiveFileIdentity.Number(doc, currentYear),
            doc.FileType,
            EffectiveFileIdentity.Year(doc, currentYear),
            doc.Court);
    }

    private ReviewLetterDto ToDto(ReviewLetter letter)
    {
        var messages = letter.Messages.OrderBy(m => m.Id).ToList();
        return new ReviewLetterDto(
            letter.Id,
            letter.LetterNumber,
            letter.LetterDate,
            letter.IsAnswered,
            letter.DocumentId,
            FileContextOf(letter),
            letter.BranchId,
            letter.Branch?.Name,
            letter.CreatedBy?.FullName ?? string.Empty,
            messages.Any(m => m.Kind == ReviewLetterMessage.KindReply && !m.IsSeenByLawyer),
            messages.Select(ToMessageDto).ToList(),
            letter.CreatedAt,
            letter.RecipientSectionId,
            letter.RecipientSection?.Name,
            letter.Version);
    }

    private ReviewLetterListItemDto ToListItem(ReviewLetter letter, bool revealUnseen)
    {
        var messages = letter.Messages.OrderBy(m => m.Id).ToList();
        var snippet = messages.FirstOrDefault(m => m.Kind == ReviewLetterMessage.KindLetter)?.BodyPlainText
            ?? messages.FirstOrDefault()?.BodyPlainText
            ?? string.Empty;
        var lastKind = messages.Count > 0 ? messages[^1].Kind : ReviewLetterMessage.KindLetter;
        var hasUnseenReply = revealUnseen
            && messages.Any(m => m.Kind == ReviewLetterMessage.KindReply && !m.IsSeenByLawyer);

        return new ReviewLetterListItemDto(
            letter.Id,
            letter.LetterNumber,
            letter.LetterDate,
            letter.IsAnswered,
            letter.DocumentId,
            FileContextOf(letter),
            letter.CreatedBy?.FullName ?? string.Empty,
            snippet.Length > 160 ? snippet[..160] + "…" : snippet,
            lastKind,
            hasUnseenReply,
            messages.Count,
            letter.Branch?.Name,
            letter.UpdatedAt,
            letter.RecipientSectionId,
            letter.RecipientSection?.Name);
    }
}
