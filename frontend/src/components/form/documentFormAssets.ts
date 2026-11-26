import type { AssetDto } from '../../types';

/** الأنواع المدعومة للأموال (مطابقة AssetKindCatalog في الخلفية). */
export const ASSET_KINDS = {
  realEstate: 'عقار',
  vehicle: 'مركبة',
  shop: 'متجر',
  salaryGuarantee: 'كفالة رواتب',
  unregisteredShop: 'متجر غير مسجل',
} as const;

/** الأنواع الحاملة لمقدار الحصة (تمام/حصة سهمية). */
export const SHAREABLE_ASSET_KINDS = new Set<string>([
  ASSET_KINDS.realEstate,
  ASSET_KINDS.vehicle,
  ASSET_KINDS.shop,
]);

/** الأنواع الحاملة لنوع الحق: العقار والمركبة فقط (قرار المالك). */
export const ASSET_RIGHT_TYPE_KINDS = new Set<string>([ASSET_KINDS.realEstate, ASSET_KINDS.vehicle]);

/** خيارات نوع الحق — مطابقة لـ `RightTypeCatalog` في الخلفية. */
export const RIGHT_TYPE_OPTIONS = ['حق رقبة', 'حق انتفاع'] as const;

/** نوع الحق الافتراضي للأصول المدعومة. */
export const DEFAULT_RIGHT_TYPE = 'حق رقبة';

export function supportsRightType(kind: string | undefined): boolean {
  return Boolean(kind && ASSET_RIGHT_TYPE_KINDS.has(kind));
}

/** قيمة «تمام» الخاصة بنوع الأصل. */
export function fullShareLabel(kind: string | undefined): string {
  if (kind === ASSET_KINDS.vehicle) return 'تمام المركبة';
  if (kind === ASSET_KINDS.shop) return 'تمام المتجر';
  return 'تمام العقار';
}

/** خيارات مقدار الحصة حسب نوع الأصل (العقار/المركبة/المتجر فقط). */
export function shareTypesFor(kind: string | undefined): string[] {
  if (!kind || !SHAREABLE_ASSET_KINDS.has(kind)) return [];
  return [fullShareLabel(kind), 'حصة سهمية'];
}

/** سقف عدد الأصول من كل نوع على حدة. */
export const MAX_ASSETS_PER_KIND = 20;

/** أنواع الأموال التي تُعرض في «منفذ جبريا» (كفالة الرواتب مستثناة). */
export function isAuctionableKind(kind: string | null | undefined): boolean {
  return Boolean(kind && kind !== ASSET_KINDS.salaryGuarantee);
}

export function emptyAsset(kind: string): AssetDto {
  const base: AssetDto = { assetKind: kind, owners: [] };
  if (SHAREABLE_ASSET_KINDS.has(kind)) base.shareType = fullShareLabel(kind);
  if (ASSET_RIGHT_TYPE_KINDS.has(kind)) base.rightType = DEFAULT_RIGHT_TYPE;
  return base;
}
