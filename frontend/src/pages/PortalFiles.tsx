import { useEffect, useState } from 'react';
import { api } from '../api/client';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { downloadBlob } from '../utils/download';
import { todayLocalKey } from '../utils/dates';
import {
  EXECUTED_STATUS_EXECUTED,
  EXEC_STATUS_DEFERRED,
  EXEC_STATUS_REFERRED_TO_START,
  STATE_CIRCULATING,
  STATE_DRAFT,
} from '../utils/documentStatus';
import PortalBranchSelect from '../components/portal/PortalBranchSelect';
import PortalFileCard from '../components/portal/PortalFileCard';
import type {
  PortalFilesResponse,
  PortalScopeDto,
} from '../types';

const PAGE_SIZE = 20;

const STATUS_OPTIONS: ReadonlyArray<{ value: string; label: string }> = [
  { value: '', label: 'كل الحالات' },
  { value: STATE_CIRCULATING, label: STATE_CIRCULATING },
  { value: EXECUTED_STATUS_EXECUTED, label: EXECUTED_STATUS_EXECUTED },
  { value: EXEC_STATUS_DEFERRED, label: EXEC_STATUS_DEFERRED },
  { value: STATE_DRAFT, label: STATE_DRAFT },
  { value: EXEC_STATUS_REFERRED_TO_START, label: EXEC_STATUS_REFERRED_TO_START },
];

/** صفحة «الملفات التنفيذية» — البوابة القرائية لمندوب الجهة العامة (بلا إحصاءات). */
export default function PortalFiles() {
  const scopeQuery = useCancellableRequest<PortalScopeDto>(
    (signal) => api.get('/portal/my-scope', { signal }).then((r) => r.data),
    [],
  );
  const scope = scopeQuery.data;

  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('');
  const [entryId, setEntryId] = useState('');
  const [page, setPage] = useState(1);
  const [exporting, setExporting] = useState(false);
  const [exportMsg, setExportMsg] = useState('');

  // إلغاء الطلب السابق عند كل تغيير (لا استجابة قديمة تكتب فوق الأحدث) +
  // تأخير البحث النصي 300ms (طلب واحد لكل كتابة لا لكل حرف).
  const debouncedQuery = useDebouncedValue(query, 300);

  const showBranchSelect = scope?.scopeType === 'group' && (scope?.entries?.length ?? 0) > 1;

  // تغيّر النطاق يُبطل الترقيم: عودة للصفحة الأولى (setPage بلا أثر عند التطابق فلا حلقة).
  const scopeKey = scope == null ? null : `${scope.scopeType}:${scope.groupId}:${scope.entries?.length ?? 0}`;
  useEffect(() => {
    setPage(1);
  }, [scopeKey]);

  const listQuery = useCancellableRequest<PortalFilesResponse>(
    (signal) => api.get<PortalFilesResponse>('/portal/files', {
      signal,
      params: {
        q: debouncedQuery.trim() || undefined,
        status: status || undefined,
        entryId: entryId ? Number(entryId) : undefined,
        page,
        perPage: PAGE_SIZE,
      },
    }).then((r) => r.data),
    [debouncedQuery, status, entryId, page],
  );
  const list = listQuery.data;
  const loading = listQuery.isLoading;
  const error = listQuery.error;

  const exportExcel = () => {
    setExportMsg('');
    setExporting(true);
    // عبر مثيل axios المشترك: يضيف CSRF تلقائيًا ويعيد التوجيه عند انتهاء الجلسة.
    api
      .get('/portal/export', {
        params: {
          // المؤجَّل لا الخام (M1): المصدَّر يطابق القائمة المعروضة حرفيًا —
          // وإلا صدّر المستخدم خلال نافذة الـ300ms مجموعةً غير ما يرى.
          q: debouncedQuery.trim() || undefined,
          status: status || undefined,
          entryId: entryId ? Number(entryId) : undefined,
        },
        responseType: 'blob',
      })
      .then((res) => {
        downloadBlob(res.data as Blob, `الملفات التنفيذية ${todayLocalKey()}.xlsx`);
      })
      .catch(() => setExportMsg('تعذر تصدير الملف. حاول مرة أخرى'))
      .finally(() => setExporting(false));
  };

  const entries = list?.items ?? [];
  const totalCount = list?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className="max-w-6xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-2">الملفات التنفيذية</h2>
      {scope && (
        <p className="text-sm text-gray-500 mb-4">
          نطاقك:{' '}
          <span className="font-medium text-gray-700">{scope.canonicalName || 'غير مضبوط بعد'}</span>
          {' · '}
          {scope.entries?.length ?? 0} قيدًا نشطًا
        </p>
      )}

      <div className="bg-white rounded-xl shadow p-4 mb-4 flex flex-wrap items-center gap-3">
        <div className="grow min-w-[200px]">
          <label htmlFor="portal-search" className="sr-only">بحث في الملفات التنفيذية</label>
          <input
            id="portal-search"
            value={query}
            onChange={(e) => { setQuery(e.target.value); setPage(1); }}
            placeholder="بحث في الملفات…"
            autoComplete="off"
            name="q"
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          />
        </div>
        <div>
          <label htmlFor="portal-status" className="sr-only">فلتر الحالة</label>
          <select
            id="portal-status"
            value={status}
            onChange={(e) => { setStatus(e.target.value); setPage(1); }}
            className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            {STATUS_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </div>
        {showBranchSelect && (
          <PortalBranchSelect
            entries={scope?.entries ?? []}
            value={entryId}
            onChange={(next) => { setEntryId(next); setPage(1); }}
            id="portal-files-branch"
            label="فلتر الفرع"
          />
        )}
        <button
          onClick={exportExcel}
          disabled={exporting || loading}
          className="border border-sky-200 text-sky-800 hover:bg-sky-50 disabled:opacity-40 rounded-lg px-4 py-2 text-sm min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-500"
        >
          {exporting ? 'جارِ التصدير…' : 'تصدير إكسل'}
        </button>
      </div>

      {exportMsg && <p role="alert" className="text-red-600 text-sm mb-3">{exportMsg}</p>}
      {error && <div role="alert" className="text-red-600 mb-4">{error}</div>}
      {scopeQuery.error && <div role="alert" className="text-red-600 mb-4">{scopeQuery.error}</div>}

      <div className="bg-white rounded-xl shadow overflow-hidden">
        {/* قائمة قرائية: كل صف رابط للتفاصيل فقط، لا أزرار تعديل إطلاقًا.
            الفراغ يختبئ عند أي خطأ (قائمة أو نطاق) — «لا توجد ملفات في نطاق
            جهتك» زعم غير صحيح بعد فشل الجلب أو فشل تحميل النطاق نفسه. */}
        {!loading && !error && !scopeQuery.error && entries.length === 0 && (
          <div className="px-4 py-8 text-center text-gray-400">لا توجد ملفات مطابقة في نطاق جهتك</div>
        )}
        <ul className="divide-y divide-gray-100">
          {entries.map((f) => (
            <PortalFileCard key={f.id} file={f} canonicalName={scope?.canonicalName} />
          ))}
        </ul>

        {totalCount > PAGE_SIZE && (
          <nav aria-label="تصفح الملفات" className="flex items-center justify-between gap-2 px-4 py-3 border-t border-gray-100 text-sm">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1 || loading}
              className="border border-gray-300 rounded-lg px-3 py-2 min-h-11 disabled:opacity-40 hover:bg-gray-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
            >
              السابق
            </button>
            <span className="text-gray-500 tabular-nums">{page} من {totalPages} — {totalCount} ملفًا</span>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages || loading}
              className="border border-gray-300 rounded-lg px-3 py-2 min-h-11 disabled:opacity-40 hover:bg-gray-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
            >
              التالي
            </button>
          </nav>
        )}
      </div>

      <p className="mt-4 text-xs text-gray-400">
        هذه بوابة اطلاع قرائية: لا يمكنك تعديل الملفات أو حالتها أو توليد مستندات منها.
      </p>
    </div>
  );
}
