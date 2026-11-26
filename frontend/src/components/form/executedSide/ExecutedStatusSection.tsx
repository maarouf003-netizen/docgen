import AutoResizeTextarea from '../../AutoResizeTextarea';
import MultiAmountEditor from '../../MultiAmountEditor';
import { FormSectionTitle } from '../FormSectionTitle';
import { RenewalFields } from '../RenewalFields';
import { paidAmountKeys, paidCurrencyKeys } from '../documentFormConstants';
import {
  EXECUTED_STATUS_EXECUTED,
  EXEC_STATUS_STRUCK_OFF,
  STATE_CIRCULATING,
  targetsOf,
} from '../../../utils/documentStatus';
import type { FormSet, makeFieldHelpers } from '../formFields';
import type { DocumentUpsertRequest } from '../../../types';

type FieldHelper = ReturnType<typeof makeFieldHelpers>['field'];

interface ExecutedStatusSectionProps {
  form: DocumentUpsertRequest;
  set: FormSet;
  isDeposit: boolean;
  field: FieldHelper;
  paidAmountSlots: number;
  setPaidAmountSlots: (n: number) => void;
  wasOriginallyStruckOff?: boolean;
  currentExecutedStatus?: string;
}

/** «حالة الملف»: خيارات الحالة المرشَّحة، مبالغ التنفيذ/الإيداع، الشطب والتجديد. */
export function ExecutedStatusSection({
  form,
  set,
  isDeposit,
  field,
  paidAmountSlots,
  setPaidAmountSlots,
  wasOriginallyStruckOff,
  currentExecutedStatus,
}: ExecutedStatusSectionProps) {
  // خيارات «الحالة» في نموذج التعديل تُرشَّح بمنطق نافذة تغيير الحالة (§4.6): الحالة الحالية
  // تبقى قابلة للإبقاء عليها، ولا تُعرض الحالات المحظورة. الإرجاع المباشر من «منفذ» في
  // «عرض وايداع» يبقى خاصًا بالنافذة (كتاب السير بالملف) فيُستثنى خيار «متداول» من النموذج.
  const statusCurrent = currentExecutedStatus || STATE_CIRCULATING;
  const statusTargets = targetsOf(statusCurrent, isDeposit).filter(
    (t) => !(statusCurrent === EXECUTED_STATUS_EXECUTED && isDeposit && t === STATE_CIRCULATING),
  );
  const statusOptions = [statusCurrent, ...statusTargets];

  return (
    <>
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
              {statusOptions.map((s) => (
                <option key={s} value={s === STATE_CIRCULATING ? '' : s}>
                  {s}
                </option>
              ))}
            </select>
          </div>
        </div>
        {form.executedStatus === EXECUTED_STATUS_EXECUTED && (
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
        {form.executedStatus === EXEC_STATUS_STRUCK_OFF && (
          <div className="mt-4 grid md:grid-cols-3 gap-4 items-end">
            {field('تاريخ الشطب', 'struckOffDate', 'مثال: 1/8/2026')}
          </div>
        )}
        {wasOriginallyStruckOff && !(form.executedStatus ?? '') && (
          <div className="mt-4">
            <RenewalFields value={form} onSet={(key, value) => set(key, value)} hideYear />
          </div>
        )}
      </div>
    </>
  );
}
