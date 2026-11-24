using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// مساعدات كتابة حسابات مندوبي الجهات المشتركة بين الخدمة وأوامر `P4-01bf`:
/// تطبيع بيانات الدخول، وحل النطاق، وحارس محافظة الرئيس، وبناء الـDTO — بلا فتح معاملة.
/// </summary>
internal interface IEntityDelegateWriteRules
{
    Task<(int? GroupId, int? EntryId)> ResolveScopeAsync(int? groupId, int? entryId, CancellationToken ct);
    Task EnsureHeadGovernorateAsync(EntityRegistryActor? actor, int? groupId, int? entryId, CancellationToken ct);
    Task<DelegateDto> BuildDtoWithScopeAsync(User user, CancellationToken ct);
    string NormalizeUsername(string? value);
    void ValidateCredentials(string username, string password, string? fullName);
    string DescribeScope(int? groupId, int? entryId);
}
