using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// مساعدات الإصدار اليدوي للتنبيهات المشتركة بين الخدمة وأوامر `P4-01bi`:
/// حل النطاق والمستلمين ووسم النوع — بلا فتح معاملة.
/// </summary>
internal interface IHeadAlertManualWriteRules
{
    int RequireBranchForWrite(HeadAlertActor actor);
    Task<List<User>> ResolveRecipientsAsync(
        DocGenerator.Domain.Enums.HeadAlertTargetType targetType,
        CreateHeadAlertRequest request,
        int branchId,
        CancellationToken ct);
    DocGenerator.Domain.Enums.HeadAlertTargetType ParseManualTargetType(string? value);
    string ManualKindFor(DocGenerator.Domain.Enums.HeadAlertTargetType targetType);
    HeadAlert BuildManualAlert(
        CreateHeadAlertRequest request,
        HeadAlertActor actor,
        int branchId,
        DocGenerator.Domain.Enums.HeadAlertTargetType targetType,
        string kind,
        string message,
        IReadOnlyList<User> recipients,
        DateTime now);
    Task AddManualAlertAsync(HeadAlert alert, CancellationToken ct);
}
