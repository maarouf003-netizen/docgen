import { useState } from 'react';
import type { FormEvent } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import type { AppealDto } from '../../types';

/**
 * تعديل قيد الاستئناف أمام محكمة الاستئناف (رقم الأساس/السنة/المحكمة/تاريخ الإقرار/النوع)
 * — للمحامي المتابع، على نمط حقول «صفحة التعديل» المعتمدة.
 */
export default function AppealRegistrationModal({
  appeal,
  onClose,
  onSaved,
}: {
  appeal: AppealDto;
  onClose: () => void;
  onSaved: (updated: AppealDto) => void;
}) {
  const [appealTypeLabel, setAppealTypeLabel] = useState(appeal.appealTypeLabel ?? '');
  const [appellateCourt, setAppellateCourt] = useState(appeal.appellateCourt ?? '');
  const [appealBaseNumber, setAppealBaseNumber] = useState(appeal.currentBaseNumber ?? appeal.appealBaseNumber ?? '');
  const [appealYear, setAppealYear] = useState(appeal.appealYear ?? '');
  const [registrationDate, setRegistrationDate] = useState(appeal.registrationDate ?? '');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const submit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError(null);
    setSaving(true);
    try {
      const response = await api.put<AppealDto>(`/appeals/${appeal.id}/registration`, {
        appealTypeLabel: appealTypeLabel.trim() || null,
        appellateCourt: appellateCourt.trim() || null,
        appealBaseNumber: appealBaseNumber.trim() || null,
        appealYear: normalizeArabicDigits(appealYear).trim() || null,
        registrationDate: normalizeArabicDigits(registrationDate).trim() || null,
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
      aria-label="تعديل قيد الاستئناف"
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4 overscroll-contain"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md max-h-[90vh] overflow-y-auto">
        <div className="sticky top-0 bg-white flex justify-between items-center px-5 py-4 border-b border-gray-200 rounded-t-xl">
          <h3 className="text-lg font-bold text-emerald-800">تعديل قيد الاستئناف</h3>
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
            <label htmlFor="reg-base-number" className="block text-sm font-medium text-gray-700 mb-1">رقم الأساس الاستئنافي</label>
            <input id="reg-base-number" value={appealBaseNumber} onChange={(e) => setAppealBaseNumber(e.target.value)} autoComplete="off" className={fieldCls} />
          </div>
          <div>
            <label htmlFor="reg-base-year" className="block text-sm font-medium text-gray-700 mb-1">لعام</label>
            <input id="reg-base-year" value={appealYear} onChange={(e) => setAppealYear(e.target.value)} inputMode="numeric" autoComplete="off" placeholder="مثال: 2026" className={fieldCls} />
          </div>
          <div>
            <label htmlFor="reg-court" className="block text-sm font-medium text-gray-700 mb-1">المحكمة الناظرة بالاستئناف</label>
            <input id="reg-court" value={appellateCourt} onChange={(e) => setAppellateCourt(e.target.value)} autoComplete="off" className={fieldCls} />
          </div>
          <div>
            <label htmlFor="reg-date" className="block text-sm font-medium text-gray-700 mb-1">تاريخ إقرار الاستئناف</label>
            <input id="reg-date" value={registrationDate} onChange={(e) => setRegistrationDate(e.target.value)} inputMode="numeric" autoComplete="off" placeholder="مثال: 1/8/2026…" className={fieldCls} />
          </div>
          <div>
            <label htmlFor="reg-type" className="block text-sm font-medium text-gray-700 mb-1">نوع الاستئناف (مصرفي / جمركي / عادي…)</label>
            <input id="reg-type" value={appealTypeLabel} onChange={(e) => setAppealTypeLabel(e.target.value)} autoComplete="off" className={fieldCls} />
          </div>

          <div className="flex gap-2 pt-1">
            <button
              type="submit"
              disabled={saving}
              className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-60 text-white rounded-lg px-5 py-2 text-sm font-medium min-h-11 focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {saving ? 'جارٍ الحفظ…' : 'حفظ'}
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
