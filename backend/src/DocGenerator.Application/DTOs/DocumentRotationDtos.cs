using DocGenerator.Application.Common;
using System.Text.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.DTOs;

/// <summary>
/// طµظپ ظ…ظ„ظپ ظˆط§ط­ط¯ ظپظٹ ط¬ط¯ظˆظ„ طھط¯ظˆظٹط± ط£ط±ظ‚ط§ظ… ط§ظ„ط£ط³ط§ط³ â€” ظ…ظ„ظپط§طھ ط§ظ„ظ…ط­ط§ظ…ظٹ ط§ظ„ظ…ط¤ظ‡ظ„ط© ظ„ظ„طھط¯ظˆظٹط±
/// ظ…ط¹ ط±ظ‚ظ… ط£ط³ط§ط³ ط§ظ„ط³ظ†ط© ط§ظ„ط­ط§ظ„ظٹط© ط¥ظ† ظˆظڈط¬ط¯ (ظˆط¥ظ„ط§ ظٹظƒظˆظ† null ظپظٹظڈطھط±ظƒ ط§ظ„ط­ظ‚ظ„ ظپط§ط±ط؛ظ‹ط§ ظ„ظ„ط¥ط¯ط®ط§ظ„).
/// DisplayName ظ‡ظˆ ط§ط³ظ… ط§ظ„ط¹ط±ط¶ (ط§ط³ظ… ط§ظ„ظ…ظ‚طھط±ط¶طŒ ط£ظˆ ط§ط³ظ… ط·ط§ظ„ط¨ ط§ظ„ط¹ط±ط¶ ظ„ظ…ظ† ظ„ظپط§ط¦ظپ ط§ظ„ط¹ط§ط¦ظ„طھظٹظ†
/// Executed + Deposit ط§ظ„ظ„طھظٹظ† ط¨ظ„ط§ ظ…ظ‚طھط±ط¶)طŒ ظˆظٹط¹ظˆط¯ ظ„ظ„ظˆط§ط¬ظ‡ط© ظ„ط¹ط±ط¶ظ‡ ط¹ظ…ظˆط¯ ط§ظ„ط§ط³ظ… ط§ظ„ظ…ظˆط­ط¯.
/// </summary>
public record RotationDocumentDto(
    int DocumentId,
    string? Court,
    string? BorrowerName,
    string? BorrowerFather,
    string? BorrowerFamily,
    string? FileNumber,
    string? FileType,
    string? BaseNumber,
    string? DisplayName,
    string? EffectiveFileNumber = null,
    string? EffectiveFileYear = null);

/// <summary>
/// ط±ظ‚ظ… ط£ط³ط§ط³ ظ…ظڈط¯ط®ظ„ ظ„ظ…ظ„ظپ ظˆط§ط­ط¯ ظپظٹ ط³ظ†ط© ط§ظ„طھط¯ظˆظٹط± ط§ظ„ط­ط§ظ„ظٹط©. ط§ظ„ظ‚ظٹظ…ط© ط§ظ„ظپط§ط±ط؛ط© طھط¹ظ†ظٹ
/// ط¥ظ„ط؛ط§ط، ط±ظ‚ظ… ط£ط³ط§ط³ ط§ظ„ط³ظ†ط© ط§ظ„ط­ط§ظ„ظٹط© ظ„ظ‡ط°ط§ ط§ظ„ظ…ظ„ظپ (ظ…ط¹ ط§ظ„ط§ط­طھظپط§ط¸ ط¨ط£ط±ظ‚ط§ظ… ط§ظ„ط³ظ†ظˆط§طھ ط§ظ„ط³ط§ط¨ظ‚ط©).
/// </summary>
public record BaseNumberEntry(
    int DocumentId,
    string? BaseNumber);

public class SaveBaseNumbersRequest
{
    public List<BaseNumberEntry> Entries { get; set; } = new();
}

/// <summary>
/// ط³ط¬ظ„ ط±ظ‚ظ… ط£ط³ط§ط³ ظ„ط³ظ†ط© ظˆط§ط­ط¯ط© ظ…ظ† طھط§ط±ظٹط® طھط¯ظˆظٹط± ط£ط±ظ‚ط§ظ… ط§ظ„ط£ط³ط§ط³ ظ„ظ„ظ…ظ„ظپ â€” طھظڈط¹ط±ط¶ ظƒظ„ ط§ظ„ط³ط¬ظ„ط§طھ
/// (ط³ظ†ط© ظˆط§ط­ط¯ط© ظ‚ط¯ طھظ…ظ„ظƒ ط£ظƒط«ط± ظ…ظ† ط±ظ‚ظ… â€” ط§ظ„ط£ط­ط¯ط« CreatedAt ظ‡ظˆ ط§ظ„ظ…ط¹طھط¨ط±)طŒ ظ…ط±طھط¨ط© ط¨ط§ظ„ط³ظ†ط© ط«ظ… CreatedAt طھظ†ط§ط²ظ„ظٹظ‹ط§.
/// </summary>
public record BaseNumberHistoryDto(
    int Year,
    string BaseNumber);

/// <summary>
/// ظˆظ‚ط¹ط© ظˆط§ط­ط¯ط© ظ…ظ† آ«ظˆظ‚ظˆط¹ط§طھ ط§ظ„ظ…ظ„ظپآ»: ط´ط·ط¨/طھط¬ط¯ظٹط¯ (ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ») ط£ظˆ ط¥ط¬ط±ط§ط، طھط؛ظٹظٹط± ط­ط§ظ„ط©
/// (ظ†ط¸ط§ظ… آ«ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ»: طھط±ظٹط«/ظ…ظ†ظپط° ط¨ط§ظ„طھط³ظˆظٹط©/ظ…ظ†ظپط° ط¬ط¨ط±ظٹط§/طھط±ط§ط¬ط¹). EventDate/ReceiptDate
/// ط²ظ…ظ†ظٹظ‘ط§ظ† (طھظڈظپط³ظژظ‘ط± ط§ظ„ظ†طµظˆطµ ط§ظ„ط­ط±ط© ظپظٹ ط§ظ„ط®ط¯ظ…ط© ظˆطھط­ظˆظژظ‘ظ„ ط¥ظ„ظ‰ DateTime)طŒ ظˆDetails طھط­ظ…ظ„ ط­ظ‚ظˆظ„
/// ط¥ط¬ط±ط§ط،ط§طھ طھط؛ظٹظٹط± ط§ظ„ط­ط§ظ„ط© ط§ظ„ظƒط§ظ…ظ„ط© (ط§ظ„ظ…ظپط§طھظٹط­ ط§ظ„ط¥ظ†ظƒظ„ظٹط²ظٹط© ط§ظ„ظ…ط¹طھظ…ط¯ط© ظپظٹ ط§ظ„ط®ط¯ظ…ط©)طŒ ط¨ظٹظ†ظ…ط§
/// DetailsText طھط­ظ…ظ„ ط§ظ„ط³ط±ط¯ ط§ظ„ظ†طµظٹ ط§ظ„ط­ط± ظ„ظ„ظˆظ‚ظˆط¹ط§طھ ط§ظ„ط¢ظ„ظٹط© (ظƒظ†ظˆط¹ آ«طھط؛ظٹظٹط± ط¬ظ‡ط©آ») â€” ط£ط­ط¯ظ‡ظ…ط§
/// ظپظ‚ط· ط؛ظٹط± nullطŒ ظپظ„ط§ ظٹظڈظ„ظˆظژظ‘ط« ط§ظ„ظ‚ط§ظ…ظˆط³ ط¨ط§ظ„ظ†طµ ط§ظ„ط®ط§ظ….
/// </summary>
public record DocumentOccurrenceDto(
    int Id,
    string OccurrenceType,
    string OccurrenceTypeLabel,
    DateTime? EventDate,
    string? FileNumber,
    string? FileType,
    int? Year,
    string? ReceiptNumber,
    DateTime? ReceiptDate,
    IReadOnlyDictionary<string, string>? Details,
    string? CreatedByName,
    string? Source = null,
    string? DetailsText = null,
    string? FromCircuitName = null,
    string? ToCircuitName = null);
