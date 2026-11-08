import { useEffect, useRef, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { getNumberReuseWarning, type NumberReuseWarning } from '../../api/numberWarning';
import NumberReuseWarningModal from '../NumberReuseWarningModal';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import { useCurrentYear } from '../../hooks/useCurrentYear';
import { FUTURE_FILE_YEAR_MESSAGE, isFutureFileYear } from '../form/documentFormConstants';
import type { DelegationDto, ExecutionCircuitDto, RegisterDelegationRequest } from '../../types';
import { DelegationDetails } from './DelegationDetails';

const DATE_PLACEHOLDER = 'مثال: 1/8/2026';

/**
 * نافذة «تسجيل الإنابة أصولًا» لمحامي الملف المناب: إدخال رقم أساس الإنابة وسنة قيدها
 * وتاريخ القيد — فتُقيد بيانات الملف المناب (رقم الملف/السنة) ويُرفع من «تحت رفع» إلى متداول.
 */
export default function RegisterDelegationModal({
  delegation,
  onClose,
  onRegistered,
}: {
  delegation: DelegationDto;
  onClose: () => void;
  onRegistered: () => void;
}) {
  const [fileNumber, setFileNumber] = useState('');
  const [fileYear, setFileYear] = useState('');
  const [fileRegistrationDate, setFileRegistrationDate] = useState('');
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');
  const [circuits, setCircuits] = useState<ExecutionCircuitDto[]>([]);
  const [circuitId, setCircuitId] = useState('');
  const [warning, setWarning] = useState<NumberReuseWarning | null>(null);
  const numberRef = useRef<HTMLInputElement>(null);
  const yearRef = useRef<HTMLInputElement>(null);
  const dateRef = useRef<HTMLInputElement>(null);
  // سقف سنة القيد (تصبح سنة الملف): الجارية خادميًا — المستقبل مرفوض خلفيةً أيضًا.
  const { currentYear } = useCurrentYear();

  // الإنابة الخارجية: الملف المناب بلا دائرة، فتختار دائرة الإدخال هنا (V-18).
  useEffect(() => {
    if (!delegation.isExternal) return;
    let alive = true;
    api
      .get<ExecutionCircuitDto[]>('/execution-circuits/for-delegation')
      .then((r) => {
        if (alive) setCircuits(Array.isArray(r.data) ? r.data : []);
      })
      .catch(() => {
        if (alive) setCircuits([]);
      });
    return () => {
      alive = false;
    };
  }, [delegation.isExternal]);

  const validate = (): { message: string; ref: HTMLInputElement | null } => {
    if (delegation.isExternal && !circuitId)
      return { message: 'دائرة إدخال رقم الإنابة مطلوبة', ref: null };
    if (!fileNumber.trim()) return { message: 'رقم أساس الإنابة مطلوب', ref: numberRef.current };
    if (!fileYear.trim()) return { message: 'سنة قيد الإنابة مطلوبة', ref: yearRef.current };
    if (isFutureFileYear(normalizeArabicDigits(fileYear).trim(), currentYear))
      return { message: FUTURE_FILE_YEAR_MESSAGE, ref: yearRef.current };
    if (!fileRegistrationDate.trim()) return { message: 'تاريخ قيد الإنابة مطلوب', ref: dateRef.current };
    return { message: '', ref: null };
  };

  const submit = async (e: FormEvent, warningToken?: string) => {
    e.preventDefault();
    const problem = validate();
    if (problem.message) {
      setFormError(problem.message);
      problem.ref?.focus();
      return;
    }
    setFormError('');
    setSaving(true);
    const payload: RegisterDelegationRequest = {
      fileNumber: normalizeArabicDigits(fileNumber).trim(),
      fileYear: normalizeArabicDigits(fileYear).trim(),
      fileRegistrationDate: normalizeArabicDigits(fileRegistrationDate).trim(),
      ...(delegation.isExternal ? { executionCircuitId: Number(circuitId) } : {}),
      ...(warningToken ? { warningToken } : {}),
    };
    try {
      await api.post(`/delegations/${delegation.id}/register`, payload);
      setWarning(null);
      onRegistered();
    } catch (err) {
      const reuse = getNumberReuseWarning(err);
      if (reuse) setWarning(reuse);
      else setFormError(getApiErrorMessage(err));
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
      aria-label="تسجيل الإنابة أصولًا"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-y-auto overscroll-contain">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 sticky top-0 bg-white">
          <h3 className="text-lg font-bold text-gray-800">تسجيل الإنابة أصولًا</h3>
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
          </div>

          {formError && <p className="text-red-600 text-sm" role="alert">{formError}</p>}

          <div className="grid sm:grid-cols-2 gap-3">
            <div>
              <label htmlFor="fileNumber" className="block text-xs font-bold text-gray-600 mb-1">
                رقم أساس الإنابة
              </label>
              <input
                id="fileNumber"
                ref={numberRef}
                type="text"
                value={fileNumber}
                onChange={(e) => setFileNumber(e.target.value)}
                placeholder="مثال: 890…"
                className={inputCls}
                autoComplete="off"
              />
            </div>
            <div>
              <label htmlFor="fileYear" className="block text-xs font-bold text-gray-600 mb-1">
                سنة قيد الإنابة
              </label>
              <input
                id="fileYear"
                ref={yearRef}
                type="text"
                value={fileYear}
                onChange={(e) => setFileYear(e.target.value)}
                placeholder="مثال: 2026…"
                className={inputCls}
                autoComplete="off"
              />
            </div>
          </div>

          {delegation.isExternal && (
            <div>
              <label htmlFor="executionCircuitId" className="block text-xs font-bold text-gray-600 mb-1">
                دائرة إدخال رقم الإنابة
              </label>
              <select
                id="executionCircuitId"
                value={circuitId}
                onChange={(e) => setCircuitId(e.target.value)}
                className={inputCls}
                autoComplete="off"
              >
                <option value="">— اختر الدائرة —</option>
                {circuits.map((circuit) => (
                  <option key={circuit.id} value={circuit.id}>
                    {circuit.name}
                  </option>
                ))}
              </select>
              {circuits.length === 0 && (
                <p className="mt-1 text-xs text-amber-700">
                  لا توجد دوائر متاحة للإنابة في محافظة فرعك — راجع الإدارة.
                </p>
              )}
            </div>
          )}

          <div>
            <label htmlFor="fileRegistrationDate" className="block text-xs font-bold text-gray-600 mb-1">
              تاريخ قيد الإنابة
            </label>
            <input
              id="fileRegistrationDate"
              ref={dateRef}
              type="text"
              value={fileRegistrationDate}
              onChange={(e) => setFileRegistrationDate(e.target.value)}
              placeholder={DATE_PLACEHOLDER}
              className={inputCls}
              autoComplete="off"
            />
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
              {saving ? 'جارِ التسجيل...' : 'تسجيل أصولًا'}
            </button>
          </div>
        </form>
      </div>

      {warning && (
        <NumberReuseWarningModal
          message={warning.message}
          busy={saving}
          onCancel={() => setWarning(null)}
          onConfirm={() => {
            const token = warning.warningToken;
            setWarning(null);
            void submit({ preventDefault() {} } as FormEvent, token);
          }}
        />
      )}
    </div>
  );
}
