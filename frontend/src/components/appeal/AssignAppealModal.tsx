import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/useAuth';
import { useCancellableRequest } from '../../hooks/useCancellableRequest';
import type { AppealDto, LawyerListItem } from '../../types';

/**
 * إسناد الاستئناف إلى محامي الفرع (أو نقله بينهم) — رئيس القسم:
 * قائمة المحامين المفعلين في فرعه فقط، وبعد النجاح يُشعَر المحامي تلقائيًا من الخلفية.
 */
export default function AssignAppealModal({
  appeal,
  mode,
  onClose,
  onDone,
}: {
  appeal: AppealDto;
  /** assign: إسناد أول؛ transfer: نقل من المحامي الحالي. */
  mode: 'assign' | 'transfer';
  onClose: () => void;
  onDone: (lawyerName: string) => void;
}) {
  const { user } = useAuth();
  const lawyersQuery = useCancellableRequest<LawyerListItem[]>(
    (signal) =>
      api
        .get<LawyerListItem[]>('/users/lawyers', { signal })
        .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [],
  );

  const [targetId, setTargetId] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // رئيس القسم يختار من محامي فرعه فقط (الخلفية تتحقق أيضًا).
  const options = (lawyersQuery.data ?? []).filter(
    (l) => l.isActive && l.branchId === user?.branchId && l.id !== appeal.assignedLawyerId,
  );

  useEffect(() => {
    if (!targetId && options.length > 0) {
      setTargetId(String(options[0].id));
    }
  }, [options, targetId]);

  const submit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError(null);
    if (!targetId) {
      setError('اختر المحامي المختص');
      return;
    }
    setSaving(true);
    try {
      const isTransfer = mode === 'transfer';
      const response = await api.post<AppealDto>(
        isTransfer ? `/appeals/${appeal.id}/transfer` : `/appeals/${appeal.id}/assign`,
        isTransfer
          ? { targetLawyerId: Number(targetId) }
          : { assignedLawyerId: Number(targetId) },
      );
      onDone(response.data?.assignedLawyerName ?? 'المحامي المختص');
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const fieldCls =
    'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white text-gray-800 focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11';

  return (
    <div
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={mode === 'assign' ? 'إسناد الاستئناف لمحامٍ' : 'نقل الاستئناف لمحامٍ آخر'}
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4 overscroll-contain"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md">
        <div className="flex justify-between items-center px-5 py-4 border-b border-gray-200">
          <h3 className="text-lg font-bold text-emerald-800">
            {mode === 'assign' ? 'إسناد الاستئناف لمحامٍ' : 'نقل الاستئناف لمحامٍ آخر'}
          </h3>
          <button
            type="button"
            onClick={onClose}
            aria-label="إغلاق"
            disabled={saving}
            className="text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg w-11 h-11 inline-flex items-center justify-center text-xl focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
            ×
          </button>
        </div>

        <form onSubmit={submit} noValidate className="px-5 py-4 space-y-4">
          {error && (
            <div role="alert" className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
              {error}
            </div>
          )}
          {mode === 'transfer' && (
            <p className="text-sm text-gray-600">
              المحامي الحالي: <span className="font-medium">{appeal.assignedLawyerName ?? '—'}</span>
            </p>
          )}
          <div>
            <label htmlFor="assign-lawyer" className="block text-sm font-medium text-gray-700 mb-1">
              المحامي المختص للمتابعة
            </label>
            {lawyersQuery.isLoading ? (
              <p className="text-gray-500 text-sm">جارِ التحميل...</p>
            ) : (
              <select id="assign-lawyer" value={targetId} onChange={(e) => setTargetId(e.target.value)} className={fieldCls}>
                <option value="">اختر المحامي…</option>
                {options.map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.fullName}
                  </option>
                ))}
              </select>
            )}
          </div>

          {saving ? (
            <p role="status" className="text-sm text-gray-500">جارٍ التنفيذ…</p>
          ) : (
            <div className="flex gap-2 pt-1">
              <button
                type="submit"
                disabled={saving || !targetId}
                className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-60 text-white rounded-lg px-5 py-2 text-sm font-medium min-h-11 focus:outline-none focus:ring-2 focus:ring-emerald-500"
              >
                {mode === 'assign' ? 'إسناد' : 'نقل'}
              </button>
              <button
                type="button"
                onClick={onClose}
                disabled={saving}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
              >
                إلغاء
              </button>
            </div>
          )}
        </form>
      </div>
    </div>
  );
}
