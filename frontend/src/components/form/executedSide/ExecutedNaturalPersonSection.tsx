import type { ExecutedHeirDto, ExecutedNaturalPersonDto } from '../../../types';
import {
  EXECUTED_HEIR_ADDRESS_TYPE_OPTIONS,
  REPRESENTATION_TYPES,
  hasRepresentative,
  heirAddressLabelOf,
} from '../documentFormConstants';
import { ExecutedHeirsEditor } from '../HeirsEditors';
import { RepresentativeEditor } from '../RepresentativeEditor';

interface ExecutedNaturalPersonSectionProps {
  isMirror: boolean;
  partyRepLocked: (key: string) => boolean;
  executedNaturalPersons: ExecutedNaturalPersonDto[];
  onPersonSet: (i: number, key: keyof ExecutedNaturalPersonDto, value: string) => void;
  onPersonRemove: (i: number) => void;
  onPersonHeirSet: (i: number, hi: number, key: keyof ExecutedHeirDto, value: string) => void;
  onPersonHeirAdd: (i: number) => void;
  onPersonHeirRemove: (i: number, hi: number) => void;
  onPersonRepActivate: (i: number) => void;
  onPersonRepRemove: (i: number) => void;
}

/** بطاقات الأشخاص الطبيعيين المنفذ عليهم (اختياريون) مع الورثة/الممثل. */
export function ExecutedNaturalPersonSection({
  isMirror,
  partyRepLocked,
  executedNaturalPersons,
  onPersonSet,
  onPersonRemove,
  onPersonHeirSet,
  onPersonHeirAdd,
  onPersonHeirRemove,
  onPersonRepActivate,
  onPersonRepRemove,
}: ExecutedNaturalPersonSectionProps) {
  return (
    <>
      {executedNaturalPersons.map((p, i) => {
        const pHasRep = hasRepresentative(p);
        const hasEstate = p.representationType === 'إضافة لتركة' || p.representationType === 'أصالة وإضافة';
        return (
          <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
            <div className="flex justify-between items-center mb-3">
              <span className="font-medium text-gray-700 text-sm">شخص طبيعي {i + 1}</span>
              {!isMirror && executedNaturalPersons.length > 1 && (
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
                <select value={p.addressType ?? 'عنوان'} disabled={isMirror} aria-readonly={isMirror || undefined} onChange={(e) => onPersonSet(i, 'addressType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:bg-gray-50 disabled:cursor-not-allowed">
                  {EXECUTED_HEIR_ADDRESS_TYPE_OPTIONS.map((o) => (
                    <option key={o.value} value={o.value}>{o.label}</option>
                  ))}
                </select>
              </div>
              <div className="md:col-span-1">
                <label className="block text-xs font-bold text-gray-600 mb-1">
                  {heirAddressLabelOf(p.addressType)}
                </label>
                <input value={p.addressOrRepresentative ?? ''} readOnly={isMirror} aria-readonly={isMirror || undefined} onChange={(e) => onPersonSet(i, 'addressOrRepresentative', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
              </div>
              <div>
                <label className="block text-xs font-bold text-gray-600 mb-1">نوع التمثيل</label>
                <div className="flex items-center gap-2">
                  <select value={p.representationType ?? 'أصالة'} disabled={isMirror} aria-readonly={isMirror || undefined} onChange={(e) => onPersonSet(i, 'representationType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:bg-gray-50 disabled:cursor-not-allowed">
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
<input value={p[k] ?? ''} readOnly={isMirror} aria-readonly={isMirror || undefined} onChange={(e) => onPersonSet(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                  </div>
                ))}
                <div className="md:col-span-3">
                  <ExecutedHeirsEditor
                    idPrefix={`executed-person-${i}`}
                    heirs={p.heirs ?? []}
                    onSet={(hi, k, v) => onPersonHeirSet(i, hi, k, v)}
                    onAdd={() => onPersonHeirAdd(i)}
                    onRemove={(hi) => onPersonHeirRemove(i, hi)}
                    locked={isMirror}
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
                locked={partyRepLocked(`person-${i}`)}
              />
            )}
          </div>
        );
      })}
    </>
  );
}
