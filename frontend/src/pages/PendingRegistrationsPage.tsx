import { useCallback, useEffect, useMemo, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { normalizeArabicDigits } from '../utils/arabicDigits';
import type { PendingRegistrationDto } from '../types';

interface RowState {
  fileNumber: string;
  fileType: string;
  fileYear: string;
  error: string;
}

/**
 * صفحة معلقات المحامي مجموعةً بالدوائر (تُفتح من التنبيه — البند 19):
 * جدول الملفات + حقول الرقم/النوع/السنة + أخطاء inline + حفظ ذري.
 * المعالج يفرّق بصريًا: «المعروض حاليًا (قديم): X — أدخل الجديد».
 */
export default function PendingRegistrationsPage() {
  const [pending, setPending] = useState<PendingRegistrationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [rows, setRows] = useState<Record<number, RowState>>({});
  const [saving, setSaving] = useState(false);
  const [savedMessage, setSavedMessage] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const r = await api.get<PendingRegistrationDto[]>('/documents/my-pending-registrations');
      const items = r.data ?? [];
      setPending(items);
      setRows((prev) => {
        const next: Record<number, RowState> = {};
        for (const p of items) {
          next[p.documentId] = prev[p.documentId] ?? {
            fileNumber: '',
            fileType: p.oldFileType ?? '',
            fileYear: p.oldFileYear ?? '',
            error: '',
          };
        }
        return next;
      });
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const grouped = useMemo(() => {
    const map = new Map<string, PendingRegistrationDto[]>();
    for (const p of pending) {
      const key = p.circuitName ?? `دائرة ${p.circuitId}`;
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(p);
    }
    return [...map.entries()];
  }, [pending]);

  const setRow = (id: number, patch: Partial<RowState>) => {
    setRows((prev) => ({ ...prev, [id]: { ...prev[id], ...patch } }));
  };

  const save = async () => {
    setError('');
    setSavedMessage('');
    const entries = pending.map((p) => {
      const row = rows[p.documentId] ?? { fileNumber: '', fileType: '', fileYear: '', error: '' };
      return {
        documentId: p.documentId,
        fileNumber: normalizeArabicDigits(row.fileNumber).trim(),
        fileType: row.fileType.trim(),
        fileYear: normalizeArabicDigits(row.fileYear).trim(),
      };
    });
    // تحقق أمامي: الرقم الجديد إلزامي لكل ملف.
    let firstBad: number | null = null;
    const nextRows = { ...rows };
    for (const e of entries) {
      const problems: string[] = [];
      if (!e.fileNumber) problems.push('الرقم الجديد إلزامي');
      if (!e.fileYear) problems.push('السنة إلزامية');
      if (problems.length > 0 && firstBad === null) firstBad = e.documentId;
      nextRows[e.documentId] = { ...(nextRows[e.documentId] ?? { fileNumber: '', fileType: '', fileYear: '' }), error: problems.join(' — ') };
    }
    setRows(nextRows);
    if (firstBad !== null) {
      document.getElementById(`pending-number-${firstBad}`)?.focus();
      return;
    }
    setSaving(true);
    try {
      const r = await api.post<{ completedCount: number }>('/documents/complete-registrations', { entries });
      setSavedMessage(`حُفظ ${r.data.completedCount} ملفًا — خرجت من الانتظار`);
      await load();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <div className="flex items-center justify-center text-gray-500 py-16">جارِ تحميل المعلقات…</div>;
  }

  return (
    <div className="max-w-6xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-1 text-balance">ملفات محالة حديثًا</h2>
      <p className="text-sm text-gray-500 mb-6">بانتظار تحديث بياناتها في الدوائر المُحال إليها</p>
      {error && (
        <div className="bg-red-50 text-red-700 border border-red-200 rounded-lg p-3 mb-4" role="alert">
          {error}
        </div>
      )}
      {savedMessage && (
        <div className="bg-emerald-50 text-emerald-800 border border-emerald-200 rounded-lg p-3 mb-4" role="status">
          {savedMessage}
        </div>
      )}
      {pending.length === 0 ? (
        <p className="text-gray-500 py-8 text-center">لا توجد ملفات محالة حديثًا بانتظار تحديث بياناتها</p>
      ) : (
        <>
          {grouped.map(([circuitName, items]) => (
            <section key={circuitName} className="mb-6">
              <h3 className="text-lg font-bold text-gray-700 mb-2">دائرة: {circuitName}</h3>
              <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="bg-gray-50 text-gray-600">
                      <th className="px-3 py-2 text-start">الملف</th>
                      <th className="px-3 py-2 text-start">المعروض حاليًا (قديم)</th>
                      <th className="px-3 py-2 text-start">الرقم الجديد</th>
                      <th className="px-3 py-2 text-start">النوع</th>
                      <th className="px-3 py-2 text-start">السنة</th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((p) => {
                      const row = rows[p.documentId] ?? { fileNumber: '', fileType: '', fileYear: '', error: '' };
                      return (
                        <tr key={p.documentId} className="border-t border-gray-100">
                          <td className="px-3 py-2 tabular-nums">#{p.documentId} · {p.borrowerName ?? '—'}</td>
                          <td className="px-3 py-2 text-gray-500 tabular-nums">
                            {p.oldFileNumber ?? '—'} {p.oldFileType ?? ''} {p.oldFileYear ?? ''}
                          </td>
                          <td className="px-3 py-2">
                            <input
                              id={`pending-number-${p.documentId}`}
                              type="text"
                              inputMode="numeric"
                              value={row.fileNumber}
                              onChange={(e) => setRow(p.documentId, { fileNumber: e.target.value, error: '' })}
                              placeholder="أدخل الجديد…"
                              aria-label={`الرقم الجديد للملف ${p.documentId}`}
                              autoComplete="off"
                              className="w-28 min-h-11 border border-gray-300 rounded-lg px-2 py-1.5 text-sm tabular-nums focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                            />
                            {row.error && (
                              <p className="text-xs text-red-600 mt-1" role="alert">
                                {row.error}
                              </p>
                            )}
                          </td>
                          <td className="px-3 py-2">
                            <input
                              type="text"
                              value={row.fileType}
                              onChange={(e) => setRow(p.documentId, { fileType: e.target.value })}
                              aria-label={`نوع الملف ${p.documentId}`}
                              autoComplete="off"
                              className="w-24 min-h-11 border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                            />
                          </td>
                          <td className="px-3 py-2">
                            <input
                              type="text"
                              inputMode="numeric"
                              value={row.fileYear}
                              onChange={(e) => setRow(p.documentId, { fileYear: e.target.value })}
                              aria-label={`سنة الملف ${p.documentId}`}
                              autoComplete="off"
                              className="w-20 min-h-11 border border-gray-300 rounded-lg px-2 py-1.5 text-sm tabular-nums focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                            />
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </section>
          ))}
          <div className="flex justify-end">
            <button
              type="button"
              onClick={save}
              disabled={saving}
              className="bg-emerald-700 hover:bg-emerald-600 text-white rounded-lg px-5 py-2 text-sm min-h-11 disabled:opacity-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
            >
              {saving ? 'جارِ الحفظ…' : 'حفظ الكل (ذري)'}
            </button>
          </div>
        </>
      )}
    </div>
  );
}
