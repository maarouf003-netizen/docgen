import { ASSET_KINDS } from '../components/form/documentFormConstants';
import type { AssetDto } from '../types';

/** تسمية نوع الأصل للعرض داخل بطاقته (عقار / مركبة / متجر / كفالة رواتب / متجر غير مسجل). */
export function assetKindLabel(kind: string | undefined): string {
  switch (kind) {
    case ASSET_KINDS.vehicle:
      return 'مركبة';
    case ASSET_KINDS.shop:
      return 'متجر';
    case ASSET_KINDS.salaryGuarantee:
      return 'كفالة رواتب';
    case ASSET_KINDS.unregisteredShop:
      return 'متجر غير مسجل';
    default:
      return 'عقار';
  }
}

/**
 * تسمية قراءة للأصل في قوائم الاختيار والعرض (مثال: «مركبة سيارة — لوحة 123»):
 * تُستعمل في «منفذ جبريا» وفي بطاقات الأموال. قيمة النوع تُقدَّم قبل التفاصيل الخاصة به.
 */
export function assetDisplayName(a: AssetDto & { id?: number }): string {
  if (a.assetKind === ASSET_KINDS.vehicle) {
    const type = (a.vehicleType ?? '').trim();
    const plate = (a.plateNumber ?? '').trim();
    if (type && plate) return `مركبة ${type} — لوحة ${plate}`;
    if (plate) return `مركبة لوحة ${plate}`;
    return type ? `مركبة ${type}` : `مركبة ${a.id ?? ''}`.trim();
  }
  if (a.assetKind === ASSET_KINDS.shop) {
    const reg = (a.registerNumber ?? '').trim();
    if (reg) return `متجر سجل رقم ${reg}`;
    return (a.shopDescription ?? '').trim() || `متجر ${a.id ?? ''}`.trim();
  }
  if (a.assetKind === ASSET_KINDS.salaryGuarantee) {
    const entity = (a.publicEntity ?? '').trim();
    return entity ? `كفالة رواتب — ${entity}` : 'كفالة رواتب';
  }
  if (a.assetKind === ASSET_KINDS.unregisteredShop) {
    const license = (a.licenseNumber ?? '').trim();
    return license ? `متجر غير مسجل ترخيص رقم ${license}` : `متجر غير مسجل ${a.id ?? ''}`.trim();
  }
  const property = (a.property ?? '').trim();
  if (property) return property;
  return a.propertyNumber ? `عقار رقم ${a.propertyNumber}` : `عقار ${a.id ?? ''}`.trim();
}
