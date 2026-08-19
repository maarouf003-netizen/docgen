import type { AssetDto, DelegationAssetDto } from '../types';
import { assetDisplayName } from './assetDisplay';

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
  const remaining = [...currentAssets];
  const matchedIds: number[] = [];
  const unmatched: DelegationAssetDto[] = [];
  for (const snapshot of snapshots) {
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