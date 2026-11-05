import { useState } from 'react';
import type { ExecutedHeirDto, HeirDto } from '../../types';
import {
  EXECUTED_HEIR_ADDRESS_TYPE_OPTIONS,
  HEIR_ADDRESS_TYPE_OPTIONS,
  HEIR_CAPACITIES,
  heirAddressLabelOf,
  hasRepresentative,
} from './documentFormConstants';
import { RepresentativeEditor, type RepresentativeFields } from './RepresentativeEditor';

/** مفاتيح الممثل الشرعي في صف الوريث — تُفرَّغ كلها عند «حذف الممثل». */
const REPRESENTATIVE_KEYS = [
  'representativeName',
  'representativeFather',
  'representativeFamily',
  'representativeCapacity',
  'representativeAddressType',
  'representativeAddress',
] as const;

const inputCls =
  'w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500';
const labelCls = 'block text-xs font-bold text-gray-600 mb-1';

/**
 * حالة «ممثل جديد لم يُحفظ بعد» على الملف المناب: الممثل المخزَّن من المنيب يُعرض
 * للقراءة فقط (الحارس يرفض تعديله/إزالته)، بينما الممثل الذي يفعّله محامي المناب
 * الآن يبقى قابلًا للتحرير حتى الحفظ.
 */
function useDraftRepresentatives() {
  const [drafts, setDrafts] = useState<ReadonlySet<number>>(() => new Set());

  return {
    isLocked: (index: number, locked: boolean) => locked && !drafts.has(index),
    mark: (index: number) =>
      setDrafts((prev) => {
        if (prev.has(index)) return prev;
        const next = new Set(prev);
        next.add(index);
        return next;
      }),
    clear: (index: number) =>
      setDrafts((prev) => {
        if (!prev.has(index)) return prev;
        const next = new Set(prev);
        next.delete(index);
        return next;
      }),
  };
}

/** زر/محرر الممثل الشرعي لوريث واحد — مشترك بين محرري السند ووضع «منفذ عليه». */
function HeirRepresentativeSection({
  heir,
  idPrefix,
  locked,
  onSet,
  onActivate,
  onRemove,
}: {
  heir: RepresentativeFields;
  idPrefix: string;
  locked: boolean;
  onSet: (key: string, value: string) => void;
  onActivate: () => void;
  onRemove: () => void;
}) {
  if (!hasRepresentative(heir)) {
    return (
      <div className="mt-3 flex justify-end">
        <button
          type="button"
          onClick={onActivate}
          aria-label="إضافة ممثل شرعي لهذا الوريث"
          className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
        >
          ＋ إضافة ممثل شرعي
        </button>
      </div>
    );
  }

  return (
    <RepresentativeEditor
      idPrefix={idPrefix}
      mode="address"
      representative={heir}
      onSet={onSet}
      onRemove={onRemove}
      locked={locked}
    />
  );
}

export function HeirsEditor({
  heirs,
  onSet,
  onAdd,
  onRemove,
  idPrefix,
  locked = false,
  allowAdd = true,
  allowRepresentative = true,
}: {
  heirs: HeirDto[];
  onSet: (i: number, key: keyof HeirDto, value: string) => void;
  onAdd: () => void;
  onRemove: (i: number) => void;
  idPrefix: string;
  /** الملف المناب (F2): يخفي حذف الورثة — المرآة تضيف ورثة ولا تحذف (الحارس خلفيًا يرفض). */
  locked?: boolean;
  /** يُخفى زر الإضافة عند وجود ممثل شرعي للطرف (لا يجتمعان). */
  allowAdd?: boolean;
  /** يُخفى زر الممثل عند وجود ورثة للطرف (لا يجتمعان). */
  allowRepresentative?: boolean;
}) {
  const drafts = useDraftRepresentatives();

  return (
    <div className="mt-4 rounded-lg bg-gray-50 border border-gray-200 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2 mb-3">
        <span className="text-sm font-medium text-gray-700">ورثة المتوفى</span>
        {allowAdd && (
          <button
            type="button"
            onClick={onAdd}
            className="bg-gray-500 hover:bg-gray-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
          >
            ＋ إضافة وريث
          </button>
        )}
      </div>
      {!allowAdd && (
        <p className="mb-3 text-xs font-medium text-amber-800 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2" role="note">
          الممثل الشرعي للطرف قائم — لا يمكن إضافة ورثة (لا يجتمعان). احذف الممثل الشرعي أولًا.
        </p>
      )}

      {heirs.map((h, i) => {
        const hHasRep = hasRepresentative(h);
        return (
          <div key={i} className="rounded-lg border border-gray-200 bg-white p-3 mb-3 last:mb-0">
            <div className={hHasRep ? 'grid grid-cols-1 md:grid-cols-5 gap-3' : 'grid grid-cols-1 md:grid-cols-8 gap-3'}>
              <div className="md:col-span-1">
                <label htmlFor={`${idPrefix}-heir-name-${i}`} className={labelCls}>اسم الوريث</label>
                <input
                  id={`${idPrefix}-heir-name-${i}`}
                  name="heirName"
                  autoComplete="off"
                  value={h.name ?? ''}
                  onChange={(e) => onSet(i, 'name', e.target.value)}
                  className={inputCls}
                />
              </div>
              <div className="md:col-span-1">
                <label htmlFor={`${idPrefix}-heir-father-${i}`} className={labelCls}>اسم أب الوريث</label>
                <input
                  id={`${idPrefix}-heir-father-${i}`}
                  name="heirFather"
                  autoComplete="off"
                  value={h.father ?? ''}
                  onChange={(e) => onSet(i, 'father', e.target.value)}
                  className={inputCls}
                />
              </div>
              <div className="md:col-span-1">
                <label htmlFor={`${idPrefix}-heir-family-${i}`} className={labelCls}>النسبة</label>
                <input
                  id={`${idPrefix}-heir-family-${i}`}
                  name="heirFamily"
                  autoComplete="off"
                  value={h.family ?? ''}
                  onChange={(e) => onSet(i, 'family', e.target.value)}
                  className={inputCls}
                />
              </div>
              <div className="md:col-span-1">
                <label htmlFor={`${idPrefix}-heir-capacity-${i}`} className={labelCls}>صفة الوريث</label>
                <select
                  id={`${idPrefix}-heir-capacity-${i}`}
                  name="heirCapacity"
                  value={h.capacity ?? 'أصالة'}
                  onChange={(e) => onSet(i, 'capacity', e.target.value)}
                  className={inputCls}
                >
                  {HEIR_CAPACITIES.map((o) => (
                    <option key={o} value={o}>{o}</option>
                  ))}
                </select>
              </div>
              {!hHasRep && (
                <>
                  <div className="md:col-span-1">
                    <label htmlFor={`${idPrefix}-heir-type-${i}`} className={labelCls}>نوع العنوان</label>
                    <select
                      id={`${idPrefix}-heir-type-${i}`}
                      name="heirAddressType"
                      value={h.addressType ?? 'عنوان'}
                      onChange={(e) => onSet(i, 'addressType', e.target.value)}
                      className={inputCls}
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
                      name="heirAddress"
                      autoComplete="off"
                      value={h.address ?? ''}
                      onChange={(e) => onSet(i, 'address', e.target.value)}
                      className={inputCls}
                    />
                  </div>
                </>
              )}
              <div className="flex items-end">
                {!locked && (
                  <button type="button" onClick={() => onRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
                    ✖ حذف
                  </button>
                )}
              </div>
            </div>

            {allowRepresentative || hHasRep ? (
              <HeirRepresentativeSection
                heir={h}
                idPrefix={`${idPrefix}-heir-rep-${i}`}
                locked={drafts.isLocked(i, locked)}
                onSet={(key, value) => onSet(i, key as keyof HeirDto, value)}
                onActivate={() => {
                  drafts.mark(i);
                  onSet(i, 'representativeCapacity', 'ولي');
                  onSet(i, 'representativeAddressType', 'عنوان');
                }}
                onRemove={() => {
                  drafts.clear(i);
                  for (const key of REPRESENTATIVE_KEYS) onSet(i, key, '');
                }}
              />
            ) : (
              <p className="mt-2 text-xs text-amber-700" role="note">
                لا يجتمع الممثل الشرعي مع الورثة — احذف الورثة لإضافة ممثل شرعي.
              </p>
            )}
          </div>
        );
      })}
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
  allowRepresentative = true,
  locked = false,
}: {
  heirs: ExecutedHeirDto[];
  onSet: (i: number, key: keyof ExecutedHeirDto, value: string) => void;
  onAdd: () => void;
  onRemove: (i: number) => void;
  idPrefix: string;
  allowAdd?: boolean;
  /** يُخفى زر الممثل عند وجود ورثة للطرف (لا يجتمعان). */
  allowRepresentative?: boolean;
  /** الملف المناب: يمنع حذف الورثة ويقفل ممثلهم المخزَّن. */
  locked?: boolean;
}) {
  const drafts = useDraftRepresentatives();

  if (heirs.length === 0 && !allowAdd && allowRepresentative) return null;

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
      {!allowAdd && (
        <p className="mb-3 text-xs font-medium text-amber-800 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2" role="note">
          الممثل الشرعي للطرف قائم — لا يمكن إضافة ورثة (لا يجتمعان). احذف الممثل الشرعي أولًا.
        </p>
      )}

      {heirs.map((h, i) => {
        const hHasRep = hasRepresentative(h);
        return (
          <div key={i} className="rounded-lg border border-gray-200 bg-white p-3 mb-3 last:mb-0">
            <div className={hHasRep ? 'grid grid-cols-1 md:grid-cols-4 gap-3' : 'grid grid-cols-1 md:grid-cols-7 gap-3'}>
              <div className="md:col-span-1">
                <label htmlFor={`${idPrefix}-heir-name-${i}`} className={labelCls}>الاسم</label>
                <input
                  id={`${idPrefix}-heir-name-${i}`}
                  name="heirName"
                  autoComplete="off"
                  value={h.heirName ?? ''}
                  onChange={(e) => onSet(i, 'heirName', e.target.value)}
                  className={inputCls}
                />
              </div>
              <div className="md:col-span-1">
                <label htmlFor={`${idPrefix}-heir-father-${i}`} className={labelCls}>اسم الأب</label>
                <input
                  id={`${idPrefix}-heir-father-${i}`}
                  name="heirFather"
                  autoComplete="off"
                  value={h.heirFather ?? ''}
                  onChange={(e) => onSet(i, 'heirFather', e.target.value)}
                  className={inputCls}
                />
              </div>
              <div className="md:col-span-1">
                <label htmlFor={`${idPrefix}-heir-family-${i}`} className={labelCls}>النسبة</label>
                <input
                  id={`${idPrefix}-heir-family-${i}`}
                  name="heirFamily"
                  autoComplete="off"
                  value={h.heirFamily ?? ''}
                  onChange={(e) => onSet(i, 'heirFamily', e.target.value)}
                  className={inputCls}
                />
              </div>
              {!hHasRep && (
                <>
                  <div className="md:col-span-1">
                    <label htmlFor={`${idPrefix}-heir-type-${i}`} className={labelCls}>نوع العنوان</label>
                    <select
                      id={`${idPrefix}-heir-type-${i}`}
                      name="heirAddressType"
                      value={h.addressType ?? 'عنوان'}
                      onChange={(e) => onSet(i, 'addressType', e.target.value)}
                      className={inputCls}
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
                      name="heirAddress"
                      autoComplete="off"
                      value={h.heirAddress ?? ''}
                      onChange={(e) => onSet(i, 'heirAddress', e.target.value)}
                      className={inputCls}
                    />
                  </div>
                </>
              )}
              <div className="flex items-end">
                {!locked && (
                  <button type="button" onClick={() => onRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
                    ✖ حذف
                  </button>
                )}
              </div>
            </div>

            {allowRepresentative || hHasRep ? (
              <HeirRepresentativeSection
                heir={h}
                idPrefix={`${idPrefix}-heir-rep-${i}`}
                locked={drafts.isLocked(i, locked)}
                onSet={(key, value) => onSet(i, key as keyof ExecutedHeirDto, value)}
                onActivate={() => {
                  drafts.mark(i);
                  onSet(i, 'representativeCapacity', 'ولي');
                  onSet(i, 'representativeAddressType', 'عنوان');
                }}
                onRemove={() => {
                  drafts.clear(i);
                  for (const key of REPRESENTATIVE_KEYS) onSet(i, key, '');
                }}
              />
            ) : (
              <p className="mt-2 text-xs text-amber-700" role="note">
                لا يجتمع الممثل الشرعي مع الورثة — احذف الورثة لإضافة ممثل شرعي.
              </p>
            )}
          </div>
        );
      })}
    </div>
  );
}
