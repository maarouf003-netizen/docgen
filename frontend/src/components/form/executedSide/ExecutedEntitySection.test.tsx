import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ExecutedEntitySection } from './ExecutedEntitySection';
import type { ExecutedEntitySectionProps } from './ExecutedEntitySection';
import { emptyExecutedPublicEntity } from '../documentFormConstants';

function props(overrides: Partial<ExecutedEntitySectionProps> = {}): ExecutedEntitySectionProps {
  return {
    isMirror: false,
    executedPublicEntities: [
      { ...emptyExecutedPublicEntity(), entityName: 'جهة أولى', registryId: 1 },
      { ...emptyExecutedPublicEntity(), entityName: 'جهة ثانية', registryId: 2 },
    ],
    onEntitySet: vi.fn(),
    onEntityAdd: vi.fn(),
    onEntityRemove: vi.fn(),
    onPersonAdd: vi.fn(),
    ...overrides,
  };
}

describe('ExecutedEntitySection', () => {
  it('يخفي أزرار الإضافة والحذف في الملف المناب', () => {
    render(<ExecutedEntitySection {...props({ isMirror: true })} />);
    expect(screen.queryByRole('button', { name: /إضافة جهة عامة/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /إضافة شخص اعتباري/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /إضافة شخص طبيعي/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /حذف/ })).not.toBeInTheDocument();
  });

  it('يعرض أزرار الإضافة الثلاثة والحذف عند التحرير', () => {
    render(<ExecutedEntitySection {...props()} />);
    expect(screen.getByRole('button', { name: /إضافة جهة عامة/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /إضافة شخص اعتباري/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /إضافة شخص طبيعي/ })).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /حذف/ })).toHaveLength(2);
  });
});
