import { describe, it, expect } from 'vitest';
import type { AssetDto, DelegationAssetDto, DelegationDto } from '../types';
import {
  availableDelegationAssets,
  blockedAssetIds,
  delegationAssetLabel,
  delegationAssetsLine,
  matchDelegationAssets,
} from './delegationAssets';

function snapshot(id: number, kind: string, label: string): DelegationAssetDto {
  return { id, assetKind: kind, assetLabel: label, snapshotAdjusted: false };
}

function asset(id: number, kind: string, overrides: Record<string, string> = {}): AssetDto {
  return { id, assetKind: kind, ...overrides } as AssetDto;
}

function blockingDelegation(id: number, snapshots: DelegationAssetDto[], blocksAssets = true): DelegationDto {
  return {
    id,
    sourceDocumentId: 10,
    status: 'بانتظار رئيس القسم',
    isExternal: false,
    createdById: 7,
    createdAt: '2026-08-01',
    assets: snapshots,
    blocksAssets,
  } as DelegationDto;
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

  it('E2: رقم عقاري بمسافات محيطة يُطابَق (اللقطة مخزنة مطبّعة خلفيًا)', () => {
    const snapshots = [snapshot(10, 'عقار', 'عقار رقم 77')];
    const current = [asset(5, 'عقار', { propertyNumber: '77 ' })];
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

  it('C1: المطابقة حتمية تحت إعادة ترتيب اللقطات والأصول', () => {
    const snapshots = [
      snapshot(11, 'عقار', 'عقار رقم 77'),
      snapshot(10, 'عقار', 'عقار رقم 77'),
    ];
    const current = [
      asset(6, 'عقار', { property: 'عقار رقم 77' }),
      asset(5, 'عقار', { property: 'عقار رقم 77' }),
    ];
    const expected = { matchedIds: [5, 6], unmatched: [] };
    expect(matchDelegationAssets(snapshots, current)).toEqual(expected);
    expect(matchDelegationAssets([...snapshots].reverse(), [...current].reverse())).toEqual(expected);
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

describe('blockedAssetIds', () => {
  it('يحجب أموال الإنابة الحاجبة ويتجاهل غير الحاجبة (منفذة)', () => {
    const assets = [
      asset(5, 'مركبة', { vehicleType: 'سيارة', plateNumber: '123' }),
      asset(6, 'عقار', { property: 'عقار رقم 77' }),
    ];
    const delegations = [
      blockingDelegation(1, [snapshot(10, 'مركبة', 'مركبة سيارة — لوحة 123')]),
      { ...blockingDelegation(2, [snapshot(11, 'عقار', 'عقار رقم 77')]), blocksAssets: false },
    ];
    expect(blockedAssetIds(delegations, assets)).toEqual(new Set([5]));
  });

  it('يستثني الإنابة ذاتها عند التعديل (تعديل المعلّقة على أموالها مسموح)', () => {
    const assets = [asset(5, 'مركبة', { vehicleType: 'سيارة', plateNumber: '123' })];
    const delegations = [
      blockingDelegation(1, [snapshot(10, 'مركبة', 'مركبة سيارة — لوحة 123')]),
    ];
    expect(blockedAssetIds(delegations, assets, 1)).toEqual(new Set());
    expect(blockedAssetIds(delegations, assets, 2)).toEqual(new Set([5]));
  });

  it('يستهلك توأمًا واحدًا فقط عند تطابق التسميات (التوأم السليم حر)', () => {
    const assets = [
      asset(5, 'عقار', { property: 'عقار رقم 77' }),
      asset(6, 'عقار', { property: 'عقار رقم 77' }),
    ];
    const delegations = [
      blockingDelegation(1, [snapshot(10, 'عقار', 'عقار رقم 77')]),
    ];
    expect(blockedAssetIds(delegations, assets)).toEqual(new Set([5]));
  });

  it('E2: الأصل المحجوب ذو الرقم المباعد يُحجب في المرآة', () => {
    const assets = [asset(5, 'عقار', { propertyNumber: ' 77 ' })];
    const delegations = [blockingDelegation(1, [snapshot(10, 'عقار', 'عقار رقم 77')])];
    expect(blockedAssetIds(delegations, assets)).toEqual(new Set([5]));
  });

  it('C1: المجموعة المحجوبة ثابتة تحت إعادة ترتيب اللقطات والأصول والإنابات', () => {
    const assets = [
      asset(6, 'عقار', { property: 'عقار رقم 77' }),
      asset(5, 'عقار', { property: 'عقار رقم 77' }),
      asset(7, 'مركبة', { vehicleType: 'سيارة', plateNumber: '123' }),
    ];
    const mk = () => [
      blockingDelegation(2, [snapshot(11, 'مركبة', 'مركبة سيارة — لوحة 123')]),
      blockingDelegation(1, [snapshot(10, 'عقار', 'عقار رقم 77'), snapshot(12, 'عقار', 'عقار رقم 77')]),
    ];
    const shuffledAssets = [...assets].reverse();
    const shuffledDelegations = [...mk()].reverse().map((d) => ({ ...d, assets: [...d.assets].reverse() }));
    expect(blockedAssetIds(mk(), assets)).toEqual(new Set([5, 6, 7]));
    expect(blockedAssetIds(shuffledDelegations, shuffledAssets)).toEqual(new Set([5, 6, 7]));
  });
});

describe('availableDelegationAssets', () => {
  it('يستبعد الكفالة دائمًا (لا إنابة عليها) والمحجوب — إخفاء تام', () => {
    const assets = [
      asset(5, 'مركبة', { vehicleType: 'سيارة', plateNumber: '123', seizureDate: '1/8/2026' }),
      asset(6, 'كفالة رواتب', { publicEntity: 'مؤسسة المياه' }),
      asset(7, 'عقار', { property: 'عقار رقم 77', seizureDate: '1/8/2026' }),
    ];
    const delegations = [
      blockingDelegation(1, [snapshot(10, 'مركبة', 'مركبة سيارة — لوحة 123')]),
    ];
    const available = availableDelegationAssets(delegations, assets);
    expect(available.map((a) => a.id)).toEqual([7]);
  });

  it('يعيد الكل عند غياب الحاجب ما عدا الكفالة', () => {
    const assets = [
      asset(5, 'مركبة', { vehicleType: 'سيارة', plateNumber: '123', seizureDate: '1/8/2026' }),
      asset(6, 'كفالة رواتب', { publicEntity: 'مؤسسة المياه' }),
    ];
    expect(availableDelegationAssets([], assets).map((a) => a.id)).toEqual([5]);
  });

  it('يستبعد الأموال بلا تاريخ حجز (seizureDate فارغ) — لا إنابة عليها قانونًا', () => {
    const assets = [
      asset(5, 'مركبة', { vehicleType: 'سيارة', plateNumber: '123', seizureDate: '1/8/2026' }),
      asset(6, 'عقار', { property: 'عقار رقم 77', seizureDate: '' }),
      asset(7, 'عقار', { property: 'عقار رقم 88', seizureDate: '' }),
    ];
    expect(availableDelegationAssets([], assets).map((a) => a.id)).toEqual([5]);
  });

  it('يقبل seizureDate بعد التقليم (مسافات محيطة)', () => {
    const assets = [
      asset(5, 'مركبة', { vehicleType: 'سيارة', plateNumber: '123', seizureDate: '  1/8/2026  ' }),
    ];
    expect(availableDelegationAssets([], assets).map((a) => a.id)).toEqual([5]);
  });

  it('يجمع الشروط الثلاثة: لا كفالة + لا محجوب + به حجز', () => {
    const assets = [
      asset(5, 'مركبة', { vehicleType: 'سيارة', plateNumber: '123', seizureDate: '1/8/2026' }),
      asset(6, 'كفالة رواتب', { publicEntity: 'مؤسسة المياه', seizureDate: '1/8/2026' }),
      asset(7, 'عقار', { property: 'عقار رقم 77', seizureDate: '' }),
      asset(8, 'عقار', { property: 'عقار رقم 88', seizureDate: '1/8/2026' }),
    ];
    const delegations = [
      blockingDelegation(1, [snapshot(10, 'مركبة', 'مركبة سيارة — لوحة 123')]),
    ];
    const available = availableDelegationAssets(delegations, assets);
    expect(available.map((a) => a.id)).toEqual([8]);
  });
});
