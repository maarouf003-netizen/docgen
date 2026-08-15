import { CURRENCIES, slotCurrencyOptions, slotDefaultCurrency } from '../utils/amountCurrencies';

export interface MultiAmountEditorProps {
  /** بادئة معرفات حقول المبلغ/العملة لتفردها عن حقول غيرها من المحررات في نفس الصفحة. */
  idPrefix: string;
  amountKeys: readonly string[];
  currencyKeys: readonly string[];
  /** كائن النموذج (form) الحامل لقيم المبالغ والعملات. */
  values: object;
  /** تحديث حقل واحد في النموذج. */
  onSet: (key: string, value: unknown) => void;
  /** عدد الخانات المعروضة (1 إلى 3). */
  slots: number;
  onSlotsChange: (slots: number) => void;
  /** تسمية أول خانة (المبلغ الأساسي). */
  firstLabel: string;
  /** تسمية الخانات اللاحقة (i ابتداءً من 1). */
  otherLabel: (index: number) => string;
  maxSlots?: number;
}

/**
 * محرر مبالغ موحّد (حتى ثلاثة) لكل خانة مبلغ + عملة، بقاعدة «لا تكرار للعملة»:
 * الخانات اللاحقة تستثني عملات السابقة، وتغيير عملة خانة يُعيد ضبط أي خانة لاحقة
 * تعارضت تلقائيًا، وحذف خانة يزاح ما بعدها لأسفل فيبقى أول المبالغ دائمًا في الأولى.
 * يستخدمه وضع «منفذ عليه» و«طالبة تنفيذ» (مصرفي/عادي) على حد سواء.
 */
export default function MultiAmountEditor({
  idPrefix,
  amountKeys,
  currencyKeys,
  values,
  onSet,
  slots,
  onSlotsChange,
  firstLabel,
  otherLabel,
  maxSlots = 3,
}: MultiAmountEditorProps) {
  const raw = (key: string) => (values as Record<string, unknown>)[key];

  const setCurrency = (i: number, value: string) => {
    onSet(currencyKeys[i], value);
    // لو جعل التعديل عملةَ خانة لاحقة مطابقةً لعملة هذه الخانة، تُعاد الخانة اللاحقة
    // تلقائيًا إلى أول عملة متاحة لها حفاظًا على قاعدة «لا تكرار للعملة».
    for (let j = i + 1; j < currencyKeys.length; j++) {
      if (raw(currencyKeys[j]) === value) {
        const used = new Set<string>();
        for (let k = 0; k < j; k++) {
          used.add(k === i ? value : ((raw(currencyKeys[k]) as string | undefined) ?? 'ليرة سورية'));
        }
        const fallback = CURRENCIES.find((c) => !used.has(c));
        if (fallback) onSet(currencyKeys[j], fallback);
      }
    }
  };

  // حذف خانة يزاح ما بعدها لأسفل فيبقى أول المبالغ دائمًا في الخانة الأولى.
  const removeSlot = (i: number) => {
    for (let j = i; j < slots - 1; j++) {
      onSet(amountKeys[j], raw(amountKeys[j + 1]));
      onSet(currencyKeys[j], raw(currencyKeys[j + 1]));
    }
    onSet(amountKeys[slots - 1], undefined);
    onSet(currencyKeys[slots - 1], undefined);
    onSlotsChange(slots - 1);
  };

  const addSlot = () => onSlotsChange(Math.min(maxSlots, slots + 1));

  return (
    <div className="grid gap-4">
      {Array.from({ length: slots }, (_, i) => (
        <div key={i} className="grid md:grid-cols-3 gap-3 items-end">
          <div>
            <label htmlFor={`${idPrefix}-amount-${i}`} className="block text-xs font-bold text-gray-600 mb-1">
              {i === 0 ? firstLabel : otherLabel(i)}
            </label>
            <input
              id={`${idPrefix}-amount-${i}`}
              type="number"
              value={(raw(amountKeys[i]) as number | undefined) ?? ''}
              onChange={(e) => onSet(amountKeys[i], e.target.value === '' ? undefined : Number(e.target.value))}
              onWheel={(e) => e.currentTarget.blur()}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor={`${idPrefix}-currency-${i}`} className="block text-xs font-bold text-gray-600 mb-1">
              العملة
            </label>
            <select
              id={`${idPrefix}-currency-${i}`}
              value={slotDefaultCurrency(values, currencyKeys, i)}
              onChange={(e) => setCurrency(i, e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {slotCurrencyOptions(values, currencyKeys, i).map((o) => (
                <option key={o}>{o}</option>
              ))}
            </select>
          </div>
          <div className="flex items-end">
            {i === 0 && slots < maxSlots ? (
              <button
                type="button"
                onClick={addSlot}
                className="bg-gray-100 hover:bg-gray-200 text-gray-700 text-xs font-bold rounded-md px-3 py-2 min-h-11"
              >
                ➕ مبلغ آخر
              </button>
            ) : (
              <button
                type="button"
                onClick={() => removeSlot(i)}
                className="text-red-500 text-xs hover:underline min-h-11"
              >
                ✖ حذف
              </button>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}
