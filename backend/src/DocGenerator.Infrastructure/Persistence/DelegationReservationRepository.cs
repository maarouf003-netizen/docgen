using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// حجوزات الأصول الصريحة (B3): قراءات بلا تتبع، وحذف مباشر بلا تحميل.
/// </summary>
public class DelegationReservationRepository(DocGeneratorDbContext db)
    : Repository<DelegationAssetReservation>(db), IDelegationReservationRepository
{
    public async Task<List<DelegationAssetReservation>> ListBySourceAsync(int sourceDocumentId, CancellationToken ct = default)
    {
        return await Db.DelegationAssetReservations
            .AsNoTracking()
            .Where(r => r.SourceDocumentId == sourceDocumentId)
            .ToListAsync(ct);
    }

    public async Task DeleteByDelegationAsync(int delegationId, CancellationToken ct = default)
    {
        await Db.DelegationAssetReservations
            .Where(r => r.DelegationId == delegationId)
            .ExecuteDeleteAsync(ct);
        DetachTracked(delegationId);
    }

    public async Task DeleteStaleAsync(int sourceDocumentId, CancellationToken ct = default)
    {
        // الشرط يُقيَّم داخل `DELETE` نفسه: كل حجز يُقارَن بإنابته الحالية في القاعدة،
        // فأي إنابة التزمت قبل بدء الجملة تبقى حاجزة، وأي حجز لم يكن مرئيًا أصلًا لا يُحذف.
        var blocking = DelegationActivityPolicy.IsAssetBlockingExpression;
        await Db.DelegationAssetReservations
            .Where(r => r.SourceDocumentId == sourceDocumentId)
            .Where(r => !Db.DocumentDelegations
                .Where(d => d.Id == r.DelegationId)
                .Any(blocking))
            .ExecuteDeleteAsync(ct);
        DetachTracked(sourceDocumentId: sourceDocumentId);
    }

    /// <summary>
    /// فصل النسخ المتتبعة بعد الحذف المباشر: ExecuteDelete يتجاوز المتتبع فيترك أشباحًا
    /// (Unchanged بلا صف) يسقطها حذف لاحق للإنابة تتاليًا فيفشل (0 صفوف) — الفصل يبقي
    /// المتتبع متماسكًا في السياقات طويلة العمر (الاختبارات وأي مسار مركب).
    /// </summary>
    private void DetachTracked(int? delegationId = null, int? sourceDocumentId = null)
    {
        var tracked = Db.ChangeTracker.Entries<DelegationAssetReservation>().ToList();
        foreach (var entry in tracked)
        {
            var row = entry.Entity;
            if (delegationId.HasValue && row.DelegationId != delegationId.Value)
                continue;
            if (sourceDocumentId.HasValue && row.SourceDocumentId != sourceDocumentId.Value)
                continue;
            entry.State = EntityState.Detached;
        }
    }
}
