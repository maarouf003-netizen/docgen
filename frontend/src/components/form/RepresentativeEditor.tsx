import { REPRESENTATIVE_ADDRESS_TYPE_OPTIONS, REPRESENTATIVE_CAPACITIES, representativeAddressLabelOf } from './documentFormConstants';

export interface RepresentativeFields {
  representativeName?: string;
  representativeFather?: string;
  representativeFamily?: string;
  representativeCapacity?: string;
  representativeAddressType?: string;
  representativeAddress?: string;
  representativeLegalRepresentative?: string;
}

/**
 * محرر الممثل الشرعي (ولي/وصي/قيم) لشخصٍ واحد. يعرض الحقول فقط عندما يكون الممثل حاضرًا،
 * ويُتحكم بالإظهار من المكوّن الأب عبر زر «إضافة ممثل شرعي». بنوعَين:
 * - «address»: نوع عنوان (موطن مختار/عنوان/وكيل قانوني) + حقل قيمته.
 * - «legalRep»: حقل «الوكيل القانوني» وحيد (يخص طالب التنفيذ في وضع «منفذ عليه»).
 */
export function RepresentativeEditor({
  representative,
  onSet,
  onRemove,
  mode,
  idPrefix,
}: {
  representative: RepresentativeFields;
  onSet: (key: string, value: string) => void;
  onRemove: () => void;
  mode: 'address' | 'legalRep';
  idPrefix: string;
}) {
  const inputCls =
    'w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500';
  const labelCls = 'block text-xs font-bold text-gray-600 mb-1';

  return (
    <div className="mt-4 rounded-lg bg-white border border-emerald-200 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2 mb-3">
        <span className="text-sm font-bold text-emerald-800">الممثل الشرعي</span>
        <button type="button" onClick={onRemove} className="text-red-500 text-xs hover:underline min-h-11">
          ✖ حذف الممثل
        </button>
      </div>
      <div className="grid md:grid-cols-3 gap-3">
        <div>
          <label htmlFor={`${idPrefix}-rep-name`} className={labelCls}>اسم الممثل الشرعي</label>
          <input
            id={`${idPrefix}-rep-name`}
            value={representative.representativeName ?? ''}
            onChange={(e) => onSet('representativeName', e.target.value)}
            className={inputCls}
          />
        </div>
        <div>
          <label htmlFor={`${idPrefix}-rep-father`} className={labelCls}>اسم أب الممثل الشرعي</label>
          <input
            id={`${idPrefix}-rep-father`}
            value={representative.representativeFather ?? ''}
            onChange={(e) => onSet('representativeFather', e.target.value)}
            className={inputCls}
          />
        </div>
        <div>
          <label htmlFor={`${idPrefix}-rep-family`} className={labelCls}>نسبة الممثل الشرعي</label>
          <input
            id={`${idPrefix}-rep-family`}
            value={representative.representativeFamily ?? ''}
            onChange={(e) => onSet('representativeFamily', e.target.value)}
            className={inputCls}
          />
        </div>
      </div>
      <div className="grid md:grid-cols-3 gap-3 mt-3">
        <div>
          <label htmlFor={`${idPrefix}-rep-capacity`} className={labelCls}>صفة الممثل الشرعي</label>
          <select
            id={`${idPrefix}-rep-capacity`}
            value={representative.representativeCapacity ?? ''}
            onChange={(e) => onSet('representativeCapacity', e.target.value)}
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
            {REPRESENTATIVE_CAPACITIES.map((o) => (
              <option key={o} value={o}>{o}</option>
            ))}
          </select>
        </div>
        {mode === 'address' ? (
          <>
            <div>
              <label htmlFor={`${idPrefix}-rep-type`} className={labelCls}>نوع العنوان</label>
              <select
                id={`${idPrefix}-rep-type`}
                value={representative.representativeAddressType ?? 'عنوان'}
                onChange={(e) => onSet('representativeAddressType', e.target.value)}
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
              >
                {REPRESENTATIVE_ADDRESS_TYPE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>
            <div>
              <label htmlFor={`${idPrefix}-rep-address`} className={labelCls}>
                {representativeAddressLabelOf(representative.representativeAddressType)}
              </label>
              <input
                id={`${idPrefix}-rep-address`}
                value={representative.representativeAddress ?? ''}
                onChange={(e) => onSet('representativeAddress', e.target.value)}
                className={inputCls}
              />
            </div>
          </>
        ) : (
          <div className="md:col-span-2">
            <label htmlFor={`${idPrefix}-rep-legal`} className={labelCls}>الوكيل القانوني</label>
            <input
              id={`${idPrefix}-rep-legal`}
              value={representative.representativeLegalRepresentative ?? ''}
              onChange={(e) => onSet('representativeLegalRepresentative', e.target.value)}
              className={inputCls}
            />
          </div>
        )}
      </div>
    </div>
  );
}
