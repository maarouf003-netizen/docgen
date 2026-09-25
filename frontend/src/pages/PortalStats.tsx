import { useState } from 'react';
import { api } from '../api/client';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import PortalBranchSelect from '../components/portal/PortalBranchSelect';
import PortalStatsView from '../components/portal/PortalStatsView';
import type { PortalScopeDto, PortalStatsDto } from '../types';

/**
 * صفحة «الإحصائيات» لمندوب الجهة العامة:
 * - مندوب الهوية: إجمالي كل الفروع افتراضيًا + منتقي فرع لإحصائيات فرع بعينه.
 * - مندوب القيد: إحصائيات قيده فقط (بلا منتقي).
 * - بلا كتلة «أعلى العملات» — بدلها مجموع المبالغ الإجمالي ولكل نوع حسب العملة.
 */
export default function PortalStats() {
  const scopeQuery = useCancellableRequest<PortalScopeDto>(
    (signal) => api.get('/portal/my-scope', { signal }).then((r) => r.data),
    [],
  );
  const scope = scopeQuery.data;

  const [entryId, setEntryId] = useState('');

  const statsQuery = useCancellableRequest<PortalStatsDto>(
    (signal) => api.get('/portal/stats', {
      signal,
      params: entryId ? { entryId: Number(entryId) } : undefined,
    }).then((r) => r.data),
    [entryId],
  );
  const stats = statsQuery.data;

  const isGroup = scope?.scopeType === 'group';
  const showBranchSelect = isGroup && (scope?.entries?.length ?? 0) > 1;
  const selectedEntry = scope?.entries?.find((e) => String(e.id) === entryId) ?? null;

  return (
    <div className="max-w-6xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-2">الإحصائيات</h2>
      {scope && (
        <p className="text-sm text-gray-500 mb-4">
          نطاقك:{' '}
          <span className="font-medium text-gray-700">{scope.canonicalName || 'غير مضبوط بعد'}</span>
          {selectedEntry ? (
            <> · {selectedEntry.governorate}/{selectedEntry.branchName}</>
          ) : (
            <> · {scope.entries?.length ?? 0} قيدًا نشطًا</>
          )}
        </p>
      )}

      {showBranchSelect && (
        <div className="bg-white rounded-xl shadow p-4 mb-4 flex flex-wrap items-center gap-3">
          <PortalBranchSelect
            entries={scope?.entries ?? []}
            value={entryId}
            onChange={setEntryId}
            id="portal-stats-branch"
            label="اختيار الفرع لعرض إحصائياته"
          />
          {selectedEntry && (
            <p className="text-xs text-gray-500">
              تعرض الآن إحصائيات فرع {selectedEntry.governorate}/{selectedEntry.branchName} فقط
            </p>
          )}
        </div>
      )}

      {scopeQuery.isLoading && (
        <p className="text-gray-500 text-sm motion-safe:animate-pulse">جارِ تحميل النطاق…</p>
      )}
      {scopeQuery.error && <div role="alert" className="text-red-600 mb-4">{scopeQuery.error}</div>}

      {statsQuery.isLoading && !stats && (
        <p className="text-gray-500 text-sm motion-safe:animate-pulse">جارِ تحميل الإحصائيات…</p>
      )}
      {statsQuery.error && <div role="alert" className="text-red-600 mb-4">{statsQuery.error}</div>}

      {stats && (
        <PortalStatsView
          stats={stats}
          scopeType={scope?.scopeType}
          // النطاق المجهول (لم يُحلّ بعد) يُعامل جماعيًا: إظهار ملاحظة زائدة
          // أسلم من إخفاء واجبة.
          singleEntry={!!selectedEntry || (scope ? (scope.entries?.length ?? 0) <= 1 : false)}
        />
      )}

      {!statsQuery.isLoading && !stats && !statsQuery.error && (
        <p className="text-gray-400 text-sm">لا توجد إحصائيات في هذا النطاق بعد</p>
      )}

      <p className="mt-4 text-xs text-gray-400">
        هذه بوابة اطلاع قرائية: لا يمكنك تعديل الملفات أو حالتها أو توليد مستندات منها.
      </p>
    </div>
  );
}
