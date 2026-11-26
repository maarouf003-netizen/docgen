import type { ReactNode } from 'react';
import type { AssetDto } from '../../../types';
import {
  ASSET_KINDS,
  DEFAULT_RIGHT_TYPE,
  MAX_ASSETS_PER_KIND,
  RIGHT_TYPE_OPTIONS,
  shareTypesFor,
  supportsRightType,
} from '../documentFormConstants';
import { FormSectionTitle } from '../FormSectionTitle';
import { assetKindLabel } from '../../../utils/assetDisplay';

const ASSET_CONTROL_CLS =
  'w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500';
const ASSET_FIELD_CLS = 'min-w-[150px] flex-1';

/** حقل أصل داخل شبكة مرنة تملأ الصف بلا فراغات (عقار/مركبة). */
function AssetField({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className={ASSET_FIELD_CLS}>
      <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
      {children}
    </div>
  );
}

/** حقول نهاية بطاقة العقار/المركبة: نوع الحق ثم الحصة ثم تاريخ الحجز (آخرًا دائمًا). */
function AssetRightShareSeizureFields({
  index,
  kind,
  asset,
  shareable,
  onSet,
  seizureLocked,
}: {
  index: number;
  kind: string;
  asset: AssetDto;
  shareable: boolean;
  onSet: (key: keyof AssetDto, value: string) => void;
  seizureLocked: boolean;
}) {
  return (
    <>
      <AssetField label="نوع الحق">
        <select
          aria-label="نوع الحق"
          name="assetRightType"
          value={asset.rightType ?? DEFAULT_RIGHT_TYPE}
          onChange={(ev) => onSet('rightType', ev.target.value)}
          className={ASSET_CONTROL_CLS}
        >
          {RIGHT_TYPE_OPTIONS.map((o) => (
            <option key={o} value={o}>{o}</option>
          ))}
        </select>
      </AssetField>
      {shareable && (
        <AssetField label="مقدار الحصة">
          <select
            aria-label="مقدار الحصة"
            name="assetShareType"
            value={asset.shareType ?? shareTypesFor(kind)[0]}
            onChange={(ev) => onSet('shareType', ev.target.value)}
            className={ASSET_CONTROL_CLS}
          >
            {shareTypesFor(kind).map((o) => (
              <option key={o}>{o}</option>
            ))}
          </select>
        </AssetField>
      )}
      <AssetField label="تاريخ القاء الحجز">
        <input
          id={`asset-seizure-${index}`}
          aria-label="تاريخ القاء الحجز"
          name="assetSeizureDate"
          autoComplete="off"
          value={asset.seizureDate ?? ''}
          onChange={(ev) => onSet('seizureDate', ev.target.value)}
          type="text"
          placeholder="مثال: 1/8/2026"
          className={ASSET_CONTROL_CLS}
        />
        {seizureLocked && (
          <p className="text-xs text-amber-700 mt-1" role="note">
            عليه إنابة سارية — التاريخ يُعدَّل ولا يُحذف
          </p>
        )}
      </AssetField>
    </>
  );
}

/** أزرار إضافة الأموال (بالترتيب المعتمد) مع تسمية بطاقة العرض لكل نوع. */
const ASSET_ADD_BUTTONS: { kind: string; label: string; icon: string }[] = [
  { kind: ASSET_KINDS.realEstate, label: 'إضافة عقار', icon: '🏡' },
  { kind: ASSET_KINDS.vehicle, label: 'إضافة مركبة', icon: '🚗' },
  { kind: ASSET_KINDS.shop, label: 'إضافة متجر', icon: '🏪' },
  { kind: ASSET_KINDS.salaryGuarantee, label: 'إضافة كفالة رواتب', icon: '💼' },
  { kind: ASSET_KINDS.unregisteredShop, label: 'إضافة متجر غير مسجل', icon: '🛒' },
];

export interface ApplicantAssetsSectionProps {
  lock: boolean;
  assets: AssetDto[];
  onEstateSet: (i: number, key: keyof AssetDto, value: string) => void;
  seizureLockedIds?: Set<number>;
  onEstateRemove: (i: number) => void;
  onOwnerToggle: (i: number, name: string) => void;
  onSingleOwnerSet: (i: number, name: string) => void;
  onEstateAdd: (kind: string) => void;
  ownerOptions: () => string[];
}

/**
 * أقسام أموال «طالبة التنفيذ» (عقار/مركبة/متجر/كفالة رواتب/متجر غير مسجل)
 * وبطاقاتها وأزرار الإضافة — منقولة حرفيًا من `ApplicantSideSections`.
 */
export function ApplicantAssetsSection({
  lock,
  assets,
  onEstateSet,
  seizureLockedIds,
  onEstateRemove,
  onOwnerToggle,
  onSingleOwnerSet,
  onEstateAdd,
  ownerOptions,
}: ApplicantAssetsSectionProps) {
  if (lock) return null;

  return (
    <>
      <FormSectionTitle title="الأموال المنقولة وغير المنقولة" />
      {assets.map((a, i) => {
        const kind = a.assetKind ?? ASSET_KINDS.realEstate;
        const kindLabel = assetKindLabel(kind);
        const shareable = shareTypesFor(kind).length > 0;
        const singleOwner = kind === ASSET_KINDS.salaryGuarantee;
        const ownersLabel = singleOwner
          ? 'صاحب الراتب'
          : kind === ASSET_KINDS.vehicle
            ? 'مالكو المركبة'
            : kind === ASSET_KINDS.shop
              ? 'مالكو المتجر'
              : kind === ASSET_KINDS.unregisteredShop
                ? 'مالك المتجر'
                : 'مالكو العقار';
        const options = [...new Set([...ownerOptions(), ...(a.owners ?? [])])];
        return (
          <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
            <div className="flex justify-between items-center mb-3">
              <span className="font-medium text-gray-700 text-sm">{kindLabel} {i + 1}</span>
              <button type="button" onClick={() => onEstateRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
                ✖ حذف
              </button>
            </div>

            {kind === ASSET_KINDS.realEstate && (
              <div className="flex flex-wrap gap-3">
                {([
                  ['propertyNumber', 'رقم العقار'],
                  ['caseNumber', 'رقم المحضر'],
                  ['propertyDistrict', 'المنطقة العقارية'],
                  ['landRegistry', 'المصالح العقارية'],
                ] as const).map(([k, label]) => (
                  <AssetField key={k} label={label}>
                    <input
                      aria-label={label}
                      name={`asset-${k}`}
                      autoComplete="off"
                      value={a[k] ?? ''}
                      onChange={(ev) => onEstateSet(i, k, ev.target.value)}
                      className={ASSET_CONTROL_CLS}
                    />
                  </AssetField>
                ))}
                <AssetRightShareSeizureFields
                  index={i}
                  kind={kind}
                  asset={a}
                  shareable={shareable}
                  onSet={(key, value) => onEstateSet(i, key, value)}
                  seizureLocked={a.id != null && (seizureLockedIds?.has(a.id) ?? false)}
                />
              </div>
            )}

            {kind === ASSET_KINDS.vehicle && (
              <div className="flex flex-wrap gap-3">
                {([
                  ['vehicleType', 'النوع'],
                  ['vehicleClass', 'الفئة'],
                  ['plateNumber', 'رقم اللوحة'],
                  ['vehicleGovernorate', 'محافظة المركبة'],
                ] as const).map(([k, label]) => (
                  <AssetField key={k} label={label}>
                    <input
                      aria-label={label}
                      name={`asset-${k}`}
                      autoComplete="off"
                      value={a[k] ?? ''}
                      onChange={(ev) => onEstateSet(i, k, ev.target.value)}
                      className={ASSET_CONTROL_CLS}
                    />
                  </AssetField>
                ))}
                <AssetRightShareSeizureFields
                  index={i}
                  kind={kind}
                  asset={a}
                  shareable={shareable}
                  onSet={(key, value) => onEstateSet(i, key, value)}
                  seizureLocked={a.id != null && (seizureLockedIds?.has(a.id) ?? false)}
                />
              </div>
            )}

            {kind === ASSET_KINDS.shop && (
              <div className="grid md:grid-cols-3 gap-3">
                {([
                  ['registerNumber', 'رقم السجل'],
                  ['registrationDate', 'تاريخ التسجيل', 'مثال: 1/8/2026'],
                  ['shopGovernorate', 'المحافظة'],
                  ['shopDescription', 'وصف المتجر'],
                  ['shopLocation', 'موقعه'],
                ] as const).map(([k, label, placeholder]) => (
                  <div key={k}>
                    <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                    <input
                      value={a[k] ?? ''}
                      onChange={(ev) => onEstateSet(i, k, ev.target.value)}
                      type="text"
                      placeholder={placeholder}
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    />
                  </div>
                ))}
              </div>
            )}

            {kind === ASSET_KINDS.salaryGuarantee && (
              <div className="grid md:grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">{ownersLabel}</label>
                  <select
                    value={(a.owners ?? [])[0] ?? ''}
                    onChange={(ev) => onSingleOwnerSet(i, ev.target.value)}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  >
                    <option value="">— اختر —</option>
                    {options.map((o) => (
                      <option key={o} value={o}>{o}</option>
                    ))}
                  </select>
                  {options.length === 0 && (
                    <p className="text-xs text-gray-400">أدخل اسم المقترض أو الكفيل أو الورثة أولًا لاختيار صاحب الراتب</p>
                  )}
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">الجهة العامة التي يعمل لديها</label>
                  <input
                    aria-label="الجهة العامة التي يعمل لديها"
                    name="salaryPublicEntity"
                    autoComplete="organization"
                    value={a.publicEntity ?? ''}
                    onChange={(ev) => onEstateSet(i, 'publicEntity', ev.target.value)}
                    placeholder="مثال: المصرف التجاري السوري"
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  />
                </div>
                <div className="md:col-span-2">
                  <label className="block text-xs font-bold text-gray-600 mb-1">ملاحظات</label>
                  <textarea value={a.notes ?? ''} onChange={(ev) => onEstateSet(i, 'notes', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
              </div>
            )}

            {kind === ASSET_KINDS.unregisteredShop && (
              <div className="grid md:grid-cols-3 gap-3">
                {([
                  ['licenseNumber', 'رقم الترخيص'],
                  ['licenseDate', 'تاريخ الترخيص', 'مثال: 1/8/2026'],
                  ['licenseIssuer', 'الجهة مصدرة الترخيص'],
                ] as const).map(([k, label, placeholder]) => (
                  <div key={k}>
                    <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                    <input
                      value={a[k] ?? ''}
                      onChange={(ev) => onEstateSet(i, k, ev.target.value)}
                      type="text"
                      placeholder={placeholder}
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    />
                  </div>
                ))}
              </div>
            )}

            {/* تاريخ القاء الحجز: الأنواع التي لا تحمل شبكة حق/حصة (متجر/كفالة/غير مسجل) — نص حر بقاعدة التواريخ */}
            {!supportsRightType(kind) && (
              <div className="mt-3 max-w-xs">
                <label htmlFor={`asset-seizure-${i}`} className="block text-xs font-bold text-gray-600 mb-1">تاريخ القاء الحجز</label>
                <input
                  id={`asset-seizure-${i}`}
                  value={a.seizureDate ?? ''}
                  onChange={(ev) => onEstateSet(i, 'seizureDate', ev.target.value)}
                  type="text"
                  placeholder="مثال: 1/8/2026"
                  className={ASSET_CONTROL_CLS}
                />
                {a.id != null && (seizureLockedIds?.has(a.id) ?? false) && (
                  <p className="text-xs text-amber-700 mt-1" role="note">
                    عليه إنابة سارية — التاريخ يُعدَّل ولا يُحذف
                  </p>
                )}
              </div>
            )}

            {shareable && !supportsRightType(kind) && (
              <div className="mt-3 max-w-xs">
                <label className="block text-xs font-bold text-gray-600 mb-1">مقدار الحصة</label>
                <select
                  value={a.shareType ?? shareTypesFor(kind)[0]}
                  onChange={(ev) => onEstateSet(i, 'shareType', ev.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  {shareTypesFor(kind).map((o) => (
                    <option key={o}>{o}</option>
                  ))}
                </select>
              </div>
            )}

            {!singleOwner && (
              <div className="mt-3">
                <span className="block text-xs font-bold text-gray-600 mb-1">{ownersLabel}</span>
                {options.length > 0 ? (
                  <div className="flex flex-wrap gap-x-5 gap-y-1">
                    {options.map((o) => (
                      <label key={o} className="flex items-center gap-2 min-h-11 text-sm text-gray-700">
                        <input
                          type="checkbox"
                          checked={(a.owners ?? []).includes(o)}
                          onChange={() => onOwnerToggle(i, o)}
                          className="h-4 w-4 text-emerald-600"
                        />
                        {o}
                      </label>
                    ))}
                  </div>
                ) : (
                  <p className="text-xs text-gray-400">أدخل اسم المقترض أو الكفيل أو الورثة أولًا لاختيار المالك</p>
                )}
              </div>
            )}
          </div>
        );
      })}
      <div className="flex flex-wrap gap-2">
        {ASSET_ADD_BUTTONS.map((b) => {
          const count = assets.filter((a) => a.assetKind === b.kind).length;
          const atMax = count >= MAX_ASSETS_PER_KIND;
          return (
            <button
              key={b.kind}
              type="button"
              onClick={() => onEstateAdd(b.kind)}
              disabled={atMax}
              className="bg-red-700 hover:bg-red-600 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
            >
              {atMax ? '🛑 الحد الأقصى' : `${b.icon} ${b.label}`}
            </button>
          );
        })}
      </div>
    </>
  );
}
