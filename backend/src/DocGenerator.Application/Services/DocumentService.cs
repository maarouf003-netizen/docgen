using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Services;

public interface IDocumentService
{
    Task<DocumentResponse?> GetAsync(int documentId, CancellationToken ct = default);
    /// <summary>
    /// إسقاط خفيف (F-01) لفحص الوصول في نقاط المتدخلين: لا يحمّل شجرة المستند الكاملة.
    /// </summary>
    Task<DocumentAccessInfo?> GetAccessInfoAsync(int documentId, CancellationToken ct = default);
    Task<DocumentResponse?> GetDeletedAsync(int documentId, CancellationToken ct = default);
    /// <summary>عدد ملفات المحامي غير المحذوفة (للمعاينة قبل النقل الجماعي).</summary>
    Task<int> CountFilesByOwnerAsync(int ownerId, int? scopeBranchId, CancellationToken ct = default, int? ownerSectionId = null);
    /// <summary>جدول تدوير أرقام الأساس للمحامي (ترحّلي): ملفاته المؤهلة مع رقم أساس السنة الحالية إن وُجد.</summary>
    Task<PagedResult<RotationDocumentDto>> GetRotationListAsync(int userId, int page, int perPage, CancellationToken ct = default);
    /// <summary>
    /// تاريخ أرقام الأساس للملف (سنة + رقم) مرتبًا تنازليًا بالسنوات — لعرضه عند الضغط على رقم الملف.
    /// </summary>
    Task<List<BaseNumberHistoryDto>> GetBaseNumberHistoryAsync(int documentId, CancellationToken ct = default);
    /// <summary>سجل أرقام ملف محذوف (IgnoreQueryFilters) — لصفحة المحذوفات.</summary>
    Task<List<BaseNumberHistoryDto>> GetDeletedBaseNumberHistoryAsync(int documentId, CancellationToken ct = default);
}

public sealed partial class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IDocumentNumberRegistry _numberRegistry;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public DocumentService(
        IDocumentRepository documents,
        IUserRepository users,
        IDocumentNumberRegistry numberRegistry,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _documents = documents;
        _users = users;
        _numberRegistry = numberRegistry;
        _clock = clock;
        _timeZone = timeZone;
    }

    public async Task<DocumentResponse?> GetAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        return doc is null ? null : DocumentResponse.FromEntity(doc, ServerClock.CurrentYear(_clock, _timeZone));
    }

    public Task<DocumentAccessInfo?> GetAccessInfoAsync(int documentId, CancellationToken ct = default)
        => _documents.GetAccessInfoAsync(documentId, ct);

    public async Task<DocumentResponse?> GetDeletedAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetDeletedByIdAsync(documentId, ct);
        return doc is null || !doc.IsDeleted ? null : DocumentResponse.FromEntity(doc, ServerClock.CurrentYear(_clock, _timeZone));
    }

    /// <summary>سنة «قرار السنة» الحالية من ساعة النظام ومنطقته — مصدر واحد لا تكرار قرار.</summary>
    private int CurrentYear() => ServerClock.CurrentYear(_clock, _timeZone);
}
