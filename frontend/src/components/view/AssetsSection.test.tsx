import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AssetsSection } from './AssetsSection';
import type { AssetDto, DelegationDto, DocumentResponse } from '../../types';

function asset(id: number, overrides: Record<string, unknown> = {}): AssetDto {
  return { id, assetKind: 'عقار', propertyNumber: '77', ...overrides } as unknown as AssetDto;
}

function doc(assets: AssetDto[]): DocumentResponse {
  return { assets } as unknown as DocumentResponse;
}

function snapshot(id: number, label: string): DelegationDto['assets'][number] {
  return { id, assetKind: 'عقار', assetLabel: label, snapshotAdjusted: false };
}

function blockingDelegation(
  snapshots: DelegationDto['assets'],
  blocksAssets = true,
): DelegationDto {
  return { id: 1, blocksAssets, assets: snapshots } as DelegationDto;
}

describe('AssetsSection', () => {
  it('يعرض البطاقة وحالة الفراغ', () => {
    render(<AssetsSection doc={doc([])} />);

    expect(screen.getByText('الأموال المنقولة وغير المنقولة')).toBeInTheDocument();
    expect(screen.getByText('لا توجد أموال مرهونة')).toBeInTheDocument();
  });

  it('شارة الحجز: خضراء لمال له تاريخ إلقاء حجز وحمراء لمن لا تاريخ له', () => {
    render(
      <AssetsSection
        doc={doc([
          asset(5, { propertyNumber: '77', seizureDate: '2026-08-01' }),
          asset(6, { propertyNumber: '78' }),
        ])}
      />,
    );

    expect(screen.getAllByText('تم القاء الحجز')).toHaveLength(1);
    expect(screen.getAllByText('لم يتم القاء الحجز')).toHaveLength(1);
  });

  it('تاريخ فراغ-فقط يُعامل كغياب (شارة حمراء)', () => {
    render(<AssetsSection doc={doc([asset(5, { seizureDate: '   ' })])} />);

    expect(screen.queryByText('تم القاء الحجز')).not.toBeInTheDocument();
    expect(screen.getByText('لم يتم القاء الحجز')).toBeInTheDocument();
  });

  it('شارة «مناب» زرقاء للمال المحجوب بإنابة سارية فقط', () => {
    render(
      <AssetsSection
        doc={doc([asset(5, { propertyNumber: '77' }), asset(6, { propertyNumber: '78' })])}
        delegations={[blockingDelegation([snapshot(10, 'عقار رقم 77')])]}
      />,
    );

    expect(screen.getAllByText('مناب')).toHaveLength(1);
  });

  it('لا «مناب» لأموال إنابة غير حاجبة (منتهية)', () => {
    render(
      <AssetsSection
        doc={doc([asset(5, { propertyNumber: '77' })])}
        delegations={[blockingDelegation([snapshot(10, 'عقار رقم 77')], false)]}
      />,
    );

    expect(screen.queryByText('مناب')).not.toBeInTheDocument();
  });

  it('التوأم السليم لا يُوسم «مناب» (استهلاك واحد-لواحد)', () => {
    render(
      <AssetsSection
        doc={doc([
          asset(5, { propertyNumber: '77' }),
          asset(6, { propertyNumber: '77' }),
        ])}
        delegations={[blockingDelegation([snapshot(10, 'عقار رقم 77')])]}
      />,
    );

    expect(screen.getAllByText('مناب')).toHaveLength(1);
  });
});
