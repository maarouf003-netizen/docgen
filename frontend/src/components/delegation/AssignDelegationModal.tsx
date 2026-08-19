import { useEffect, useRef, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import type { AssignDelegationRequest, DelegationDto, LawyerListItem } from '../../types';
import { DelegationDetails } from './DelegationDetails';

/**
 * نافذة «اعتماد الإنابة واختيار المحامي المختص» لرئيس القسم: تعرض طلب الإنابة المعلّق
 * وقائمة محامي الفرع (فرع رئيس القسم نفسه تلقائيًا)، وباعتماده يُنشأ الملف المناب تلقائيًا
 * في الخلفية ويُشعَر المحامي المختص بتنبييه.
 */
export default function AssignDelegationModal({
  delegation,
  onClose,
  onAssigned,
}: {
  delegation: DelegationDto;
  onClose: () => void;
  onAssigned: (lawyerName: string) => void;
}) {
  const [lawyers, setLawyers] = useState<LawyerListItem[]>([]);
  const [selectedLawyerId, setSelectedLawyerId] = useState<number | ''>('');
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');
  const lawyerRef = useRef<HTMLSelectElement>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .get<LawyerListItem[]>('/users/lawyers')
      .then((r) => {
        if (!cancelled) setLawyers(Array.isArray(r.data) ? r.data : []);
      })
      .catch(() => {
        if (!cancelled) setLawyers([]);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (selectedLawyerId === '') {
      setFormError('اختر المحامي المختص');
      lawyerRef.current?.focus();
      return;
    }
    setFormError('');
    setSaving(true);
    const lawyerId = Number(selectedLawyerId);
    const payload: AssignDelegationRequest = { assignedLawyerId: lawyerId };
    try {
      await api.post(`/delegations/${delegation.id}/assign`, payload);
      onAssigned(lawyers.find((l) => l.id === lawyerId)?.fullName ?? '');
    } catch (err) {
      setFormError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const inputCls =
    'w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500';

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="اعتماد الإنابة"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-y-auto overscroll-contain">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 sticky top-0 bg-white">
          <h3 className="text-lg font-bold text-gray-800">اعتماد الإنابة واختيار المحامي المختص</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <form onSubmit={submit} className="px-5 py-4 space-y-4">
          <div className="rounded-lg bg-gray-50 border border-gray-200 px-3 py-2 space-y-3">
            <p className="text-xs text-gray-500">
              الملف المنيب:{' '}
              <span className="font-medium text-gray-800">
                {delegation.sourceDocumentLabel || `ملف رقم ${delegation.sourceDocumentId}`}
              </span>
            </p>
            <DelegationDetails d={delegation} />
            {delegation.isExternal && (
              <p className="text-xs text-sky-700">
                إنابة خارجية — سيُنشأ الملف المناب في {delegation.externalBranchName ?? 'الفرع المناب'}.
              </p>
            )}
          </div>

          {formError && <p className="text-red-600 text-sm" role="alert">{formError}</p>}

          <div>
            <label htmlFor="assignedLawyerId" className="block text-xs font-bold text-gray-600 mb-1">
              المحامي المختص
            </label>
            <select
              id="assignedLawyerId"
              ref={lawyerRef}
              value={selectedLawyerId}
              onChange={(e) => setSelectedLawyerId(e.target.value === '' ? '' : Number(e.target.value))}
              className={inputCls}
              autoComplete="off"
            >
              <option value="">اختر المحامي…</option>
              {lawyers.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.fullName}
                </option>
              ))}
            </select>
            {lawyers.length === 0 && (
              <p className="text-xs text-gray-500 mt-1">تعذّر تحميل قائمة محامي الفرع — حاول لاحقًا</p>
            )}
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={saving}
              className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-5 py-2 text-sm min-h-11 disabled:opacity-50"
            >
              {saving ? 'جارِ الاعتماد...' : 'اعتماد وتكليف المحامي'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
