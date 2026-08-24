using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IHeadAlertService
{
    Task<List<HeadAlertDto>> ListForLawyerAsync(int userId, CancellationToken ct = default);
    Task<List<HeadAlertDto>> ListForHeadAsync(int branchId, CancellationToken ct = default);
    Task<int> CountUnreadAsync(int userId, CancellationToken ct = default);
    Task<HeadAlertDto> CreateAsync(CreateHeadAlertRequest request, int actorUserId, int actorBranchId, string? actorName, CancellationToken ct = default);
    Task<bool> MarkReadAsync(int alertId, int userId, CancellationToken ct = default);
    Task<HeadAlertDto?> UpdateDelegationAlertAsync(int delegationId, string message, CancellationToken ct = default);
    Task<bool> DeleteByDelegationAsync(int delegationId, CancellationToken ct = default);
    /// <summary>حذف كل تنبيهات الاستئناف (تصفية تنبيه «اختيار المحامي» بعد الإسناد).</summary>
    Task<bool> DeleteByAppealAsync(int appealId, CancellationToken ct = default);
}

/// <summary>
/// تنبيهات رئيس القسم: الإصدار لفرعه فقط (رئيس القسم)، والاستلام بحسب الدور.
/// الحذف النهائي للفرع/المستخدم مقيد بالعلاقات، والكتابة ضمن معاملة مع سجل التدقيق.
/// </summary>
public sealed class HeadAlertService : IHeadAlertService
{
    private readonly IHeadAlertRepository _alerts;
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IRepository<Branch> _branches;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public HeadAlertService(
        IHeadAlertRepository alerts,
        IDocumentRepository documents,
        IUserRepository users,
        IRepository<Branch> branches,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _alerts = alerts;
        _documents = documents;
        _users = users;
        _branches = branches;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    public async Task<List<HeadAlertDto>> ListForLawyerAsync(int userId, CancellationToken ct = default)
    {
        var alerts = await _alerts.ListForRecipientAsync(userId, ct);
        return alerts.Select(a => ToLawyerDto(a, userId)).ToList();
    }

    public async Task<List<HeadAlertDto>> ListForHeadAsync(int branchId, CancellationToken ct = default)
    {
        var alerts = await _alerts.ListByBranchAsync(branchId, ct);
        return alerts.Select(ToHeadDto).ToList();
    }

    public Task<int> CountUnreadAsync(int userId, CancellationToken ct = default)
        => _alerts.CountUnreadAsync(userId, ct);

    public async Task<HeadAlertDto> CreateAsync(
        CreateHeadAlertRequest request,
        int actorUserId,
        int actorBranchId,
        string? actorName,
        CancellationToken ct = default)
    {
        var message = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("نص التنبيه مطلوب");

        var branch = await _branches.GetByIdAsync(actorBranchId, ct);
        if (branch is null)
            throw new ArgumentException("الفرع غير موجود");

        var targetType = ParseTargetType(request.TargetType);
        var recipients = await ResolveRecipientsAsync(targetType, request, actorBranchId, ct);
        if (recipients.Count == 0)
            throw new ArgumentException("لا يوجد مستلمون للتنبيه");

        var alert = new HeadAlert
        {
            BranchId = actorBranchId,
            CreatedById = actorUserId,
            TargetType = targetType,
            DocumentId = targetType == HeadAlertTargetType.Lawyer ? null : request.DocumentId,
            TargetLawyerId = targetType == HeadAlertTargetType.Lawyer ? request.TargetLawyerId : null,
            DelegationId = request.DelegationId,
            AppealId = request.AppealId,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            Recipients = recipients.Select(u => new HeadAlertRecipient { UserId = u.Id }).ToList(),
        };

        await _tx.RunAsync(async token =>
        {
            await _alerts.AddAsync(alert, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_alert",
                details: $"أصدر تنبيهاً في فرع {branch.Name} بنطاق {targetType}: {message[..Math.Min(message.Length, 80)]}",
                ct: token);
        }, ct);

        return ToHeadDto(alert);
    }

    public async Task<bool> MarkReadAsync(int alertId, int userId, CancellationToken ct = default)
    {
        var alert = await _alerts.GetByIdWithRecipientsAsync(alertId, ct);
        if (alert is null)
            return false;

        var recipient = alert.Recipients.FirstOrDefault(r => r.UserId == userId);
        if (recipient is null)
            return false;

        if (recipient.IsRead)
            return true;

        await _tx.RunAsync(async token =>
        {
            recipient.IsRead = true;
            recipient.ReadAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(token);
        }, ct);
        return true;
    }

    /// <summary>
    /// تحديث رسالة آخر تنبيه لإنابة معلّقة (بانتظار الاعتماد) بعد تعديل الإنابة —
    /// يعيد null عندما لا يوجد تنبيه للإنابة (لم يُنشأ حينها)، ويُبقي المستلمين وعلامات القراءة.
    /// </summary>
    public async Task<HeadAlertDto?> UpdateDelegationAlertAsync(int delegationId, string message, CancellationToken ct = default)
    {
        var alert = await _alerts.FindLatestByDelegationAsync(delegationId, ct);
        if (alert is null)
            return null;

        var trimmed = message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("نص التنبيه مطلوب");
        if (alert.Message == trimmed)
            return ToHeadDto(alert);

        alert.Message = trimmed;
        await _tx.RunAsync(async token =>
        {
            _alerts.Update(alert);
            await _uow.SaveChangesAsync(token);
        }, ct);
        return ToHeadDto(alert);
    }

    /// <summary>حذف كل تنبيهات الإنابة (تصفية المرحلية منها عند الاعتماد أو الإتمام أو حذف الإنابة).</summary>
    public async Task<bool> DeleteByDelegationAsync(int delegationId, CancellationToken ct = default)
    {
        var alerts = await _alerts.ListByDelegationAsync(delegationId, ct);
        if (alerts.Count == 0)
            return false;

        await _tx.RunAsync(async token =>
        {
            foreach (var alert in alerts)
                _alerts.Remove(alert);
            await _uow.SaveChangesAsync(token);
        }, ct);
        return true;
    }

    /// <summary>حذف كل تنبيهات الاستئناف (تصفية تنبيه «اختيار المحامي» بعد إسناد الاستئناف).</summary>
    public async Task<bool> DeleteByAppealAsync(int appealId, CancellationToken ct = default)
    {
        var alerts = await _alerts.ListByAppealAsync(appealId, ct);
        if (alerts.Count == 0)
            return false;

        await _tx.RunAsync(async token =>
        {
            foreach (var alert in alerts)
                _alerts.Remove(alert);
            await _uow.SaveChangesAsync(token);
        }, ct);
        return true;
    }

    private async Task<List<User>> ResolveRecipientsAsync(
        HeadAlertTargetType targetType,
        CreateHeadAlertRequest request,
        int branchId,
        CancellationToken ct)
    {
        switch (targetType)
        {
            case HeadAlertTargetType.Document:
            {
                if (request.DocumentId is null)
                    throw new ArgumentException("يجب تحديد الملف المرتبط بالتنبيه");

                var doc = await _documents.GetByIdAsync(request.DocumentId.Value, ct);
                if (doc is null)
                    throw new ArgumentException("الملف غير موجود");
                if (doc.BranchId != branchId)
                    throw new ArgumentException("الملف ليس ضمن فرعك");
                if (doc.CreatedBy is null || doc.CreatedBy.Role != UserRole.Lawyer)
                    throw new ArgumentException("لا يوجد محامٍ مختص لهذا الملف");

                return new List<User> { doc.CreatedBy };
            }
            case HeadAlertTargetType.Lawyer:
            {
                if (request.TargetLawyerId is null)
                    throw new ArgumentException("يجب تحديد المحامي المستلم");

                var lawyer = await _users.GetByIdAsync(request.TargetLawyerId.Value, ct);
                if (lawyer is null || lawyer.Role != UserRole.Lawyer || lawyer.BranchId != branchId)
                    throw new ArgumentException("المحامي غير موجود ضمن فرعك");

                return new List<User> { lawyer };
            }
            case HeadAlertTargetType.Branch:
                return await _alerts.ListActiveLawyersAsync(branchId, ct);
            case HeadAlertTargetType.Head:
                // تنبيهات النظام لروّاد القسم (مراحل الإنابة): تصل لرؤساء أقسام الفرع المفعلين،
                // وعند غياب أي رئيس يُرفض الإنشاء ويُسجَّل فشل الإشعار في سجل التدقيق.
                return await _alerts.ListActiveHeadsAsync(branchId, ct);
            default:
                throw new ArgumentException("نوع التنبيه غير صالح");
        }
    }

    private static HeadAlertTargetType ParseTargetType(string? value)
    {
        if (Enum.TryParse<HeadAlertTargetType>(value?.Trim(), ignoreCase: true, out var parsed))
            return parsed;
        throw new ArgumentException("نوع التنبيه غير صالح");
    }

    private static string DocumentTitle(HeadAlert alert)
    {
        var doc = alert.Document;
        if (doc is null)
            return alert.DocumentId is null ? string.Empty : "مستند محذوف";

        var parts = new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var name = parts.Any() ? string.Join(" ", parts) : doc.DocumentType;
        return string.IsNullOrWhiteSpace(name) ? $"مستند {doc.Id}" : name;
    }

    private static HeadAlertDto ToLawyerDto(HeadAlert a, int userId) => new(
        a.Id,
        a.Message,
        a.TargetType.ToString().ToLowerInvariant(),
        a.DocumentId,
        DocumentTitle(a),
        a.TargetLawyerId,
        a.TargetLawyer?.FullName,
        a.Recipients.FirstOrDefault(r => r.UserId == userId)?.IsRead,
        null,
        null,
        a.CreatedAt,
        a.CreatedBy?.FullName,
        a.AppealId,
        a.ReviewLetterId);

    private static HeadAlertDto ToHeadDto(HeadAlert a) => new(
        a.Id,
        a.Message,
        a.TargetType.ToString().ToLowerInvariant(),
        a.DocumentId,
        DocumentTitle(a),
        a.TargetLawyerId,
        a.TargetLawyer?.FullName,
        null,
        a.Recipients.Count,
        a.Recipients.Count(r => !r.IsRead),
        a.CreatedAt,
        a.CreatedBy?.FullName,
        a.AppealId,
        a.ReviewLetterId);
}
