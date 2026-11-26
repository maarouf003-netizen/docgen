import type {
  DocumentInterventionUpsertRequest,
  HeirDto,
} from '../../../types';
import { HeirsEditor } from '../HeirsEditors';
import { RepresentativeEditor } from '../RepresentativeEditor';
import {
  addressLabelOf,
  ADDRESS_TYPE_OPTIONS,
  INTERVENTION_LIMITS,
} from '../documentFormConstants';
import { inputCls, labelCls } from './interventionStyles';

type SetField = <K extends keyof DocumentInterventionUpsertRequest>(
  key: K,
  value: DocumentInterventionUpsertRequest[K],
) => void;

/**
 * أقسام طرف المتدخل حسب الصفة: شخص طبيعي (بممثله وورثته) / اعتباري / جهة عامة.
 * عرض فقط: الحالة والتحقق والحمولة في `interventionForm.ts`.
 */
export function InterventionPartyFields({
  form,
  setField,
  showRepresentative,
  addressSuppressed,
  onAddRepresentative,
  onRemoveRepresentative,
  onAddHeir,
  onSetHeir,
  onRemoveHeir,
  onOpenRegistry,
}: {
  form: DocumentInterventionUpsertRequest;
  setField: SetField;
  showRepresentative: boolean;
  addressSuppressed: boolean;
  onAddRepresentative: () => void;
  onRemoveRepresentative: () => void;
  onAddHeir: () => void;
  onSetHeir: (index: number, key: keyof HeirDto, value: string) => void;
  onRemoveHeir: (index: number) => void;
  onOpenRegistry: () => void;
}) {
  return (
    <>
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
            {!addressSuppressed ? (
              <>
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
              </>
            ) : (
              <p
                className="sm:col-span-3 text-xs text-amber-800 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2"
                role="note"
              >
                {showRepresentative
                  ? 'الممثل الشرعي موجود — عنوانه أو موطنه أو وكيله القانوني هو المعتبر، ولا حاجة لعنوان المتدخل.'
                  : 'المتدخل له ورثة — لا حاجة لعنوانه أو موطنه أو وكيله.'}
              </p>
            )}
          </div>

          <div className="mt-4 flex flex-wrap items-center gap-3">
            {!showRepresentative && (
              <button
                type="button"
                onClick={onAddRepresentative}
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
              onRemove={onRemoveRepresentative}
            />
          )}

          <div className="mt-4">
            <HeirsEditor
              idPrefix="intervention"
              heirs={form.heirs ?? []}
              onSet={onSetHeir}
              onAdd={onAddHeir}
              onRemove={onRemoveHeir}
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
                <label htmlFor="intervention-public-entity" className={labelCls}>
                  اسم الجهة العامة
                </label>
                {form.registryId != null && (
                  <span className="rounded-full bg-emerald-50 border border-emerald-100 text-emerald-700 px-2 py-0.5 text-[11px] whitespace-nowrap">
                    مرتبطة بالسجل ✓
                  </span>
                )}
              </div>
              <input
                id="intervention-public-entity"
                name="entityName"
                value={form.entityName ?? ''}
                readOnly
                aria-readonly="true"
                placeholder="اختر من السجل المرجعي…"
                className={`${inputCls} bg-gray-50 cursor-not-allowed`}
              />
              <button
                type="button"
                onClick={onOpenRegistry}
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
                name="entityBranch"
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
                name="governorate"
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
    </>
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
  value?: string | null;
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
