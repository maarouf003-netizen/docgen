import { useCallback, useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import type { BranchDto, CircuitStatsDto } from '../types';

/**
 * صفحة «إحصائيات الدوائر» للمدير/المشرف (منتقي فرع + جدول الدوائر)
 * ورئيس القسم (فرعه فقط — بلا منتقي إذ يُتجاهل اختياره خادميًا).
 * لكل دائرة: ملفات × محامون نشطون × معلقات.
 */
export default function CircuitStatsPage() {
  const { user } = useAuth();
  const isHead = user?.role === 'head';
  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [branchId, setBranchId] = useState<number | ''>('');
  const [rows, setRows] = useState<CircuitStatsDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    if (isHead) {
      setBranches([]);
      return;
    }
    let cancelled = false;
    (async () => {
      try {
        const r = await api.get<BranchDto[]>('/branches');
        if (!cancelled) setBranches(Array.isArray(r?.data) ? r.data : []);
      } catch {
        if (!cancelled) setBranches([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [isHead]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const r = await api.get<CircuitStatsDto[]>('/execution-circuits/stats', {
        params: branchId === '' ? {} : { branchId },
      });
      setRows(r.data ?? []);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [branchId]);

  useEffect(() => {
    load();
  }, [load]);

  const fmt = (n: number) => new Intl.NumberFormat('ar-SY').format(n);

  return (
    <div className="max-w-6xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-6 text-balance">إحصائيات الدوائر</h2>
      {!isHead && (
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <div>
          <label htmlFor="stats-branch" className="block text-xs font-bold text-gray-600 mb-1">
            الفرع
          </label>
          <select
            id="stats-branch"
            value={branchId}
            onChange={(e) => setBranchId(e.target.value === '' ? '' : Number(e.target.value))}
            className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            <option value="">كل الفروع</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </div>
      </div>
      )}
      {isHead && (
        <p className="text-sm text-gray-500 mb-4">إحصائيات دوائر فرعك</p>
      )}
      {error && (
        <div className="bg-red-50 text-red-700 border border-red-200 rounded-lg p-3 mb-4" role="alert">
          {error}
        </div>
      )}
      {loading ? (
        <p className="text-gray-500 py-8 text-center">جارِ التحميل…</p>
      ) : rows.length === 0 ? (
        <p className="text-gray-500 py-8 text-center">لا توجد دوائر</p>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-50 text-gray-600">
                <th className="px-4 py-3 text-start font-bold">الدائرة</th>
                <th className="px-4 py-3 text-start font-bold">الفرع</th>
                <th className="px-4 py-3 text-start font-bold tabular-nums">الملفات</th>
                <th className="px-4 py-3 text-start font-bold tabular-nums">المحامون النشطون</th>
                <th className="px-4 py-3 text-start font-bold tabular-nums">المعلقات</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.circuitId} className="border-t border-gray-100">
                  <td className="px-4 py-3 font-medium text-gray-800 break-words">
                    {r.circuitName}
                    {!r.isActive && <span className="ms-2 text-xs text-gray-500">(معطلة)</span>}
                  </td>
                  <td className="px-4 py-3">{r.branchName ?? '—'}</td>
                  <td className="px-4 py-3 tabular-nums">{fmt(r.fileCount)}</td>
                  <td className="px-4 py-3 tabular-nums">{fmt(r.lawyerCount)}</td>
                  <td className="px-4 py-3 tabular-nums">{fmt(r.pendingCount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
