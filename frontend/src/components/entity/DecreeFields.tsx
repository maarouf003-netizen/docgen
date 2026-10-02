/** مناداة المرجع «بموجب قرار/قانون/مرسوم». */
const DECREE_KINDS = ['قرار', 'قانون', 'مرسوم'];

/** تاريخ حر: نص مع placeholder «مثال: 1/8/2026». */
export function FreeDateInput({
  id,
  value,
  onChange,
  required,
  disabled,
}: {
  id: string;
  value: string;
  onChange: (v: string) => void;
  required?: boolean;
  disabled?: boolean;
}) {
  return (
    <input
      id={id}
      type="text"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder="مثال: 1/8/2026"
      autoComplete="off"
      required={required}
      disabled={disabled}
      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:opacity-60"
    />
  );
}

/** رأس المرجع المشترك للأفعال الثلاثة. */
export function DecreeFields({
  kind,
  number,
  date,
  onKind,
  onNumber,
  onDate,
  disabled,
  baseId,
}: {
  kind: string;
  number: string;
  date: string;
  onKind: (v: string) => void;
  onNumber: (v: string) => void;
  onDate: (v: string) => void;
  disabled?: boolean;
  baseId: string;
}) {
  return (
    <div className="grid sm:grid-cols-3 gap-3">
      <div>
        <label htmlFor={`${baseId}-kind`} className="block text-xs font-medium text-gray-600 mb-1">
          نوع المرجع
        </label>
        <select
          id={`${baseId}-kind`}
          value={kind}
          onChange={(e) => onKind(e.target.value)}
          disabled={disabled}
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:opacity-60"
        >
          <option value="">اختر النوع…</option>
          {DECREE_KINDS.map((k) => (
            <option key={k} value={k}>
              {k}
            </option>
          ))}
        </select>
      </div>
      <div>
        <label htmlFor={`${baseId}-number`} className="block text-xs font-medium text-gray-600 mb-1">
          رقم المرجع
        </label>
        <input
          id={`${baseId}-number`}
          value={number}
          onChange={(e) => onNumber(e.target.value)}
          autoComplete="off"
          disabled={disabled}
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:opacity-60"
        />
      </div>
      <div>
        <label htmlFor={`${baseId}-date`} className="block text-xs font-medium text-gray-600 mb-1">
          تاريخ المرجع
        </label>
        <FreeDateInput id={`${baseId}-date`} value={date} onChange={onDate} disabled={disabled} />
      </div>
    </div>
  );
}
