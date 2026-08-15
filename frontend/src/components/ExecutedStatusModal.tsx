import { useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { normalizeArabicDigits } from '../utils/arabicDigits';
import type { DocumentResponse } from '../types';
import AutoResizeTextarea from './AutoResizeTextarea';
import MultiAmountEditor from './MultiAmountEditor';
import { FieldInput, SelectInput } from './form/FormInputs';
import { RenewalFields, type RenewalFieldsValue } from './form/RenewalFields';
import { paidAmountKeys, paidCurrencyKeys } from './form/documentFormConstants';
import { trimNull } from '../utils/serialization';

/** تسمية حالة وضع «الجهة العامة منفذ عليها» الحالية (الفارغ «متداول» لا يُخزَّن كقيمة). */
function currentLabelOf(doc: DocumentResponse): string {
  if (doc.executedStatus === 'منفذ') return 'منفذ';
  if (doc.executedStatus === 'مشطوب') return 'مشطوب';
  return 'متداول';
}

/** الحالات المتاحة من الحالة الحالية (كخيارات نموذج التعديل، بلا الحالة الحالية نفسها).
 * «منفذ عليها»: حالة «منفذ» نهائية لا تُغيَّر. «عرض وايداع»: من منفذه يُعاد إلى متداول فقط
 * (لا يُشطب)، بكتاب الجهة العامة بالسير بالملف. */
function targetsOf(current: string, isDeposit: boolean): string[] {
  if (current === 'مشطوب') return ['متداول', 'منفذ'];
  if (current === 'منفذ') return isDeposit ? ['متداول'] : [];
  return ['منفذ', 'مشطوب'];
}

/** قيمة الحالة المُرسَلة: «متداول» سلسلة فارغة لأنها لا تُخزَّن كقيمة في الخلفية. */
function statusValue(target: string): string {
  return target === 'متداول' ? '' : target;
}

type ExecutedFields = {
  executedPaidAmount: string;
  executedPaidCurrency: string;
  executedPaidAmount2: string;
  executedPaidCurrency2: string;
  executedPaidAmount3: string;
  executedPaidCurrency3: string;
  executedDepositDate: string;
  executedExecutionDate: string;
  executedDescription: string;
  struckOffDate: string;
  sayerNumber: string;
  sayerDate: string;
  sayerRegNumber: string;
  sayerRegDate: string;
};

function emptyExecutedFields(): ExecutedFields {
  return {
    executedPaidAmount: '',
    executedPaidCurrency: 'ليرة سورية',
    executedPaidAmount2: '',
    executedPaidCurrency2: 'دولار أمريكي',
    executedPaidAmount3: '',
    executedPaidCurrency3: 'يورو',
    executedDepositDate: '',
    executedExecutionDate: '',
    executedDescription: '',
    struckOffDate: '',
    sayerNumber: '',
    sayerDate: '',
    sayerRegNumber: '',
    sayerRegDate: '',
  };
}

/** قيمة رقمية صالحة فقط (يرفض ما لا يتحول لعددٍ محدود كي لا يُرسل NaN). */
function numberOrUndefined(value: string): number | undefined {
  const n = Number(value);
  return Number.isFinite(n) ? n : undefined;
}

export default function ExecutedStatusModal({
  doc,
  onClose,
  onChanged,
}: {
  doc: DocumentResponse;
  onClose: () => void;
  onChanged: () => void;
}) {
  const isDeposit = doc.generalEntitySide === 'deposit';
  const current = currentLabelOf(doc);
  const targets = targetsOf(current, isDeposit);
  const isStruckOffNow = doc.executedStatus === 'مشطوب';
  const [target, setTarget] = useState<string>(targets[0] ?? '');
  const isDepositRevert = target === 'متداول' && isDeposit && current === 'منفذ';
  const [fields, setFields] = useState<ExecutedFields>(emptyExecutedFields());
  const [paidSlots, setPaidSlots] = useState(1);
  const [renewal, setRenewal] = useState<RenewalFieldsValue>({});
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const set = <K extends keyof ExecutedFields>(key: K, value: ExecutedFields[K]) =>
    setFields((f) => ({ ...f, [key]: value }));

  // محرر المبالغ الموحّد يعمل بمفاتيح نصية عامة ويُحدِّث قيمًا رقمية/فارغة؛ تُخزَّن في حقول
  // النافذة نصيًا (نفس تمثيل باقي الحقول) ليُعيد البناء تحويلها إلى رقم صالح عند الإرسال.
  const onMultiSet = (key: string, value: unknown) =>
    set(key as keyof ExecutedFields, value === undefined || value === null ? '' : String(value));

  const onRenewalSet: (key: keyof RenewalFieldsValue, value: string) => void = (key, value) => {
    setRenewal((r) => ({
      ...r,
      [key]: key === 'renewalYear' ? (value.trim() ? Number(value.trim()) : undefined) : value,
    }));
  };

  const normalize = (s: string) => normalizeArabicDigits(s.trim());

  const buildBody = (): Record<string, unknown> => {
    const body: Record<string, unknown> = { status: statusValue(target) };
    if (target === 'منفذ') {
      // المبلغ المدفوع يتبع القاعدة العامة «حتى ثلاثة مبالغ بعملات متمايزة» في الصفّين:
      // كل خانة معبأة تُرسل بمبلغها وعملتها المختارة، والخانات الفارغة تُتجاهل كليًا.
      const raw = fields as unknown as Record<string, string>;
      for (let i = 0; i < paidAmountKeys.length; i++) {
        const rawAmount = (raw[paidAmountKeys[i]] ?? '').trim();
        if (!rawAmount) continue;
        const amount = numberOrUndefined(normalize(rawAmount));
        if (amount !== undefined) {
          body[paidAmountKeys[i]] = amount;
          const currency = (raw[paidCurrencyKeys[i]] ?? '').trim();
          if (currency) body[paidCurrencyKeys[i]] = currency;
        }
      }
      if (isDeposit) {
        if (fields.executedDepositDate.trim()) body.executedDepositDate = normalize(fields.executedDepositDate);
      } else {
        if (fields.executedDescription.trim()) body.executedDescription = fields.executedDescription.trim();
        if (fields.executedExecutionDate.trim()) body.executedExecutionDate = normalize(fields.executedExecutionDate);
      }
    } else if (target === 'مشطوب') {
      if (fields.struckOffDate.trim()) body.struckOffDate = normalize(fields.struckOffDate);
    } else if (isDepositRevert) {
      // الإرجاع من «منفذ» إلى «متداول» في «عرض وايداع»: كتاب الجهة العامة بالسير بالملف إلزامي.
      if (!fields.sayerNumber.trim() || !fields.sayerDate.trim()
        || !fields.sayerRegNumber.trim() || !fields.sayerRegDate.trim()) {
        throw new Error('يجب إدخال رقم وتاريخ كتاب الجهة العامة بالسير بالملف وورودهما');
      }
      body.sayerNumber = normalize(fields.sayerNumber);
      body.sayerDate = normalize(fields.sayerDate);
      body.sayerRegNumber = normalize(fields.sayerRegNumber);
      body.sayerRegDate = normalize(fields.sayerRegDate);
    } else if (target === 'متداول' && isStruckOffNow) {
      if (!(renewal.renewalFileNumber ?? '').trim()) {
        throw new Error('رقم الملف الجديد مطلوب عند إعادة الملف المشطوب');
      }
      body.renewalFileNumber = renewal.renewalFileNumber?.trim();
      if (renewal.renewalYear != null) body.renewalYear = renewal.renewalYear;
      body.renewalFileType = trimNull(renewal.renewalFileType);
      body.renewalFileReceiptNumber = trimNull(renewal.renewalFileReceiptNumber);
      body.renewalFileReceiptDate = trimNull(renewal.renewalFileReceiptDate);
      body.renewalDate = trimNull(renewal.renewalDate);
    }
    return body;
  };

  const submit = async () => {
    setError('');
    let body: Record<string, unknown>;
    try {
      body = buildBody();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'بيانات غير مكتملة');
      return;
    }
    setBusy(true);
    try {
      await api.post(`/documents/${doc.id}/executed-status`, body);
      onChanged();
      onClose();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="تغيير الحالة"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[85vh] overflow-y-auto">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 sticky top-0 bg-white">
          <h3 className="text-lg font-bold text-gray-800">تغيير الحالة</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4">
          <div className="rounded-lg bg-gray-50 border border-gray-200 px-3 py-2 mb-4">
            <p className="text-xs text-gray-500 mb-1">الحالة الحالية</p>
            <p className="font-medium text-gray-800">{current}</p>
          </div>

          {targets.length === 0 ? (
            <p className="text-gray-600 text-sm">
              حالة «{current}» في صفة «{doc.generalEntitySideLabel ?? 'الملف'}» نهائية لا يمكن تغييرها.
            </p>
          ) : (
            <>
              {error && <p className="text-red-600 text-sm mb-3">{error}</p>}

              <div className="mb-4">
                <SelectInput
                  id="executed-status-target"
                  label="الحالة"
                  value={target}
                  onChange={setTarget}
                  options={targets}
                />
              </div>

              {target === 'منفذ' &&
                (isDeposit ? (
                  <div className="grid gap-3">
                    <MultiAmountEditor
                      idPrefix="executed-status-paid"
                      amountKeys={paidAmountKeys}
                      currencyKeys={paidCurrencyKeys}
                      values={fields}
                      onSet={onMultiSet}
                      slots={paidSlots}
                      onSlotsChange={setPaidSlots}
                      firstLabel="المبلغ المودع"
                      otherLabel={(i) => `المبلغ المودع ${i + 1}`}
                    />
                    <FieldInput
                      id="executedDepositDate"
                      label="تاريخ ايداعه حساب الجهة العامة"
                      value={fields.executedDepositDate}
                      onChange={(v) => set('executedDepositDate', v)}
                    />
                  </div>
                ) : (
                  <div className="space-y-3">
                    <div>
                      <label htmlFor="executedDescription" className="block text-xs font-bold text-gray-600 mb-1">
                        كيفية تنفيذ الملف
                      </label>
                      <AutoResizeTextarea
                        id="executedDescription"
                        value={fields.executedDescription}
                        onChange={(v) => set('executedDescription', v)}
                        placeholder="كيف تم تنفيذ الملف..."
                        minRows={2}
                        maxHeight={200}
                        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                      />
                    </div>
                    <MultiAmountEditor
                      idPrefix="executed-status-paid"
                      amountKeys={paidAmountKeys}
                      currencyKeys={paidCurrencyKeys}
                      values={fields}
                      onSet={onMultiSet}
                      slots={paidSlots}
                      onSlotsChange={setPaidSlots}
                      firstLabel="المبلغ الذي دفعته الجهة العامة"
                      otherLabel={(i) => `المبلغ الذي دفعته الجهة العامة ${i + 1}`}
                    />
                    <FieldInput
                      id="executedExecutionDate"
                      label="تاريخ التنفيذ"
                      value={fields.executedExecutionDate}
                      onChange={(v) => set('executedExecutionDate', v)}
                    />
                  </div>
                ))}

              {target === 'مشطوب' && (
                <FieldInput
                  id="executedStruckOffDate"
                  label="تاريخ الشطب"
                  value={fields.struckOffDate}
                  onChange={(v) => set('struckOffDate', v)}
                />
              )}

              {isDepositRevert && (
                <div className="grid sm:grid-cols-2 gap-3">
                  <FieldInput id="executed-sayerNumber" label="رقم كتاب الجهة العامة بالسير بالملف" value={fields.sayerNumber} onChange={(v) => set('sayerNumber', v)} />
                  <FieldInput id="executed-sayerDate" label="تاريخ كتاب الجهة العامة بالسير بالملف" value={fields.sayerDate} onChange={(v) => set('sayerDate', v)} />
                  <FieldInput id="executed-sayerRegNumber" label="رقم ورود كتاب بالسير بالملف" value={fields.sayerRegNumber} onChange={(v) => set('sayerRegNumber', v)} />
                  <FieldInput id="executed-sayerRegDate" label="تاريخ ورود كتاب بالسير بالملف" value={fields.sayerRegDate} onChange={(v) => set('sayerRegDate', v)} />
                </div>
              )}

              {target === 'متداول' && isStruckOffNow && (
                <RenewalFields value={renewal} onSet={onRenewalSet} idPrefix="executed-status-" />
              )}

              <div className="mt-5 flex justify-end gap-2">
                <button
                  type="button"
                  onClick={onClose}
                  className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
                >
                  إلغاء
                </button>
                <button
                  type="button"
                  onClick={submit}
                  disabled={busy}
                  className="bg-blue-700 hover:bg-blue-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                >
                  {busy ? 'جارِ الحفظ...' : 'حفظ الحالة'}
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
