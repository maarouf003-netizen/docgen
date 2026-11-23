using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

public interface IAppSuggestionService
{
    Task<List<AppSuggestionDto>> ListMineAsync(int senderId, CancellationToken ct = default);
    Task<AppSuggestionDto?> GetByIdAsync(int id, int? senderId, CancellationToken ct = default);
    Task<PagedResult<AppSuggestionDto>> ListForAdminAsync(int page, int perPage, CancellationToken ct = default);
    /// <summary>عدد الاقتراحات غير المقروءة في صندوق المشرف/المدير.</summary>
    Task<int> CountUnreadAsync(CancellationToken ct = default);
}

/// <summary>
/// اقتراحات تطوير التطبيق: الإرسال للمستخدمين (المحامي الآن)، والقراءة الشاملة
/// وتعليم المقروء للمشرف والمدير (`BQ-001`) — والكتابة ضمن معاملة مع سجل التدقيق.
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

    public Task<int> CountUnreadAsync(CancellationToken ct = default)
        => _suggestions.CountUnreadAsync(ct);

    private static AppSuggestionDto ToDto(AppSuggestion s, bool includeSender) => new(
        s.Id,
        s.Message,
        s.CreatedAt,
        s.IsRead,
        includeSender ? s.Sender?.FullName ?? s.Sender?.Username : null);
}
