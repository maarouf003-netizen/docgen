using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

public interface IAppSuggestionRepository : IRepository<AppSuggestion>
{
    /// <summary>صندوق المشرف مرقّمًا — الأحدث أولًا مع اسم المرسل.</summary>
    Task<PagedResult<AppSuggestion>> PagedForAdminAsync(int page, int perPage, CancellationToken ct = default);

    /// <summary>اقتراحات مرسلٍ بعينه (سجل «اقتراحاتي») — الأحدث أولًا.</summary>
    Task<List<AppSuggestion>> ListBySenderAsync(int senderId, CancellationToken ct = default);

    /// <summary>اقتراح واحد مع اسم المرسل (لعرض المشرف) — `null` عند الغياب.</summary>
    Task<AppSuggestion?> GetByIdWithSenderAsync(int id, CancellationToken ct = default);
}
