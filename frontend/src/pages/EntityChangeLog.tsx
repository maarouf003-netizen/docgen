import { useMemo, useState } from 'react';
import { api } from '../api/client';
import { formatDateTime } from '../utils/dates';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import type { EntityChangeEventDto } from '../types';

interface Paged<T> {
  items: T[];
  page: number;
  perPage: number;
  totalCount: number;
}

const ACTION_LABELS: Record<string, string> = {
  create: 'إنشاء',
  rename: 'إعادة تسمية',
  move: 'نقل',
  merge: 'دمج',
  abolish: 'إلغاء',
  review: 'مراجعة',
  import: 'استيراد',
};

export default function EntityChangeLog() {
  const [page, setPage] = useState(1);
  const [governorate, setGovernorate] = useState('');
  const [actionKind, setActionKind] = useState('');
  const [actorUserId, setActorUserId] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [exportError, setExportError] = useState('');
  const perPage = 20;

  const debouncedGovernorate = useDebouncedValue(governorate, 300);
  const debouncedActorUserId = useDebouncedValue(actorUserId, 300);

  const query = useCancellableRequest<Paged<EntityChangeEventDto>>((signal) => {
    const params = new URLSearchParams({ page: String(page), perPage: String(perPage) });
    if (debouncedGovernorate) params.set('governorate', debouncedGovernorate);
    if (actionKind) params.set('actionKind', actionKind);
    if (debouncedActorUserId) params.set('actorUserId', debouncedActorUserId);
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    return api.get(`/entity-registry/change-events?${params.toString()}`, { signal }).then((r) => r.data);
  }, [page, debouncedGovernorate, actionKind, debouncedActorUserId, from, to]);

  const rows = useMemo(() => query.data?.items ?? [], [query.data]);
  const total = query.data?.totalCount ?? 0;
  const error = query.error ?? '';
  const pages = Math.max(1, Math.ceil(total / perPage));

  const exportExcel = () => {
    setExportError('');
    const params = new URLSearchParams();
    if (debouncedGovernorate) params.set('governorate', debouncedGovernorate);
    if (actionKind) params.set('actionKind', actionKind);
    if (debouncedActorUserId) params.set('actorUserId', debouncedActorUserId);
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    api.get(`/entity-registry/change-events/export?${params.toString()}`, { responseType: 'blob' }).then((res) => {
      const url = window.URL.createObjectURL(new Blob([res.data]));
      const a = document.createElement('a');
      a.href = url;
      a.download = 'change-events.xlsx';
      a.click();
      window.URL.revokeObjectURL(url);
    }).catch(() => setExportError('فشل التصدير — حاول مرة أخرى'));
  };

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex flex-wrap items-center justify-between gap-3 mb-6">
        <h2 className="text-2xl font-bold text-gray-800 text-wrap-balance">سجل تغييرات الجهات</h2>
        <button type="button" onClick={exportExcel} aria-label="تصدير سجل التغييرات إلى Excel" className="bg-emerald-800 hover:bg-emerald-700 text-white text-sm font-bold rounded-lg px-4 py-2 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2">تصدير Excel</button>
      </div>

      <div className="bg-white rounded-xl shadow p-4 mb-6 flex flex-wrap gap-3 items-end">
        <div className="flex flex-col min-w-36">
          <label htmlFor="chg-governorate" className="text-sm text-gray-600 mb-1">المحافظة</label>
          <input id="chg-governorate" name="governorate" autoComplete="address-level1" value={governorate} onChange={(e) => { setGovernorate(e.target.value); setPage(1); }} placeholder="مثال: دمشق…" className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500" />
        </div>
        <div className="flex flex-col min-w-36">
          <label htmlFor="chg-actionKind" className="text-sm text-gray-600 mb-1">نوع الحدث</label>
          <select id="chg-actionKind" name="actionKind" autoComplete="off" value={actionKind} onChange={(e) => { setActionKind(e.target.value); setPage(1); }} className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500">
            <option value="">الكل</option>
            {Object.entries(ACTION_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </div>
        <div className="flex flex-col min-w-28">
          <label htmlFor="chg-actor" className="text-sm text-gray-600 mb-1">المستخدم</label>
          <input id="chg-actor" name="actorUserId" autoComplete="off" value={actorUserId} onChange={(e) => { setActorUserId(e.target.value); setPage(1); }} placeholder="معرّف المستخدم…" inputMode="numeric" className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500" />
        </div>
        <div className="flex flex-col min-w-36">
          <label htmlFor="chg-from" className="text-sm text-gray-600 mb-1">من تاريخ</label>
          <input id="chg-from" name="from" autoComplete="off" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1); }} placeholder="مثال: 1/8/2026…" className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500" />
        </div>
        <div className="flex flex-col min-w-36">
          <label htmlFor="chg-to" className="text-sm text-gray-600 mb-1">إلى تاريخ</label>
          <input id="chg-to" name="to" autoComplete="off" value={to} onChange={(e) => { setTo(e.target.value); setPage(1); }} placeholder="مثال: 1/8/2026…" className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500" />
        </div>
      </div>

      {error && <div className="text-red-600 mb-4">{error}</div>}
      {exportError && <div className="text-red-600 mb-4">{exportError}</div>}

      {/* Desktop table */}
      <div className="hidden md:block bg-white rounded-xl shadow overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 text-gray-600">
            <tr className="text-right">
              <th className="px-4 py-3">الوقت</th>
              <th className="px-4 py-3">الفاعل</th>
              <th className="px-4 py-3">النوع</th>
              <th className="px-4 py-3">الجهة</th>
              <th className="px-4 py-3">المحافظة</th>
              <th className="px-4 py-3">المرسوم</th>
              <th className="px-4 py-3">التفاصيل</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {rows.map((r) => (
              <tr key={r.id} className="hover:bg-gray-50">
                <td className="px-4 py-3 whitespace-nowrap tabular-nums">{formatDateTime(r.createdAtUtc)}</td>
                <td className="px-4 py-3 min-w-0 break-words">{r.actorName ?? `#${r.actorUserId}`}</td>
                <td className="px-4 py-3">{ACTION_LABELS[r.actionKind] ?? r.actionKind}</td>
                <td className="px-4 py-3 min-w-0 break-words">{r.canonicalName ?? '-'}</td>
                <td className="px-4 py-3">{r.governorate ?? '-'}</td>
                <td className="px-4 py-3 tabular-nums">{[r.decreeKind, r.decreeNumber, r.decreeDate].filter(Boolean).join(' ') || '-'}</td>
                <td className="px-4 py-3 max-w-xs truncate" title={r.payloadJson}>{r.payloadJson.slice(0, 120)}</td>
              </tr>
            ))}
            {rows.length === 0 && <tr><td colSpan={7} className="px-4 py-8 text-center text-gray-400">لا توجد سجلات</td></tr>}
          </tbody>
        </table>
      </div>

      {/* Mobile cards */}
      <div className="md:hidden space-y-3">
        {rows.map((r) => (
          <div key={r.id} className="bg-white rounded-xl shadow p-4">
            <div className="flex flex-wrap justify-between gap-2 text-sm">
              <span className="font-bold text-gray-800">{r.canonicalName ?? '—'}</span>
              <span className="text-emerald-700 bg-emerald-50 rounded-full px-2 py-0.5">{ACTION_LABELS[r.actionKind] ?? r.actionKind}</span>
            </div>
            <div className="mt-2 text-sm text-gray-600 space-y-1 break-words">
              <div>الفاعل: {r.actorName ?? `#${r.actorUserId}`} — {formatDateTime(r.createdAtUtc)}</div>
              <div>المحافظة: {r.governorate ?? '-'}</div>
              <div>المرسوم: {[r.decreeKind, r.decreeNumber, r.decreeDate].filter(Boolean).join(' ') || '-'}</div>
              <div className="line-clamp-3 break-words">{r.payloadJson}</div>
            </div>
          </div>
        ))}
        {rows.length === 0 && <div className="bg-white rounded-xl shadow p-8 text-center text-gray-400">لا توجد سجلات</div>}
      </div>

      <div className="flex items-center justify-between mt-4 text-sm text-gray-600">
        <span>إجمالي السجلات: {total}</span>
        <div className="flex gap-2">
          <button type="button" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="border border-gray-300 rounded-lg px-3 py-1.5 disabled:opacity-40 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500">السابق</button>
          <span className="px-3 py-1.5 tabular-nums">صفحة {page} من {pages}</span>
          <button type="button" onClick={() => setPage((p) => Math.min(pages, p + 1))} disabled={page >= pages} className="border border-gray-300 rounded-lg px-3 py-1.5 disabled:opacity-40 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500">التالي</button>
        </div>
      </div>
    </div>
  );
}
