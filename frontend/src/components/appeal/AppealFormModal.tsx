import { useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import { APPEAL_DIRECTION_APPELLANTS } from '../../utils/appealStatus';
import type { AppealDirection, AppealDto, AppealPartySelectionDto, DocumentResponse } from '../../types';
import { buildAllParties, buildAppellantOptions } from './appealOptions';

/** حقل تاريخ حر (قاعدة التواريخ الحرة): نص عادي مع مثال، وتُطبَّع الأرقام العربية عند الإرسال. */
function FreeDateField({
  id,
  label,
  value,
  onChange,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <div>
      <label htmlFor={id} className="block text-sm font-medium text-gray-700 mb-1">
        {label}
      </label>
      <input
        id={id}
        name={id}
        type="text"
        inputMode="numeric"
        autoComplete="off"
        placeholder="مثال: 1/8/2026…"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11"
      />
    </div>
  );
}

/** حقل نصي قصير اختياري. */
function TextField({
  id,
  label,
  value,
  onChange,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <div>
      <label htmlFor={id} className="block text-sm font-medium text-gray-700 mb-1">
        {label}
      </label>
      <input
        id={id}
        name={id}
        type="text"
        autoComplete="off"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11"
      />
    </div>
  );
}

/** حقل نص كبير يتوسع تلقائيًا ليسع النص المدخل. */
function AutoGrowField({
  id,
  label,
  value,
  onChange,
  placeholder = 'اكتب هنا…',
  invalid = false,
  onInputOnce,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  invalid?: boolean;
  onInputOnce?: () => void;
}) {
  const grow = (el: HTMLTextAreaElement) => {
    el.style.height = 'auto';
    el.style.height = `${el.scrollHeight}px`;
  };
  return (
    <div>
      <label htmlFor={id} className="block text-sm font-medium text-gray-700 mb-1">
        {label}
      </label>
      <textarea
        id={id}
        name={id}
        rows={3}
        value={value}
        placeholder={`${placeholder}…`}
        aria-invalid={invalid || undefined}
        onChange={(e) => {
          onChange(e.target.value);
          if (invalid) onInputOnce?.();
        }}
        onInput={(e) => grow(e.currentTarget)}
        ref={(el) => {
          if (el && value) grow(el);
        }}
        className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm text-gray-800 resize-y overflow-hidden focus:outline-none focus:ring-2 focus:ring-emerald-500"
      />
    </div>
  );
}

/**
 * نموذج تسطير استئناف على الملف (مستأنِفين أو مستأنف علينا).
 * حقول كل مسار كما هي معتمدة، والتواريخ حرة تُطبَّع أرقامها العربية عند الإرسال.
 */
export default function AppealFormModal({
  doc,
  variant,
  onClose,
  onSaved,
}: {
  doc: DocumentResponse;
  variant: AppealDirection;
  onClose: () => void;
  onSaved: (appeal: AppealDto) => void;
}) {
  const appellantsVariant = variant === APPEAL_DIRECTION_APPELLANTS;
  const options = useMemo(() => buildAppellantOptions(doc, variant), [doc, variant]);

  const [selectedKeys, setSelectedKeys] = useState<string[]>([]);
  const [appealTypeLabel, setAppealTypeLabel] = useState('');
  const [decisionText, setDecisionText] = useState('');
  const [decisionSummary, setDecisionSummary] = useState('');
  const [decisionDate, setDecisionDate] = useState('');
  const [inspectionNumber, setInspectionNumber] = useState('');
  const [inspectionDate, setInspectionDate] = useState('');
  const [grounds, setGrounds] = useState('');
  const [noticeNumber, setNoticeNumber] = useState('');
  const [noticeDate, setNoticeDate] = useState('');
  const [court, setCourt] = useState('');
  const [baseNumber, setBaseNumber] = useState('');
  const [baseYear, setBaseYear] = useState('');
  const [depositBookNumber, setDepositBookNumber] = useState('');
  const [depositBookDate, setDepositBookDate] = useState('');
  const [defenseOpinion, setDefenseOpinion] = useState('');
  const [notes, setNotes] = useState('');

  const [error, setError] = useState<string | null>(null);
  const [errorField, setErrorField] = useState<'appellants' | 'decision' | null>(null);
  const [saving, setSaving] = useState(false);

  const clearFieldError = (field: 'appellants' | 'decision') => {
    setErrorField((prev) => (prev === field ? null : prev));
  };

  const toggleSelection = (key: string) => {
    setSelectedKeys((prev) =>
      prev.includes(key) ? prev.filter((k) => k !== key) : [...prev, key],
    );
    clearFieldError('appellants');
  };

  const clean = (v: string) => {
    const t = v.trim();
    return t.length > 0 ? t : undefined;
  };
  const cleanDate = (v: string) => {
    const t = normalizeArabicDigits(v).trim();
    return t.length > 0 ? t : undefined;
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setErrorField(null);

    if (options.length === 0) {
      setError('لا توجد أطراف مؤهلة للاختيار كمستأنف على هذا الملف');
      return;
    }
    const appellants: AppealPartySelectionDto[] = selectedKeys.map((key) => {
      const [kind, idRaw] = key.split(':');
      return { kind, partyId: Number(idRaw) };
    });
    if (appellants.length === 0) {
      setError('يجب اختيار المستأنف');
      setErrorField('appellants');
      document.getElementById('appeal-appellant-0')?.focus();
      return;
    }
    if (!clean(decisionText)) {
      setError('يجب إدخال نص القرار المستأنف على الأقل');
      setErrorField('decision');
      document.getElementById('appeal-decision-text')?.focus();
      return;
    }

    setSaving(true);
    try {
      const response = await api.post<AppealDto>(`/documents/${doc.id}/appeals`, {
        direction: variant,
        appellants,
        appealTypeLabel: appellantsVariant ? undefined : clean(appealTypeLabel),
        appealedDecisionText: clean(decisionText),
        appealedDecisionSummary: clean(decisionSummary),
        appealedDecisionDate: cleanDate(decisionDate),
        inspectionBookNumber: appellantsVariant ? clean(inspectionNumber) : undefined,
        inspectionBookDate: appellantsVariant ? cleanDate(inspectionDate) : undefined,
        groundsSummary: appellantsVariant ? clean(grounds) : undefined,
        noticeNumber: appellantsVariant ? undefined : clean(noticeNumber),
        noticeDate: appellantsVariant ? undefined : cleanDate(noticeDate),
        appellateCourt: appellantsVariant ? undefined : clean(court),
        appealBaseNumber: appellantsVariant ? undefined : clean(baseNumber),
        appealYear: appellantsVariant ? undefined : clean(normalizeArabicDigits(baseYear)),
        depositBookNumber: clean(depositBookNumber),
        depositBookDate: cleanDate(depositBookDate),
        defenseOpinion: appellantsVariant ? undefined : clean(defenseOpinion),
        notes: clean(notes),
      });
      onSaved(response.data);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  // المستأنف عليهم = سجل أطراف الملف الكامل ناقص المختارين (مواجهة الجميع حكمًا).
  const appelleesNames = useMemo(() => {
    const selected = new Set(selectedKeys);
    return buildAllParties(doc)
      .filter((o) => !selected.has(`${o.kind}:${o.partyId}`))
      .map((o) => o.name);
  }, [doc, selectedKeys]);

  return (
    <div
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={appellantsVariant ? 'تسطير استئناف — مستأنِفين' : 'تسطير استئناف — مستأنف علينا'}
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4 overscroll-contain"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
        <div className="sticky top-0 bg-white flex justify-between items-center px-5 py-4 border-b border-gray-200 rounded-t-xl z-10">
          <h3 className="text-lg font-bold text-emerald-800">
            {appellantsVariant ? 'استئناف قرار رئيس التنفيذ — مستأنِفين' : 'استئناف قرار رئيس التنفيذ — مستأنف علينا'}
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

          {/* المستأنف: مربعات تحديد من أطراف الملف المؤهلة. */}
          <fieldset>
            <legend className="text-sm font-medium text-gray-700 mb-1">المستأنف</legend>
            {options.length === 0 ? (
              <p className="text-sm text-gray-500">لا توجد أطراف مؤهلة على هذا الملف.</p>
            ) : (
              <div className="grid sm:grid-cols-2 gap-x-4 gap-y-1.5">
                {options.map((o, index) => {
                  const key = `${o.kind}:${o.partyId}`;
                  return (
                    <label key={key} htmlFor={`appeal-appellant-${index}`} className="inline-flex items-center gap-2 text-sm text-gray-800 min-h-11 cursor-pointer">
                      <input
                        id={`appeal-appellant-${index}`}
                        type="checkbox"
                        checked={selectedKeys.includes(key)}
                        onChange={() => toggleSelection(key)}
                        aria-invalid={errorField === 'appellants' || undefined}
                        className="w-4 h-4 accent-emerald-700"
                      />
                      <span>{o.name}</span>
                    </label>
                  );
                })}
              </div>
            )}
          </fieldset>

          {/* المستأنف عليه: باقي أطراف الملف تلقائيًا (عرض فقط). */}
          <div>
            <span className="block text-sm font-medium text-gray-700 mb-1">المستأنف عليه</span>
            <p className="text-sm text-gray-600 bg-gray-50 border border-gray-200 rounded-lg px-3 py-2">
              {appelleesNames.length > 0 ? appelleesNames.join('، ') : '—'}
              <span className="text-xs text-gray-400"> (جميع أطراف الملف عدا المستأنف)</span>
            </p>
          </div>

          <AutoGrowField
            id="appeal-decision-text"
            label={appellantsVariant ? 'القرار المطلوب استئنافه' : 'القرار المستأنف'}
            value={decisionText}
            onChange={setDecisionText}
            onInputOnce={() => clearFieldError('decision')}
            invalid={errorField === 'decision'}
            placeholder="نص القرار"
          />

          {appellantsVariant && (
            <>
              <AutoGrowField
                id="appeal-decision-summary"
                label="ملخص القرار المطلوب استئنافه"
                value={decisionSummary}
                onChange={setDecisionSummary}
                placeholder="ملخص موجز للقرار"
              />
              <FreeDateField
                id="appeal-decision-date"
                label="تاريخ قرار رئيس التنفيذ المطلوب استئنافه"
                value={decisionDate}
                onChange={setDecisionDate}
              />
              <TextField
                id="appeal-inspection-number"
                label="رقم كتاب المطالعة وإيداع الملف رئيس القسم"
                value={inspectionNumber}
                onChange={setInspectionNumber}
              />
              <FreeDateField
                id="appeal-inspection-date"
                label="تاريخ كتاب المطالعة وإيداع الملف رئيس القسم"
                value={inspectionDate}
                onChange={setInspectionDate}
              />
              <AutoGrowField
                id="appeal-grounds"
                label="ملخص كتاب المطالعة المتضمن موجبات الاستئناف"
                value={grounds}
                onChange={setGrounds}
                placeholder="موجبات الاستئناف"
              />
            </>
          )}

          {!appellantsVariant && (
            <>
              <FreeDateField
                id="appeal-decision-date"
                label="تاريخ القرار المستأنف"
                value={decisionDate}
                onChange={setDecisionDate}
              />
              <TextField
                id="appeal-notice-number"
                label="رقم ورود سند تبليغ الاستئناف"
                value={noticeNumber}
                onChange={setNoticeNumber}
              />
              <FreeDateField
                id="appeal-notice-date"
                label="تاريخ ورود سند تبليغ الاستئناف"
                value={noticeDate}
                onChange={setNoticeDate}
              />
              <TextField
                id="appeal-court"
                label="محكمة الاستئناف التنفيذية المختصة"
                value={court}
                onChange={setCourt}
              />
              <TextField
                id="appeal-base-number"
                label="رقم الأساس الاستئنافي"
                value={baseNumber}
                onChange={setBaseNumber}
              />
              <TextField
                id="appeal-base-year"
                label="لعام"
                value={baseYear}
                onChange={setBaseYear}
              />
              <AutoGrowField
                id="appeal-defense-opinion"
                label="رأي المحامي المتابع للملف بأسباب الاستئناف"
                value={defenseOpinion}
                onChange={setDefenseOpinion}
                placeholder="رأي المحامي في أسباب الاستئناف"
              />
            </>
          )}

          {/* كتاب إيداع الملف رئيس القسم: يملؤه محامي الملف الأساس (المنشئ) في الاتجاهين. */}
          <TextField
            id="appeal-deposit-book-number"
            label="رقم كتاب إيداع الملف رئيس القسم"
            value={depositBookNumber}
            onChange={setDepositBookNumber}
          />
          <FreeDateField
            id="appeal-deposit-book-date"
            label="تاريخ كتاب إيداع الملف رئيس القسم"
            value={depositBookDate}
            onChange={setDepositBookDate}
          />

          {/* نوع الاستئناف: يُدخله محامي القيد لاحقًا في «تعديل القيد» — لا يظهر في تسطير «مستأنِفين». */}
          {!appellantsVariant && (
            <TextField
              id="appeal-type-label"
              label="نوع الاستئناف (مصرفي / جمركي / عادي…)"
              value={appealTypeLabel}
              onChange={setAppealTypeLabel}
            />
          )}

          <AutoGrowField
            id="appeal-notes"
            label="ملاحظات"
            value={notes}
            onChange={setNotes}
            placeholder="ملاحظات إضافية"
          />

          <div className="flex justify-start gap-2 pt-1 pb-1 flex-wrap">
            <button
              type="submit"
              disabled={saving}
              className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-5 py-2 text-sm font-medium min-h-11 disabled:opacity-60 focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {saving ? 'جارٍ الحفظ…' : 'حفظ'}
            </button>
            <button
              type="button"
              onClick={onClose}
              disabled={saving}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11 focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              إلغاء
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
