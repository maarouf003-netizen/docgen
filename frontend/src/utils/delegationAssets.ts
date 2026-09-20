import type { AssetDto, DelegationAssetDto, DelegationDto } from '../types';
import { assetDisplayName } from './assetDisplay';
import { ASSET_KINDS } from '../components/form/documentFormConstants';

/** وصف قراءة لأصلٍ في لقطة الإنابة (يعتمد AssetLabel من الخلفية أساسًا). */
export function delegationAssetLabel(snapshot: DelegationAssetDto): string {
  return snapshot.assetLabel?.trim() || snapshot.assetKind || `أصل رقم ${snapshot.id}`;
}

/**
 * مطابقة أصول الملف الحالية مع لقطة أصول الإنابة (تعديل إنابة معلّقة):
 * اللقطة لا تحمل معرفات الأصول (تُعاد بناؤها عند تعديل الملف)، فتُطابَق بالنوع والوصف
 * معّا؛ ما لا يطابق (أصل محذوف أو تغيّر وصفه) يُعاد ضمن غير المتاح ليبقى مرئيًا للمستخدم.
 */
export function matchDelegationAssets(
  snapshots: DelegationAssetDto[],
  currentAssets: AssetDto[],
): { matchedIds: number[]; unmatched: DelegationAssetDto[] } {
  // C1: الفرز بالمفتاح قبل الجشع — مرآة الفرز الخلفي، فأي توأم يُطابَق حتمي.
  const remaining = [...currentAssets].sort((a, b) => (a.id ?? 0) - (b.id ?? 0));
  const matchedIds: number[] = [];
  const unmatched: DelegationAssetDto[] = [];
  for (const snapshot of [...snapshots].sort((a, b) => a.id - b.id)) {
    const index = remaining.findIndex(
      (a) => a.assetKind === snapshot.assetKind && assetDisplayName(a) === delegationAssetLabel(snapshot),
    );
    if (index >= 0) {
      matchedIds.push(remaining[index].id ?? 0);
      remaining.splice(index, 1);
    } else {
      unmatched.push(snapshot);
    }
  }
  return { matchedIds, unmatched };
}

/** سرد مختصر لأصول إنابة في البطاقات («…، و2 أخرى») بلا تجاوز أفقي. */
export function delegationAssetsLine(delegation: { assets: DelegationAssetDto[] }, max = 3): string {
  const labels = delegation.assets.map(delegationAssetLabel);
  if (labels.length === 0) return '';
  const shown = labels.slice(0, max);
  const rest = labels.length - shown.length;
  const base = shown.join('، ');
  return rest > 0 ? `${base} — و${rest} أخرى` : base;
}

/**
 * معرفات الأموال المحجوبة بإنابات سارية (حقل `blocksAssets` المحسوب خلفيًا):
 * مطابقة واحد-لواحد على كامل المسبح — مرآة `ValidateAssetsNotBlockedAsync` الخلفية —
 * فالتوأم السليم لا يُحجب. تُستثنى الإنابة ذاتها عند التعديل (تعديل المعلّقة على أموالها مسموح).
 */
export function blockedAssetIds(
  delegations: DelegationDto[],
  assets: AssetDto[],
  excludeId?: number | null,
): Set<number> {
  const remaining = [...assets].sort((a, b) => (a.id ?? 0) - (b.id ?? 0));
  const blocked = new Set<number>();
  for (const d of delegations) {
    if (!d.blocksAssets) continue;
    if (excludeId != null && d.id === excludeId) continue;
    // C1: فرز اللقطات بالمفتاح — مرآة الفرز الخلفي.
    for (const snap of [...(d.assets ?? [])].sort((a, b) => a.id - b.id)) {
      const index = remaining.findIndex(
        (a) =>
          !blocked.has(a.id ?? 0) &&
          a.assetKind === snap.assetKind &&
          assetDisplayName(a) === delegationAssetLabel(snap),
      );
      if (index >= 0) blocked.add(remaining[index].id ?? 0);
    }
  }
  return blocked;
}

/**
 * الأموال المتاحة للاختيار في نافذة التسطير: بلا «كفالة رواتب» أبدًا (لا إنابة عليها)
 * وبلا المحجوب بإنابة سارية — إخفاء تام.
 */
export function availableDelegationAssets(
  delegations: DelegationDto[],
  assets: AssetDto[],
  excludeId?: number | null,
): AssetDto[] {
  const blocked = blockedAssetIds(delegations, assets, excludeId);
  return assets.filter(
    (a) => a.assetKind !== ASSET_KINDS.salaryGuarantee && !blocked.has(a.id ?? 0),
  );
}