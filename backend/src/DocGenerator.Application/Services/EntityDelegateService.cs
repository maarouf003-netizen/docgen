using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.PublicEntities;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// إدارة حسابات مندوبي الجهات العامة داخل النظام نفسه (د11): يضيفها المدير/
/// المشرف/رئيس القسم ويربط كل حساب بنطاقه (هوية أم أو قيد بعينه) — والتحقق
/// من الصلاحية في المتحكم عبر RolePermissions.CanManageDelegates.
/// بقيت في الخدمة مساعدات الكتابة فقط (`IEntityDelegateWriteRules`)؛ القراءة
/// صارت استعلام `ListEntityDelegatesQuery` (`P4-01ct`).
/// </summary>
public sealed class EntityDelegateService : IEntityDelegateWriteRules
{
    private const int MaxUsernameLength = 50;

    private readonly IPublicEntityRepository _registry;
    private readonly IRepository<Branch>? _branches;

    public EntityDelegateService(
        IPublicEntityRepository registry,
        IRepository<Branch>? branches = null)
    {
        _registry = registry;
        // مستودع الفروع لنطاق المحافظة اختياري التركيب (اختبارات قديمة بلا فاعل) —
        // الإنتاج يمرره دائمًا عبر `DI`؛ غيابه مع فاعل رئيس = بلا قيد (موثق).
        _branches = branches;
    }

    private async Task<(int? GroupId, int? EntryId)> ResolveScopeAsync(int? groupId, int? entryId, CancellationToken ct)
    {
        if (groupId is null && entryId is null)
            throw new ArgumentException("حدّد نطاق المندوب: هوية أم قيدًا بعينه");
        if (groupId.HasValue && entryId.HasValue)
            throw new ArgumentException("حدّد نطاقًا واحدًا فقط: هوية أم قيدًا بعينه");

        if (entryId.HasValue)
        {
            var entry = await _registry.GetEntryAsync(entryId.Value, ct)
                ?? throw new ArgumentException("قيد الجهة غير موجود في السجل");
            // شرط الظهور نفسه في البوابة (PortalRepository.ResolveForUserAsync):
            // نهائي + نشط + بلا مراجعة معلقة — وإلا قُبِل الربط وبقيت بوابة
            // المندوب فارغة بصمت. الرسائل على نسق PublicEntityService.
            if (entry.Status != EntityStatusCatalog.Final)
                throw new ArgumentException("لا يمكن ربط مندوب بقيد بانتظار الاعتماد");
            if (entry.NeedsReview)
                throw new ArgumentException("لا يمكن ربط مندوب بقيد بانتظار المراجعة؛ اعتمده أولًا");
            if (!entry.IsActive)
                throw new ArgumentException("لا يمكن ربط مندوب بقيد غير نشط");
            return (null, entry.Id);
        }

        var group = await _registry.GetGroupAsync(groupId!.Value, ct)
            ?? throw new ArgumentException("هوية الجهة غير موجودة في السجل");
        return (group.Id, null);
    }

    private static void ValidateCredentials(string username, string password, string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("الاسم الكامل مطلوب");
        if (username.Length == 0)
            throw new ArgumentException("اسم الدخول مطلوب");
        if (username.Length > MaxUsernameLength)
            throw new ArgumentException($"اسم الدخول أطول من المسموح ({MaxUsernameLength} حرفاً)");
        // M-02: نفس السياسة الموحدة (لا باب خلفي بحد أضعف للمندوبين).
        PasswordPolicy.EnsureValid(password, username);
    }

    private static string NormalizeUsername(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return ArabicNameNormalizer.Normalize(normalized);
    }

    private static string DescribeScope(int? groupId, int? entryId)
        => entryId.HasValue ? $"قيد #{entryId}" : $"هوية #{groupId}";

    // ── تنفيذ عقد مساعدات الكتابة المشتركة (P4-01bf) ─────────────────────

    Task<(int? GroupId, int? EntryId)> IEntityDelegateWriteRules.ResolveScopeAsync(
        int? groupId, int? entryId, CancellationToken ct)
        => ResolveScopeAsync(groupId, entryId, ct);

    Task IEntityDelegateWriteRules.EnsureHeadGovernorateAsync(
        EntityRegistryActor? actor, int? groupId, int? entryId, CancellationToken ct)
        => EntityDelegateProjection.EnsureHeadGovernorateAsync(
            actor, groupId, entryId, _branches, _registry, ct);

    Task<DelegateDto> IEntityDelegateWriteRules.BuildDtoWithScopeAsync(User user, CancellationToken ct)
        => EntityDelegateProjection.BuildDtoWithScopeAsync(user, _registry, ct);

    string IEntityDelegateWriteRules.NormalizeUsername(string? value)
        => NormalizeUsername(value);

    void IEntityDelegateWriteRules.ValidateCredentials(string username, string password, string? fullName)
        => ValidateCredentials(username, password, fullName);

    string IEntityDelegateWriteRules.DescribeScope(int? groupId, int? entryId)
        => DescribeScope(groupId, entryId);
}
