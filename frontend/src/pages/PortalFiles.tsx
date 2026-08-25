import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import type {
  PortalFileListItemDto,
  PortalFilesResponse,
  PortalScopeDto,
  PortalStatsDto,
} from '../types';

const PAGE_SIZE = 20;

const STATUS_OPTIONS: ReadonlyArray<{ value: string; label: string }> = [
  { value: '', label: 'كل الحالات' },
  { value: 'متداول', label: 'متداول' },
  { value: 'منفذ', label: 'منفذ' },
  { value: 'تريث', label: 'تريث' },
  { value: 'تحت رفع', label: 'تحت رفع' },
];

const AR_MONTHS = ['ك2', 'شباط', 'آذار', 'نيسان', 'أيار', 'حزيران', 'تموز', 'آب', 'أيلول', 'ت1', 'ت2', 'كانون الأول'];

function monthLabel(year: number, month: number): string {
  return `${AR_MONTHS[month - 1] ?? month} ${year}`;
}

/** صفحة «ملفات الجهة» — البوابة القرائية لمندوب الجهة العامة (المراحل 3+4). */
export default function PortalFiles() {
  const scopeQuery = useCancellableRequest<PortalScopeDto>(
    (signal) => api.get('/portal/my-scope', { signal }).then((r) => r.data),
    [],
  );
  const scope = scopeQuery.data;

  const statsQuery = useCancellableRequest<PortalStatsDto>(
    (signal) => api.get('/portal/stats', { signal }).then((r) => r.data),
    [],
  );
  const stats = statsQuery.data;

  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const [list, setList] = useState<PortalFilesResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [exporting, setExporting] = useState(false);
  const [exportMsg, setExportMsg] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const res = await api.get<PortalFilesResponse>('/portal/files', {
        params: {
          q: query.trim() || undefined,
          status: status || undefined,
          page,
          perPage: PAGE_SIZE,
        },
      });
      setList(res.data);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [query, status, page]);

  useEffect(() => {
    void load();
  }, [load]);

  const exportExcel = () => {
    setExportMsg('');
    setExporting(true);
    // عبر مثيل axios المشترك: يضيف CSRF تلقائيًا ويعيد التوجيه عند انتهاء الجلسة.
    api
      .get('/portal/export', { params: { q: query.trim() || undefined, status: status || undefined }, responseType: 'blob' })
      .then((res) => {
        const blob = res.data as Blob;
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = `ملفات الجهة ${new Date().toISOString().slice(0, 10)}.xlsx`;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(link.href);
      })
      .catch(() => setExportMsg('تعذر تصدير الملف. حاول مرة أخرى'))
      .finally(() => setExporting(false));
  };

  const entries = list?.items ?? [];
  const totalCount = list?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className="max-w-6xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-2">ملفات الجهة</h2>
      {scope && (
        <p className="text-sm text-gray-500 mb-4">
          نطاقك:{' '}
          <span className="font-medium text-gray-700">{scope.canonicalName || 'غير مضبوط بعد'}</span>
          {' · '}
          {scope.entries.length} قيدًا نشطًا
          {scope.entries.length > 0 && (
            <>
              {' ('}
              {scope.entries.map((e) => `${e.governorate}/${e.branchName}`).join(' · ')}
              {')'}
            </>
          )}
        </p>
      )}

      <div className="bg-white rounded-xl shadow p-4 mb-4 flex flex-wrap items-center gap-3">
        <div className="grow min-w-[200px]">
          <label htmlFor="portal-search" className="sr-only">بحث في ملفات الجهة</label>
          <input
            id="portal-search"
            value={query}
            onChange={(e) => { setQuery(e.target.value); setPage(1); }}
            placeholder="بحث في الملفات…"
            autoComplete="off"
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>
        <div>
          <label htmlFor="portal-status" className="sr-only">فلتر الحالة</label>
          <select
            id="portal-status"
            value={status}
            onChange={(e) => { setStatus(e.target.value); setPage(1); }}
            className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
            {STATUS_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </div>
        <button
          onClick={exportExcel}
          disabled={exporting || loading}
          className="border border-sky-200 text-sky-800 hover:bg-sky-50 disabled:opacity-40 rounded-lg px-4 py-2 text-sm min-h-11"
        >
          {exporting ? 'جارِ التصدير…' : 'تصدير إكسل'}
        </button>
      </div>

      {exportMsg && <p role="alert" className="text-red-600 text-sm mb-3">{exportMsg}</p>}
      {error && <div role="alert" className="text-red-600 mb-4">{error}</div>}

      {/* بطاقة إحصاءات الجهة (المرحلة 4) — قرائية بالكامل */}
      {stats && (
        <section aria-labelledby="portal-stats-title" className="bg-white rounded-xl shadow p-4 sm:p-5 mb-4">
          <h3 id="portal-stats-title" className="font-bold text-gray-800 mb-3">إحصاءات نطاق جهتك</h3>

          <dl className="grid grid-cols-2 sm:grid-cols-5 gap-2 text-center">
            {([
              ['الإجمالي', stats.totalFiles, 'bg-emerald-800 text-white'],
              ['متداول', stats.circulatingFiles, 'bg-emerald-50 text-emerald-900'],
              ['منفذ', stats.executedFiles, 'bg-sky-50 text-sky-900'],
              ['تريث', stats.deferredFiles, 'bg-amber-50 text-amber-900'],
              ['تحت رفع', stats.draftFiles, 'bg-gray-100 text-gray-700'],
            ] as const).map(([label, value, cls]) => (
              <div key={label} className={`rounded-lg px-2 py-3 ${cls}`}>
                <dt className="text-[11px] opacity-80">{label}</dt>
                <dd className="text-xl font-bold tabular-nums">{value}</dd>
              </div>
            ))}
          </dl>

          <p className="mt-3 text-xs text-gray-500 tabular-nums">
            الاستئنافات: {stats.pendingAppeals} معلّقًا · {stats.closedAppeals} مغلقًا
          </p>

          {/* سلسلة آخر 12 شهرًا */}
          <h4 className="text-xs font-bold text-gray-600 mt-4 mb-1">الملفات الواردة — آخر 12 شهرًا</h4>
          <div className="flex items-end gap-1 h-20" role="img"
               aria-label={`ملفات آخر 12 شهرًا، الإجمالي ${stats.monthly.reduce((s, m) => s + m.files, 0)}`}>
            {(() => {
              const max = Math.max(1, ...stats.monthly.map((m) => m.files));
              return stats.monthly.map((m) => (
                <div key={`${m.year}-${m.month}`} className="flex-1 flex flex-col items-center justify-end gap-0.5 min-w-0"
                     title={`${monthLabel(m.year, m.month)}: ${m.files}`}>
                  <span className="text-[9px] text-gray-400 tabular-nums">{m.files || ''}</span>
                  <div
                    className="w-full bg-emerald-600/80 rounded-t"
                    style={{ height: `${Math.max(2, Math.round((m.files / max) * 56))}px` }}
                    aria-hidden="true"
                  />
                  <span className="text-[8px] text-gray-400 truncate w-full text-center">{AR_MONTHS[m.month - 1] ?? m.month}</span>
                </div>
              ));
            })()}
          </div>

          <div className="grid sm:grid-cols-2 gap-5 mt-5">
            {/* توزيع القيود */}
            <div>
              <h4 className="text-xs font-bold text-gray-600 mb-1">توزيع الارتباط على القيود</h4>
              {(() => {
                const max = Math.max(1, ...stats.perEntry.map((e) => e.files));
                return (
                  <ul className="space-y-1.5">
                    {stats.perEntry.slice(0, 6).map((e) => (
                      <li key={e.entryId} className="text-xs">
                        <div className="flex justify-between gap-2 mb-0.5">
                          <span className="truncate text-gray-700">{e.governorate}/{e.branchName}</span>
                          <span className="tabular-nums text-gray-500 shrink-0">{e.files}</span>
                        </div>
                        <div className="h-1.5 bg-gray-100 rounded-full overflow-hidden">
                          <div className="h-full bg-emerald-600/70" style={{ width: `${(e.files / max) * 100}%` }} aria-hidden="true" />
                        </div>
                      </li>
                    ))}
                  </ul>
                );
              })()}
            </div>

            {/* العملات الأعلى */}
            <div>
              <h4 className="text-xs font-bold text-gray-600 mb-1">أعلى العملات</h4>
              {stats.topCurrencies.length === 0 ? (
                <p className="text-xs text-gray-400">لا توجد مبالغ مسجلة</p>
              ) : (
                <ul className="divide-y divide-gray-100">
                  {stats.topCurrencies.map((c) => (
                    <li key={c.currency} className="py-1.5 flex items-center justify-between gap-3 text-sm">
                      <span className="truncate text-gray-700">{c.currency}</span>
                      <span className="shrink-0 tabular-nums text-gray-600">
                        {c.files} ملفًا · {c.totalAmount.toLocaleString('ar-SY')}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>

          {scope?.scopeType === 'group' && (
            <p className="mt-3 text-[11px] text-gray-400">
              ملاحظة: الملف المرتبط بأكثر من قيد ضمن النطاق يُحتسب تحت كل قيد ارتبط به.
            </p>
          )}
        </section>
      )}

      <div className="bg-white rounded-xl shadow overflow-hidden">
        {/* قائمة قرائية: كل صف رابط للتفاصيل فقط، لا أزرار تعديل إطلاقًا */}
        {!loading && entries.length === 0 && (
          <div className="px-4 py-8 text-center text-gray-400">لا توجد ملفات مطابقة في نطاق جهتك</div>
        )}
        <ul className="divide-y divide-gray-100">
          {entries.map((f: PortalFileListItemDto) => (
            <li key={f.id}>
              <Link
                to={`/portal/files/${f.id}`}
                className="block px-4 py-3 hover:bg-emerald-50/60 min-h-11"
              >
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <span className="min-w-0">
                    <span className="block font-medium text-gray-800 break-words">
                      {f.borrowerName || f.documentType}
                    </span>
                    <span className="block text-xs text-gray-500 mt-0.5 truncate">
                      {f.applicant && <>{f.applicant} · </>}
                      {f.executedEntitiesSummary}
                    </span>
                  </span>
                  <span className="shrink-0 text-xs tabular-nums text-gray-600">
                    {(f.amountNumeric ?? 0).toLocaleString('ar-SY')} {f.currency}
                  </span>
                </div>
                <div className="mt-1 flex flex-wrap gap-2 text-xs">
                  <span className={`rounded-full px-2 py-0.5 ${f.isDraft ? 'bg-amber-100 text-amber-800' : 'bg-emerald-100 text-emerald-800'}`}>
                    {f.execStatus || (f.isDraft ? 'تحت رفع' : 'متداول')}
                  </span>
                  <span className="text-gray-400">{f.documentType}</span>
                </div>
              </Link>
            </li>
          ))}
        </ul>

        {totalCount > PAGE_SIZE && (
          <nav aria-label="تصفح الملفات" className="flex items-center justify-between gap-2 px-4 py-3 border-t border-gray-100 text-sm">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1 || loading}
              className="border border-gray-300 rounded-lg px-3 py-2 min-h-11 disabled:opacity-40 hover:bg-gray-50"
            >
              السابق
            </button>
            <span className="text-gray-500 tabular-nums">{page} من {totalPages} — {totalCount} ملفًا</span>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages || loading}
              className="border border-gray-300 rounded-lg px-3 py-2 min-h-11 disabled:opacity-40 hover:bg-gray-50"
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
