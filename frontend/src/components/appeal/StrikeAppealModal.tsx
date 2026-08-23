import { useState } from 'react';
import type { FormEvent } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import type { AppealDto } from '../../types';

/** شطب الاستئناف: تاريخ الشطب ورقم قرار الشطب — المحامي المتابع. */
export default function StrikeAppealModal({
  appeal,
  onClose,
  onSaved,
}: {
  appeal: AppealDto;
  onClose: () => void;
  onSaved: (updated: AppealDto) => void;
}) {
  const [struckOffDate, setStruckOffDate] = useState('');
  const [decisionNumber, setDecisionNumber] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const submit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError(null);
    if (!normalizeArabicDigits(struckOffDate).trim()) return setError('تاريخ الشطب مطلوب');
    if (!decisionNumber.trim()) return setError('رقم قرار الشطب مطلوب');

    setSaving(true);
    try {
      const response = await api.post<AppealDto>(`/appeals/${appeal.id}/strike`, {
        struckOffDate: normalizeArabicDigits(struckOffDate).trim(),
        struckOffDecisionNumber: decisionNumber.trim(),
      });
      onSaved(response.data);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const fieldCls =
    'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11';

  return (
    <div
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="شطب الاستئناف"
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4 overscroll-contain"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md">
        <div className="flex justify-between items-center px-5 py-4 border-b border-gray-200">
          <h3 className="text-lg font-bold text-emerald-800">شطب الاستئناف</h3>
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
          <div>
            <label htmlFor="strike-date" className="block text-sm font-medium text-gray-700 mb-1">تاريخ الشطب</label>
            <input id="strike-date" value={struckOffDate} onChange={(e) => setStruckOffDate(e.target.value)} inputMode="numeric" autoComplete="off" placeholder="مثال: 1/8/2026…" className={fieldCls} />
          </div>
          <div>
            <label htmlFor="strike-number" className="block text-sm font-medium text-gray-700 mb-1">رقم قرار الشطب</label>
            <input id="strike-number" value={decisionNumber} onChange={(e) => setDecisionNumber(e.target.value)} autoComplete="off" className={fieldCls} />
          </div>

          <div className="flex gap-2 pt-1">
            <button
              type="submit"
              disabled={saving}
              className="bg-red-700 hover:bg-red-600 disabled:opacity-60 text-white rounded-lg px-5 py-2 text-sm font-medium min-h-11 focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {saving ? 'جارٍ الحفظ…' : 'حفظ الشطب'}
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
        </form>
      </div>
    </div>
  );
}
