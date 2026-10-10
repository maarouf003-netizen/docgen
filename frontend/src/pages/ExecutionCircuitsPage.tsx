import { useCallback, useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useIsMobile } from '../hooks/useMediaQuery';
import type { ExecutionCircuitDto } from '../types';
import CircuitEmptyingWizard from '../components/circuit/CircuitEmptyingWizard';

/**
 * صفحة «إدارة دوائر التنفيذ» لرئيس القسم/الشعبة (نطاق المالك من الرمز):
 * زر «إدخال دائرة» + جدول (الاسم × الشعبة المالكة × عدد الملفات × شارة معطل × تعديل).
 * الجوال: بطاقات؛ المكتبي: جدول داخل overflow-x-auto.
 * (نقل الملكية بين القسم والشعب إجراء مدير/مشرف في «إدارة الفروع» — §8.3.)
 */
export default function ExecutionCircuitsPage() {
  const isMobile = useIsMobile();
  const [circuits, setCircuits] = useState<ExecutionCircuitDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [createName, setCreateName] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const [createBusy, setCreateBusy] = useState(false);
  const [editing, setEditing] = useState<ExecutionCircuitDto | null>(null);
  const [editName, setEditName] = useState('');
  const [editBusy, setEditBusy] = useState(false);
  const [emptying, setEmptying] = useState<ExecutionCircuitDto | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const r = await api.get<ExecutionCircuitDto[]>('/execution-circuits/mine');
      setCircuits(r.data ?? []);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const create = async () => {
    const name = createName.trim();
    if (!name) {
      setError('اسم الدائرة مطلوب');
      return;
    }
    setCreateBusy(true);
    try {
      await api.post('/execution-circuits', { name });
      setCreateName('');
      setCreateOpen(false);
      await load();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setCreateBusy(false);
    }
  };

  const saveRename = async () => {
    if (!editing) return;
    const name = editName.trim();
    if (!name) {
      setError('اسم الدائرة مطلوب');
      return;
    }
    setEditBusy(true);
    try {
      await api.put(`/execution-circuits/${editing.id}/rename`, { name, version: editing.version });
      setEditing(null);
      await load();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setEditBusy(false);
    }
  };

  const toggleActive = async (c: ExecutionCircuitDto) => {
    try {
      await api.put(`/execution-circuits/${c.id}/active`, { isActive: !c.isActive, version: c.version });
      await load();
    } catch (err) {
      setError(getApiErrorMessage(err));
    }
  };

  const remove = async (c: ExecutionCircuitDto) => {
    if (c.fileCount > 0) {
      setError('لا يمكن حذف الدائرة — أفرغها أولًا');
      return;
    }
    if (!window.confirm(`حذف دائرة «${c.name}» نهائيًا؟`)) return;
    try {
      await api.delete(`/execution-circuits/${c.id}`);
      await load();
    } catch (err) {
      setError(getApiErrorMessage(err));
    }
  };

  if (loading) {
    return <div className="flex items-center justify-center text-gray-500 py-16">جارِ تحميل الدوائر…</div>;
  }

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex flex-wrap items-center justify-between gap-2 mb-6">
        <h2 className="text-2xl font-bold text-gray-800 text-balance">إدارة دوائر التنفيذ</h2>
        <button
          type="button"
          onClick={() => setCreateOpen(true)}
          className="bg-emerald-700 hover:bg-emerald-600 text-white text-sm font-bold rounded-lg px-4 py-2 min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
        >
          ➕ إدخال دائرة
        </button>
      </div>

      {error && (
        <div className="bg-red-50 text-red-700 border border-red-200 rounded-lg p-3 mb-4" role="alert">
          {error}
        </div>
      )}

      {circuits.length === 0 ? (
        <p className="text-gray-500 py-8 text-center">لا توجد دوائر مسجلة بعد — أدخل أول دائرة لفرعك</p>
      ) : isMobile ? (
        <div className="grid grid-cols-1 gap-3">
          {circuits.map((c) => (
            <article key={c.id} className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
              <div className="flex items-center justify-between gap-2">
                <h3 className="font-bold text-gray-800 break-words min-w-0">{c.name}</h3>
                {!c.isActive && (
                  <span className="rounded-full bg-gray-100 text-gray-600 px-2 py-0.5 text-[11px] whitespace-nowrap">
                    معطلة
                  </span>
                )}
              </div>
              <p className="text-sm text-gray-600 mt-1 tabular-nums">
                {c.fileCount} ملفًا · {c.pendingCount} بانتظار إعادة القيد
              </p>
              <p className="text-xs text-gray-500 mt-1 break-words">
                {c.sectionName ? `شعبة ${c.sectionName}` : 'قسم الفرع'}
              </p>
              <div className="flex flex-wrap gap-2 mt-3">
                <button
                  type="button"
                  onClick={() => {
                    setEditing(c);
                    setEditName(c.name);
                  }}
                  aria-label={`تعديل ${c.name}`}
                  className="border border-gray-300 rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  تعديل
                </button>
                <button
                  type="button"
                  onClick={() => toggleActive(c)}
                  aria-label={`${c.isActive ? 'تعطيل' : 'تفعيل'} ${c.name}`}
                  className="border border-gray-300 rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  {c.isActive ? 'تعطيل' : 'تفعيل'}
                </button>
                {c.fileCount > 0 ? (
                  <button
                    type="button"
                    onClick={() => setEmptying(c)}
                    aria-label={`إفراغ ${c.name}`}
                    className="bg-amber-600 hover:bg-amber-500 text-white rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-amber-500"
                  >
                    إفراغ ونقل
                  </button>
                ) : (
                  <button
                    type="button"
                    onClick={() => remove(c)}
                    aria-label={`حذف ${c.name}`}
                    className="border border-red-200 text-red-700 rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-red-500"
                  >
                    حذف
                  </button>
                )}
              </div>
            </article>
          ))}
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-50 text-gray-600">
                <th className="px-4 py-3 text-start font-bold">الاسم</th>
                <th className="px-4 py-3 text-start font-bold">الشعبة</th>
                <th className="px-4 py-3 text-start font-bold tabular-nums">عدد الملفات</th>
                <th className="px-4 py-3 text-start font-bold">الحالة</th>
                <th className="px-4 py-3 text-start font-bold">إجراءات</th>
              </tr>
            </thead>
            <tbody>
              {circuits.map((c) => (
                <tr key={c.id} className="border-t border-gray-100">
                  <td className="px-4 py-3 font-medium text-gray-800 break-words">{c.name}</td>
                  <td className="px-4 py-3 break-words">{c.sectionName ?? 'القسم'}</td>
                  <td className="px-4 py-3 tabular-nums">
                    {c.fileCount}
                    {c.pendingCount > 0 && <span className="text-amber-700"> ({c.pendingCount} معلق)</span>}
                  </td>
                  <td className="px-4 py-3">
                    {c.isActive ? (
                      <span className="text-emerald-700">نشطة</span>
                    ) : (
                      <span className="rounded-full bg-gray-100 text-gray-600 px-2 py-0.5 text-[11px]">معطلة</span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap gap-2">
                      <button
                        type="button"
                        onClick={() => {
                          setEditing(c);
                          setEditName(c.name);
                        }}
                        aria-label={`تعديل ${c.name}`}
                        className="border border-gray-300 rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                      >
                        تعديل
                      </button>
                      <button
                        type="button"
                        onClick={() => toggleActive(c)}
                        aria-label={`${c.isActive ? 'تعطيل' : 'تفعيل'} ${c.name}`}
                        className="border border-gray-300 rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                      >
                        {c.isActive ? 'تعطيل' : 'تفعيل'}
                      </button>
                      {c.fileCount > 0 ? (
                        <button
                          type="button"
                          onClick={() => setEmptying(c)}
                          aria-label={`إفراغ ${c.name}`}
                          className="bg-amber-600 hover:bg-amber-500 text-white rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-amber-500"
                        >
                          إفراغ ونقل
                        </button>
                      ) : (
                        <button
                          type="button"
                          onClick={() => remove(c)}
                          aria-label={`حذف ${c.name}`}
                          className="border border-red-200 text-red-700 rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-red-500"
                        >
                          حذف
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {createOpen && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" dir="rtl" role="dialog" aria-modal="true" aria-label="إدخال دائرة">
          <div className="bg-white rounded-xl shadow-xl w-full max-w-md p-5 space-y-3">
            <h3 className="text-lg font-bold text-gray-800">إدخال دائرة جديدة</h3>
            <label htmlFor="new-circuit-name" className="block text-xs font-bold text-gray-600">
              اسم الدائرة
            </label>
            <input
              id="new-circuit-name"
              type="text"
              value={createName}
              onChange={(e) => setCreateName(e.target.value)}
              placeholder="مثال: دائرة التنفيذ الأولى…"
              autoComplete="off"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
            />
            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setCreateOpen(false)}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm min-h-11"
              >
                إلغاء
              </button>
              <button
                type="button"
                onClick={create}
                disabled={createBusy}
                className="bg-emerald-700 hover:bg-emerald-600 text-white rounded-lg px-4 py-2 text-sm min-h-11 disabled:opacity-50"
              >
                {createBusy ? 'جارِ الحفظ…' : 'إدخال'}
              </button>
            </div>
          </div>
        </div>
      )}

      {editing && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" dir="rtl" role="dialog" aria-modal="true" aria-label={`تعديل ${editing.name}`}>
          <div className="bg-white rounded-xl shadow-xl w-full max-w-md p-5 space-y-3">
            <h3 className="text-lg font-bold text-gray-800">تسمية الدائرة (تحديث جماعي فوري)</h3>
            <p className="text-xs text-gray-500">إعادة التسمية تُحدّث كل ملفات الدائرة وإناباتها المستهدِفة في نفس العملية.</p>
            <label htmlFor="edit-circuit-name" className="block text-xs font-bold text-gray-600">
              الاسم الجديد
            </label>
            <input
              id="edit-circuit-name"
              type="text"
              value={editName}
              onChange={(e) => setEditName(e.target.value)}
              autoComplete="off"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
            />
            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setEditing(null)}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm min-h-11"
              >
                إلغاء
              </button>
              <button
                type="button"
                onClick={saveRename}
                disabled={editBusy}
                className="bg-emerald-700 hover:bg-emerald-600 text-white rounded-lg px-4 py-2 text-sm min-h-11 disabled:opacity-50"
              >
                {editBusy ? 'جارِ الحفظ…' : 'حفظ التسمية'}
              </button>
            </div>
          </div>
        </div>
      )}

      {emptying && (
        <CircuitEmptyingWizard
          source={emptying}
          circuits={circuits}
          onDone={() => {
            setEmptying(null);
            load();
          }}
          onCancel={() => {
            setEmptying(null);
            load();
          }}
        />
      )}
    </div>
  );
}
