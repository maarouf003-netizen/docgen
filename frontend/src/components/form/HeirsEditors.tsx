import type { ExecutedHeirDto, HeirDto } from '../../types';
import { EXECUTED_HEIR_ADDRESS_TYPE_OPTIONS, HEIR_ADDRESS_TYPE_OPTIONS, HEIR_CAPACITIES, heirAddressLabelOf } from './documentFormConstants';

export function HeirsEditor({
  heirs,
  onSet,
  onAdd,
  onRemove,
  idPrefix,
  hideAddress = false,
}: {
  heirs: HeirDto[];
  onSet: (i: number, key: keyof HeirDto, value: string) => void;
  onAdd: () => void;
  onRemove: (i: number) => void;
  idPrefix: string;
  /** عند وجود ممثل شرعي يُخفى نوع العنوان وحقله (عنوان الممثل هو المعتبر). */
  hideAddress?: boolean;
}) {
  const inputCls =
    'w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500';
  const labelCls = 'block text-xs font-bold text-gray-600 mb-1';
  const gridCls = hideAddress
    ? 'grid grid-cols-1 md:grid-cols-5 gap-3 mb-3 last:mb-0'
    : 'grid grid-cols-1 md:grid-cols-8 gap-3 mb-3 last:mb-0';

  return (
    <div className="mt-4 rounded-lg bg-gray-50 border border-gray-200 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2 mb-3">
        <span className="text-sm font-medium text-gray-700">ورثة المتوفى</span>
        <button
          type="button"
          onClick={onAdd}
          className="bg-gray-500 hover:bg-gray-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
        >
          ＋ إضافة وريث
        </button>
      </div>
      {heirs.map((h, i) => (
        <div key={i} className={gridCls}>
          <div className="md:col-span-1">
            <label htmlFor={`${idPrefix}-heir-name-${i}`} className={labelCls}>اسم الوريث</label>
            <input
              id={`${idPrefix}-heir-name-${i}`}
              value={h.name ?? ''}
              onChange={(e) => onSet(i, 'name', e.target.value)}
              className={inputCls}
            />
          </div>
          <div className="md:col-span-1">
            <label htmlFor={`${idPrefix}-heir-father-${i}`} className={labelCls}>اسم أب الوريث</label>
            <input
              id={`${idPrefix}-heir-father-${i}`}
              value={h.father ?? ''}
              onChange={(e) => onSet(i, 'father', e.target.value)}
              className={inputCls}
            />
          </div>
          <div className="md:col-span-1">
            <label htmlFor={`${idPrefix}-heir-family-${i}`} className={labelCls}>النسبة</label>
            <input
              id={`${idPrefix}-heir-family-${i}`}
              value={h.family ?? ''}
              onChange={(e) => onSet(i, 'family', e.target.value)}
              className={inputCls}
            />
          </div>
          <div className="md:col-span-1">
            <label htmlFor={`${idPrefix}-heir-capacity-${i}`} className={labelCls}>صفة الوريث</label>
            <select
              id={`${idPrefix}-heir-capacity-${i}`}
              value={h.capacity ?? 'أصالة'}
              onChange={(e) => onSet(i, 'capacity', e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {HEIR_CAPACITIES.map((o) => (
                <option key={o} value={o}>{o}</option>
              ))}
            </select>
          </div>
          {!hideAddress && (
            <>
              <div className="md:col-span-1">
                <label htmlFor={`${idPrefix}-heir-type-${i}`} className={labelCls}>نوع العنوان</label>
                <select
                  id={`${idPrefix}-heir-type-${i}`}
                  value={h.addressType ?? 'عنوان'}
                  onChange={(e) => onSet(i, 'addressType', e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                >
                  {HEIR_ADDRESS_TYPE_OPTIONS.map((o) => (
                    <option key={o.value} value={o.value}>{o.label}</option>
                  ))}
                </select>
              </div>
              <div className="md:col-span-2">
                <label htmlFor={`${idPrefix}-heir-address-${i}`} className={labelCls}>
                  {heirAddressLabelOf(h.addressType)}
                </label>
                <input
                  id={`${idPrefix}-heir-address-${i}`}
                  value={h.address ?? ''}
                  onChange={(e) => onSet(i, 'address', e.target.value)}
                  className={inputCls}
                />
              </div>
            </>
          )}
          <div className="flex items-end">
            <button type="button" onClick={() => onRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
              ✖ حذف
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}

/** محرر ورثة المورث المتوفى في وضع «منفذ عليه» (طالب تنفيذ أو منفذ عليه طبيعي متوفى). */
export function ExecutedHeirsEditor({
  heirs,
  onSet,
  onAdd,
  onRemove,
  idPrefix,
  allowAdd = true,
}: {
  heirs: ExecutedHeirDto[];
  onSet: (i: number, key: keyof ExecutedHeirDto, value: string) => void;
  onAdd: () => void;
  onRemove: (i: number) => void;
  idPrefix: string;
  allowAdd?: boolean;
}) {
  const inputCls =
    'w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500';
  const labelCls = 'block text-xs font-bold text-gray-600 mb-1';

  if (heirs.length === 0 && !allowAdd) return null;

  return (
    <div className="mt-4 rounded-lg bg-gray-50 border border-gray-200 p-4">
      {allowAdd && (
        <div className="flex flex-wrap items-center justify-end gap-2 mb-3">
          <button
            type="button"
            onClick={onAdd}
            className="bg-gray-500 hover:bg-gray-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
          >
            ＋ إضافة وريث
          </button>
        </div>
      )}
      {heirs.map((h, i) => (
        <div key={i} className="grid grid-cols-1 md:grid-cols-7 gap-3 mb-3 last:mb-0">
          <div className="md:col-span-1">
            <label htmlFor={`${idPrefix}-heir-name-${i}`} className={labelCls}>الاسم</label>
            <input
              id={`${idPrefix}-heir-name-${i}`}
              value={h.heirName ?? ''}
              onChange={(e) => onSet(i, 'heirName', e.target.value)}
              className={inputCls}
            />
          </div>
          <div className="md:col-span-1">
            <label htmlFor={`${idPrefix}-heir-father-${i}`} className={labelCls}>اسم الأب</label>
            <input
              id={`${idPrefix}-heir-father-${i}`}
              value={h.heirFather ?? ''}
              onChange={(e) => onSet(i, 'heirFather', e.target.value)}
              className={inputCls}
            />
          </div>
          <div className="md:col-span-1">
            <label htmlFor={`${idPrefix}-heir-family-${i}`} className={labelCls}>النسبة</label>
            <input
              id={`${idPrefix}-heir-family-${i}`}
              value={h.heirFamily ?? ''}
              onChange={(e) => onSet(i, 'heirFamily', e.target.value)}
              className={inputCls}
            />
          </div>
          <div className="md:col-span-1">
            <label htmlFor={`${idPrefix}-heir-type-${i}`} className={labelCls}>نوع العنوان</label>
            <select
              id={`${idPrefix}-heir-type-${i}`}
              value={h.addressType ?? 'عنوان'}
              onChange={(e) => onSet(i, 'addressType', e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {EXECUTED_HEIR_ADDRESS_TYPE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>
          <div className="md:col-span-2">
            <label htmlFor={`${idPrefix}-heir-address-${i}`} className={labelCls}>
              {heirAddressLabelOf(h.addressType)}
            </label>
            <input
              id={`${idPrefix}-heir-address-${i}`}
              value={h.heirAddress ?? ''}
              onChange={(e) => onSet(i, 'heirAddress', e.target.value)}
              className={inputCls}
            />
          </div>
          <div className="flex items-end">
            <button type="button" onClick={() => onRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
              ✖ حذف
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}
