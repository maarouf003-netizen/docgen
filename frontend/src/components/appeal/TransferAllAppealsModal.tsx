import { useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { useCancellableRequest } from '../../hooks/useCancellableRequest';
import type { LawyerListItem } from '../../types';

/**
 * نقل كل استئنافات محامٍ إلى محامٍ آخر ضمن الفرع — رئيس القسم.
 * آلة خطوتين على نمط نقل الملفات: اختيار وعدّاد معاينة، ثم تأكيد نهائي أحمر
 * لا يُتراجع عنه (فعل خطر يتطلب تأكيدًا صريحًا).
 */
export default function TransferAllAppealsModal({
  onClose,
  onTransferred,
}: {
  onClose: () => void;
  onTransferred: (count: number) => void;
}) {
  const lawyersQuery = useCancellableRequest<LawyerListItem[]>(
    (signal) =>
      api
        .get<LawyerListItem[]>('/users/lawyers', { signal })
        .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [],
  );

  const [sourceId, setSourceId] = useState('');
  const [targetId, setTargetId] = useState('');
  const [step, setStep] = useState<1 | 2>(1);
  const [count, setCount] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const lawyers = (lawyersQuery.data ?? []).filter((l) => l.isActive);
  const sourceName = lawyers.find((l) => String(l.id) === sourceId)?.fullName ?? '';
  const targetName = lawyers.find((l) => String(l.id) === targetId)?.fullName ?? '';

  const loadCount = async () => {
    setError(null);
    if (!sourceId || !targetId) {
      setError('اختر المحامي المصدر والمحامي المستهدف');
      return;
    }
    if (sourceId === targetId) {
      setError('لا يمكن النقل إلى المحامي نفسه');
      return;
    }
    setBusy(true);
    try {
      const r = await api.get<{ count: number }>(`/appeals/owner/${sourceId}/count`);
      setCount(typeof r.data?.count === 'number' ? r.data.count : 0);
      setStep(2);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const confirmTransfer = async () => {
    setBusy(true);
    setError(null);
    try {
      const r = await api.post<{ transferredCount: number }>('/appeals/transfer-all', {
        sourceLawyerId: Number(sourceId),
        targetLawyerId: Number(targetId),
      });
      onTransferred(r.data?.transferredCount ?? 0);
    } catch (err) {
      setError(getApiErrorMessage(err));
      setStep(1);
    } finally {
      setBusy(false);
    }
  };

  const fieldCls =
    'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white text-gray-800 focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11';
  const activeLawyers = lawyers.filter((l) => String(l.id) !== sourceId);

  return (
    <div
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="نقل كل استئنافات محامٍ"
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4 overscroll-contain"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md">
        <div className="flex justify-between items-center px-5 py-4 border-b border-gray-200">
          <h3 className="text-lg font-bold text-emerald-800">نقل استئنافات محامٍ</h3>
          <button
            type="button"
            onClick={onClose}
            aria-label="إغلاق"
            disabled={busy}
            className="text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg w-11 h-11 inline-flex items-center justify-center text-xl focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4 space-y-4">
          {error && (
            <div role="alert" className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
              {error}
            </div>
          )}
          {lawyersQuery.isLoading ? (
            <p className="text-gray-500 text-sm">جارِ التحميل...</p>
          ) : (
            <>
              <div>
                <label htmlFor="transfer-source" className="block text-sm font-medium text-gray-700 mb-1">من المحامي</label>
                <select
                  id="transfer-source"
                  value={sourceId}
                  onChange={(e) => { setSourceId(e.target.value); setStep(1); setCount(null); }}
                  disabled={busy || step === 2}
                  className={fieldCls}
                >
                  <option value="">اختر المحامي المصدر…</option>
                  {lawyers.map((l) => (
                    <option key={l.id} value={l.id}>{l.fullName}</option>
                  ))}
                </select>
              </div>
              <div>
                <label htmlFor="transfer-target" className="block text-sm font-medium text-gray-700 mb-1">إلى المحامي</label>
                <select
                  id="transfer-target"
                  value={targetId}
                  onChange={(e) => { setTargetId(e.target.value); setStep(1); setCount(null); }}
                  disabled={busy || step === 2}
                  className={fieldCls}
                >
                  <option value="">اختر المحامي المستهدف…</option>
                  {activeLawyers.map((l) => (
                    <option key={l.id} value={l.id}>{l.fullName}</option>
                  ))}
                </select>
              </div>

              {step === 2 && (
                <div role="status" className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
                  سيتم نقل <span className="font-bold tabular-nums">{count}</span> استئنافًا من{' '}
                  <span className="font-semibold">{sourceName}</span> إلى{' '}
                  <span className="font-semibold">{targetName}</span>.
                </div>
              )}

              {step === 2 ? (
                <>
                  <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
                    لا يمكن التراجع عن هذه العملية. ستنتقل متابعة جميع الاستئنافات المذكورة إلى المحامي المستهدف.
                  </div>
                  <div className="flex gap-2 flex-wrap">
                    <button
                      type="button"
                      onClick={confirmTransfer}
                      disabled={busy}
                      className="bg-red-700 hover:bg-red-600 disabled:opacity-60 text-white rounded-lg px-5 py-2 text-sm font-medium min-h-11 focus:outline-none focus:ring-2 focus:ring-red-400"
                    >
                      {busy ? 'جارٍ النقل…' : 'تأكيد النقل النهائي'}
                    </button>
                    <button
                      type="button"
                      onClick={() => { setStep(1); setCount(null); }}
                      disabled={busy}
                      className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
                    >
                      رجوع
                    </button>
                  </div>
                </>
              ) : (
                <div className="flex gap-2 pt-1">
                  <button
                    type="button"
                    onClick={loadCount}
                    disabled={busy || !sourceId || !targetId}
                    className="bg-sky-800 hover:bg-sky-700 disabled:opacity-60 text-white rounded-lg px-5 py-2 text-sm font-medium min-h-11 focus:outline-none focus:ring-2 focus:ring-sky-400"
                  >
                    {busy ? 'جارٍ الحساب…' : 'متابعة'}
                  </button>
                  <button
                    type="button"
                    onClick={onClose}
                    disabled={busy}
                    className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
                  >
                    إلغاء
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
