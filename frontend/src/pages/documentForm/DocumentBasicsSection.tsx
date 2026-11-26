import type { ReactNode, RefObject } from 'react';
import { FieldInfoHint } from '../../components/form/FieldInfoHint';
import { FormSectionTitle } from '../../components/form/FormSectionTitle';
import { fileYearsUpto } from '../../components/form/documentFormConstants';
import type { FormSet } from '../../components/form/formFields';
import type { ApplicantPublicEntityDto, DocumentUpsertRequest } from '../../types';

/** رسالة تلميح «رقم الملف»: الملف بلا رقم/سنة يبقى «تحت رفع» في وضع طالبة التنفيذ. */
const FILE_NUMBER_HINT =
  'يمكن إضافة رقم الملف ونوعه لاحقًا؛ سيُدخَل الملف كـ«تحت رفع» لحين تعديله وإدخال الرقم والنوع.';

type FieldHelper = (label: string, key: keyof DocumentUpsertRequest, placeholder?: string, type?: string) => ReactNode;
type SelectHelper = (
  label: string,
  id: string,
  options: string[],
  value: string | undefined,
  onChange: (v: string) => void,
  extraClass?: string,
) => ReactNode;

interface DocumentBasicsSectionProps {
  form: DocumentUpsertRequest;
  set: FormSet;
  isEdit: boolean;
  isExecuted: boolean;
  isMirror: boolean;
  currentYear: number;
  /** حقول قابلة للتحرير في الوضع الحالي (المقترض). */
  field: FieldHelper;
  /** حقول مقفلة في الملف المناب (الورود/الحجز/الكتاب). */
  lockField: FieldHelper;
  selectField: SelectHelper;
  applicantPublicEntities: ApplicantPublicEntityDto[];
  onApplicantRemove: (index: number) => void;
  onApplicantAdd: () => void;
  onApplicantPickRegistry: (index: number) => void;
  onApplicantUnlink: (index: number) => void;
  circuitPickerButtonRef: RefObject<HTMLButtonElement | null>;
  onOpenCircuitPicker: () => void;
}

/**
 * قسم «المعلومات الأساسية»: الدائرة (من سجل الدوائر)، رقم/سنة/نوع الملف،
 * وتواريخ الورود/القيد/الحجز، وجهات الطالب العامة عند وضع «طالبة التنفيذ».
 * منقول حرفيًا من `DocumentForm` (بلا تغيير سلوكي).
 */
export function DocumentBasicsSection({
  form,
  set,
  isEdit,
  isExecuted,
  isMirror,
  currentYear,
  field,
  lockField,
  selectField,
  applicantPublicEntities,
  onApplicantRemove,
  onApplicantAdd,
  onApplicantPickRegistry,
  onApplicantUnlink,
  circuitPickerButtonRef,
  onOpenCircuitPicker,
}: DocumentBasicsSectionProps) {
  return (
    <>
      <FormSectionTitle title="🏛️ المعلومات الأساسية" />
      <div className="grid md:grid-cols-5 gap-4 items-end">
        <div>
          <label htmlFor="court-display" className="block text-xs font-bold text-gray-600 mb-1">
            دائرة التنفيذ
          </label>
          <div className="relative">
            <input
              id="court-display"
              value={form.court ?? ''}
              readOnly
              placeholder="اختر من السجل…"
              aria-readonly="true"
              className={`w-full min-h-11 border border-gray-300 bg-gray-50 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 cursor-not-allowed ${
                form.executionCircuitId != null ? 'pe-20' : 'pe-14'
              }`}
            />
            <div className="absolute inset-y-0 end-1 flex items-center gap-1">
              {form.executionCircuitId != null && (
                <span className="text-emerald-600 font-bold leading-none" title="مرتبطة بالسجل">
                  <span aria-hidden="true">✓</span>
                  <span className="sr-only">مرتبطة بالسجل</span>
                </span>
              )}
              <button
                ref={circuitPickerButtonRef}
                type="button"
                onClick={onOpenCircuitPicker}
                aria-label={form.executionCircuitId != null ? 'تغيير الدائرة' : 'اختيار الدائرة'}
                title={form.executionCircuitId != null ? 'تغيير الدائرة' : 'اختيار الدائرة'}
                className="flex items-center justify-center min-h-11 min-w-11 rounded-lg border border-emerald-200 text-emerald-800 hover:bg-emerald-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
              >
                {form.executionCircuitId != null ? (
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
                    <path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z" />
                  </svg>
                ) : (
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
                    <circle cx="11" cy="11" r="7" />
                    <path d="m21 21-4.3-4.3" />
                  </svg>
                )}
              </button>
            </div>
          </div>
        </div>
        <div>
          <label htmlFor="fileNumber" className="block text-xs font-bold text-gray-600 mb-1">
            رقم الملف
          </label>
          <div className="relative">
            <input
              id="fileNumber"
              name="fileNumber"
              autoComplete="off"
              value={form.fileNumber ?? ''}
              onChange={(e) => set('fileNumber', e.target.value)}
              placeholder="رقم الملف…"
              className={`w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 ${
                !isEdit && !isExecuted ? 'pe-14' : ''
              }`}
            />
            {!isEdit && !isExecuted && (
              <FieldInfoHint
                id="fileNumber"
                label="معلومات عن رقم الملف"
                message={FILE_NUMBER_HINT}
              />
            )}
          </div>
        </div>
        {selectField('سنة الملف', 'fileYear', ['', ...fileYearsUpto(currentYear)], form.fileYear ?? '', (v) => set('fileYear', v))}
        {field('نوع الملف', 'fileType', 'نوع الملف...')}
        {isExecuted ? (
          <>
            {field('رقم ورود الإخطار التنفيذي', 'fileReceiptNumber', 'رقم ورود الإخطار التنفيذي...')}
            {field('تاريخ ورود الاخطار', 'fileReceiptDate', 'مثال: 1/8/2026')}
          </>
        ) : (
          <>
            <div className="md:col-span-5">
              <span className="block text-xs font-bold text-gray-600 mb-1">طالب التنفيذ (الجهات العامة)</span>
              {applicantPublicEntities.map((a, i) => (
                <div key={i} className="flex flex-wrap gap-2 mb-2">
                  <input
                    aria-label={`اسم الجهة ${i + 1}`}
                    value={a.name ?? ''}
                    readOnly
                    placeholder="اختر من السجل المرجعي…"
                    className="w-full sm:w-64 min-h-11 border border-gray-300 bg-gray-50 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 cursor-not-allowed"
                  />
                  <input
                    aria-label={`فرع الجهة ${i + 1}`}
                    value={a.branch ?? ''}
                    readOnly
                    placeholder="الفرع"
                    className="w-full sm:w-40 min-h-11 border border-gray-300 bg-gray-50 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 cursor-not-allowed"
                  />
                  <input
                    aria-label={`المحافظة ${i + 1}`}
                    value={a.governorate ?? ''}
                    readOnly
                    placeholder="المحافظة"
                    className="w-full sm:w-40 min-h-11 border border-gray-300 bg-gray-50 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 cursor-not-allowed"
                  />
                  {(a.name ?? '').trim() && a.registryId == null && (
                    <p className="w-full text-xs text-red-600">يجب اختيار هذه الجهة من السجل المرجعي</p>
                  )}
                  {!isMirror && (
                    <>
                      {applicantPublicEntities.length > 1 && (
                        <button
                          type="button"
                          onClick={() => onApplicantRemove(i)}
                          className="text-red-500 text-xs hover:underline min-h-11 px-2"
                        >
                          ✖ حذف
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={() => onApplicantPickRegistry(i)}
                        className="border border-emerald-200 text-emerald-800 hover:bg-emerald-50 rounded-lg px-3 py-2 text-xs min-h-11"
                      >
                        {a.registryId != null ? 'تغيير من السجل…' : 'اختيار من السجل…'}
                      </button>
                      {(a.name ?? '').trim() && a.registryId == null && (
                        <button
                          type="button"
                          onClick={() => onApplicantUnlink(i)}
                          className="self-center border border-gray-200 text-gray-500 hover:bg-gray-50 rounded-lg px-3 py-2 text-xs min-h-11"
                        >
                          مسح
                        </button>
                      )}
                      {a.registryId != null && (
                        <>
                          <span className="self-center rounded-full bg-emerald-50 border border-emerald-100 text-emerald-700 px-2 py-0.5 text-[11px] whitespace-nowrap">
                            مرتبطة بالسجل ✓
                          </span>
                          <button
                            type="button"
                            onClick={() => onApplicantUnlink(i)}
                            className="self-center border border-gray-200 text-gray-500 hover:bg-gray-50 rounded-lg px-3 py-2 text-xs min-h-11"
                          >
                            فك الربط
                          </button>
                        </>
                      )}
                    </>
                  )}
                </div>
              ))}
              {!isMirror && (
                <button
                  type="button"
                  onClick={onApplicantAdd}
                  className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
                >
                  ➕ إضافة جهة
                </button>
              )}
            </div>
            {lockField('رقم ورود الملف', 'fileArrivalNumber')}
            {lockField('تاريخ ورود الملف', 'fileArrivalDate', 'مثال: 1/8/2026')}
            {lockField('رقم كتاب الجهة العامة', 'fileIncoming')}
            {lockField('تاريخ كتاب الجهة العامة', 'fileIncomingDate')}
            {lockField('رقم تحت رفع', 'underFilingNumber')}
            {field('تاريخ قيد الملف', 'fileRegistrationDate', 'مثال: 1/8/2026')}
            {lockField('تاريخ إلقاء حجز المنظومة', 'seizureDate', 'مثال: 1/8/2026')}
          </>
        )}
      </div>
    </>
  );
}
