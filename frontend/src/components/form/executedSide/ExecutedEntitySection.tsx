import type { EntityNature, ExecutedPublicEntityDto } from '../../../types';
import { ADDRESS_TYPE_OPTIONS, ENTITY_NATURE_OPTIONS, addressLabelOf } from '../documentFormConstants';
import { FormSectionTitle } from '../FormSectionTitle';

export interface ExecutedEntitySectionProps {
  isMirror: boolean;
  executedPublicEntities: ExecutedPublicEntityDto[];
  onEntitySet: (i: number, key: keyof ExecutedPublicEntityDto, value: string) => void;
  onEntityAdd: (nature?: EntityNature) => void;
  onEntityRemove: (i: number) => void;
  onPickRegistry?: (i: number) => void;
  onEntityUnlink?: (i: number) => void;
  onPersonAdd: () => void;
}

/** بطاقات «المنفذ عليه»: جهة عامة (مرتبطة بالسجل) أو شخص اعتباري. */
export function ExecutedEntitySection({
  isMirror,
  executedPublicEntities,
  onEntitySet,
  onEntityAdd,
  onEntityRemove,
  onPickRegistry,
  onEntityUnlink,
  onPersonAdd,
}: ExecutedEntitySectionProps) {
  return (
    <>
      <FormSectionTitle title="🏛️ المنفذ عليه" />
      {executedPublicEntities.map((e, i) => {
        const eIsLegal = e.nature === 'legal';
        return (
          <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
            <div className="flex justify-between items-center mb-3">
              <span className="font-medium text-gray-700 text-sm">
                {eIsLegal ? 'شخص اعتباري' : 'جهة عامة'} {i + 1}
              </span>
              {!isMirror && executedPublicEntities.length > 1 && (
                <button type="button" onClick={() => onEntityRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
                  ✖ حذف
                </button>
              )}
            </div>
            <div className="grid md:grid-cols-3 gap-3 mb-3">
              <select
                aria-label="نوع الطرف"
                disabled={isMirror || e.registryId != null}
                value={e.nature ?? 'public'}
                onChange={(ev) => onEntitySet(i, 'nature', ev.target.value)}
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:bg-gray-50 disabled:cursor-not-allowed"
              >
                {ENTITY_NATURE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>
            {eIsLegal ? (
              <div className="grid md:grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">الشخص الاعتباري</label>
                  <input value={e.entityName ?? ''} readOnly={isMirror} aria-readonly={isMirror || undefined} onChange={(ev) => onEntitySet(i, 'entityName', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">رقم تسجيله</label>
                  <input value={e.registrationNumber ?? ''} readOnly={isMirror} aria-readonly={isMirror || undefined} onChange={(ev) => onEntitySet(i, 'registrationNumber', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">يمثلها</label>
                  <input value={e.representedBy ?? ''} readOnly={isMirror} aria-readonly={isMirror || undefined} onChange={(ev) => onEntitySet(i, 'representedBy', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">المحافظة</label>
                  <input value={e.governorate ?? ''} readOnly={isMirror} aria-readonly={isMirror || undefined} onChange={(ev) => onEntitySet(i, 'governorate', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">نوع العنوان</label>
                  <select value={e.addressType ?? 'موطن مختار'} disabled={isMirror} aria-readonly={isMirror || undefined} onChange={(ev) => onEntitySet(i, 'addressType', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:bg-gray-50 disabled:cursor-not-allowed">
                    {ADDRESS_TYPE_OPTIONS.map((o) => (
                      <option key={o.value} value={o.value}>{o.label}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">{addressLabelOf(e.addressType)}</label>
                  <input value={e.address ?? ''} readOnly={isMirror} aria-readonly={isMirror || undefined} onChange={(ev) => onEntitySet(i, 'address', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
              </div>
            ) : (
              <div className="grid md:grid-cols-3 gap-3">
                <div>
                  <div className="flex items-center justify-between gap-2 mb-1">
                    <label className="block text-xs font-bold text-gray-600">اسم الجهة العامة</label>
                    {e.registryId != null && (
                      <span className="rounded-full bg-emerald-50 border border-emerald-100 text-emerald-700 px-2 py-0.5 text-[11px] whitespace-nowrap">
                        مرتبطة بالسجل ✓
                      </span>
                    )}
                  </div>
                  <input value={e.entityName ?? ''} readOnly placeholder="اختر من السجل المرجعي…" className="w-full min-h-11 border border-gray-300 bg-gray-50 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 cursor-not-allowed" />
                  {(e.entityName ?? '').trim() && e.registryId == null && (
                    <p className="text-xs text-red-600 mt-1">يجب اختيار هذه الجهة من السجل المرجعي</p>
                  )}
                  {!isMirror && onPickRegistry && (
                    <div className="flex items-center gap-2 mt-1.5">
                      <button
                        type="button"
                        onClick={() => onPickRegistry(i)}
                        className="border border-emerald-200 text-emerald-800 hover:bg-emerald-50 rounded-lg px-3 py-2 text-xs min-h-11"
                      >
                        {e.registryId != null ? 'تغيير من السجل…' : 'اختيار من السجل…'}
                      </button>
                      {e.registryId != null && onEntityUnlink && (
                        <button
                          type="button"
                          onClick={() => onEntityUnlink(i)}
                          className="border border-gray-200 text-gray-500 hover:bg-gray-50 rounded-lg px-3 py-2 text-xs min-h-11"
                        >
                          فك الربط
                        </button>
                      )}
                      {(e.entityName ?? '').trim() && e.registryId == null && onEntityUnlink && (
                        <button
                          type="button"
                          onClick={() => onEntityUnlink(i)}
                          className="border border-gray-200 text-gray-500 hover:bg-gray-50 rounded-lg px-3 py-2 text-xs min-h-11"
                        >
                          مسح
                        </button>
                      )}
                    </div>
                  )}
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">الفرع</label>
                  <input value={e.entityBranch ?? ''} readOnly placeholder="الفرع" className="w-full min-h-11 border border-gray-300 bg-gray-50 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 cursor-not-allowed" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">المحافظة</label>
                  <input value={e.governorate ?? ''} readOnly placeholder="المحافظة" className="w-full min-h-11 border border-gray-300 bg-gray-50 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 cursor-not-allowed" />
                </div>
              </div>
            )}
          </div>
        );
      })}
      {!isMirror && (
        <div className="flex flex-wrap gap-3 items-center">
          <button
            type="button"
            onClick={() => onEntityAdd('public')}
            className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
          >
            ＋ إضافة جهة عامة
          </button>
          <button
            type="button"
            onClick={() => onEntityAdd('legal')}
            className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
          >
            ＋ إضافة شخص اعتباري
          </button>
          <button
            type="button"
            onClick={onPersonAdd}
            className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
          >
            ＋ إضافة شخص طبيعي
          </button>
        </div>
      )}
    </>
  );
}
