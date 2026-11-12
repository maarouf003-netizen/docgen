import { useCallback, useMemo, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
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
  addressLabelOf,
  ADDRESS_TYPE_OPTIONS,
  emptyIntervention,
  hasStoredRepresentative,
  INTERVENTION_LIMITS,
  INTERVENTION_NATURE_OPTIONS,
  interventionToRequest,
} from './documentFormConstants';
import { HeirsEditor } from './HeirsEditors';
import { RepresentativeEditor } from './RepresentativeEditor';

const labelCls = 'block text-xs font-bold text-gray-600 mb-1';
const inputCls =
  'w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500';

/**
 * نافذة إضافة/تعديل متدخل: قائمة «صفة المتدخل» أولًا، ثم بيانات طلب التدخل
 * (الرقم/تاريخ الورود/تاريخ القبول/الموضوع/الملاحظات بنصوص حرة وأسطر متعددة)،
 * ثم حقول الطرف حسب الصفة. الحفظ فوري عبر مسار فرعي مستقل عن حفظ نموذج الملف،
 * و«إعادة تعيين» تعيد الحالة الابتدائية دون إغلاق، و«إلغاء» تغلق دون حفظ.
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

  const setField = useCallback(
    <K extends keyof DocumentInterventionUpsertRequest>(
      key: K,
      value: DocumentInterventionUpsertRequest[K],
    ) => setForm((prev) => ({ ...prev, [key]: value })),
    [],
  );

  /** تبديل الصفة يصفّر حقول الطرف ويبقي بيانات الطلب المشتركة (كما يطبّعها الخادم). */
  const changeNature = (nature: InterventionNature) => {
    setForm((prev) => ({
      ...emptyIntervention(),
      nature,
      receiptNumber: prev.receiptNumber,
      receiptDate: prev.receiptDate,
      acceptanceDate: prev.acceptanceDate,
      subject: prev.subject,
      notes: prev.notes,
    }));
    setShowRepresentative(false);
    setError('');
  };

  const addRepresentative = () => {
    setForm((prev) => ({
      ...prev,
      // الممثل الشرعي والورثة لا يجتمعان (حارس خلفي كذلك) — تفعيل الممثل يفرّغ الورثة.
      heirs: [],
      representativeCapacity: prev.representativeCapacity || 'ولي',
      representativeAddressType: prev.representativeAddressType || 'عنوان',
    }));
    setShowRepresentative(true);
  };

  const removeRepresentative = () => {
    setForm((prev) => ({
      ...prev,
      representativeName: '',
      representativeFather: '',
      representativeFamily: '',
      representativeCapacity: '',
      representativeAddressType: '',
      representativeAddress: '',
    }));
    setShowRepresentative(false);
  };

  const addHeir = () =>
    setForm((prev) => ({
      ...prev,
      heirs: [
        ...(prev.heirs ?? []),
        { name: '', father: '', family: '', capacity: 'أصالة', addressType: 'عنوان', address: '' },
      ],
    }));

  const setHeir = (index: number, key: keyof HeirDto, value: string) =>
    setForm((prev) => ({
      ...prev,
      heirs: (prev.heirs ?? []).map((h, i) => (i === index ? { ...h, [key]: value } : h)),
    }));

  const removeHeir = (index: number) =>
    setForm((prev) => ({ ...prev, heirs: (prev.heirs ?? []).filter((_, i) => i !== index) }));

  const applyRegistryPick = (entry: PublicEntityEntryDto) => {
    setForm((prev) => ({
      ...prev,
      entityName: entry.canonicalName,
      entityBranch: entry.branchName,
      governorate: entry.governorate,
      registryId: entry.id,
    }));
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

    if (form.nature === 'public') {
      if (form.registryId == null) {
        setError('اختر جهة عامة صالحة من السجل المرجعي');
        return;
      }
    } else {
      const partyName = form.nature === 'natural'
        ? (form.name ?? '').trim()
        : (form.entityName ?? '').trim();
      if (!partyName) {
        setError(
          form.nature === 'natural'
            ? 'اسم المتدخل مطلوب'
            : 'اسم الشخص الاعتباري مطلوب',
        );
        return;
      }
    }
    if (!(form.receiptDate ?? '').trim()) {
      setError('تاريخ ورود طلب التدخل مطلوب');
      return;
    }
    if (showRepresentative && !(form.representativeName ?? '').trim()) {
      setError('اسم الممثل الشرعي مطلوب عند إضافة ممثل');
      return;
    }

    const payload: DocumentInterventionUpsertRequest = {
      nature: form.nature,
      receiptNumber: normalizeArabicDigits(form.receiptNumber ?? '').trim(),
      receiptDate: normalizeArabicDigits(form.receiptDate ?? '').trim(),
      acceptanceDate: normalizeArabicDigits(form.acceptanceDate ?? '').trim(),
      subject: (form.subject ?? '').trim(),
      notes: (form.notes ?? '').trim(),
      ...(form.nature === 'natural'
        ? {
            name: (form.name ?? '').trim(),
            father: (form.father ?? '').trim(),
            family: (form.family ?? '').trim(),
            mother: (form.mother ?? '').trim(),
            birth: (form.birth ?? '').trim(),
            register: (form.register ?? '').trim(),
            nationalId: (form.nationalId ?? '').trim(),
            addressType: form.addressType,
            address: (form.address ?? '').trim(),
            representativeName: showRepresentative ? (form.representativeName ?? '').trim() : '',
            representativeFather: showRepresentative ? (form.representativeFather ?? '').trim() : '',
            representativeFamily: showRepresentative ? (form.representativeFamily ?? '').trim() : '',
            representativeCapacity: showRepresentative ? form.representativeCapacity : '',
            representativeAddressType: showRepresentative ? form.representativeAddressType : '',
            representativeAddress: showRepresentative ? (form.representativeAddress ?? '').trim() : '',
            heirs: showRepresentative ? [] : form.heirs ?? [],
          }
        : {}),
      ...(form.nature === 'legal'
        ? {
            entityName: (form.entityName ?? '').trim(),
            registrationNumber: (form.registrationNumber ?? '').trim(),
            representedBy: (form.representedBy ?? '').trim(),
            governorate: (form.governorate ?? '').trim(),
            addressType: form.addressType,
            address: (form.address ?? '').trim(),
          }
        : {}),
      ...(form.nature === 'public'
        ? {
            entityName: (form.entityName ?? '').trim(),
            entityBranch: (form.entityBranch ?? '').trim(),
            governorate: (form.governorate ?? '').trim(),
            registryId: form.registryId,
          }
        : {}),
    };

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
                  placeholder="مثال: 5/8/2026"
                  className={inputCls}
                />
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

          {form.nature === 'natural' && (
            <fieldset className="rounded-lg border border-gray-200 p-3 sm:p-4">
              <legend className="px-1 text-sm font-bold text-emerald-800">بيانات الشخص الطبيعي</legend>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                <NamedInput id="intervention-name" label="الاسم" value={form.name} onChange={(v) => setField('name', v)} />
                <NamedInput id="intervention-father" label="اسم الأب" value={form.father} onChange={(v) => setField('father', v)} />
                <NamedInput id="intervention-family" label="النسبة" value={form.family} onChange={(v) => setField('family', v)} />
                <NamedInput id="intervention-mother" label="اسم الأم" value={form.mother} onChange={(v) => setField('mother', v)} />
                <NamedInput id="intervention-birth" label="مكان وتاريخ الولادة" value={form.birth} onChange={(v) => setField('birth', v)} maxLength={50} />
                <NamedInput id="intervention-register" label="مكان ورقم القيد" value={form.register} onChange={(v) => setField('register', v)} maxLength={100} />
                <NamedInput id="intervention-national-id" label="الرقم الوطني" value={form.nationalId} onChange={(v) => setField('nationalId', v)} maxLength={50} />
                <div>
                  <label htmlFor="intervention-address-type" className={labelCls}>
                    نوع العنوان
                  </label>
                  <select
                    id="intervention-address-type"
                    name="addressType"
                    value={form.addressType ?? 'موطن مختار'}
                    onChange={(e) => setField('addressType', e.target.value)}
                    className={inputCls}
                  >
                    {ADDRESS_TYPE_OPTIONS.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="sm:col-span-2">
                  <label htmlFor="intervention-address" className={labelCls}>
                    {addressLabelOf(form.addressType)}
                  </label>
                  <input
                    id="intervention-address"
                    name="address"
                    autoComplete="street-address"
                    maxLength={INTERVENTION_LIMITS.address}
                    value={form.address ?? ''}
                    onChange={(e) => setField('address', e.target.value)}
                    className={inputCls}
                  />
                </div>
              </div>

              <div className="mt-4 flex flex-wrap items-center gap-3">
                {!showRepresentative && (
                  <button
                    type="button"
                    onClick={addRepresentative}
                    className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
                  >
                    ＋ إضافة ممثل شرعي
                  </button>
                )}
              </div>

              {showRepresentative && (
                <RepresentativeEditor
                  idPrefix="intervention"
                  mode="address"
                  representative={form}
                  onSet={(key, value) => setField(key as keyof DocumentInterventionUpsertRequest, value)}
                  onRemove={removeRepresentative}
                />
              )}

              <div className="mt-4">
                <HeirsEditor
                  idPrefix="intervention"
                  heirs={form.heirs ?? []}
                  onSet={setHeir}
                  onAdd={addHeir}
                  onRemove={removeHeir}
                  allowAdd={!showRepresentative}
                />
              </div>
            </fieldset>
          )}

          {form.nature === 'legal' && (
            <fieldset className="rounded-lg border border-gray-200 p-3 sm:p-4">
              <legend className="px-1 text-sm font-bold text-emerald-800">بيانات الشخص الاعتباري</legend>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                <NamedInput id="intervention-entity-name" label="اسم الشخص الاعتباري" value={form.entityName} onChange={(v) => setField('entityName', v)} maxLength={200} />
                <NamedInput id="intervention-registration" label="رقم تسجيله" value={form.registrationNumber} onChange={(v) => setField('registrationNumber', v)} />
                <NamedInput id="intervention-represented-by" label="يمثلها" value={form.representedBy} onChange={(v) => setField('representedBy', v)} maxLength={200} />
                <NamedInput id="intervention-legal-governorate" label="المحافظة" value={form.governorate} onChange={(v) => setField('governorate', v)} />
                <div>
                  <label htmlFor="intervention-legal-address-type" className={labelCls}>
                    نوع العنوان
                  </label>
                  <select
                    id="intervention-legal-address-type"
                    name="addressType"
                    value={form.addressType ?? 'موطن مختار'}
                    onChange={(e) => setField('addressType', e.target.value)}
                    className={inputCls}
                  >
                    {ADDRESS_TYPE_OPTIONS.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="sm:col-span-2">
                  <label htmlFor="intervention-legal-address" className={labelCls}>
                    {addressLabelOf(form.addressType)}
                  </label>
                  <input
                    id="intervention-legal-address"
                    name="address"
                    autoComplete="street-address"
                    maxLength={INTERVENTION_LIMITS.address}
                    value={form.address ?? ''}
                    onChange={(e) => setField('address', e.target.value)}
                    className={inputCls}
                  />
                </div>
              </div>
            </fieldset>
          )}

          {form.nature === 'public' && (
            <fieldset className="rounded-lg border border-gray-200 p-3 sm:p-4">
              <legend className="px-1 text-sm font-bold text-emerald-800">بيانات الجهة العامة</legend>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                <div className="sm:col-span-1">
                  <div className="flex items-center justify-between gap-2 mb-1">
                    <label className={labelCls}>اسم الجهة العامة</label>
                    {form.registryId != null && (
                      <span className="rounded-full bg-emerald-50 border border-emerald-100 text-emerald-700 px-2 py-0.5 text-[11px] whitespace-nowrap">
                        مرتبطة بالسجل ✓
                      </span>
                    )}
                  </div>
                  <input
                    value={form.entityName ?? ''}
                    readOnly
                    aria-readonly="true"
                    placeholder="اختر من السجل المرجعي…"
                    className={`${inputCls} bg-gray-50 cursor-not-allowed`}
                  />
                  <button
                    type="button"
                    onClick={() => setRegistryPickerOpen(true)}
                    className="mt-2 border border-emerald-200 text-emerald-800 hover:bg-emerald-50 rounded-lg px-3 py-2 text-xs min-h-11"
                  >
                    {form.registryId != null ? 'تغيير من السجل…' : 'اختيار من السجل…'}
                  </button>
                </div>
                <div>
                  <label htmlFor="intervention-entity-branch" className={labelCls}>
                    الفرع
                  </label>
                  <input
                    id="intervention-entity-branch"
                    value={form.entityBranch ?? ''}
                    readOnly
                    aria-readonly="true"
                    placeholder="الفرع"
                    className={`${inputCls} bg-gray-50 cursor-not-allowed`}
                  />
                </div>
                <div>
                  <label htmlFor="intervention-entity-governorate" className={labelCls}>
                    المحافظة
                  </label>
                  <input
                    id="intervention-entity-governorate"
                    value={form.governorate ?? ''}
                    readOnly
                    aria-readonly="true"
                    placeholder="المحافظة"
                    className={`${inputCls} bg-gray-50 cursor-not-allowed`}
                  />
                </div>
              </div>
            </fieldset>
          )}
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

function NamedInput({
  id,
  label,
  value,
  onChange,
  maxLength = INTERVENTION_LIMITS.name,
}: {
  id: string;
  label: string;
  value?: string;
  onChange: (value: string) => void;
  maxLength?: number;
}) {
  return (
    <div>
      <label htmlFor={id} className={labelCls}>
        {label}
      </label>
      <input
        id={id}
        name={id}
        autoComplete="off"
        maxLength={maxLength}
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value)}
        className={inputCls}
      />
    </div>
  );
}
