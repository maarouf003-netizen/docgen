using DocGenerator.Application.Common;
using System.Text.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.DTOs;

/// <summary>
/// ط®ظٹط§ط±ط§طھ ظپظ„طھط±ط© آ«ط§ظ„ظ…ظ„ظپط§طھ ط§ظ„طھظ†ظپظٹط°ظٹط©آ» ط¨ط£ط³ظ„ظˆط¨ ط¥ظƒط³ظ„.
/// ظƒظ„ ظ‚ط§ط¦ظ…ط© ظ…ظڈظ‚ظٹط¯ط© ط¨ط¨ط§ظ‚ظٹ ط§ظ„ظپظ„ط§طھط± ط§ظ„ظ†ط´ط·ط© (ظ…ط§ ط¹ط¯ط§ ظپظ„طھط± ط§ظ„ط­ظ‚ظ„ ظ†ظپط³ظ‡)
/// ظپظٹظ„طھط²ظ… ط§ظ„ط§ط®طھظٹط§ط± ط§ظ„ظ„ط§ط­ظ‚ ط¨ظ†طھط§ط¦ط¬ ط§ظ„ظپظ„طھط± ط§ظ„ط³ط§ط¨ظ‚ طھظ„ظ‚ط§ط¦ظٹظ‹ط§.
/// </summary>
public record DocumentFilterOptions(
    List<string> Applicants,
    List<string> Courts,
    List<string> Lawyers,
    List<string> AdministrativeBranches,
    List<string> Branches,
    List<string> ExecutedEntities,
    List<string> PublicEntityBranches);

public class AddExecutionActionRequest
{
    public string Type { get; set; } = "action";
    public string Text { get; set; } = string.Empty;
    public string? ActionDate { get; set; }
    public string? ReminderDuration { get; set; }
    public string? ReminderColor { get; set; }
}

public class UpdateExecutionActionRequest
{
    public string Type { get; set; } = "action";
    public string Text { get; set; } = string.Empty;
    public string? ActionDate { get; set; }
    public string? ReminderDuration { get; set; }
    public string? ReminderColor { get; set; }
}

/// <summary>
/// ظ†ظ‚ظ„ طھط°ظƒظٹط± ط¥ط¬ط±ط§ط، ط¥ظ„ظ‰ ظٹظˆظ… ط§ط³طھط­ظ‚ط§ظ‚ ط¬ط¯ظٹط¯ (`POST .../actions/{id}/reschedule` ظ…ظ† ظ†ط§ظپط°ط©
/// ط§ظ„ظٹظˆظ…): ط§ظ„ظ…ط¯ط®ظ„ ظ‡ظˆ ظٹظˆظ… ط§ظ„ط§ط³طھط­ظ‚ط§ظ‚ ظ†ظپط³ظ‡طŒ ظˆط§ظ„ط®ظ„ظپظٹط© طھط¹ظٹط¯ ط­ط³ط§ط¨ `ActionDate` ط®ظ„ظپظٹظ‹ط§
/// (`newDueDate âˆ’ DurationDays`) ظ…ط¹ ط¨ظ‚ط§ط، ط§ظ„ظ…ط¯ط© â€” ظ„ط§ ظ…ط³ط§ط³ ط¨ط¨ظ‚ظٹط© ط­ظ‚ظˆظ„ ط§ظ„ط¥ط¬ط±ط§ط،.
/// </summary>
public class RescheduleActionReminderRequest
{
    public string? NewDueDate { get; set; }
}

/// <summary>
/// ط¨ظٹط§ظ† طھط¬ط¯ظٹط¯ ظ…ظ„ظپ ظ…ط´ط·ظˆط¨ ط¹ظ†ط¯ ط¥ط¹ط§ط¯طھظ‡ ط¥ظ„ظ‰ ط§ظ„ظ…طھط¯ط§ظˆظ„ (ظپظƒ ط§ظ„ط´ط·ط¨ ظ…ط¹ ط±ظ‚ظ… ظ…ظ„ظپ ط¬ط¯ظٹط¯ ظ„ط³ظ†ط© ط§ظ„ط¥ط¹ط§ط¯ط©).
/// ط§ظ„ط­ظ‚ظˆظ„ ط§ظ„ظ…ط±ظƒط¨ط© طھظڈط±ط³ظژظ„ ظ†طµظˆطµظ‹ط§ ط­ط±ط© (ظ…ط«ط§ظ„: 1/8/2026) ظˆطھظڈظپط³ظژظ‘ط± ظˆطھظڈط®ط²ظژظ‘ظ† ط²ظ…ظ†ظٹظ‹ط§ ظƒط¨ط§ظ‚ظٹ طھظˆط§ط±ظٹط®
/// آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ». آ«ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ ط§ظ„ط¬ط¯ظٹط¯آ» ط¥ظ„ط²ط§ظ…ظٹطŒ ظˆظ…ط§ ط¹ط¯ط§ظ‡ط§ ط§ط®طھظٹط§ط±ظٹ.
/// </summary>
public class RenewalRequest
{
    /// <summary>
    /// ط¹ط¯ظ‘ط§ط¯ ط§ظ„طھط²ط§ظ…ظ† ط§ظ„ظ…طھظپط§ط¦ظ„ (RF-010): ظ‚ظٹظ…ط© `Version` ظƒظ…ط§ ظ‚ظڈط±ط¦طھ ط¹ظ†ط¯ ط§ظ„ط¬ظ„ط¨.
    /// ط§ط®طھظٹط§ط±ظٹ ظ„ظ„طھظˆط§ظپظ‚ (ط؛ظٹط§ط¨ظ‡ = ظ‚ط¨ظˆظ„ ط¨ظ„ط§ ظپط­طµ ظ…ط¨ظƒط±)ط› ط§ظ„ظ‚ط¯ظٹظ… ظٹظڈط±ظپط¶ `409`.
    /// ظ…ظˆط±ظˆط« ظپظٹ `DocumentUpsertRequest` ظˆ`ReturnReferredToStartRequest` ظˆ`ExecutedStatusRequest`.
    /// </summary>
    public long? Version { get; set; }

    /// <summary>ط±ظ‚ظ… ظˆط±ظˆط¯ ط§ط®ط·ط§ط± ط§ظ„طھط¬ط¯ظٹط¯ (ط§ط®طھظٹط§ط±ظٹ).</summary>
    public string? RenewalFileReceiptNumber { get; set; }

    /// <summary>طھط§ط±ظٹط® ظˆط±ظˆط¯ ط§ط®ط·ط§ط± ط§ظ„طھط¬ط¯ظٹط¯ (ط§ط®طھظٹط§ط±ظٹطŒ ظ†طµ ط­ط±).</summary>
    public string? RenewalFileReceiptDate { get; set; }

    /// <summary>ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ ط§ظ„ط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط¥ظ„ط²ط§ظ…ظٹ).</summary>
    public string? RenewalFileNumber { get; set; }

    /// <summary>ظ†ظˆط¹ ط§ظ„ظ…ظ„ظپ ط§ظ„ط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط§ط®طھظٹط§ط±ظٹ).</summary>
    public string? RenewalFileType { get; set; }

    /// <summary>ط³ظ†ط© ط§ظ„ط¥ط¹ط§ط¯ط© (ط¥ظ„ط²ط§ظ…ظٹط© ظپظٹ ظ†ط¸ط§ظ… آ«ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ»طŒ ظˆطھظڈط³ط¬ظژظ‘ظ„ ط¨ظ‡ط§ ط³ظ†ط© ط±ظ‚ظ… ط§ظ„ط£ط³ط§ط³ ط§ظ„ط¬ط¯ظٹط¯).</summary>
    public int? RenewalYear { get; set; }

    /// <summary>طھط§ط±ظٹط® ط§ظ„طھط¬ط¯ظٹط¯ (ط§ط®طھظٹط§ط±ظٹطŒ ظ†طµ ط­ط±).</summary>
    public string? RenewalDate { get; set; }
}

/// <summary>
/// ط¨ظٹط§ظ† ط§ظ„ط¹ظˆط¯ط© ظ…ظ† آ«ظ…ط­ط§ظ„ ط§ظ„ظ‰ ط§ظ„ط¨ط¯ط§ظٹط©آ» (ظ†ظ‚ط·ط© آ«return-referred-to-startآ») â€” ط¨ظ†طھظٹط¬طھظٹظ†:
/// ط§ط³طھط¹ط§ط¯ط© آ«ظ…ظ†ظپط° ط¬ط¨ط±ظٹط§آ» (ظ…ظ†ظپط° ط¬ط²ط¦ظٹط§) ط­ط³ط¨ ط§ظ„ظ„ط§ط²ظ…ط© ظˆط¥ظ„ط§ آ«ظ…طھط¯ط§ظˆظ„آ». ط­ظ‚ظˆظ„ ط§ظ„طھط¬ط¯ظٹط¯ ظ…ظˆط­ظ‘ط¯ط© ظپظٹ
/// ط§ظ„ظ…ط³ط§ط±ظٹظ† (ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ ط§ظ„ط¬ط¯ظٹط¯ ظٹظپط¹ظ‘ظ„ ط§ظ„طھط¬ط¯ظٹط¯ ظˆظٹظ„ط²ظ… ط§ظ„ط³ظ†ط© 1900â€“2100 ظˆطھط·ط§ط¨ظ‚ظ‡ط§ ظ…ط¹ ط³ظ†ط© ط§ظ„طھط§ط±ظٹط®)طŒ
/// ظˆآ«طھط§ط±ظٹط® ط§ظ„ط´ط·ط¨آ» ط®ط§طµ ط¨ظ†طھظٹط¬ط©-ظ…طھط¯ط§ظˆظ„ ظپظ‚ط· ظˆظٹظڈطھط¬ط§ظ‡ظ„ ظپظٹ ط¹ظˆط¯ط©-ط¬ط²ط¦ظٹط§. ظƒظ„ظ‡ط§ ظ†طµظˆطµ ط­ط±ط© (ظ…ط«ط§ظ„: 1/8/2026)
/// طھظڈظپط³ظژظ‘ط± ظˆطھظڈط®ط²ظژظ‘ظ† ط²ظ…ظ†ظٹظ‹ط§ ظƒط¨ط§ظ‚ظٹ طھظˆط§ط±ظٹط® ط§ظ„ظ†ط¸ط§ظ….
/// </summary>
public class ReturnReferredToStartRequest : RenewalRequest
{
    /// <summary>طھط§ط±ظٹط® ط´ط·ط¨ ط§ظ„ظ…ظ„ظپ (ط§ط®طھظٹط§ط±ظٹ â€” ظ„ط¹ظˆط¯ط©-ظ…طھط¯ط§ظˆظ„ ظپظ‚ط·طŒ ظˆظٹظڈطھط¬ط§ظ‡ظ„ ظپظٹ ط¹ظˆط¯ط©-ط¬ط²ط¦ظٹط§).</summary>
    public string? StruckOffDate { get; set; }
}

/// <summary>
/// طھط¹ظٹظٹظ† ط­ط§ظ„ط© ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» (ظ…طھط¯ط§ظˆظ„/ظ…ظ†ظپط°/ظ…ط´ط·ظˆط¨) ظ…ظ† طµظپط­ط© ط§ظ„طھظپط§طµظٹظ„.
/// ط¹ظ†ط¯ ط§ظ„ط§ظ†طھظ‚ط§ظ„ ط¥ظ„ظ‰ آ«ظ…ظ†ظپط°آ» طھظڈط­ظپط¸ ط­ظ‚ظˆظ„ظ‡ (ط§ظ„ظ…ط¨ظ„ط؛/ظƒظٹظپظٹط© ط§ظ„طھظ†ظپظٹط°/طھط§ط±ظٹط® ط§ظ„ط¥ظٹط¯ط§ط¹)طŒ ظˆط¹ظ†ط¯ ط§ظ„ط§ظ†طھظ‚ط§ظ„
/// ط¥ظ„ظ‰ آ«ظ…ط´ط·ظˆط¨آ» ظٹظڈط­ظپط¸ طھط§ط±ظٹط® ط§ظ„ط´ط·ط¨ (ظ†طµ ط­ط±ط› ط¥ظ† ط؛ط§ط¨ ظٹظڈط«ط¨ظژظ‘طھ طھظˆظ‚ظٹطھ ط§ظ„ط§ظ†طھظ‚ط§ظ„)طŒ ظˆط¹ظ†ط¯ ط§ظ„ط¥ط¹ط§ط¯ط© ط¥ظ„ظ‰
/// ظ…طھط¯ط§ظˆظ„ طھظڈط·ط¨ظژظ‘ظ‚ ط­ظ‚ظˆظ„ ط§ظ„طھط¬ط¯ظٹط¯ ط§ظ„ظ…ظˆط±ظˆط«ط© (ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ ط§ظ„ط¬ط¯ظٹط¯ ط¥ظ„ط²ط§ظ…ظٹ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨).
/// </summary>
public class ExecutedStatusRequest : RenewalRequest
{
    /// <summary>ط§ظ„ط­ط§ظ„ط© ط§ظ„ظ…ط³طھظ‡ط¯ظپط©: ظ…طھط¯ط§ظˆظ„ (ط³ظ„ط³ظ„ط© ظپط§ط±ط؛ط©) / ظ…ظ†ظپط° / ظ…ط´ط·ظˆط¨.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>طھط§ط±ظٹط® ط§ظ„ط´ط·ط¨ ط¹ظ†ط¯ ط§ظ„ط§ظ†طھظ‚ط§ظ„ ط¥ظ„ظ‰ آ«ظ…ط´ط·ظˆط¨آ» (ظ†طµ ط­ط±ط› ط¥ظ† ط؛ط§ط¨ ظٹظڈط­ظپط¸ طھظˆظ‚ظٹطھ ط§ظ„ط§ظ†طھظ‚ط§ظ„).</summary>
    public string? StruckOffDate { get; set; }

    /// <summary>ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ط°ظٹ ط¯ظپط¹طھظ‡ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© (طµظپط© آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ») ط£ظˆ ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ظˆط¯ط¹ (طµظپط© آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ»).</summary>
    public decimal? ExecutedPaidAmount { get; set; }

    /// <summary>ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط£ظˆظ„ (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©).</summary>
    public string? ExecutedPaidCurrency { get; set; }

    /// <summary>ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ†ظٹ (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» / آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ»طŒ ط¨ط¹ظ…ظ„طھظ‡ ط§ظ„ط®ط§طµط©.</summary>
    public decimal? ExecutedPaidAmount2 { get; set; }

    /// <summary>ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ†ظٹ (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©).</summary>
    public string? ExecutedPaidCurrency2 { get; set; }

    /// <summary>ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ„ط« (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» / آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ»طŒ ط¨ط¹ظ…ظ„طھظ‡ ط§ظ„ط®ط§طµط©.</summary>
    public decimal? ExecutedPaidAmount3 { get; set; }

    /// <summary>ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ„ط« (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©).</summary>
    public string? ExecutedPaidCurrency3 { get; set; }

    /// <summary>ظƒظٹظپظٹط© طھظ†ظپظٹط° ط§ظ„ظ…ظ„ظپ (طµظپط© آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» ظپظ‚ط·).</summary>
    public string? ExecutedDescription { get; set; }

    /// <summary>طھط§ط±ظٹط® ط§ظٹط¯ط§ط¹ظ‡ ط­ط³ط§ط¨ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© (طµظپط© آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ» ظپظ‚ط·).</summary>
    public string? ExecutedDepositDate { get; set; }

    /// <summary>طھط§ط±ظٹط® ط§ظ„طھظ†ظپظٹط° (طµظپط© آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» ظپظ‚ط·طŒ ظ†طµ ط­ط± ظٹظڈظپط³ظژظ‘ط± ط¨طµظٹط؛ آ«1/8/2026آ»).</summary>
    public string? ExecutedExecutionDate { get; set; }

    /// <summary>ط±ظ‚ظ… ظƒطھط§ط¨ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط¨ط§ظ„ط³ظٹط± ط¨ط§ظ„ظ…ظ„ظپ ط¹ظ†ط¯ ط¥ط±ط¬ط§ط¹ آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ» ظ…ظ† آ«ظ…ظ†ظپط°آ» ط¥ظ„ظ‰ آ«ظ…طھط¯ط§ظˆظ„آ» (ط¥ظ„ط²ط§ظ…ظٹ).</summary>
    public string? SayerNumber { get; set; }

    /// <summary>طھط§ط±ظٹط® ظƒطھط§ط¨ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط¨ط§ظ„ط³ظٹط± ط¨ط§ظ„ظ…ظ„ظپ ط¹ظ†ط¯ ط§ظ„ط¥ط±ط¬ط§ط¹ ظ…ظ† آ«ظ…ظ†ظپط°آ» ط¥ظ„ظ‰ آ«ظ…طھط¯ط§ظˆظ„آ» (ط¥ظ„ط²ط§ظ…ظٹ).</summary>
    public string? SayerDate { get; set; }

    /// <summary>ط±ظ‚ظ… ظˆط±ظˆط¯ ظƒطھط§ط¨ ط¨ط§ظ„ط³ظٹط± ط¨ط§ظ„ظ…ظ„ظپ ط¹ظ†ط¯ ط§ظ„ط¥ط±ط¬ط§ط¹ ظ…ظ† آ«ظ…ظ†ظپط°آ» ط¥ظ„ظ‰ آ«ظ…طھط¯ط§ظˆظ„آ» (ط¥ظ„ط²ط§ظ…ظٹ).</summary>
    public string? SayerRegNumber { get; set; }

    /// <summary>طھط§ط±ظٹط® ظˆط±ظˆط¯ ظƒطھط§ط¨ ط¨ط§ظ„ط³ظٹط± ط¨ط§ظ„ظ…ظ„ظپ ط¹ظ†ط¯ ط§ظ„ط¥ط±ط¬ط§ط¹ ظ…ظ† آ«ظ…ظ†ظپط°آ» ط¥ظ„ظ‰ آ«ظ…طھط¯ط§ظˆظ„آ» (ط¥ظ„ط²ط§ظ…ظٹ).</summary>
    public string? SayerRegDate { get; set; }
}

public class DocumentUpsertRequest : RenewalRequest
{
    public string? DocumentType { get; set; }
    public string? BorrowerName { get; set; }
    public string? BorrowerFather { get; set; }
    public string? BorrowerFamily { get; set; }
    public string? BorrowerMother { get; set; }
    public string? BorrowerBirth { get; set; }
    public string? BorrowerRegister { get; set; }
    public string? BorrowerNationalId { get; set; }
    public string? BorrowerAddress { get; set; }
    public string? BorrowerAddressType { get; set; } = "ظ…ظˆط·ظ† ظ…ط®طھط§ط±";

    /// <summary>ط§ظ„ظ…ظ…ط«ظ„ ط§ظ„ط´ط±ط¹ظٹ ظ„ظ„ظ…ظ‚طھط±ط¶ (ظˆظ„ظٹ/ظˆطµظٹ/ظ‚ظٹظ…) ط¥ظ† ظˆظڈط¬ط¯: ط§ط³ظ…ظ‡ ط§ظ„ط«ظ„ط§ط«ظٹ ظˆطµظپطھظ‡ ظˆط¹ظ†ظˆط§ظ†ظ‡.</summary>
    public string? BorrowerRepresentativeName { get; set; }
    public string? BorrowerRepresentativeFather { get; set; }
    public string? BorrowerRepresentativeFamily { get; set; }
    public string? BorrowerRepresentativeCapacity { get; set; }
    public string? BorrowerRepresentativeAddressType { get; set; }
    public string? BorrowerRepresentativeAddress { get; set; }

    /// <summary>ط·ط¨ظٹط¹ط© ط§ظ„ظ…ظ‚طھط±ط¶/ط§ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡: ط´ط®طµ ط·ط¨ظٹط¹ظٹ (natural) ط£ظˆ ط´ط®طµ ط§ط¹طھط¨ط§ط±ظٹ (legal).</summary>
    public string BorrowerNature { get; set; } = PartyNatureCatalog.Natural;
    /// <summary>ط±ظ‚ظ… طھط³ط¬ظٹظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© ط§ظ„ط§ط¹طھط¨ط§ط±ظٹط© (ط§ط®طھظٹط§ط±ظٹ).</summary>
    public string? BorrowerRegistrationNumber { get; set; }
    /// <summary>ظ…ظ† ظٹظ…ط«ظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© ط§ظ„ط§ط¹طھط¨ط§ط±ظٹط© (ط§ط®طھظٹط§ط±ظٹ).</summary>
    public string? BorrowerRepresentedBy { get; set; }

    public string? ContractType { get; set; }
    public string? ContractTypeSelector { get; set; } = "ظ…طµط±ظپظٹ";
    public string? ContractNumber { get; set; }
    public string? ContractDate { get; set; }
    public string? AnnexType { get; set; }
    public string? AnnexNumber { get; set; }
    public string? AnnexDate { get; set; }
    public string? InclusionText { get; set; }

    public decimal? AmountNumeric { get; set; }
    public string? AmountWords { get; set; }
    public string? Currency { get; set; } = "ظ„ظٹط±ط© ط³ظˆط±ظٹط©";
    public decimal? Amount2Numeric { get; set; }
    public string? Amount2Words { get; set; }
    public string? Currency2 { get; set; } = "ط¯ظˆظ„ط§ط± ط£ظ…ط±ظٹظƒظٹ";
    public decimal? Amount3Numeric { get; set; }
    public string? Amount3Words { get; set; }
    public string? Currency3 { get; set; } = "ظ„ظٹط±ط© ط³ظˆط±ظٹط©";
    public decimal? InclusionAmountNumeric { get; set; }
    public string? InclusionAmountWords { get; set; }
    public string? InclusionCurrency { get; set; } = "ظ„ظٹط±ط© ط³ظˆط±ظٹط©";
    public decimal? InclusionAmount2Numeric { get; set; }
    public string? InclusionAmount2Words { get; set; }
    public string? InclusionCurrency2 { get; set; } = "ظ„ظٹط±ط© ط³ظˆط±ظٹط©";
    public decimal? InclusionAmount3Numeric { get; set; }
    public string? InclusionAmount3Words { get; set; }
    public string? InclusionCurrency3 { get; set; } = "ظ„ظٹط±ط© ط³ظˆط±ظٹط©";

    public string? Court { get; set; }
    /// <summary>
    /// ط¯ط§ط¦ط±ط© ط§ظ„طھظ†ظپظٹط° (ط³ط¬ظ„ ط§ظ„ط¯ظˆط§ط¦ط±): ط¥ظ„ط²ط§ظ…ظٹط© ظ„ظ„ط¬ط¯ظٹط¯ ط¹ط¯ط§ ظ…ظ„ظپط§طھ ط§ظ„ظ…ظ†ط§ط¨ ط§ظ„ط®ط§ط±ط¬ظٹ
    /// (IsExternal=true). ط§ظ„ط®ط§ط¯ظ… ظٹط´طھظ‚ Court/CourtNorm ظ…ظ† ط§ظ„ط¯ط§ط¦ط±ط© ظˆظٹطھط¬ط§ظ‡ظ„ ظ†طµ ط§ظ„ط¹ظ…ظٹظ„ ط¯ط§ط¦ظ…ظ‹ط§.
    /// </summary>
    public int? ExecutionCircuitId { get; set; }
    public string? Applicant { get; set; }

    public string? FileNumber { get; set; }
    public string? FileType { get; set; }
    public string? FileYear { get; set; }
    public string? FileIncoming { get; set; }
    public string? FileIncomingDate { get; set; }
    public string? UnderFilingNumber { get; set; }
    public string? FileRegistrationDate { get; set; }
    public string? BranchName { get; set; }

    public string? SeizureDate { get; set; }
    public string? ImmediateActions { get; set; }
    public string? Notes { get; set; }

    public List<GuarantorDto> Guarantors { get; set; } = new();
    public List<AssetDto> Assets { get; set; } = new();

    /// <summary>ظˆط±ط«ط© ط§ظ„ظ…ظ‚طھط±ط¶ ط§ظ„ظ…طھظˆظپظ‰ (ط¥ظ† ظˆظڈط¬ط¯ظˆط§)ط› ط§ظ„طµظپظˆظپ ط¨ظ„ط§ ط§ط³ظ… طھظڈطھط¬ط§ظ‡ظ„ ط¹ظ†ط¯ ط§ظ„ط­ظپط¸.</summary>
    public List<HeirDto> BorrowerHeirs { get; set; } = new();

    /// <summary>
    /// طµظپط© ط§ظ„ظ…ظ„ظپ (GeneralEntitySideCatalog): طھظڈط«ط¨ظژظ‘طھ ط¹ظ†ط¯ ط§ظ„ط¥ظ†ط´ط§ط، ظˆظ„ط§ طھظڈط؛ظٹظ‘ط± ط¹ظ†ط¯ ط§ظ„طھط¹ط¯ظٹظ„.
    /// ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» ظٹظڈط´طھط±ط· ط¹ط§ط¯ظٹ ظپظ‚ط· (ظ„ط§ ظ…طµط±ظپظٹ) ظˆطھظڈظ„ط؛ظ‰ ط¨ظٹط§ظ†ط§طھ ط§ظ„ظ…ظ‚طھط±ط¶ ظˆط§ظ„ظƒظپظ„ط§ط، ظˆط§ظ„ط£ظ…ظˆط§ظ„.
    /// </summary>
    public string? GeneralEntitySide { get; set; } = GeneralEntitySideCatalog.Applicant;

    /// <summary>ط­ط§ظ„ط© ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» (ExecutedStatusCatalog): ظ…طھط¯ط§ظˆظ„/ظ…ظ†ظپط°/ظ…ط´ط·ظˆط¨.</summary>
    public string? ExecutedStatus { get; set; } = string.Empty;

    /// <summary>طھط§ط±ظٹط® ط§ظ„ط´ط·ط¨ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» (ظ†طµ ط­ط± ظٹظڈظپط³ظژظ‘ط± ط¨طµظٹط؛ آ«1/8/2026آ» ظˆظٹظڈط®ط²ظژظ‘ظ† DateTime ظپظٹ ط§ظ„ظ‚ط§ط¹ط¯ط©).</summary>
    public string? StruckOffDate { get; set; }

    /// <summary>ظˆطµظپ/ط¨ظٹط§ظ† ط¥ط¶ط§ظپظٹ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ».</summary>
    public string? ExecutedDescription { get; set; }

    /// <summary>طھط§ط±ظٹط® ظˆط±ظˆط¯ ط§ظ„ط§ط®ط·ط§ط± ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» (ظ†طµ ط­ط± ظٹظڈظپط³ظژظ‘ط± ط¨طµظٹط؛ آ«1/8/2026آ» ظˆظٹط؛ط°ظٹ ظپطھط±ط© ط¥ط­طµط§ط¦ظٹط© آ«ظ…طھط¯ط§ظˆظ„ ظ„ظ„ط¶ط¯آ»).</summary>
    public string? FileReceiptDate { get; set; }

    /// <summary>ط±ظ‚ظ… ظˆط±ظˆط¯ ط§ظ„ط¥ط®ط·ط§ط± ط§ظ„طھظ†ظپظٹط°ظٹ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ».</summary>
    public string? FileReceiptNumber { get; set; }

    /// <summary>ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط¯ظپط¹ظ‡ ظ…ظ† ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ».</summary>
    public decimal? ExecutedRequiredAmount { get; set; }

    /// <summary>ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط£ظˆظ„ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©).</summary>
    public string? ExecutedRequiredCurrency { get; set; }

    /// <summary>ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط«ط§ظ†ظٹ (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ».</summary>
    public decimal? ExecutedRequiredAmount2 { get; set; }

    /// <summary>ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط«ط§ظ†ظٹ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©).</summary>
    public string? ExecutedRequiredCurrency2 { get; set; }

    /// <summary>ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط«ط§ظ„ط« (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ».</summary>
    public decimal? ExecutedRequiredAmount3 { get; set; }

    /// <summary>ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط«ط§ظ„ط« ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©).</summary>
    public string? ExecutedRequiredCurrency3 { get; set; }

    /// <summary>ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ط°ظٹ ط¯ظپط¹طھظ‡ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» / ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ظˆط¯ط¹ ظپظٹ آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ».</summary>
    public decimal? ExecutedPaidAmount { get; set; }

    /// <summary>ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط£ظˆظ„ (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©).</summary>
    public string? ExecutedPaidCurrency { get; set; }

    /// <summary>ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ†ظٹ (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» / آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ».</summary>
    public decimal? ExecutedPaidAmount2 { get; set; }

    /// <summary>ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ†ظٹ (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©).</summary>
    public string? ExecutedPaidCurrency2 { get; set; }

    /// <summary>ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ„ط« (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» / آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ».</summary>
    public decimal? ExecutedPaidAmount3 { get; set; }

    /// <summary>ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ„ط« (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©).</summary>
    public string? ExecutedPaidCurrency3 { get; set; }

    /// <summary>طھط§ط±ظٹط® ط§ظٹط¯ط§ط¹ظ‡ ط­ط³ط§ط¨ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظپظٹ ظˆط¶ط¹ آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ» (ظ†طµ ط­ط± ظٹظڈظپط³ظژظ‘ط± ط¨طµظٹط؛ آ«1/8/2026آ»).</summary>
    public string? ExecutedDepositDate { get; set; }

    /// <summary>طھط§ط±ظٹط® ط§ظ„طھظ†ظپظٹط° ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» (ظ†طµ ط­ط± ظٹظڈظپط³ظژظ‘ط± ط¨طµظٹط؛ آ«1/8/2026آ»).</summary>
    public string? ExecutedExecutionDate { get; set; }

    /// <summary>طھط§ط±ظٹط® ظ‚ط±ط§ط± ط§ظ„ط¥ط­ط§ظ„ط© ط§ظ„ظ‚ط·ط¹ظٹط© ظپظٹ آ«ظ…ظ†ظپط° ط¬ط¨ط±ظٹط§آ» (ظ†طµ ط­ط±).</summary>
    public string? ForcedExecutionDate { get; set; }

    /// <summary>ط·ط§ظ„ط¨ظˆ ط§ظ„طھظ†ظپظٹط° ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ».</summary>
    public List<ExecutionApplicantDto> ExecutionApplicants { get; set; } = new();

    /// <summary>ط§ظ„ط¬ظ‡ط§طھ ط§ظ„ط¹ط§ظ…ط© ط§ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ».</summary>
    public List<ExecutedPublicEntityDto> ExecutedPublicEntities { get; set; } = new();

    /// <summary>ط§ظ„ط£ط´ط®ط§طµ ط§ظ„ط·ط¨ظٹط¹ظٹظˆظ† ط§ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ظ… ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ».</summary>
    public List<ExecutedNaturalPersonDto> ExecutedNaturalPersons { get; set; } = new();

    /// <summary>ط§ظ„ط¬ظ‡ط§طھ ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© ط§ظ„طھظ†ظپظٹط° ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ظˆط§ط­ط¯ط© ط£ظˆ ط£ظƒط«ط±).</summary>
    public List<ApplicantPublicEntityDto> ApplicantPublicEntities { get; set; } = new();

    /// <summary>ط±ظ‚ظ… ظˆط±ظˆط¯ ط§ظ„ظ…ظ„ظپ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ط§ط®طھظٹط§ط±ظٹ).</summary>
    public string? FileArrivalNumber { get; set; }

    /// <summary>طھط§ط±ظٹط® ظˆط±ظˆط¯ ط§ظ„ظ…ظ„ظپ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ظ†طµ ط­ط±).</summary>
    public string? FileArrivalDate { get; set; }

    /// <summary>
    /// ط¥ط¬ط±ط§ط،ط§طھ ظˆظ…ظ„ط§ط­ط¸ط§طھ طھظڈط²ط±ط¹ ظپظٹ ط¬ط¯ظˆظ„ آ«ط§ظ„ط¥ط¬ط±ط§ط،ط§طھ ظˆط§ظ„ظ…ظ„ط§ط­ط¸ط§طھآ» ط°ط±ظ‘ظٹظ‹ط§ ظ…ط¹ ط­ظپط¸ ط§ظ„ظ…ط³طھظ†ط¯.
    /// ظƒظ„ ط¹ظ†طµط± ظٹظ…ط± ط¨ظ†ظپط³ ط¯ظˆط§ظ„ ط§ظ„طھط¹ظ‚ظٹظ… ظˆط§ظ„طھط­ظ‚ظ‚ ط§ظ„ط®ط§طµط© ط¨ط§ظ„ط¥ط¬ط±ط§ط،ط§طھ (NormalizeAction/NormalizeReminder)طŒ
    /// ظˆط£ظٹ ط¹ظ†طµط± ط؛ظٹط± طµط§ظ„ط­ ظٹط±ظپط¶ ط§ظ„ط­ظپط¸ ظƒط§ظ…ظ„ظ‹ط§ ط¨ظ†ظپط³ ط§ظ„ظ…ط¹ط§ظ…ظ„ط©طŒ ظˆظ„ط§ ظٹظڈظ†ط´ط£ ط£ظٹ ط³ط¬ظ„ ظ…ظƒط±ط± ط¹ظ†ط¯ ط§ظ„طھط¹ط¯ظٹظ„.
    /// ط§ظ„ط­ظ‚ظˆظ„ ط§ظ„ظ…طھط±ظˆظƒط© ظپط§ط±ط؛ط© طھظڈطھط¬ط§ظ‡ظ„ ظˆظ„ط§ طھظڈط³ط¨ط¨ ظپط´ظ„ ط§ظ„ط­ظپط¸.
    /// </summary>
    public List<AddExecutionActionRequest> InitialActions { get; set; } = new();
}
