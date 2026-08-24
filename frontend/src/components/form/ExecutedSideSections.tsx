import type { Dispatch, SetStateAction } from 'react';
import AutoResizeTextarea from '../AutoResizeTextarea';
import MultiAmountEditor from '../MultiAmountEditor';
import type {
  DocumentUpsertRequest,
  EntityNature,
  ExecutedHeirDto,
  ExecutedNaturalPersonDto,
  ExecutedPublicEntityDto,
  ExecutionApplicantDto,
  PartyNature,
} from '../../types';
import {
  ADDRESS_TYPE_OPTIONS,
  ENTITY_NATURE_OPTIONS,
  EXECUTED_HEIR_ADDRESS_TYPE_OPTIONS,
  EXECUTED_STATUS_OPTIONS,
  PARTY_NATURE_OPTIONS,
  REPRESENTATION_TYPES,
  addressLabelOf,
  hasRepresentative,
  heirAddressLabelOf,
  paidAmountKeys,
  paidCurrencyKeys,
  requiredAmountKeys,
  requiredCurrencyKeys,
} from './documentFormConstants';
import { ExecutedHeirsEditor } from './HeirsEditors';
import { RepresentativeEditor } from './RepresentativeEditor';
import { FormSectionTitle } from './FormSectionTitle';
import { RenewalFields } from './RenewalFields';
import { makeFieldHelpers, type FormSet } from './formFields';

export interface ExecutedSideSectionsProps {
  /** صفة الملف: executed = «الجهة العامة منفذ عليها»، deposit = «عرض وايداع». */
  side: 'executed' | 'deposit';
  form: DocumentUpsertRequest;
  set: FormSet;
  isEdit: boolean;
  showRequiredAmount: boolean;
  setShowRequiredAmount: Dispatch<SetStateAction<boolean>>;
  requiredAmountSlots: number;
  setRequiredAmountSlots: (n: number) => void;
  executionApplicants: ExecutionApplicantDto[];
  onApplicantSet: (i: number, key: keyof ExecutionApplicantDto, value: string) => void;
  onApplicantAdd: (nature?: PartyNature) => void;
  onApplicantRemove: (i: number) => void;
  onApplicantHeirSet: (i: number, hi: number, key: keyof ExecutedHeirDto, value: string) => void;
  onApplicantHeirAdd: (i: number) => void;
  onApplicantHeirRemove: (i: number, hi: number) => void;
  onApplicantRepActivate: (i: number) => void;
  onApplicantRepRemove: (i: number) => void;
  executedPublicEntities: ExecutedPublicEntityDto[];
  onEntitySet: (i: number, key: keyof ExecutedPublicEntityDto, value: string) => void;
  onEntityAdd: (nature?: EntityNature) => void;
  onEntityRemove: (i: number) => void;
  /** فتح نافذة اختيار الجهة العامة من السجل المرجعي لصف المنفذ رقم i (اختياري). */
  onPickRegistry?: (i: number) => void;
  executedNaturalPersons: ExecutedNaturalPersonDto[];
  onPersonSet: (i: number, key: keyof ExecutedNaturalPersonDto, value: string) => void;
  onPersonAdd: () => void;
  onPersonRemove: (i: number) => void;
  onPersonHeirSet: (i: number, hi: number, key: keyof ExecutedHeirDto, value: string) => void;
  onPersonHeirAdd: (i: number) => void;
  onPersonHeirRemove: (i: number, hi: number) => void;
  onPersonRepActivate: (i: number) => void;
  onPersonRepRemove: (i: number) => void;
  /** عدد خانات المبلغ المدفوع المعروضة (1 إلى 3) في وضع «منفذ عليها»/«عرض وايداع». */
  paidAmountSlots: number;
  setPaidAmountSlots: (n: number) => void;
  /** هل كان الملف مشطوبًا قبل التعديل؟ (يكشف انتقال مشطوب ← متداول يُظهر حقول التجديد). */
  wasOriginallyStruckOff?: boolean;
}

/** أقسام عائلة وضع «منفذ عليه» في نموذج الملف (الجهة العامة منفذ عليها / عرض وايداع). */
export function ExecutedSideSections({
  side,
  form,
  set,
  isEdit,
  showRequiredAmount,
  setShowRequiredAmount,
  requiredAmountSlots,
  setRequiredAmountSlots,
  executionApplicants,
  onApplicantSet,
  onApplicantAdd,
  onApplicantRemove,
  onApplicantHeirSet,
  onApplicantHeirAdd,
  onApplicantHeirRemove,
  onApplicantRepActivate,
  onApplicantRepRemove,
  executedPublicEntities,
  onEntitySet,
  onEntityAdd,
  onEntityRemove,
  onPickRegistry,
  executedNaturalPersons,
  onPersonSet,
  onPersonAdd,
  onPersonRemove,
  onPersonHeirSet,
  onPersonHeirAdd,
  onPersonHeirRemove,
  onPersonRepActivate,
  onPersonRepRemove,
  paidAmountSlots,
  setPaidAmountSlots,
  wasOriginallyStruckOff,
}: ExecutedSideSectionsProps) {
  const { field } = makeFieldHelpers(form, set);
  const isDeposit = side === 'deposit';
  const applicantLabel = isDeposit ? 'طالب العرض' : 'طالب التنفيذ';
  const applicantButtonLabel = isDeposit ? 'طالب عرض' : 'طالب التنفيذ';

  return (
    <>
      <FormSectionTitle title="📄 بيانات السند التنفيذي" />
      <div className="grid md:grid-cols-3 gap-4 items-end">
        {field('المحكمة مصدرة القرار', 'contractType')}
        {field('رقم القرار', 'contractNumber')}
        {field('تاريخ القرار', 'contractDate')}
      </div>
      <div className="mt-4 rounded-lg bg-gray-50 border border-gray-200 p-4">
        <div>
          <label htmlFor="inclusionText" className="block text-xs font-bold text-gray-600 mb-1">
            المتضمن
          </label>
          <AutoResizeTextarea
            id="inclusionText"
            value={form.inclusionText ?? ''}
            onChange={(v) => set('inclusionText', v)}
            placeholder="أدخل خلاصة القرار باختصار..."
            minRows={1}
            maxHeight={200}
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>
        <div className="mt-4 rounded-lg bg-white border border-gray-200 p-4">
          <button
            type="button"
            onClick={() => setShowRequiredAmount((v) => !v)}
            className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 shrink-0 min-h-11"
          >
            {showRequiredAmount ? '− إخفاء المبلغ' : '➕ إضافة مبلغ'}
          </button>
          {showRequiredAmount && (
            <div className="mt-4">
              <MultiAmountEditor
                idPrefix="required"
                amountKeys={requiredAmountKeys}
                currencyKeys={requiredCurrencyKeys}
                values={form}
                onSet={(k, v) => set(k as keyof DocumentUpsertRequest, v)}
                slots={requiredAmountSlots}
                onSlotsChange={setRequiredAmountSlots}
                firstLabel={isDeposit ? 'المبلغ المعروض' : 'المبلغ المطلوب دفعه من الجهة العامة'}
                otherLabel={(i) => (isDeposit ? `المبلغ المعروض ${i + 1}` : `المبلغ المطلوب ${i + 1}`)}
              />
            </div>
          )}
        </div>
      </div>

      <FormSectionTitle title={`👤 ${applicantLabel}`} />
      {executionApplicants.map((a, i) => {
        const aHasRep = hasRepresentative(a);
        const hasEstate = a.representationType === 'إضافة لتركة' || a.representationType === 'أصالة وإضافة';
        const aIsLegal = a.nature === 'legal';
        return (
          <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
            <div className="flex justify-between items-center mb-3">
              <span className="font-medium text-gray-700 text-sm">{applicantLabel} {i + 1}</span>
              {executionApplicants.length > 1 && (
                <button type="button" onClick={() => onApplicantRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
                  ✖ حذف
                </button>
              )}
            </div>
            <div className="grid md:grid-cols-3 gap-3 mb-3">
              <select
                aria-label="نوع الطرف"
                value={a.nature ?? 'natural'}
                onChange={(e) => onApplicantSet(i, 'nature', e.target.value)}
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
              >
                {PARTY_NATURE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>
            {aIsLegal ? (
              <div className="grid md:grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">الشخص الاعتباري</label>
                  <input value={a.name ?? ''} onChange={(e) => onApplicantSet(i, 'name', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">رقم تسجيله</label>
                  <input value={a.registrationNumber ?? ''} onChange={(e) => onApplicantSet(i, 'registrationNumber', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">يمثلها</label>
                  <input value={a.representedBy ?? ''} onChange={(e) => onApplicantSet(i, 'representedBy', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">نوع العنوان</label>
                  <select value={a.addressType ?? 'موطن مختار'} onChange={(e) => onApplicantSet(i, 'addressType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500">
                    {ADDRESS_TYPE_OPTIONS.map((o) => (
                      <option key={o.value} value={o.value}>{o.label}</option>
                    ))}
                  </select>
                </div>
                <div className="md:col-span-2">
                  <label className="block text-xs font-bold text-gray-600 mb-1">{addressLabelOf(a.addressType)}</label>
                  <input value={a.address ?? ''} onChange={(e) => onApplicantSet(i, 'address', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
              </div>
            ) : (
              <>
                <div className="grid md:grid-cols-3 gap-3">
                  {([['name', 'الاسم'], ['father', 'اسم الأب'], ['family', 'النسبة']] as const).map(([k, label]) => (
                    <div key={k}>
                      <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                      <input value={a[k] ?? ''} onChange={(e) => onApplicantSet(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                    </div>
                  ))}
                </div>
                <div className="grid md:grid-cols-3 gap-3 mt-3">
                  {!aHasRep && (
                    <div className="md:col-span-2">
                      <label className="block text-xs font-bold text-gray-600 mb-1">الوكيل القانوني</label>
                      <input value={a.legalRepresentative ?? ''} onChange={(e) => onApplicantSet(i, 'legalRepresentative', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                    </div>
                  )}
                  <div className={aHasRep ? 'md:col-span-3' : undefined}>
                    <label className="block text-xs font-bold text-gray-600 mb-1">نوع التمثيل</label>
                    <div className="flex items-center gap-2">
                      <select value={a.representationType ?? 'أصالة'} onChange={(e) => onApplicantSet(i, 'representationType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none">
                        {REPRESENTATION_TYPES.map((o) => (
                          <option key={o}>{o}</option>
                        ))}
                      </select>
                      {!aHasRep && (
                        <button
                          type="button"
                          onClick={() => onApplicantRepActivate(i)}
                          className="shrink-0 bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
                        >
                          ＋ إضافة ممثل شرعي
                        </button>
                      )}
                    </div>
                  </div>
                </div>
                {hasEstate && (
                  <div className="mt-4 rounded-lg bg-white border border-gray-200 p-4 grid md:grid-cols-3 gap-3">
                    {([['deceasedName', 'اسم المورث المتوفى'], ['deceasedFather', 'اسم أب المورث'], ['deceasedFamily', 'نسبة المورث']] as const).map(([k, label]) => (
                      <div key={k}>
                        <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                        <input value={a[k] ?? ''} onChange={(e) => onApplicantSet(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                      </div>
                    ))}
                    <div className="md:col-span-3">
                      <ExecutedHeirsEditor
                        idPrefix={`applicant-${i}`}
                        heirs={a.heirs ?? []}
                        onSet={(hi, k, v) => onApplicantHeirSet(i, hi, k, v)}
                        onAdd={() => onApplicantHeirAdd(i)}
                        onRemove={(hi) => onApplicantHeirRemove(i, hi)}
                        allowAdd={isEdit}
                      />
                    </div>
                  </div>
                )}
                {aHasRep && (
                  <RepresentativeEditor
                    idPrefix={`applicant-${i}`}
                    mode="legalRep"
                    representative={a}
                    onSet={(key, value) => onApplicantSet(i, key as keyof ExecutionApplicantDto, value)}
                    onRemove={() => onApplicantRepRemove(i)}
                  />
                )}
              </>
            )}
          </div>
        );
      })}
      <div className="flex flex-wrap gap-3 items-center">
        <button
          type="button"
          onClick={() => onApplicantAdd('natural')}
          className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
        >
          ＋ إضافة {applicantButtonLabel} (شخص طبيعي)
        </button>
        <button
          type="button"
          onClick={() => onApplicantAdd('legal')}
          className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
        >
          ＋ إضافة {applicantButtonLabel} (شخص اعتباري)
        </button>
      </div>

      <FormSectionTitle title="🏛️ المنفذ عليه" />
      {executedPublicEntities.map((e, i) => {
        const eIsLegal = e.nature === 'legal';
        return (
          <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
            <div className="flex justify-between items-center mb-3">
              <span className="font-medium text-gray-700 text-sm">
                {eIsLegal ? 'شخص اعتباري' : 'جهة عامة'} {i + 1}
              </span>
              {executedPublicEntities.length > 1 && (
                <button type="button" onClick={() => onEntityRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
                  ✖ حذف
                </button>
              )}
            </div>
            <div className="grid md:grid-cols-3 gap-3 mb-3">
              <select
                aria-label="نوع الطرف"
                value={e.nature ?? 'public'}
                onChange={(ev) => onEntitySet(i, 'nature', ev.target.value)}
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
              >
                {ENTITY_NATURE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>
            {eIsLegal ? (
              <div className="grid md:grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">الشخص الاعتباري</label>
                  <input value={e.entityName ?? ''} onChange={(ev) => onEntitySet(i, 'entityName', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">رقم تسجيله</label>
                  <input value={e.registrationNumber ?? ''} onChange={(ev) => onEntitySet(i, 'registrationNumber', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">يمثلها</label>
                  <input value={e.representedBy ?? ''} onChange={(ev) => onEntitySet(i, 'representedBy', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">المحافظة</label>
                  <input value={e.governorate ?? ''} onChange={(ev) => onEntitySet(i, 'governorate', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">نوع العنوان</label>
                  <select value={e.addressType ?? 'موطن مختار'} onChange={(ev) => onEntitySet(i, 'addressType', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500">
                    {ADDRESS_TYPE_OPTIONS.map((o) => (
                      <option key={o.value} value={o.value}>{o.label}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">{addressLabelOf(e.addressType)}</label>
                  <input value={e.address ?? ''} onChange={(ev) => onEntitySet(i, 'address', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
              </div>
            ) : (
              <div className="grid md:grid-cols-3 gap-3">
                <div>
                  <div className="flex items-center justify-between gap-2 mb-1">
                    <label className="block text-xs font-bold text-gray-600">اسم الجهة العامة</label>
                    {e.registryId != null && (
                      <span className="rounded-full bg-emerald-50 border border-emerald-100 text-emerald-700 px-2 py-0.5 text-[11px] whitespace-nowrap">
                        مرتبطة بالسجل ✓
                      </span>
                    )}
                  </div>
                  <input value={e.entityName ?? ''} onChange={(ev) => onEntitySet(i, 'entityName', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                  {onPickRegistry && (
                    <button
                      type="button"
                      onClick={() => onPickRegistry(i)}
                      className="mt-1.5 border border-emerald-200 text-emerald-800 hover:bg-emerald-50 rounded-lg px-3 py-2 text-xs min-h-11"
                    >
                      اختيار من السجل…
                    </button>
                  )}
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">الفرع</label>
                  <input value={e.entityBranch ?? ''} onChange={(ev) => onEntitySet(i, 'entityBranch', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">المحافظة</label>
                  <input value={e.governorate ?? ''} onChange={(ev) => onEntitySet(i, 'governorate', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
              </div>
            )}
          </div>
        );
      })}
      <div className="flex flex-wrap gap-3 items-center">
        <button
          type="button"
          onClick={() => onEntityAdd('public')}
          className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
        >
          ＋ إضافة جهة عامة
        </button>
        <button
          type="button"
          onClick={() => onEntityAdd('legal')}
          className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
        >
          ＋ إضافة شخص اعتباري
        </button>
        <button
          type="button"
          onClick={onPersonAdd}
          className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
        >
          ＋ إضافة شخص طبيعي
        </button>
      </div>

      {executedNaturalPersons.map((p, i) => {
        const pHasRep = hasRepresentative(p);
        const hasEstate = p.representationType === 'إضافة لتركة' || p.representationType === 'أصالة وإضافة';
        return (
          <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
            <div className="flex justify-between items-center mb-3">
              <span className="font-medium text-gray-700 text-sm">شخص طبيعي {i + 1}</span>
              {executedNaturalPersons.length > 1 && (
                <button type="button" onClick={() => onPersonRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
                  ✖ حذف
                </button>
              )}
            </div>
            <div className="grid md:grid-cols-3 gap-3">
              {([['name', 'الاسم'], ['father', 'اسم الأب'], ['family', 'النسبة']] as const).map(([k, label]) => (
                <div key={k}>
                  <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                  <input value={p[k] ?? ''} onChange={(e) => onPersonSet(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
              ))}
            </div>
            <div className="grid md:grid-cols-3 gap-3 mt-3">
              <div>
                <label className="block text-xs font-bold text-gray-600 mb-1">نوع العنوان</label>
                <select value={p.addressType ?? 'عنوان'} onChange={(e) => onPersonSet(i, 'addressType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none">
                  {EXECUTED_HEIR_ADDRESS_TYPE_OPTIONS.map((o) => (
                    <option key={o.value} value={o.value}>{o.label}</option>
                  ))}
                </select>
              </div>
              <div className="md:col-span-1">
                <label className="block text-xs font-bold text-gray-600 mb-1">
                  {heirAddressLabelOf(p.addressType)}
                </label>
                <input value={p.addressOrRepresentative ?? ''} onChange={(e) => onPersonSet(i, 'addressOrRepresentative', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
              </div>
              <div>
                <label className="block text-xs font-bold text-gray-600 mb-1">نوع التمثيل</label>
                <div className="flex items-center gap-2">
                  <select value={p.representationType ?? 'أصالة'} onChange={(e) => onPersonSet(i, 'representationType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none">
                    {REPRESENTATION_TYPES.map((o) => (
                      <option key={o}>{o}</option>
                    ))}
                  </select>
                  {!pHasRep && (
                    <button
                      type="button"
                      onClick={() => onPersonRepActivate(i)}
                      className="shrink-0 bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
                    >
                      ＋ إضافة ممثل شرعي
                    </button>
                  )}
                </div>
              </div>
            </div>
            {hasEstate && (
              <div className="mt-4 rounded-lg bg-white border border-gray-200 p-4 grid md:grid-cols-3 gap-3">
                {([['deceasedName', 'اسم المورث المتوفى'], ['deceasedFather', 'اسم أب المورث'], ['deceasedFamily', 'نسبة المورث']] as const).map(([k, label]) => (
                  <div key={k}>
                    <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                    <input value={p[k] ?? ''} onChange={(e) => onPersonSet(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                  </div>
                ))}
                <div className="md:col-span-3">
                  <ExecutedHeirsEditor
                    idPrefix={`executed-person-${i}`}
                    heirs={p.heirs ?? []}
                    onSet={(hi, k, v) => onPersonHeirSet(i, hi, k, v)}
                    onAdd={() => onPersonHeirAdd(i)}
                    onRemove={(hi) => onPersonHeirRemove(i, hi)}
                    allowAdd={isEdit}
                  />
                </div>
              </div>
            )}
            {pHasRep && (
              <RepresentativeEditor
                idPrefix={`executed-person-${i}`}
                mode="address"
                representative={p}
                onSet={(key, value) => onPersonSet(i, key as keyof ExecutedNaturalPersonDto, value)}
                onRemove={() => onPersonRepRemove(i)}
              />
            )}
          </div>
        );
      })}

      <FormSectionTitle title="📋 حالة الملف" />
      <div className="rounded-lg bg-gray-50 border border-gray-200 p-4">
        <div className="grid md:grid-cols-3 gap-4 items-end">
          <div>
            <label htmlFor="executedStatus" className="block text-xs font-bold text-gray-600 mb-1">
              الحالة
            </label>
            <select
              id="executedStatus"
              value={form.executedStatus ?? ''}
              onChange={(e) => set('executedStatus', e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {EXECUTED_STATUS_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>
        </div>
        {form.executedStatus === 'منفذ' && (
          <div className="mt-4 rounded-lg bg-white border border-gray-200 p-4">
            {isDeposit ? (
              <div className="grid gap-4">
                <MultiAmountEditor
                  idPrefix="paid-deposit"
                  amountKeys={paidAmountKeys}
                  currencyKeys={paidCurrencyKeys}
                  values={form}
                  onSet={(k, v) => set(k as keyof DocumentUpsertRequest, v)}
                  slots={paidAmountSlots}
                  onSlotsChange={setPaidAmountSlots}
                  firstLabel="المبلغ المودع"
                  otherLabel={(i) => `المبلغ المودع ${i + 1}`}
                />
                {field('تاريخ ايداعه حساب الجهة العامة', 'executedDepositDate', 'مثال: 1/8/2026')}
              </div>
            ) : (
              <>
                <label htmlFor="executedDescription" className="block text-xs font-bold text-gray-600 mb-1">
                  كيفية تنفيذ الملف
                </label>
                <AutoResizeTextarea
                  id="executedDescription"
                  value={form.executedDescription ?? ''}
                  onChange={(v) => set('executedDescription', v)}
                  placeholder="كيف تم تنفيذ الملف..."
                  minRows={2}
                  maxHeight={200}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
                <div className="mt-4">
                  <MultiAmountEditor
                    idPrefix="paid-executed"
                    amountKeys={paidAmountKeys}
                    currencyKeys={paidCurrencyKeys}
                    values={form}
                    onSet={(k, v) => set(k as keyof DocumentUpsertRequest, v)}
                    slots={paidAmountSlots}
                    onSlotsChange={setPaidAmountSlots}
                    firstLabel="المبلغ الذي دفعته الجهة العامة"
                    otherLabel={(i) => `المبلغ الذي دفعته الجهة العامة ${i + 1}`}
                  />
                </div>
              </>
            )}
          </div>
        )}
        {form.executedStatus === 'مشطوب' && (
          <div className="mt-4 grid md:grid-cols-3 gap-4 items-end">
            {field('تاريخ الشطب', 'struckOffDate', 'مثال: 1/8/2026')}
          </div>
        )}
        {wasOriginallyStruckOff && !(form.executedStatus ?? '') && (
          <div className="mt-4">
            <RenewalFields value={form} onSet={(key, value) => set(key, value)} />
          </div>
        )}
      </div>
    </>
  );
}
