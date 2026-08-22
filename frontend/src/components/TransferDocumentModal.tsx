import { useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useTimeout } from '../hooks/useTimeout';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import type { LawyerListItem } from '../types';

export default function TransferDocumentModal({
  documentId,
  currentOwnerId,
  onClose,
  onTransferred,
}: {
  documentId: number;
  currentOwnerId?: number | null;
  onClose: () => void;
  onTransferred?: () => void;
}) {
  const [targetId, setTargetId] = useState<number | ''>('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  useTimeout(onClose, success ? 700 : null);

  const lawyersQuery = useCancellableRequest<LawyerListItem[]>(
    (signal) =>
      api
        .get<LawyerListItem[]>('/users/lawyers', { signal })
        .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [],
  );
  const lawyers = lawyersQuery.data ?? [];
  const loading = lawyersQuery.isLoading;
  const fetchError = lawyersQuery.error;

  const eligible = lawyers.filter((l) => l.isActive && l.id !== currentOwnerId);

  const submit = async () => {
    if (targetId === '') {
      setError('اختر المحامي المستهدف');
      return;
    }
    setSaving(true);
    setError('');
    setSuccess('');
    try {
      await api.post(`/documents/${documentId}/transfer`, { targetLawyerId: Number(targetId) });
      setSuccess('تم نقل الملف بنجاح');
      onTransferred?.();
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
      aria-label="نقل الملف"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <h3 className="text-lg font-bold text-gray-800">نقل الملف إلى محامٍ آخر</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4">
          {loading && <p className="text-gray-500">جارِ تحميل المحامين...</p>}

          {!loading && eligible.length === 0 && (
            <p className="text-gray-500">لا يوجد محامون مفعّلون آخرون في فرعك للنقل إليهم</p>
          )}

          {!loading && eligible.length > 0 && (
            <>
              <label htmlFor="transfer-target" className="block text-xs font-medium text-gray-600 mb-1">المحامي المستهدف</label>
              <select
                id="transfer-target"
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
            </>
          )}

          {(error || fetchError) && (
        <p className="text-red-600 text-sm mt-3" role="alert">{error || fetchError}</p>
      )}
          {success && <p className="text-emerald-700 text-sm mt-3">{success}</p>}

          <div className="mt-5 flex flex-wrap gap-2">
            <button
              onClick={submit}
              disabled={saving || loading || eligible.length === 0}
              className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              {saving ? 'جارِ النقل...' : 'نقل الملف'}
            </button>
            <button
              onClick={onClose}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
            >
              إلغاء
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
