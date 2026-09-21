import { ASSET_KINDS } from '../form/documentFormConstants';
import { assetDisplayName } from '../../utils/assetDisplay';
import { blockedAssetIds } from '../../utils/delegationAssets';
import type { AssetDto, DelegationDto, DocumentResponse } from '../../types';
import { SectionCard } from './SectionCard';

/**
 * يعرض قائمة الأموال المرهونة (المنقولة وغير المنقولة) في بطاقات، مع شارة حالة
 * الحجز لكل مال (من `seizureDate` الخاص به: خضراء «تم القاء الحجز» أو حمراء
 * «لم يتم القاء الحجز») وشارة زرقاء «مناب» للمال المحجوب بإنابة سارية
 * (مطابقة واحد-لواحد عبر `blockedAssetIds` — فالتوأم السليم لا يُوسم).
 */
export function AssetsSection({ doc, delegations = [] }: { doc: DocumentResponse; delegations?: DelegationDto[] }) {
  const assets = doc.assets ?? [];
  const blocked = blockedAssetIds(delegations, assets);
  return (
    <SectionCard title="الأموال المنقولة وغير المنقولة">
      {assets.length === 0 && <p className="text-gray-400 text-sm">لا توجد أموال مرهونة</p>}
      {assets.map((r, i) => (
        <AssetRow key={r.id ?? i} asset={r} delegated={r.id != null && blocked.has(r.id)} />
      ))}
    </SectionCard>
  );
}

function AssetRow({ asset: r, delegated }: { asset: AssetDto; delegated: boolean }) {
  const kind = r.assetKind;
  const seized = Boolean(r.seizureDate?.trim());
  return (
    <div className="rounded-lg border border-gray-100 bg-gray-50/60 px-3 py-2.5 mb-2 last:mb-0 text-sm">
      <div className="flex flex-wrap items-center gap-x-5 gap-y-1">
        <span className="font-bold text-emerald-900">{assetDisplayName(r)}</span>
        <span
          className={`rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap ${
            seized ? 'bg-emerald-100 text-emerald-800' : 'bg-red-100 text-red-800'
          }`}
        >
          {seized ? 'تم القاء الحجز' : 'لم يتم القاء الحجز'}
        </span>
        {delegated && (
          <span className="rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap bg-sky-100 text-sky-800">
            مناب
          </span>
        )}
        <Rows kind={kind} asset={r} />
        <span className="inline-flex items-center gap-1">
          <span className="text-gray-500">الملاك</span>
          <span className="text-gray-800">{(r.owners ?? []).join(' و ') || '—'}</span>
        </span>
      </div>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string | undefined }) {
  return (
    <span className="inline-flex items-center gap-1">
      <span className="text-gray-500">{label}</span>
      <span className="text-gray-800">{value || '—'}</span>
    </span>
  );
}

/** صفوف العرض الخاصة بكل نوع أصل. */
function Rows({ kind, asset: r }: { kind: string | undefined; asset: AssetDto }) {
  if (kind === ASSET_KINDS.realEstate) {
    return (
      <>
        <Row label="رقم العقار" value={r.propertyNumber} />
        <Row label="المنطقة العقارية" value={r.propertyDistrict} />
        <Row label="المصالح العقارية المختصة" value={r.landRegistry} />
        <Row label="تاريخ القاء الحجز" value={r.seizureDate?.slice(0, 10)} />
        {r.shareType ? <Row label="مقدار الحصة" value={r.shareType} /> : null}
      </>
    );
  }
  if (kind === ASSET_KINDS.vehicle) {
    return (
      <>
        <Row label="النوع" value={r.vehicleType} />
        <Row label="الفئة" value={r.vehicleClass} />
        <Row label="رقم اللوحة" value={r.plateNumber} />
        <Row label="محافظة المركبة" value={r.vehicleGovernorate} />
        <Row label="تاريخ القاء الحجز" value={r.seizureDate?.slice(0, 10)} />
        {r.shareType ? <Row label="مقدار الحصة" value={r.shareType} /> : null}
      </>
    );
  }
  if (kind === ASSET_KINDS.shop) {
    return (
      <>
        <Row label="رقم السجل" value={r.registerNumber} />
        <Row label="تاريخ التسجيل" value={r.registrationDate?.slice(0, 10)} />
        <Row label="المحافظة" value={r.shopGovernorate} />
        <Row label="وصف المتجر" value={r.shopDescription} />
        <Row label="الموقع" value={r.shopLocation} />
        <Row label="تاريخ القاء الحجز" value={r.seizureDate?.slice(0, 10)} />
        {r.shareType ? <Row label="مقدار الحصة" value={r.shareType} /> : null}
      </>
    );
  }
  if (kind === ASSET_KINDS.salaryGuarantee) {
    return (
      <>
        <Row label="الجهة العامة" value={r.publicEntity} />
        {r.notes ? <Row label="ملاحظات" value={r.notes} /> : null}
        <Row label="تاريخ القاء الحجز" value={r.seizureDate?.slice(0, 10)} />
      </>
    );
  }
   if (kind === ASSET_KINDS.unregisteredShop) {
     return (
       <>
         <Row label="رقم الترخيص" value={r.licenseNumber} />
         <Row label="تاريخ الترخيص" value={r.licenseDate?.slice(0, 10)} />
         <Row label="الجهة مصدرة الترخيص" value={r.licenseIssuer} />
         <Row label="تاريخ القاء الحجز" value={r.seizureDate?.slice(0, 10)} />
         {r.notes ? <Row label="ملاحظات" value={r.notes} /> : null}
       </>
     );
   }
   return <Row label="تاريخ القاء الحجز" value={r.seizureDate?.slice(0, 10)} />;
 }