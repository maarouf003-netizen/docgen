using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class ExecutionCircuitService : IExecutionCircuitService
{
    public async Task DeleteAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, CancellationToken ct = default)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        await _tx.RunAsync(async token =>
        {
            var circuit = await GetOwnedAsync(circuitId, branchId, ownerSectionId, token);
            // حارس الحذف: الأحياء فقط (تعديل البند 41) + الإنابات الواردة المعلقة.
            var counts = await _documents.CountByCircuitsAsync(new List<int> { circuitId }, token);
            counts.TryGetValue(circuitId, out var v);
            if (v.FileCount > 0)
                throw new DocumentConflictException($"لا يمكن حذف الدائرة — أفرغها أولًا (بها {v.FileCount} ملفًا)");
            // C1: متجاوزًا فلتر المصدر المحذوف — المعلقة تحظر أيًا كان حال مصدرها.
            var pendingIncoming = await _delegations.CountPendingIncomingByCircuitIncludingDeletedAsync(circuitId, token);
            if (pendingIncoming > 0)
                throw new DocumentConflictException($"لا يمكن حذف الدائرة — بها {pendingIncoming} إنابة واردة معلقة تستهدفها");
            // التاريخ المجمد (البند 45): الإنابات غير المعلقة التي استهدفت الدائرة تُفك
            // عن المرجع (DelegatedCircuitId=null) مع إبقاء الاسم النصي — وإلا انفجر الحذف
            // بانتهاك FK (Restrict) رغم اجتياز الحارس. المعلقة محظورة أعلاه فلا تصل هنا.
            // C1: متجاوزًا الفلتر أيضًا — إنابة مصدرها محذوف ما زال صفها يشير للدائرة.
            // فصل الوارد قبل التطهير (علة الترتيب المذكورة أعلاه).
            var incoming = await _delegations.ListByDelegatedCircuitIncludingDeletedAsync(circuitId, token);
            var detached = 0;
            var now = DateTime.UtcNow;
            foreach (var g in incoming)
            {
                if (g.Status == DelegationStatusCatalog.PendingHead)
                    throw new DocumentConflictException("لا يمكن حذف الدائرة — بها إنابة واردة معلقة تستهدفها");
                g.DelegatedCircuitId = null;
                g.UpdatedAt = now;
                _delegations.Update(g);
                detached++;
            }
            // تطهير سلة المحذوفات نهائيًا (قرار المالك — تعديل البند 41): حذف فيزيائي
            // ضمن معاملة الحذف نفسها؛ والتسلسل الموثوق به: CASCADE للتوابع (الوقوعات/
            // أرقام الأساس/الإسنادات/الأطراف/الكفلاء/الورثة/الأموال/الاستئنافات/إنابات
            // الملف المحذوف)، SET NULL للتنبيهات/المرسلات/خطابات المراجعة — والعلة
            // تسهل اختبارها: أي صف شاذ يفشل بصوت عالٍ داخل المعاملة مع rollback.
            var purged = 0;
            var deletedDocs = await _documents.ListDeletedByCircuitAsync(circuitId, token);
            var deletedDocIds = deletedDocs.Select(d => d.Id).ToList();
            // FK أرقام الأساس على المستند Restrict (V-18/E17): التطهير الفيزيائي للدائرة
            // يزيل صفوف أرقام الملفات المحذوفة صراحةً قبل حذف المستندات (وإلا انفجر الحذف).
            if (deletedDocIds.Count > 0)
            {
                if (_baseNumberRepository is not null)
                {
                    foreach (var deletedDocId in deletedDocIds)
                    {
                        var rows = await _baseNumberRepository.ListByDocumentAsync(
                            deletedDocId, includeDeletedDocument: true, token);
                        foreach (var row in rows)
                            _baseNumberRepository.Remove(row);
                    }
                }
                else
                {
                    var idSet = deletedDocIds.ToHashSet();
                    var baseRows = (await _baseNumbers.ListAsync(token))
                        .Where(b => idSet.Contains(b.DocumentId))
                        .ToList();
                    foreach (var row in baseRows)
                        _baseNumbers.Remove(row);
                }
            }
            var linkedAlerts = await _alerts.ListActiveByCircuitOrDocumentsAsync(circuitId, deletedDocIds, token);
            foreach (var alert in linkedAlerts)
            {
                alert.IsArchived = true;
                alert.ArchivedAt = now;
            }
            foreach (var d in deletedDocs)
            {
                _documents.Remove(d);
                purged++;
            }
            _circuits.Remove(circuit);
            await _uow.SaveChangesAsync(token);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);
            // عبارة التطهير تُلحق للتدقيق فقط عند purged > 0 (لا «مع» مكررة ولا «تطهير 0»).
            var purgeClause = purged > 0 ? $"، وطُهِّر نهائيًا {purged} ملفًا محذوفًا" : string.Empty;
            await _audit.LogAsync(actorName, "delete_circuit", null, null,
                $"حذف دائرة التنفيذ «{circuit.Name}» مع فك {detached} إنابة مكتملة عن مرجعها (الاسم محفوظ){purgeClause}", token);
        }, ct);
    }
}
