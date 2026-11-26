import AutoResizeTextarea from '../../AutoResizeTextarea';
import MultiAmountEditor from '../../MultiAmountEditor';
import { FormSectionTitle } from '../FormSectionTitle';
import { requiredAmountKeys, requiredCurrencyKeys } from '../documentFormConstants';
import type { FormSet, makeFieldHelpers } from '../formFields';
import type { DocumentUpsertRequest } from '../../../types';

type FieldHelper = ReturnType<typeof makeFieldHelpers>['field'];

interface ExecutedContractSectionProps {
  form: DocumentUpsertRequest;
  set: FormSet;
  isDeposit: boolean;
  field: FieldHelper;
  showRequiredAmount: boolean;
  setShowRequiredAmount: (updater: (v: boolean) => boolean) => void;
  requiredAmountSlots: number;
  setRequiredAmountSlots: (n: number) => void;
}

/** السند التنفيذي والملاحظات والمبلغ المطلوب/المعروض في وضع «منفذ عليها/عرض وايداع». */
export function ExecutedContractSection({
  form,
  set,
  isDeposit,
  field,
  showRequiredAmount,
  setShowRequiredAmount,
  requiredAmountSlots,
  setRequiredAmountSlots,
}: ExecutedContractSectionProps) {
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
    </>
  );
}
