import { useCallback, useMemo, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { useFocusTrap } from '../../hooks/useFocusTrap';
import AutoResizeTextarea from '../AutoResizeTextarea';
import { PublicEntityPickerModal } from '../entity/PublicEntityPickerModal';
import type {
  DocumentInterventionDto,
  DocumentInterventionUpsertRequest,
  HeirDto,
  InterventionNature,
  PublicEntityEntryDto,
} from '../../types';
import {
  emptyIntervention,
  hasStoredRepresentative,
  INTERVENTION_LIMITS,
  INTERVENTION_NATURE_OPTIONS,
  interventionToRequest,
} from './documentFormConstants';
import { InterventionPartyFields } from './intervention/InterventionPartyFields';
import { inputCls, labelCls } from './intervention/interventionStyles';
import {
  buildInterventionPayload,
  interventionAddressSuppressed,
  validateIntervention,
  withHeirAdded,
  withHeirRemoved,
  withHeirSet,
  withInterventionNature,
  withRegistryPick,
  withRepresentativeAdded,
  withRepresentativeRemoved,
} from './intervention/interventionForm';

/**
 * نافذة إضافة/تعديل متدخل: قائمة «صفة المتدخل» أولًا، ثم بيانات طلب التدخل
 * (الرقم/تاريخ الورود/تاريخ القبول/الموضوع/الملاحظات بنصوص حرة وأسطر متعددة)،
 * ثم حقول الطرف حسب الصفة. الحفظ فوري عبر مسار فرعي مستقل عن حفظ نموذج الملف،
 * و«إعادة تعيين» تعيد الحالة الابتدائية دون إغلاق، و«إلغاء» تغلق دون حفظ.
 * منطق الحالة/التحقق/الحمولة في `intervention/interventionForm.ts`،
 * وأقسام الطرف في `intervention/InterventionPartyFields.tsx`.
 */
export function InterventionModal({
  documentId,
  initial,
  onSaved,
  onClose,
}: {
  documentId: number;
  /** المتدخل المحفوظ عند التعديل — غيابه يعني إضافة جديدة. */
  initial?: DocumentInterventionDto | null;
  onSaved: (dto: DocumentInterventionDto) => void;
  onClose: () => void;
}) {
  const initialRequest = useMemo(
    () => (initial ? interventionToRequest(initial) : emptyIntervention()),
    [initial],
  );
  const [form, setForm] = useState<DocumentInterventionUpsertRequest>(initialRequest);
  const [showRepresentative, setShowRepresentative] = useState(() =>
    hasStoredRepresentative(initialRequest),
  );
  const [registryPickerOpen, setRegistryPickerOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const dialogRef = useFocusTrap<HTMLDivElement>();

  const isEdit = initial != null;
  const title = isEdit ? 'تعديل بيانات المتدخل' : 'إضافة متدخل';
  // وجود ممثل شرعي أو ورثة يلغي الحاجة لعنوان المتدخل: عنوان الممثل هو المعتبر،
  // والورثة تعني وفاة المتدخل. تُخفى الحقول وتُصفَّر في الحمولة.
  const addressSuppressed = interventionAddressSuppressed(form, showRepresentative);

  const setField = useCallback(
    <K extends keyof DocumentInterventionUpsertRequest>(
      key: K,
      value: DocumentInterventionUpsertRequest[K],
    ) => setForm((prev) => ({ ...prev, [key]: value })),
    [],
  );

  const changeNature = (nature: InterventionNature) => {
    setForm((prev) => withInterventionNature(prev, nature));
    setShowRepresentative(false);
    setError('');
  };

  const addRepresentative = () => {
    setForm(withRepresentativeAdded);
    setShowRepresentative(true);
  };

  const removeRepresentative = () => {
    setForm(withRepresentativeRemoved);
    setShowRepresentative(false);
  };

  const addHeir = () => setForm(withHeirAdded);

  const setHeir = (index: number, key: keyof HeirDto, value: string) =>
    setForm((prev) => withHeirSet(prev, index, key, value));

  const removeHeir = (index: number) => setForm((prev) => withHeirRemoved(prev, index));

  const applyRegistryPick = (entry: PublicEntityEntryDto) => {
    setForm((prev) => withRegistryPick(prev, entry));
    setRegistryPickerOpen(false);
  };

  const reset = () => {
    setForm(initialRequest);
    setShowRepresentative(hasStoredRepresentative(initialRequest));
    setError('');
    setRegistryPickerOpen(false);
  };

  const submit = async () => {
    setError('');
    const problem = validateIntervention(form, showRepresentative);
    if (problem) {
      setError(problem);
      return;
    }
    const payload = buildInterventionPayload({
      form,
      // النسخة تُفحص في التعديل فقط؛ الإنشاء يتجاهلها الخادم.
      version: initial?.version,
      showRepresentative,
      addressSuppressed,
    });

    setBusy(true);
    try {
      const response = initial
        ? await api.put<DocumentInterventionDto>(
            `/documents/${documentId}/interventions/${initial.id}`,
            payload,
          )
        : await api.post<DocumentInterventionDto>(
            `/documents/${documentId}/interventions`,
            payload,
          );
      onSaved(response.data);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div
      className="fixed inset-0 bg-black/50 z-50 flex items-end sm:items-center justify-center sm:p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={title}
    >
      <div ref={dialogRef} className="bg-white w-full sm:max-w-2xl max-h-[95vh] sm:max-h-[90vh] rounded-t-2xl sm:rounded-xl shadow-xl flex flex-col overscroll-contain">
        <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-3 border-b border-gray-200 shrink-0">
          <h3 className="text-lg font-bold text-gray-800">{title}</h3>
          <button
            type="button"
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-2xl leading-none px-2 min-h-11 min-w-11 rounded-lg focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-4 sm:px-5 py-4 overflow-y-auto space-y-4">
          <div>
            <label htmlFor="intervention-nature" className={labelCls}>
              صفة المتدخل
            </label>
            <select
              id="intervention-nature"
              name="nature"
              value={form.nature}
              onChange={(e) => changeNature(e.target.value as InterventionNature)}
              className={inputCls}
            >
              {INTERVENTION_NATURE_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>

          <fieldset className="rounded-lg border border-gray-200 p-3 sm:p-4">
            <legend className="px-1 text-sm font-bold text-emerald-800">بيانات طلب التدخل</legend>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <div>
                <label htmlFor="intervention-receipt-number" className={labelCls}>
                  رقم ورود طلب التدخل
                </label>
                <input
                  id="intervention-receipt-number"
                  name="receiptNumber"
                  autoComplete="off"
                  maxLength={100}
                  value={form.receiptNumber ?? ''}
                  onChange={(e) => setField('receiptNumber', e.target.value)}
                  placeholder="رقم ورود الطلب…"
                  className={inputCls}
                />
              </div>
              <div>
                <label htmlFor="intervention-receipt-date" className={labelCls}>
                  تاريخ ورود طلب التدخل <span className="text-red-600">*</span>
                </label>
                <input
                  id="intervention-receipt-date"
                  name="receiptDate"
                  type="text"
                  autoComplete="off"
                  value={form.receiptDate ?? ''}
                  onChange={(e) => setField('receiptDate', e.target.value)}
                  placeholder="مثال: 1/8/2026"
                  className={inputCls}
                />
              </div>
              <div>
                <label htmlFor="intervention-acceptance-date" className={labelCls}>
                  تاريخ قبول طلب التدخل
                </label>
                <input
                  id="intervention-acceptance-date"
                  name="acceptanceDate"
                  type="text"
                  autoComplete="off"
                  value={form.acceptanceDate ?? ''}
                  onChange={(e) => setField('acceptanceDate', e.target.value)}
                  placeholder="مثال: 28/7/2026"
                  className={inputCls}
                />
                <p className="mt-1 text-[11px] text-gray-500">يسبق تاريخ ورود الطلب دائمًا.</p>
              </div>
            </div>
            <div className="mt-3">
              <label htmlFor="intervention-subject" className={labelCls}>
                موضوع التدخل
              </label>
              <AutoResizeTextarea
                id="intervention-subject"
                value={form.subject ?? ''}
                onChange={(value) => setField('subject', value.slice(0, INTERVENTION_LIMITS.subject))}
                placeholder="موضوع طلب التدخل…"
                minRows={3}
                maxHeight={200}
                className={`${inputCls} resize-none`}
              />
              <p className="mt-1 text-[11px] text-gray-500">حتى {INTERVENTION_LIMITS.subject} محرفًا.</p>
            </div>
            <div className="mt-3">
              <label htmlFor="intervention-notes" className={labelCls}>
                ملاحظات
              </label>
              <AutoResizeTextarea
                id="intervention-notes"
                value={form.notes ?? ''}
                onChange={(value) => setField('notes', value.slice(0, INTERVENTION_LIMITS.notes))}
                placeholder="ملاحظات إضافية…"
                minRows={3}
                maxHeight={200}
                className={`${inputCls} resize-none`}
              />
              <p className="mt-1 text-[11px] text-gray-500">حتى {INTERVENTION_LIMITS.notes} محرفًا.</p>
            </div>
          </fieldset>

          <InterventionPartyFields
            form={form}
            setField={setField}
            showRepresentative={showRepresentative}
            addressSuppressed={addressSuppressed}
            onAddRepresentative={addRepresentative}
            onRemoveRepresentative={removeRepresentative}
            onAddHeir={addHeir}
            onSetHeir={setHeir}
            onRemoveHeir={removeHeir}
            onOpenRegistry={() => setRegistryPickerOpen(true)}
          />
        </div>

        {error && (
          <div
            role="alert"
            className="mx-4 sm:mx-5 my-2 shrink-0 rounded-lg bg-red-50 border border-red-200 text-red-800 text-sm px-3 py-2"
          >
            {error}
          </div>
        )}

        <div className="px-4 sm:px-5 py-3 border-t border-gray-200 flex flex-col-reverse sm:flex-row sm:justify-end gap-2 shrink-0">
          <button
            type="button"
            onClick={onClose}
            disabled={busy}
            className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11 disabled:opacity-60"
          >
            إلغاء
          </button>
          <button
            type="button"
            onClick={reset}
            disabled={busy}
            className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11 disabled:opacity-60"
          >
            إعادة تعيين
          </button>
          <button
            type="button"
            onClick={submit}
            disabled={busy}
            className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-5 py-2 text-sm font-bold min-h-11 disabled:opacity-60 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            {busy ? 'جارِ الحفظ…' : 'حفظ'}
          </button>
        </div>
      </div>

      {registryPickerOpen && (
        <PublicEntityPickerModal
          onClose={() => setRegistryPickerOpen(false)}
          onPick={applyRegistryPick}
        />
      )}
    </div>
  );
}
