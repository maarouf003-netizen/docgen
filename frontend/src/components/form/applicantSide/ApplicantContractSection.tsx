import { MAX_ANNEXES, bankingAmountKeys, bankingCurrencyKeys, emptyAnnex, ordinaryAmountKeys, ordinaryCurrencyKeys } from '../documentFormConstants';
import AutoResizeTextarea from '../../AutoResizeTextarea';
import MultiAmountEditor from '../../MultiAmountEditor';
import { FormSectionTitle } from '../FormSectionTitle';
import type { FormSet, makeFieldHelpers } from '../formFields';
import type { DocumentUpsertRequest } from '../../../types';

type FieldHelper = ReturnType<typeof makeFieldHelpers>['field'];
type SelectHelper = ReturnType<typeof makeFieldHelpers>['selectField'];

interface ApplicantContractSectionProps {
  form: DocumentUpsertRequest;
  set: FormSet;
  isOrdinary: boolean;
  isBanking: boolean;
  lock: boolean;
  field: FieldHelper;
  selectField: SelectHelper;
  showInclusionAmount: boolean;
  setShowInclusionAmount: (updater: (v: boolean) => boolean) => void;
  ordinaryAmountSlots: number;
  setOrdinaryAmountSlots: (n: number) => void;
  bankingAmountSlots: number;
  setBankingAmountSlots: (n: number) => void;
}

/**
 * «بيانات السند التنفيذي»: نوع السند/العقد/القرار وتاريخه، ملاحق العقد المصرفي،
 * «المتضمن/الملاحظات» في الوضع العادي، ومبالغ المصرفي/العادي — منقولة حرفيًا.
 */
export function ApplicantContractSection({
  form,
  set,
  isOrdinary,
  isBanking,
  lock,
  field,
  selectField,
  showInclusionAmount,
  setShowInclusionAmount,
  ordinaryAmountSlots,
  setOrdinaryAmountSlots,
  bankingAmountSlots,
  setBankingAmountSlots,
}: ApplicantContractSectionProps) {
  // ملاحق العقد المصرفي: قائمة صفوف (حتى MAX_ANNEXES) — الجدول الفرعي هو المصدر.
  const annexes = form.annexes ?? [];
  const addAnnex = () => {
    if (annexes.length >= MAX_ANNEXES) return;
    set('annexes', [...annexes, emptyAnnex()]);
  };
  const removeAnnex = (i: number) =>
    set('annexes', annexes.filter((_, idx) => idx !== i));
  const setAnnex = (i: number, key: 'annexType' | 'annexNumber' | 'annexDate', value: string) =>
    set('annexes', annexes.map((a, idx) => (idx === i ? { ...a, [key]: value } : a)));

  return (
    <>
      <FormSectionTitle title="📄 بيانات السند التنفيذي" />
      <div className="grid md:grid-cols-4 gap-4">
        {selectField('نوع السند', 'contractTypeSelector', ['مصرفي', 'عادي'], form.contractTypeSelector ?? 'مصرفي', (v) => set('contractTypeSelector', v))}
        {field(isOrdinary ? 'المحكمة مصدرة القرار' : 'نوع العقد', 'contractType')}
        {field(isOrdinary ? 'رقم القرار' : 'رقم العقد', 'contractNumber')}
        <div className="flex items-end gap-2">
          <div className="flex-1">
            {field(isOrdinary ? 'تاريخ القرار' : 'تاريخ العقد', 'contractDate')}
          </div>
          {!lock && isBanking && (
            <button
              type="button"
              onClick={addAnnex}
              disabled={annexes.length >= MAX_ANNEXES}
              title={annexes.length >= MAX_ANNEXES ? `الحد الأقصى ${MAX_ANNEXES} ملاحق` : undefined}
              className="shrink-0 bg-emerald-700 hover:bg-emerald-600 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
            >
              إضافة ملحق
            </button>
          )}
        </div>
      </div>

      {isBanking && annexes.length > 0 && (
        <div className="mt-4 rounded-lg bg-gray-50 border border-gray-200 p-4">
          <span className="block text-xs font-bold text-gray-600 mb-2">ملاحق العقد</span>
          {annexes.map((a, i) => (
            <div key={i} className="mt-3 rounded-lg border border-gray-200 bg-white p-3">
              <div className="flex items-center justify-between gap-2 mb-2">
                <span className="text-xs font-bold text-gray-700">الملحق {i + 1}</span>
                {!lock && (
                  <button
                    type="button"
                    onClick={() => removeAnnex(i)}
                    className="text-red-500 text-xs hover:underline min-h-11"
                  >
                    ✖ حذف
                  </button>
                )}
              </div>
              <div className="grid md:grid-cols-3 gap-4">
                <div>
                  <label htmlFor={`annex-${i}-type`} className="block text-xs font-bold text-gray-600 mb-1">
                    {i === 0 ? 'نوع الملحق' : `نوع الملحق ${i + 1}`}
                  </label>
                  <input
                    id={`annex-${i}-type`}
                    value={a.annexType ?? ''}
                    readOnly={lock}
                    aria-readonly={lock || undefined}
                    onChange={(e) => setAnnex(i, 'annexType', e.target.value)}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  />
                </div>
                <div>
                  <label htmlFor={`annex-${i}-number`} className="block text-xs font-bold text-gray-600 mb-1">
                    {i === 0 ? 'رقم الملحق' : `رقم الملحق ${i + 1}`}
                  </label>
                  <input
                    id={`annex-${i}-number`}
                    value={a.annexNumber ?? ''}
                    readOnly={lock}
                    aria-readonly={lock || undefined}
                    onChange={(e) => setAnnex(i, 'annexNumber', e.target.value)}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  />
                </div>
                <div>
                  <label htmlFor={`annex-${i}-date`} className="block text-xs font-bold text-gray-600 mb-1">
                    {i === 0 ? 'تاريخ الملحق' : `تاريخ الملحق ${i + 1}`}
                  </label>
                  <input
                    id={`annex-${i}-date`}
                    value={a.annexDate ?? ''}
                    readOnly={lock}
                    aria-readonly={lock || undefined}
                    onChange={(e) => setAnnex(i, 'annexDate', e.target.value)}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  />
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

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
          <div className="mt-3">
            <label htmlFor="inclusionNotes" className="block text-xs font-bold text-gray-600 mb-1">
              ملاحظات
            </label>
            <AutoResizeTextarea
              id="inclusionNotes"
              value={form.inclusionNotes ?? ''}
              onChange={(v) => set('inclusionNotes', v)}
              placeholder="اكتب القرارات الأخرى هنا..."
              minRows={1}
              maxHeight={200}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
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
    </>
  );
}
