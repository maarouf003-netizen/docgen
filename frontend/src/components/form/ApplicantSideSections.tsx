import type { Dispatch, SetStateAction } from 'react';
import AutoResizeTextarea from '../AutoResizeTextarea';
import MultiAmountEditor from '../MultiAmountEditor';
import type { DocumentUpsertRequest, GuarantorDto, HeirDto, PartyNature, RealEstateDto } from '../../types';
import {
  ADDRESS_TYPE_OPTIONS,
  MAX_ESTATES,
  MAX_GUARANTORS,
  PARTY_NATURE_OPTIONS,
  SHARE_TYPES,
  addressLabelOf,
  bankingAmountKeys,
  bankingCurrencyKeys,
  hasRepresentative,
  ordinaryAmountKeys,
  ordinaryCurrencyKeys,
} from './documentFormConstants';
import { HeirsEditor } from './HeirsEditors';
import { RepresentativeEditor } from './RepresentativeEditor';
import { FormSectionTitle } from './FormSectionTitle';
import { makeFieldHelpers, type FormSet } from './formFields';

/** مطابقة مفاتيح محرر الممثل العام (بلا بادئة) إلى مفاتيح الممثل الشرعي للمقترض (ببادئة borrower). */
const BORROWER_REP_KEYS: Record<string, keyof DocumentUpsertRequest> = {
  representativeName: 'borrowerRepresentativeName',
  representativeFather: 'borrowerRepresentativeFather',
  representativeFamily: 'borrowerRepresentativeFamily',
  representativeCapacity: 'borrowerRepresentativeCapacity',
  representativeAddressType: 'borrowerRepresentativeAddressType',
  representativeAddress: 'borrowerRepresentativeAddress',
};

export interface ApplicantSideSectionsProps {
  form: DocumentUpsertRequest;
  set: FormSet;
  isOrdinary: boolean;
  guarantorLabel: string;
  remainingGuarantors: number;
  showInclusionAmount: boolean;
  setShowInclusionAmount: Dispatch<SetStateAction<boolean>>;
  ordinaryAmountSlots: number;
  setOrdinaryAmountSlots: (n: number) => void;
  bankingAmountSlots: number;
  setBankingAmountSlots: (n: number) => void;
  borrowerHeirs: HeirDto[];
  onBorrowerHeirSet: (i: number, key: keyof HeirDto, value: string) => void;
  onBorrowerHeirAdd: () => void;
  onBorrowerHeirRemove: (i: number) => void;
  guarantors: GuarantorDto[];
  onGuarantorSet: (i: number, key: keyof GuarantorDto, value: string) => void;
  onGuarantorHeirSet: (gi: number, hi: number, key: keyof HeirDto, value: string) => void;
  onGuarantorHeirAdd: (gi: number) => void;
  onGuarantorHeirRemove: (gi: number, hi: number) => void;
  onGuarantorAdd: (nature?: PartyNature) => void;
  onGuarantorRemove: (i: number) => void;
  onBorrowerRepActivate: () => void;
  onBorrowerRepRemove: () => void;
  onGuarantorRepActivate: (i: number) => void;
  onGuarantorRepRemove: (i: number) => void;
  estates: RealEstateDto[];
  onEstateSet: (i: number, key: keyof RealEstateDto, value: string) => void;
  onEstateRemove: (i: number) => void;
  onOwnerToggle: (i: number, name: string) => void;
  onEstateAdd: () => void;
  ownerOptions: () => string[];
}

/** أقسام وضع «الجهة العامة طالبة التنفيذ» (المصرفي/العادي) في نموذج الملف. */
export function ApplicantSideSections({
  form,
  set,
  isOrdinary,
  guarantorLabel,
  remainingGuarantors,
  showInclusionAmount,
  setShowInclusionAmount,
  ordinaryAmountSlots,
  setOrdinaryAmountSlots,
  bankingAmountSlots,
  setBankingAmountSlots,
  borrowerHeirs,
  onBorrowerHeirSet,
  onBorrowerHeirAdd,
  onBorrowerHeirRemove,
  guarantors,
  onGuarantorSet,
  onGuarantorHeirSet,
  onGuarantorHeirAdd,
  onGuarantorHeirRemove,
  onGuarantorAdd,
  onGuarantorRemove,
  onBorrowerRepActivate,
  onBorrowerRepRemove,
  onGuarantorRepActivate,
  onGuarantorRepRemove,
  estates,
  onEstateSet,
  onEstateRemove,
  onOwnerToggle,
  onEstateAdd,
  ownerOptions,
}: ApplicantSideSectionsProps) {
  const isBanking = !isOrdinary;
  const { field, selectField, optionSelectField } = makeFieldHelpers(form, set);

  const borrowerHasHeirs = borrowerHeirs.length > 0;
  const borrowerHasRep = hasRepresentative({
    representativeName: form.borrowerRepresentativeName,
    representativeFather: form.borrowerRepresentativeFather,
    representativeFamily: form.borrowerRepresentativeFamily,
    representativeCapacity: form.borrowerRepresentativeCapacity,
  });

  return (
    <>
      <FormSectionTitle title="📄 بيانات السند التنفيذي" />
      <div className="grid md:grid-cols-4 gap-4">
        {selectField('نوع السند', 'contractTypeSelector', ['مصرفي', 'عادي'], form.contractTypeSelector ?? 'مصرفي', (v) => set('contractTypeSelector', v))}
        {field(isOrdinary ? 'المحكمة مصدرة القرار' : 'نوع العقد', 'contractType')}
        {field(isOrdinary ? 'رقم القرار' : 'رقم العقد', 'contractNumber')}
        {field(isOrdinary ? 'تاريخ القرار' : 'تاريخ العقد', 'contractDate')}
      </div>

      {isOrdinary && (
        <div className="mt-4 rounded-lg bg-gray-50 border border-gray-200 p-4">
          <div className="flex gap-3 items-end">
            <div className="flex-1">
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
            <button
              type="button"
              onClick={() => setShowInclusionAmount((v) => !v)}
              className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 shrink-0 min-h-11"
            >
              {showInclusionAmount ? '− إخفاء المبلغ' : '➕ إضافة مبلغ'}
            </button>
          </div>
          {showInclusionAmount && (
            <div className="mt-4 rounded-lg bg-white border border-gray-200 p-4">
              <MultiAmountEditor
                idPrefix="inclusion"
                amountKeys={ordinaryAmountKeys}
                currencyKeys={ordinaryCurrencyKeys}
                values={form}
                onSet={(k, v) => set(k as keyof DocumentUpsertRequest, v)}
                slots={ordinaryAmountSlots}
                onSlotsChange={setOrdinaryAmountSlots}
                firstLabel="المبلغ"
                otherLabel={(i) => `المبلغ ${i + 1}`}
              />
            </div>
          )}
        </div>
      )}

      {isBanking && (
        <div className="mt-4 rounded-lg bg-gray-50 border border-gray-200 p-4">
          <MultiAmountEditor
            idPrefix="banking"
            amountKeys={bankingAmountKeys}
            currencyKeys={bankingCurrencyKeys}
            values={form}
            onSet={(k, v) => set(k as keyof DocumentUpsertRequest, v)}
            slots={bankingAmountSlots}
            onSlotsChange={setBankingAmountSlots}
            firstLabel="المبلغ المطالب به"
            otherLabel={(i) => (i === 1 ? 'المبلغ الثاني' : 'المبلغ الثالث')}
          />
        </div>
      )}

      <FormSectionTitle title={isOrdinary ? '👤 بيانات المنفذ عليه' : '👤 بيانات المقترض'} />
      <div className="grid md:grid-cols-3 gap-4">
        <select
          id="borrowerNature"
          aria-label="نوع الطرف"
          value={form.borrowerNature ?? 'natural'}
          onChange={(e) => set('borrowerNature', e.target.value)}
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
        >
          {PARTY_NATURE_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>{o.label}</option>
          ))}
        </select>
      </div>
      {form.borrowerNature === 'legal' ? (
        <div className="grid md:grid-cols-3 gap-4 mt-4">
          {field('الشخص الاعتباري', 'borrowerName')}
          {field('رقم تسجيله', 'borrowerRegistrationNumber')}
          {field('يمثلها', 'borrowerRepresentedBy')}
          {optionSelectField('نوع العنوان', 'borrowerAddressType', ADDRESS_TYPE_OPTIONS, form.borrowerAddressType ?? 'موطن مختار', (v) => set('borrowerAddressType', v))}
          <div className="md:col-span-2">{field(addressLabelOf(form.borrowerAddressType), 'borrowerAddress')}</div>
        </div>
      ) : (
        <>
          <div className="grid md:grid-cols-5 gap-4 mt-4">
            {field('الاسم', 'borrowerName')}
            {field('اسم الأب', 'borrowerFather')}
            {field('النسبة', 'borrowerFamily')}
            {field('اسم الأم', 'borrowerMother')}
            {field('مكان وتاريخ الولادة', 'borrowerBirth')}
            {field('مكان ورقم القيد', 'borrowerRegister')}
            {field('الرقم الوطني', 'borrowerNationalId')}
            {!borrowerHasHeirs && !borrowerHasRep && (
              <>
                {optionSelectField('نوع العنوان', 'borrowerAddressType', ADDRESS_TYPE_OPTIONS, form.borrowerAddressType ?? 'موطن مختار', (v) => set('borrowerAddressType', v))}
                <div className="md:col-span-2">{field(addressLabelOf(form.borrowerAddressType), 'borrowerAddress')}</div>
              </>
            )}
          </div>
          <HeirsEditor
            idPrefix="borrower"
            heirs={borrowerHeirs}
            onSet={onBorrowerHeirSet}
            onAdd={onBorrowerHeirAdd}
            onRemove={onBorrowerHeirRemove}
            hideAddress={borrowerHasRep}
          />
          {!borrowerHasRep ? (
            <div className="mt-3 flex justify-end">
              <button
                type="button"
                onClick={onBorrowerRepActivate}
                className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
              >
                ＋ إضافة ممثل شرعي
              </button>
            </div>
          ) : (
            <RepresentativeEditor
              idPrefix="borrower"
              mode="address"
              representative={{
                representativeName: form.borrowerRepresentativeName,
                representativeFather: form.borrowerRepresentativeFather,
                representativeFamily: form.borrowerRepresentativeFamily,
                representativeCapacity: form.borrowerRepresentativeCapacity,
                representativeAddressType: form.borrowerRepresentativeAddressType,
                representativeAddress: form.borrowerRepresentativeAddress,
              }}
              onSet={(key, value) => set(BORROWER_REP_KEYS[key] ?? 'borrowerRepresentativeName', value)}
              onRemove={onBorrowerRepRemove}
            />
          )}
        </>
      )}

      <FormSectionTitle title={isOrdinary ? '👥 المنفذ عليهم الآخرون' : '👥 الكفلاء'} />
      {guarantors.map((g, i) => {
        const gHasHeirs = (g.heirs ?? []).length > 0;
        const gHasRep = hasRepresentative(g);
        const gIsLegal = g.nature === 'legal';
        return (
          <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
            <div className="flex justify-between items-center mb-3">
              <span className="font-medium text-gray-700 text-sm">
                {guarantorLabel} {isOrdinary ? i + 2 : i + 1}
              </span>
              {guarantors.length > 1 && (
                <button type="button" onClick={() => onGuarantorRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
                  ✖ حذف
                </button>
              )}
            </div>
            <div className="grid md:grid-cols-3 gap-3 mb-3">
              <select
                aria-label="نوع الطرف"
                value={g.nature ?? 'natural'}
                onChange={(e) => onGuarantorSet(i, 'nature', e.target.value)}
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
              >
                {PARTY_NATURE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>
            {gIsLegal ? (
              <div className="grid md:grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">الشخص الاعتباري</label>
                  <input value={g.name ?? ''} onChange={(e) => onGuarantorSet(i, 'name', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">رقم تسجيله</label>
                  <input value={g.registrationNumber ?? ''} onChange={(e) => onGuarantorSet(i, 'registrationNumber', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">يمثلها</label>
                  <input value={g.representedBy ?? ''} onChange={(e) => onGuarantorSet(i, 'representedBy', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">نوع العنوان</label>
                  <select value={g.addressType ?? 'موطن مختار'} onChange={(e) => onGuarantorSet(i, 'addressType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500">
                    {ADDRESS_TYPE_OPTIONS.map((o) => (
                      <option key={o.value} value={o.value}>{o.label}</option>
                    ))}
                  </select>
                </div>
                <div className="md:col-span-2">
                  <label className="block text-xs font-bold text-gray-600 mb-1">{addressLabelOf(g.addressType)}</label>
                  <input value={g.address ?? ''} onChange={(e) => onGuarantorSet(i, 'address', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
              </div>
            ) : (
              <>
                <div className="grid md:grid-cols-5 gap-3">
                  {([
                    ['name', 'الاسم'],
                    ['father', 'اسم الأب'],
                    ['family', 'النسبة'],
                    ['mother', 'اسم الأم'],
                    ['birth', 'مكان وتاريخ الولادة'],
                    ['register', 'مكان ورقم القيد'],
                    ['nationalId', 'الرقم الوطني'],
                  ] as const).map(([k, label]) => (
                    <div key={k}>
                      <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                      <input value={g[k] ?? ''} onChange={(e) => onGuarantorSet(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                    </div>
                  ))}
                  {!gHasHeirs && !gHasRep && (
                    <>
                      <div>
                        <label className="block text-xs font-bold text-gray-600 mb-1">نوع العنوان</label>
                        <select value={g.addressType ?? 'موطن مختار'} onChange={(e) => onGuarantorSet(i, 'addressType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none">
                          {ADDRESS_TYPE_OPTIONS.map((o) => (
                            <option key={o.value} value={o.value}>{o.label}</option>
                          ))}
                        </select>
                      </div>
                      <div className="md:col-span-2">
                        <label className="block text-xs font-bold text-gray-600 mb-1">{addressLabelOf(g.addressType)}</label>
                        <input value={g.address ?? ''} onChange={(e) => onGuarantorSet(i, 'address', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                      </div>
                    </>
                  )}
                </div>
                <HeirsEditor
                  idPrefix={`guarantor-${i}`}
                  heirs={g.heirs ?? []}
                  onSet={(hi, k, v) => onGuarantorHeirSet(i, hi, k, v)}
                  onAdd={() => onGuarantorHeirAdd(i)}
                  onRemove={(hi) => onGuarantorHeirRemove(i, hi)}
                  hideAddress={gHasRep}
                />
                {!gHasRep ? (
                  <div className="mt-3 flex justify-end">
                    <button
                      type="button"
                      onClick={() => onGuarantorRepActivate(i)}
                      className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
                    >
                      ＋ إضافة ممثل شرعي
                    </button>
                  </div>
                ) : (
                  <RepresentativeEditor
                    idPrefix={`guarantor-${i}`}
                    mode="address"
                    representative={g}
                    onSet={(key, value) => onGuarantorSet(i, key as keyof GuarantorDto, value)}
                    onRemove={() => onGuarantorRepRemove(i)}
                  />
                )}
              </>
            )}
          </div>
        );
      })}
      <div className="flex gap-4 items-center flex-wrap">
        <button
          type="button"
          onClick={() => onGuarantorAdd('natural')}
          disabled={guarantors.length >= MAX_GUARANTORS}
          className="bg-emerald-700 hover:bg-emerald-600 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
        >
          {guarantors.length >= MAX_GUARANTORS ? '🛑 الحد الأقصى' : `➕ إضافة ${guarantorLabel} (شخص طبيعي)`}
        </button>
        <button
          type="button"
          onClick={() => onGuarantorAdd('legal')}
          disabled={guarantors.length >= MAX_GUARANTORS}
          className="bg-emerald-700 hover:bg-emerald-600 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
        >
          {guarantors.length >= MAX_GUARANTORS ? '🛑 الحد الأقصى' : `➕ إضافة ${guarantorLabel} (شخص اعتباري)`}
        </button>
        <span className="text-xs text-gray-500">
          {remainingGuarantors > 0 ? `متبقي: ${remainingGuarantors} من ${MAX_GUARANTORS}` : 'وصلت الحد الأقصى'}
        </span>
      </div>

      <FormSectionTitle title="العقارات" />
      {estates.map((e, i) => (
        <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
          <div className="flex justify-between items-center mb-3">
            <span className="font-medium text-gray-700 text-sm">عقار {i + 1}</span>
            <button type="button" onClick={() => onEstateRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
              ✖ حذف
            </button>
          </div>
          <div className="grid md:grid-cols-5 gap-3">
            {([
              ['propertyNumber', 'رقم العقار'],
              ['propertyDistrict', 'المنطقة العقارية'],
              ['landRegistry', 'المصالح العقارية'],
            ] as const).map(([k, label]) => (
              <div key={k}>
                <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                <input value={e[k] ?? ''} onChange={(ev) => onEstateSet(i, k, ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
              </div>
            ))}
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">مقدار الحصة</label>
              <select value={e.shareType ?? 'تمام العقار'} onChange={(ev) => onEstateSet(i, 'shareType', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none">
                {SHARE_TYPES.map((o) => (
                  <option key={o}>{o}</option>
                ))}
              </select>
            </div>
          </div>
          <div className="mt-3">
            <span className="block text-xs font-bold text-gray-600 mb-1">مالكو العقار</span>
            {(() => {
              // الخيارات: الأسماء المتاحة (مقترض/كفلاء/ورثة) + أي مالك محفوظ سابقًا
              // لم يعد من بينها، حتى لا يُفقد عند حفظ التعديل.
              const options = [...new Set([...ownerOptions(), ...(e.owners ?? [])])];
              return options.length > 0 ? (
                <div className="flex flex-wrap gap-x-5 gap-y-1">
                  {options.map((o) => (
                    <label key={o} className="flex items-center gap-2 min-h-11 text-sm text-gray-700">
                      <input
                        type="checkbox"
                        checked={(e.owners ?? []).includes(o)}
                        onChange={() => onOwnerToggle(i, o)}
                        className="h-4 w-4 text-emerald-600"
                      />
                      {o}
                    </label>
                  ))}
                </div>
              ) : (
                <p className="text-xs text-gray-400">أدخل اسم المقترض أو الكفيل أو الورثة أولًا لاختيار المالك</p>
              );
            })()}
          </div>
        </div>
      ))}
      <button
        type="button"
        onClick={onEstateAdd}
        disabled={estates.length >= MAX_ESTATES}
        className="bg-red-700 hover:bg-red-600 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
      >
        {estates.length >= MAX_ESTATES ? '🛑 الحد الأقصى' : '🏡 إضافة عقار'}
      </button>
    </>
  );
}
