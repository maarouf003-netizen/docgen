import { useState } from 'react';
import type { ExecutedHeirDto, HeirDto } from '../../types';
import {
  EXECUTED_HEIR_ADDRESS_TYPE_OPTIONS,
  HEIR_ADDRESS_TYPE_OPTIONS,
  HEIR_CAPACITIES,
  heirAddressLabelOf,
  hasRepresentative,
  hasStoredRepresentative,
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
 * مفتاح صف الوريث: هوية الوريث المخزَّن (`id`) لا موضعه — فحذف صفّ أو إعادة ترتيب لا ينقل
 * `key` ولا `htmlFor` إلى وريث آخر (ومعهما حالة الحقل وقراءة قارئ الشاشة). الصف الجديد
 * (بلا `id`) يبقى موضعيًا حتى يُحفظ فيأخذ هويته؛ ولا تصادم لأن معرّفات القاعدة تبدأ من 1.
 */
function heirRowKey(heirId: number | undefined, index: number): string {
  return heirId != null ? `id${heirId}` : String(index);
}

/** معرّف حقل داخل صف وريث — يُبنى على مفتاح الصف المستقر لا على الموضع. */
function heirFieldId(idPrefix: string, rowKey: string, field: string): string {
  return `${idPrefix}-heir-${field}-${rowKey}`;
}

/**
 * حالة «ممثل جديد لم يُحفظ بعد» على الملف المناب: الممثل المخزَّن من المنيب يُعرض
 * للقراءة فقط (الحارس يرفض تعديله/إزالته)، بينما الممثل الذي يفعّله محامي المناب
 * الآن يبقى قابلًا للتحرير حتى الحفظ.
 *
 * المفتاح هو `id` الوريث المخزَّن لا موضعه في القائمة: حذف صفّ أو إعادة ترتيب لا ينقل
 * القفل إلى وريث آخر. والصف الجديد (بلا `id`) غير مقفول أصلًا لأن ممثله «تعبئة أولى»
 * مسموحة خلفيًا (الحارس يرفض فقط تعديل ممثل مخزَّن).
 *
 * عمر العلامة = عمر نسخة المكوّن (جلسة النموذج): بعد الحفظ يغادر النموذج الصفحة فيُنشأ
 * المكوّن من جديد ببيانات الخادم (ممثل مخزَّن ⇒ مقفول). ولا تُنظَّف العلامات بنيويًا لأن
 * «صارت مخزَّنة» ليست إشارة يملكها المحرر — لا يملك إلا البيانات التي بين يديه.
 */
function useDraftRepresentatives() {
  const [drafts, setDrafts] = useState<ReadonlySet<number>>(() => new Set());

  return {
    isLocked: (heirId: number | undefined, locked: boolean) =>
      locked && heirId != null && !drafts.has(heirId),
    mark: (heirId: number) =>
      setDrafts((prev) => {
        if (prev.has(heirId)) return prev;
        const next = new Set(prev);
        next.add(heirId);
        return next;
      }),
    clear: (heirId: number) =>
      setDrafts((prev) => {
        if (!prev.has(heirId)) return prev;
        const next = new Set(prev);
        next.delete(heirId);
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
          ＋ إضافة ممثل شرعي لهذا الوريث
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
        // الظهور/الإخفاء يتبع المخزَّن فعلًا (كحمولة `toHeirPayload`)، بينما ظهور المحرر
        // يتبع التفعيل (صفة/اسم) — فتفعيل الممثل بصفة وحدها لا يُخفي العنوان بلا حفظ.
        const hHasStoredRep = hasStoredRepresentative(h);
        const rowKey = heirRowKey(h.id, i);
        return (
          <div key={rowKey} className="rounded-lg border border-gray-200 bg-white p-3 mb-3 last:mb-0">
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
              <div className="min-w-0">
                <label htmlFor={heirFieldId(idPrefix, rowKey, 'name')} className={labelCls}>اسم الوريث</label>
                <input
                  id={heirFieldId(idPrefix, rowKey, 'name')}
                  name="heirName"
                  autoComplete="off"
                  value={h.name ?? ''}
                  onChange={(e) => onSet(i, 'name', e.target.value)}
                  className={inputCls}
                />
              </div>
              <div className="min-w-0">
                <label htmlFor={heirFieldId(idPrefix, rowKey, 'father')} className={labelCls}>اسم أب الوريث</label>
                <input
                  id={heirFieldId(idPrefix, rowKey, 'father')}
                  name="heirFather"
                  autoComplete="off"
                  value={h.father ?? ''}
                  onChange={(e) => onSet(i, 'father', e.target.value)}
                  className={inputCls}
                />
              </div>
              <div className="min-w-0">
                <label htmlFor={heirFieldId(idPrefix, rowKey, 'family')} className={labelCls}>النسبة</label>
                <input
                  id={heirFieldId(idPrefix, rowKey, 'family')}
                  name="heirFamily"
                  autoComplete="off"
                  value={h.family ?? ''}
                  onChange={(e) => onSet(i, 'family', e.target.value)}
                  className={inputCls}
                />
              </div>
              <div className="min-w-0">
                <label htmlFor={heirFieldId(idPrefix, rowKey, 'capacity')} className={labelCls}>صفة الوريث</label>
                <select
                  id={heirFieldId(idPrefix, rowKey, 'capacity')}
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
              {!hHasStoredRep && (
                <>
                  <div className="min-w-0">
                    <label htmlFor={heirFieldId(idPrefix, rowKey, 'type')} className={labelCls}>نوع العنوان</label>
                    <select
                      id={heirFieldId(idPrefix, rowKey, 'type')}
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
                  <div className="sm:col-span-2 min-w-0">
                    <label htmlFor={heirFieldId(idPrefix, rowKey, 'address')} className={labelCls}>
                      {heirAddressLabelOf(h.addressType)}
                    </label>
                    <input
                      id={heirFieldId(idPrefix, rowKey, 'address')}
                      name="heirAddress"
                      autoComplete="off"
                      value={h.address ?? ''}
                      onChange={(e) => onSet(i, 'address', e.target.value)}
                      className={inputCls}
                    />
                  </div>
                </>
              )}
              <div className="flex items-end justify-end sm:col-span-2 lg:col-span-3">
                {!locked && (
                  <button type="button" onClick={() => onRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
                    ✖ حذف
                  </button>
                )}
              </div>
            </div>

            <HeirRepresentativeSection
              heir={h}
              idPrefix={`${idPrefix}-heir-rep-${rowKey}`}
              locked={drafts.isLocked(h.id, locked)}
              onSet={(key, value) => onSet(i, key as keyof HeirDto, value)}
              onActivate={() => {
                if (h.id != null) drafts.mark(h.id);
                onSet(i, 'representativeCapacity', 'ولي');
                onSet(i, 'representativeAddressType', 'عنوان');
              }}
              onRemove={() => {
                if (h.id != null) drafts.clear(h.id);
                for (const key of REPRESENTATIVE_KEYS) onSet(i, key, '');
              }}
            />
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
  locked = false,
}: {
  heirs: ExecutedHeirDto[];
  onSet: (i: number, key: keyof ExecutedHeirDto, value: string) => void;
  onAdd: () => void;
  onRemove: (i: number) => void;
  idPrefix: string;
  /** الملف المناب: يمنع حذف الورثة ويقفل ممثلهم المخزَّن. */
  locked?: boolean;
}) {
  const drafts = useDraftRepresentatives();

  return (
    <div className="mt-4 rounded-lg bg-gray-50 border border-gray-200 p-4">
      <div className="flex flex-wrap items-center justify-end gap-2 mb-3">
        <button
          type="button"
          onClick={onAdd}
          className="bg-gray-500 hover:bg-gray-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
        >
          ＋ إضافة وريث
        </button>
      </div>

      {heirs.map((h, i) => {
        const hHasStoredRep = hasStoredRepresentative(h);
        const rowKey = heirRowKey(h.id, i);
        return (
          <div key={rowKey} className="rounded-lg border border-gray-200 bg-white p-3 mb-3 last:mb-0">
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
              <div className="min-w-0">
                <label htmlFor={heirFieldId(idPrefix, rowKey, 'name')} className={labelCls}>الاسم</label>
                <input
                  id={heirFieldId(idPrefix, rowKey, 'name')}
                  name="heirName"
                  autoComplete="off"
                  value={h.heirName ?? ''}
                  onChange={(e) => onSet(i, 'heirName', e.target.value)}
                  className={inputCls}
                />
              </div>
              <div className="min-w-0">
                <label htmlFor={heirFieldId(idPrefix, rowKey, 'father')} className={labelCls}>اسم الأب</label>
                <input
                  id={heirFieldId(idPrefix, rowKey, 'father')}
                  name="heirFather"
                  autoComplete="off"
                  value={h.heirFather ?? ''}
                  onChange={(e) => onSet(i, 'heirFather', e.target.value)}
                  className={inputCls}
                />
              </div>
              <div className="min-w-0">
                <label htmlFor={heirFieldId(idPrefix, rowKey, 'family')} className={labelCls}>النسبة</label>
                <input
                  id={heirFieldId(idPrefix, rowKey, 'family')}
                  name="heirFamily"
                  autoComplete="off"
                  value={h.heirFamily ?? ''}
                  onChange={(e) => onSet(i, 'heirFamily', e.target.value)}
                  className={inputCls}
                />
              </div>
              {!hHasStoredRep && (
                <>
                  <div className="min-w-0">
                    <label htmlFor={heirFieldId(idPrefix, rowKey, 'type')} className={labelCls}>نوع العنوان</label>
                    <select
                      id={heirFieldId(idPrefix, rowKey, 'type')}
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
                  <div className="sm:col-span-2 min-w-0">
                    <label htmlFor={heirFieldId(idPrefix, rowKey, 'address')} className={labelCls}>
                      {heirAddressLabelOf(h.addressType)}
                    </label>
                    <input
                      id={heirFieldId(idPrefix, rowKey, 'address')}
                      name="heirAddress"
                      autoComplete="off"
                      value={h.heirAddress ?? ''}
                      onChange={(e) => onSet(i, 'heirAddress', e.target.value)}
                      className={inputCls}
                    />
                  </div>
                </>
              )}
              <div className="flex items-end justify-end sm:col-span-2 lg:col-span-3">
                {!locked && (
                  <button type="button" onClick={() => onRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
                    ✖ حذف
                  </button>
                )}
              </div>
            </div>

            <HeirRepresentativeSection
              heir={h}
              idPrefix={`${idPrefix}-heir-rep-${rowKey}`}
              locked={drafts.isLocked(h.id, locked)}
              onSet={(key, value) => onSet(i, key as keyof ExecutedHeirDto, value)}
              onActivate={() => {
                if (h.id != null) drafts.mark(h.id);
                onSet(i, 'representativeCapacity', 'ولي');
                onSet(i, 'representativeAddressType', 'عنوان');
              }}
              onRemove={() => {
                if (h.id != null) drafts.clear(h.id);
                for (const key of REPRESENTATIVE_KEYS) onSet(i, key, '');
              }}
            />
          </div>
        );
      })}
    </div>
  );
}
