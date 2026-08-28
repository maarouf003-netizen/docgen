import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BranchManagementModal } from './BranchManagementModal';
import type { PublicEntityEntryDto } from '../../types';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../../api/client';

function entry(overrides: Partial<PublicEntityEntryDto> = {}): PublicEntityEntryDto {
  return {
    id: 101,
    groupId: 10,
    canonicalName: 'وزارة النقل',
    entityType: 'ministry',
    governorate: 'دمشق',
    branchName: 'الفرع الرئيسي',
    citationFormula: 'add-to-job',
    status: 'final',
    isActive: true,
    createdAt: '2026-08-24T00:00:00Z',
    aliases: [],
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
    data: [
      entry(),
      entry({ id: 102, branchName: 'فرع المزة', governorate: 'دمشق' }),
    ],
  });
  (api.put as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
  (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
});

describe('BranchManagementModal', () => {
  it('يعرض فروع المجموعة في محافظة رئيس القسم', async () => {
    render(<BranchManagementModal groupId={10} groupName="وزارة النقل" onClose={vi.fn()} onCommitted={vi.fn()} />);

    expect(await screen.findByText('الفرع الرئيسي')).toBeInTheDocument();
    expect(screen.getByText('فرع المزة')).toBeInTheDocument();
  });

  it('يتيح تعديل اسم الفرع', async () => {
    const user = userEvent.setup();
    render(<BranchManagementModal groupId={10} groupName="وزارة النقل" onClose={vi.fn()} onCommitted={vi.fn()} />);

    await screen.findByText('الفرع الرئيسي');
    await user.click(screen.getAllByRole('button', { name: 'تعديل' })[0]);
    const input = screen.getByLabelText('اسم الفرع') as HTMLInputElement;
    expect(input.value).toBe('الفرع الرئيسي');
    await user.clear(input);
    await user.type(input, 'الفرع الجديد');
    await user.click(screen.getByRole('button', { name: 'حفظ' }));

    await waitFor(() => {
      expect(api.put).toHaveBeenCalledWith('/entity-registry/101', expect.objectContaining({ branchName: 'الفرع الجديد' }));
    });
  });

  it('يدمج فرعين عبر POST move', async () => {
    const user = userEvent.setup();
    render(<BranchManagementModal groupId={10} groupName="وزارة النقل" onClose={vi.fn()} onCommitted={vi.fn()} />);

    await screen.findByText('الفرع الرئيسي');
    await user.selectOptions(screen.getByLabelText('الفرع المصدر (سيُلغى)'), '101');
    await user.selectOptions(screen.getByLabelText('الفرع الهدف (يبقى)'), '102');
    await user.click(screen.getByRole('button', { name: 'دمج الفرعين' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-registry/101/move', { targetEntryId: 102 });
    });
  });

  it('يلغي فرعًا عبر PUT isActive false', async () => {
    const user = userEvent.setup();
    render(<BranchManagementModal groupId={10} groupName="وزارة النقل" onClose={vi.fn()} onCommitted={vi.fn()} />);

    await screen.findByText('الفرع الرئيسي');
    await user.click(screen.getAllByRole('button', { name: 'إلغاء' })[0]);
    expect(await screen.findByText(/هل أنت متأكد/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تأكيد الإلغاء' }));

    await waitFor(() => {
      expect(api.put).toHaveBeenCalledWith('/entity-registry/101', { isActive: false });
    });
  });

  it('يعرض رسالة عند عدم وجود فروع في محافظته', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });
    render(<BranchManagementModal groupId={10} groupName="وزارة النقل" onClose={vi.fn()} onCommitted={vi.fn()} />);

    expect(await screen.findByText(/لا توجد فروع نشطة/)).toBeInTheDocument();
  });
});
