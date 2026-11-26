import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ApplicantAssetsSection } from './ApplicantAssetsSection';
import type { ApplicantAssetsSectionProps } from './ApplicantAssetsSection';

function props(overrides: Partial<ApplicantAssetsSectionProps> = {}): ApplicantAssetsSectionProps {
  return {
    lock: false,
    assets: [],
    onEstateSet: vi.fn(),
    seizureLockedIds: new Set<number>(),
    onEstateRemove: vi.fn(),
    onOwnerToggle: vi.fn(),
    onSingleOwnerSet: vi.fn(),
    onEstateAdd: vi.fn(),
    ownerOptions: () => [],
    ...overrides,
  };
}

describe('ApplicantAssetsSection', () => {
  it('لا يعرض قسم الأموال أو أزرار الإضافة في الملف المناب (lock)', () => {
    const { container } = render(<ApplicantAssetsSection {...props({ lock: true })} />);
    expect(container).toBeEmptyDOMElement();
    expect(screen.queryByText('الأموال المنقولة وغير المنقولة')).not.toBeInTheDocument();
  });

  it('يعرض القسم وأزرار الأنواع الخمسة عند التحرير', () => {
    render(<ApplicantAssetsSection {...props()} />);
    expect(screen.getByText('الأموال المنقولة وغير المنقولة')).toBeInTheDocument();
    for (const name of [/إضافة عقار/, /إضافة مركبة/, /إضافة متجر$/, /إضافة كفالة رواتب/, /إضافة متجر غير مسجل/]) {
      expect(screen.getByRole('button', { name })).toBeInTheDocument();
    }
  });
});
