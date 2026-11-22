using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<T>> ListAsync(CancellationToken ct = default);
    /// <summary>قراءة قائمة **غير متتبَّعة** — آمنة للتخزين المؤقت (لا كيان مربوط بنطاق طلب منتهٍ).</summary>
    Task<List<T>> ListNoTrackingAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
