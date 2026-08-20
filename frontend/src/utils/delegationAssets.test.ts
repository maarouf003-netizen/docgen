import { describe, it, expect } from 'vitest';
import type { AssetDto, DelegationAssetDto } from '../types';
import { delegationAssetLabel, delegationAssetsLine, matchDelegationAssets } from './delegationAssets';

function snapshot(id: number, kind: string, label: string): DelegationAssetDto {
  return { id, assetKind: kind, assetLabel: label, snapshotAdjusted: false };
}

function asset(id: number, kind: string, overrides: Record<string, string> = {}): AssetDto {
  return { id, assetKind: kind, ...overrides } as AssetDto;
}

describe('delegationAssetLabel', () => {
  it('يستخدم الوصف ثم النوع ثم رقم اللقطة عند الفراغ', () => {
    expect(delegationAssetLabel(snapshot(1, 'عقار', 'عقار رقم 77'))).toBe('عقار رقم 77');
    expect(delegationAssetLabel(snapshot(1, 'عقار', '  '))).toBe('عقار');
    expect(delegationAssetLabel(snapshot(1, 'عقار', ''))).toBe('عقار');
    expect(delegationAssetLabel(snapshot(1, '', ''))).toBe('أصل رقم 1');
  });
});

describe('matchDelegationAssets', () => {
  it('يطابق الأصول الموجودة بالنوع والوصف معًا', () => {
    const snapshots = [snapshot(10, 'مركبة', 'مركبة سيارة — لوحة 123')];
    const current = [
      asset(5, 'مركبة', { vehicleType: 'سيارة', plateNumber: '123' }),
      asset(6, 'عقار', { property: 'عقار رقم 77' }),
    ];
    expect(matchDelegationAssets(snapshots, current)).toEqual({
      matchedIds: [5],
      unmatched: [],
    });
  });

  it('لا يطابق عند اختلاف النوع حتى لو تشابه الوصف', () => {
    const snapshots = [snapshot(10, 'متجر', 'متجر سجل رقم 9')];
    const current = [asset(5, 'عقار', { property: 'متجر سجل رقم 9' })];
    expect(matchDelegationAssets(snapshots, current)).toEqual({
      matchedIds: [],
      unmatched: [snapshots[0]],
    });
  });

  it('يُعيد غير المطابق (معدوم/متغير الوصف) كي يبقى مرئيًا', () => {
    const snapshots = [
      snapshot(10, 'مركبة', 'مركبة لوحة 999'),
      snapshot(11, 'متجر', 'متجر سجل رقم 3'),
    ];
    const current = [asset(5, 'مركبة', { plateNumber: '999' })];
    expect(matchDelegationAssets(snapshots, current)).toEqual({
      matchedIds: [5],
      unmatched: [snapshots[1]],
    });
  });

  it('لا يطابق أصلًا نفسه مرتين (لقطة مكررة) بل يبقيه غير متاح', () => {
    const snapshots = [
      snapshot(10, 'مركبة', 'مركبة لوحة 1'),
      snapshot(11, 'مركبة', 'مركبة لوحة 1'),
    ];
    const current = [asset(5, 'مركبة', { plateNumber: '1' })];
    expect(matchDelegationAssets(snapshots, current)).toEqual({
      matchedIds: [5],
      unmatched: [snapshots[1]],
    });
  });

  it('يعيد قائمتي فارغتين عند عدم وجود لقطة', () => {
    expect(matchDelegationAssets([], [asset(5, 'مركبة')])).toEqual({
      matchedIds: [],
      unmatched: [],
    });
  });
});

describe('delegationAssetsLine', () => {
  it('يجمع الأوصاف بلا تكرار ويختصر الكثرة', () => {
    expect(delegationAssetsLine({ assets: [] })).toBe('');
    expect(
      delegationAssetsLine({ assets: [snapshot(1, 'مركبة', 'مركبة سيارة')] }),
    ).toBe('مركبة سيارة');
    expect(
      delegationAssetsLine({
        assets: [snapshot(1, 'a', 'أ'), snapshot(2, 'b', 'ب'), snapshot(3, 'c', 'ج'), snapshot(4, 'd', 'د')],
      }),
    ).toBe('أ، ب، ج — و1 أخرى');
    expect(
      delegationAssetsLine(
        { assets: [snapshot(1, 'a', 'أ'), snapshot(2, 'b', 'ب'), snapshot(3, 'c', 'ج'), snapshot(4, 'd', 'د')] },
        5,
      ),
    ).toBe('أ، ب، ج، د');
  });
});
