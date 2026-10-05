import { useEffect, useMemo, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { useFocusTrap } from '../../hooks/useFocusTrap';
import { idempotencyHeaders, newIdempotencyKey } from '../../utils/idempotency';
import type { DocumentResponse, ExecutionCircuitDto } from '../../types';

interface CircuitFileRow {
  id: number;
  borrowerName?: string | null;
  fileNumber?: string | null;
  fileType?: string | null;
  fileYear?: string | null;
  lawyer?: string | null;
}

/**
 * معالج إفراغ الدائرة (التدفق المركزي): اختيار الهدف الثابت (نفس الفرع، نشطة، غير الذات)
 * ← إحالة الدفعات بمربعات الاختيار + شريط تقدم المتبقي + «إلغاء الإفراغ» يعيد التفعيل.
 * البانر الحرفي في شاشة التأكيد النهائية.
 */
export default function CircuitEmptyingWizard({
  source,
  circuits,
  onDone,
  onCancel,
}: {
  source: ExecutionCircuitDto;
  circuits: ExecutionCircuitDto[];
  onDone: () => void;
  onCancel: () => void;
}) {
  const [targetId, setTargetId] = useState<number | ''>('');
  const [files, setFiles] = useState<CircuitFileRow[]>([]);
  const [totalCount, setTotalCount] = useState<number | null>(null);
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [targetLawyerId, setTargetLawyerId] = useState<number | ''>('');
  const [lawyers, setLawyers] = useState<Array<{ id: number; fullName: string; isActive?: boolean }>>([]);
  const [step, setStep] = useState<1 | 2>(1);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState<{ referredCount: number; skippedCount: number; remainingCount: number } | null>(null);
  const [idempotencyKey] = useState(newIdempotencyKey);
  // S1: الحذف فعل خطر غير قابل للتراجع — يتطلب تسليح تأكيد صريح بخطوة ثانية.
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const dialogRef = useFocusTrap<HTMLDivElement>();

  const targets = useMemo(
    () => circuits.filter((c) => c.id !== source.id && c.isActive),
    [circuits, source.id],
  );

  useEffect(() => {
    let cancelled = false;
    // ملفات الدائرة الملغاة: Court مُزامن نصًا فيُفلتر به (العقد ثابت).
    api
      .get<{ items?: DocumentResponse[] } | DocumentResponse[]>('/documents', {
        params: { court: source.name, perPage: 100 },
      })
      .then((r) => {
        if (cancelled) return;
        const raw = r.data as { items?: DocumentResponse[] } | DocumentResponse[];
        const items = Array.isArray(raw) ? raw : (raw.items ?? []);
        setFiles(
          items.map((d) => ({
            id: d.id,
            borrowerName: d.borrowerName,
            fileNumber: d.fileNumber,
            fileType: d.fileType,
            fileYear: d.fileYear,
            lawyer: d.lawyer,
          })),
        );
        setTotalCount(items.length);
      })
      .catch(() => {
        if (!cancelled) setError('تعذّر تحميل ملفات الدائرة');
      });
    api
      .get<Array<{ id: number; fullName: string; isActive?: boolean }>>('/users/lawyers')
      .then((r) => {
        if (!cancelled) setLawyers(r.data ?? []);
      })
      .catch(() => {
        if (!cancelled) setLawyers([]);
      });
    return () => {
      cancelled = true;
    };
  }, [source.name]);

  // S6.e: `Escape` يسلّح التراجع أولًا (إلغاء تسليح الحذف)، ثم يغلق المعالج.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'Escape') return;
      if (confirmingDelete) setConfirmingDelete(false);
      else onCancel();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [confirmingDelete, onCancel]);

  const toggle = (id: number) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const toggleAll = () => {
    setSelected((prev) => (prev.size === files.length ? new Set() : new Set(files.map((f) => f.id))));
  };

  const submit = async () => {
    if (targetId === '' || targetLawyerId === '' || selected.size === 0) {
      setError('اختر الدائرة الهدف والمحامي وملفًا واحدًا على الأقل');
      return;
    }
    setBusy(true);
    setError('');
    try {
      const r = await api.post(
        `/execution-circuits/${source.id}/refer-files`,
        {
          fileIds: [...selected],
          targetLawyerId: Number(targetLawyerId),
          targetCircuitId: Number(targetId),
        },
        idempotencyHeaders(idempotencyKey),
      );
      const data = r.data as { referredCount: number; skippedCount: number; remainingCount: number };
      setResult(data);
      setSelected(new Set());
      // تُعاد القائمة بالمتبقي حتى الصفر.
      const remaining = await api.get<{ items?: DocumentResponse[] } | DocumentResponse[]>('/documents', {
        params: { court: source.name, perPage: 100 },
      });
      const raw = remaining.data as { items?: DocumentResponse[] } | DocumentResponse[];
      const items = Array.isArray(raw) ? raw : (raw.items ?? []);
      setFiles(
        items.map((d) => ({
          id: d.id,
          borrowerName: d.borrowerName,
          fileNumber: d.fileNumber,
          fileType: d.fileType,
          fileYear: d.fileYear,
          lawyer: d.lawyer,
        })),
      );
      setTotalCount(items.length);
      if (data.remainingCount === 0) setStep(2);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const cancelEmptying = async () => {
    // «إلغاء الإفراغ» يعيد تفعيل الدائرة الملغاة صراحة.
    try {
      await api.put(`/execution-circuits/${source.id}/active`, { isActive: true });
    } catch (err) {
      setError(getApiErrorMessage(err));
      return;
    }
    onCancel();
  };

  const remove = async () => {
    if (deleting) return;
    setDeleting(true);
    setError('');
    try {
      await api.delete(`/execution-circuits/${source.id}`);
      onDone();
    } catch (err) {
      setError(getApiErrorMessage(err));
      setConfirmingDelete(false);
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div
      ref={dialogRef}
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={`إفراغ ${source.name}`}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-2xl max-h-[90vh] overflow-y-auto overscroll-contain p-5 space-y-4">
        <h3 className="text-lg font-bold text-gray-800">إفراغ دائرة «{source.name}»</h3>
        {error && (
          <p className="text-sm text-red-600" role="alert">
            {error}
          </p>
        )}
        {step === 1 && (
          <>
            <div className="grid sm:grid-cols-2 gap-3">
              <div>
                <label htmlFor="empty-target" className="block text-xs font-bold text-gray-600 mb-1">
                  الدائرة الهدف (ثابتة لكل العملية — نفس الفرع)
                </label>
                {targets.length === 0 ? (
                  <p className="text-sm text-amber-700">أنشئ دائرة بديلة أولًا</p>
                ) : (
                  <select
                    id="empty-target"
                    value={targetId}
                    onChange={(e) => setTargetId(e.target.value === '' ? '' : Number(e.target.value))}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  >
                    <option value="">اختر الدائرة الهدف…</option>
                    {targets.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                )}
              </div>
              <div>
                <label htmlFor="empty-lawyer" className="block text-xs font-bold text-gray-600 mb-1">
                  المحامي الهدف (نفس الفرع)
                </label>
                <select
                  id="empty-lawyer"
                  value={targetLawyerId}
                  onChange={(e) => setTargetLawyerId(e.target.value === '' ? '' : Number(e.target.value))}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  <option value="">اختر المحامي…</option>
                  {/* المحامي المعطل مرفوض خادميًا — يُرشَّح هنا فلا يظهر أصلًا. */}
                  {lawyers
                    .filter((l) => l.isActive !== false)
                    .map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.fullName}
                      </option>
                    ))}
                </select>
              </div>
            </div>

            <div className="rounded-lg bg-gray-50 border border-gray-200 px-3 py-2 text-sm text-gray-700 tabular-nums">
              المتبقي في الدائرة الملغاة: {totalCount ?? '…'} ملفًا
              {files.length > 0 && (
                <button type="button" onClick={toggleAll} className="ms-3 text-emerald-800 hover:underline min-h-11">
                  {selected.size === files.length ? 'إلغاء تحديد الكل' : 'تحديد الكل'}
                </button>
              )}
            </div>

            <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200 max-h-64 overflow-y-auto overscroll-contain">
              {files.map((f) => (
                <li key={f.id}>
                  <label className="flex items-center gap-2 px-3 py-2 min-h-11 cursor-pointer hover:bg-gray-50">
                    <input
                      type="checkbox"
                      checked={selected.has(f.id)}
                      onChange={() => toggle(f.id)}
                      className="h-5 w-5 rounded border-gray-300 text-emerald-700"
                      aria-label={`إحالة ملف ${f.id} — ${f.borrowerName ?? '—'} — ${[f.fileNumber, f.fileType, f.fileYear].filter(Boolean).join(' ') || 'بلا رقم'}`}
                    />
                    <span className="text-sm text-gray-800 tabular-nums">
                      #{f.id} · {f.borrowerName ?? '—'} · {f.fileNumber ?? '—'} {f.fileType ?? ''} {f.fileYear ?? ''}
                    </span>
                  </label>
                </li>
              ))}
              {files.length === 0 && <li className="px-3 py-6 text-center text-sm text-gray-500">لا توجد ملفات متبقية</li>}
            </ul>

            <div className="flex flex-wrap justify-end gap-2">
              <button
                type="button"
                onClick={cancelEmptying}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm min-h-11"
              >
                إلغاء الإفراغ (إعادة تفعيل)
              </button>
              <button
                type="button"
                onClick={submit}
                disabled={busy || selected.size === 0}
                className="bg-amber-600 hover:bg-amber-500 text-white rounded-lg px-4 py-2 text-sm min-h-11 disabled:opacity-50"
              >
                {busy ? 'جارِ الإحالة…' : `إحالة (${selected.size})`}
              </button>
            </div>
          </>
        )}

        {step === 2 || result?.remainingCount === 0 ? (
          <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 space-y-2">
            <p className="text-sm text-emerald-900 font-bold">لا تنسى نقل ملفات هذه الدائرة لمحامي أو محامين اخرين ان كان لذلك مقتضى</p>
            <p className="text-sm text-gray-700">
              أُحيل {result?.referredCount ?? 0} ملفًا{result?.skippedCount ? ` (تُخطي ${result.skippedCount})` : ''} — الدائرة فارغة الآن.
            </p>
            {confirmingDelete && (
              <p className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2" role="alert">
                حذف الدائرة نهائي ولا يمكن التراجع عنه — سيُحرَّر اسمها داخل الفرع. هل أنت متأكد؟
              </p>
            )}
            <div className="flex flex-wrap justify-end gap-2">
              {confirmingDelete ? (
                <>
                  <button
                    type="button"
                    onClick={() => setConfirmingDelete(false)}
                    disabled={deleting}
                    className="border border-gray-300 rounded-lg px-4 py-2 text-sm min-h-11 bg-white disabled:opacity-50"
                  >
                    تراجع
                  </button>
                  <button
                    type="button"
                    onClick={remove}
                    disabled={deleting}
                    className="bg-red-700 hover:bg-red-600 text-white rounded-lg px-4 py-2 text-sm min-h-11 disabled:opacity-50"
                  >
                    {deleting ? 'جارِ الحذف…' : 'تأكيد الحذف نهائيًا'}
                  </button>
                </>
              ) : (
                <>
                  <button
                    type="button"
                    onClick={onCancel}
                    className="border border-gray-300 rounded-lg px-4 py-2 text-sm min-h-11 bg-white"
                  >
                    إغلاق
                  </button>
                  <button
                    type="button"
                    onClick={() => setConfirmingDelete(true)}
                    className="bg-red-700 hover:bg-red-600 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                  >
                    حذف الدائرة
                  </button>
                </>
              )}
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
}
