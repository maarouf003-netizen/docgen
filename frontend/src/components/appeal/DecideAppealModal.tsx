import { useState } from 'react';
import type { FormEvent } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import { APPEAL_OUTCOME_AGAINST, APPEAL_OUTCOME_IN_FAVOR } from '../../utils/appealStatus';
import type { AppealDto } from '../../types';

/** حسم الاستئناف: رقم قرار الحسم وتاريخه ومنطوقه ونتيجته (للصالح/للضد) — المحامي المتابع. */
export default function DecideAppealModal({
  appeal,
  onClose,
  onSaved,
}: {
  appeal: AppealDto;
  onClose: () => void;
  onSaved: (updated: AppealDto) => void;
}) {
  const [decisionNumber, setDecisionNumber] = useState('');
  const [decisionDate, setDecisionDate] = useState('');
  const [decisionRuling, setDecisionRuling] = useState('');
  const [outcome, setOutcome] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const fieldCls =
    'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11';

  const submit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError(null);
    if (!decisionNumber.trim()) return setError('رقم قرار الحسم مطلوب');
    if (!normalizeArabicDigits(decisionDate).trim()) return setError('تاريخ قرار الحسم مطلوب');
    if (!decisionRuling.trim()) return setError('منطوق القرار مطلوب');
    if (outcome !== APPEAL_OUTCOME_IN_FAVOR && outcome !== APPEAL_OUTCOME_AGAINST)
      return setError('اختر نتيجة الاستئناف: للصالح أو للضد');

    setSaving(true);
    try {
      const response = await api.post<AppealDto>(`/appeals/${appeal.id}/decide`, {
        decisionNumber: decisionNumber.trim(),
        decisionDate: normalizeArabicDigits(decisionDate).trim(),
        decisionRuling: decisionRuling.trim(),
        outcome,
      });
      onSaved(response.data);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="حسم الاستئناف"
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4 overscroll-contain"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md max-h-[90vh] overflow-y-auto">
        <div className="sticky top-0 bg-white flex justify-between items-center px-5 py-4 border-b border-gray-200 rounded-t-xl">
          <h3 className="text-lg font-bold text-emerald-800">حسم الاستئناف</h3>
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
            <label htmlFor="decide-number" className="block text-sm font-medium text-gray-700 mb-1">رقم قرار الحسم</label>
            <input id="decide-number" value={decisionNumber} onChange={(e) => setDecisionNumber(e.target.value)} autoComplete="off" className={fieldCls} />
          </div>
          <div>
            <label htmlFor="decide-date" className="block text-sm font-medium text-gray-700 mb-1">تاريخ قرار الحسم</label>
            <input id="decide-date" value={decisionDate} onChange={(e) => setDecisionDate(e.target.value)} inputMode="numeric" autoComplete="off" placeholder="مثال: 1/8/2026…" className={fieldCls} />
          </div>
          <div>
            <label htmlFor="decide-ruling" className="block text-sm font-medium text-gray-700 mb-1">منطوق القرار</label>
            <textarea
              id="decide-ruling"
              rows={4}
              value={decisionRuling}
              onChange={(e) => setDecisionRuling(e.target.value)}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm text-gray-800 resize-y focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="decide-outcome" className="block text-sm font-medium text-gray-700 mb-1">نتيجة الاستئناف</label>
            <select id="decide-outcome" value={outcome} onChange={(e) => setOutcome(e.target.value)} className={`${fieldCls} bg-white`}>
              <option value="">اختر النتيجة…</option>
              <option value={APPEAL_OUTCOME_IN_FAVOR}>للصالح</option>
              <option value={APPEAL_OUTCOME_AGAINST}>للضد</option>
            </select>
          </div>

          <div className="flex gap-2 pt-1">
            <button
              type="submit"
              disabled={saving}
              className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-60 text-white rounded-lg px-5 py-2 text-sm font-medium min-h-11 focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {saving ? 'جارٍ الحفظ…' : 'حفظ الحسم'}
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
