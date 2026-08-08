import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import type { LawyerListItem } from '../types';

export default function TransferAllFilesModal({
  sourceLawyer,
  lawyers,
  onClose,
  onTransferred,
}: {
  sourceLawyer: LawyerListItem;
  lawyers: LawyerListItem[];
  onClose: () => void;
  onTransferred: (count: number) => void;
}) {
  const [fileCount, setFileCount] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [step, setStep] = useState<1 | 2>(1);
  const [targetId, setTargetId] = useState<number | ''>('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [transferredCount, setTransferredCount] = useState<number | null>(null);

  const eligible = lawyers.filter((l) => l.isActive && l.id !== sourceLawyer.id);
  const target = eligible.find((l) => l.id === targetId);

  useEffect(() => {
    setLoading(true);
    setError('');
    api
      .get<{ count: number }>(`/documents/owner/${sourceLawyer.id}/count`)
      .then((r) => setFileCount(r.data.count))
      .catch((err) => setError(getApiErrorMessage(err)))
      .finally(() => setLoading(false));
  }, [sourceLawyer.id]);

  const goToConfirm = () => {
    if (targetId === '') {
      setError('اختر المحامي المستهدف');
      return;
    }
    setError('');
    setStep(2);
  };

  const submit = async () => {
    if (!target) return;
    setSaving(true);
    setError('');
    try {
      const res = await api.post<{ transferredCount: number }>('/documents/transfer-all', {
        sourceLawyerId: sourceLawyer.id,
        targetLawyerId: target.id,
      });
      const count = res.data.transferredCount;
      setTransferredCount(count);
      onTransferred(count);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="نقل كامل الملفات"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <h3 className="text-lg font-bold text-gray-800">نقل كامل ملفات المحامي</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4">
          {transferredCount !== null && (
            <>
              <p className="text-emerald-700 font-medium">
                تم نقل {transferredCount} {transferredCount === 1 ? 'ملف' : 'ملفًا'} إلى {target?.fullName ?? 'المحامي المستهدف'}
              </p>
              <div className="mt-5 flex flex-wrap gap-2">
                <button
                  onClick={onClose}
                  className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                >
                  إغلاق
                </button>
              </div>
            </>
          )}

          {transferredCount === null && step === 1 && (
            <>
              {loading && <p className="text-gray-500">جارِ حساب عدد الملفات...</p>}

              {!loading && fileCount !== null && (
                <div className="bg-gray-50 rounded-lg p-3 mb-4 text-sm text-gray-700">
                  <p>
                    {`سيتم نقل ${fileCount} ملفًا من ${sourceLawyer.fullName} إلى محامٍ آخر بجميع الحالات.`}
                  </p>
                  {fileCount === 0 && (
                    <p className="text-gray-500 mt-1">لا توجد ملفات لهذا المحامي حالياً.</p>
                  )}
                </div>
              )}

              {!loading && fileCount !== null && fileCount > 0 && (
                <>
                  <label htmlFor="transfer-all-target" className="block text-xs font-medium text-gray-600 mb-1">المحامي المستهدف</label>
                  <select
                    id="transfer-all-target"
                    value={targetId}
                    onChange={(e) => {
                      setTargetId(e.target.value === '' ? '' : Number(e.target.value));
                      setError('');
                    }}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  >
                    <option value="">اختر المحامي...</option>
                    {eligible.map((l) => (
                      <option key={l.id} value={l.id}>{l.fullName}</option>
                    ))}
                  </select>
                  {eligible.length === 0 && (
                    <p className="text-gray-500 text-sm mt-2">لا يوجد محامون مفعّلون آخرون في فرعك للنقل إليهم</p>
                  )}
                </>
              )}
            </>
          )}

          {transferredCount === null && step === 2 && target && fileCount !== null && (
            <div className="rounded-lg border border-red-200 bg-red-50 p-3 mb-4">
              <p className="text-sm text-gray-800">
                <span className="font-bold">تأكيد نهائي:</span> سيتم نقل <span className="font-bold">{fileCount}</span> ملفًا من{' '}
                <span className="font-bold">{sourceLawyer.fullName}</span> إلى <span className="font-bold">{target.fullName}</span>.
              </p>
              <p className="text-sm text-red-700 mt-1">لا يمكن التراجع عن هذه العملية بعد تأكيدها.</p>
            </div>
          )}

          {error && <p className="text-red-600 text-sm mt-3">{error}</p>}

          {transferredCount === null && (
            <div className="mt-5 flex flex-wrap gap-2">
              {step === 1 && (
                <button
                  onClick={goToConfirm}
                  disabled={saving || loading || fileCount === null || fileCount === 0}
                  className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                >
                  متابعة
                </button>
              )}
              {step === 2 && (
                <button
                  onClick={submit}
                  disabled={saving}
                  className="bg-red-700 hover:bg-red-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                >
                  {saving ? 'جارِ النقل...' : 'تأكيد النقل النهائي'}
                </button>
              )}
              <button
                onClick={step === 2 ? () => { setStep(1); setError(''); } : onClose}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
              >
                {step === 2 ? 'رجوع' : 'إلغاء'}
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
