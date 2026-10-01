using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

public interface IAppSuggestionService
{
    Task<List<AppSuggestionDto>> ListMineAsync(int senderId, CancellationToken ct = default);
    Task<AppSuggestionDto?> GetByIdAsync(int id, int? senderId, CancellationToken ct = default);
    Task<AppSuggestionDto> CreateAsync(string message, int senderId, string? actorName, CancellationToken ct = default);
    Task<PagedResult<AppSuggestionDto>> ListForAdminAsync(int page, int perPage, CancellationToken ct = default);
    Task<bool> MarkReadAsync(int id, CancellationToken ct = default, string? actorName = null);
}

/// <summary>
/// اقتراحات تطوير التطبيق: الإرسال للمستخدمين (المحامي الآن)، والقراءة الشاملة
/// وتعليم المقروء للمشرف فقط — والكتابة ضمن معاملة مع سجل التدقيق.
/// </summary>
public sealed class AppSuggestionService : IAppSuggestionService
{
    public const int MessageMaxLength = 2000;

    /// <summary>حجم صفحة صندوق المشرف الافتراضي والأقصى — يمنع جلب الصندوق كاملًا.</summary>
    public const int DefaultPerPage = 20;
    public const int MaxPerPage = 50;

    private readonly IAppSuggestionRepository _suggestions;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public AppSuggestionService(
        IAppSuggestionRepository suggestions,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _suggestions = suggestions;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    public async Task<List<AppSuggestionDto>> ListMineAsync(int senderId, CancellationToken ct = default)
    {
        var items = await _suggestions.ListBySenderAsync(senderId, ct);
        return items.Select(s => ToDto(s, includeSender: false)).ToList();
    }

    /// <summary>
    /// اقتراح واحد بالمعرف: `senderId` فارغ = عرض المشرف (أي اقتراح مع اسم المرسل)،
    /// وله قيمة = عرض المالك (اقتراحه فقط بلا اسم). الغائب أو غير المملوك = `null`
    /// دون كشف الوجود — بنفس عقد `List`.
    /// </summary>
    public async Task<AppSuggestionDto?> GetByIdAsync(int id, int? senderId, CancellationToken ct = default)
    {
        var suggestion = await _suggestions.GetByIdWithSenderAsync(id, ct);
        if (suggestion is null)
            return null;
        if (senderId.HasValue)
        {
            if (suggestion.SenderId != senderId.Value)
                return null;
            return ToDto(suggestion, includeSender: false);
        }
        return ToDto(suggestion, includeSender: true);
    }

    public async Task<AppSuggestionDto> CreateAsync(string message, int senderId, string? actorName, CancellationToken ct = default)
    {
        var trimmed = message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("نص الاقتراح مطلوب");
        if (trimmed.Length > MessageMaxLength)
            throw new ArgumentException($"نص الاقتراح يتجاوز الحد الأقصى ({MessageMaxLength} حرفًا)");

        var suggestion = new AppSuggestion
        {
            SenderId = senderId,
            Message = trimmed,
            CreatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _suggestions.AddAsync(suggestion, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "app-suggestion.create",
                details: $"أرسل اقتراح تطوير: {trimmed[..Math.Min(trimmed.Length, 80)]}",
                ct: token);
        }, ct);

        return ToDto(suggestion, includeSender: false);
    }

    public async Task<PagedResult<AppSuggestionDto>> ListForAdminAsync(int page, int perPage, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (perPage < 1 || perPage > MaxPerPage) perPage = DefaultPerPage;
        var paged = await _suggestions.PagedForAdminAsync(page, perPage, ct);
        return new PagedResult<AppSuggestionDto>
        {
            Items = paged.Items.Select(s => ToDto(s, includeSender: true)).ToList(),
            Page = paged.Page,
            PerPage = paged.PerPage,
            TotalCount = paged.TotalCount,
        };
    }

    public async Task<bool> MarkReadAsync(int id, CancellationToken ct = default, string? actorName = null)
    {
        var suggestion = await _suggestions.GetByIdAsync(id, ct);
        if (suggestion is null)
            return false;
        if (suggestion.IsRead)
            return true;

        await _tx.RunAsync(async token =>
        {
            suggestion.IsRead = true;
            _suggestions.Update(suggestion);
            await _uow.SaveChangesAsync(token);
            // RF-017 (SEC-015): تعليم المقروء إخفاء أثر إداري — يُوثَّق بالفاعل داخل نفس المعاملة.
            await _audit.LogAsync(actorName, "app-suggestion.read",
                details: $"علّم اقتراح التطوير (رقم {suggestion.Id}) مقروءًا", ct: token);
        }, ct);
        return true;
    }

    private static AppSuggestionDto ToDto(AppSuggestion s, bool includeSender) => new(
        s.Id,
        s.Message,
        s.CreatedAt,
        s.IsRead,
        includeSender ? s.Sender?.FullName ?? s.Sender?.Username : null);
}
