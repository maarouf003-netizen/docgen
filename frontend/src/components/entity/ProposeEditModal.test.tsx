import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ProposeEditModal } from './ProposeEditModal';
import type { PublicEntityEntryDto } from '../../types';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../../api/client';

function entry(): PublicEntityEntryDto {
  return {
    id: 101,
    groupId: 10,
    canonicalName: 'وزارة النقل',
    entityType: 'ministry',
    governorate: 'دمشق',
    branchName: 'الجهة الأم',
    citationFormula: 'add-to-job',
    status: 'final',
    isActive: true,
    createdAt: '2026-08-24T00:00:00Z',
    aliases: [],
    isParentEntity: true,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
});

describe('ProposeEditModal', () => {
  it('يعرض الحقول الافتراضية من القيد', async () => {
    render(<ProposeEditModal entry={entry()} onClose={vi.fn()} onCommitted={vi.fn()} />);

    expect((screen.getByLabelText('الاسم المعتمد') as HTMLInputElement).value).toBe('وزارة النقل');
    expect((screen.getByLabelText('المحافظة') as HTMLSelectElement).value).toBe('دمشق');
  });

  it('يرسل اقتراح التعديل إلى POST propose-edit', async () => {
    const user = userEvent.setup();
    const onCommitted = vi.fn();
    render(<ProposeEditModal entry={entry()} onClose={vi.fn()} onCommitted={onCommitted} />);

    await user.clear(screen.getByLabelText('الاسم المعتمد'));
    await user.type(screen.getByLabelText('الاسم المعتمد'), 'وزارة النقل المعدلة');
    await user.click(screen.getByRole('button', { name: 'إرسال الاقتراح' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-registry/101/propose-edit', expect.objectContaining({
        canonicalName: 'وزارة النقل المعدلة',
        governorate: 'دمشق',
      }));
    });
    expect(onCommitted).toHaveBeenCalledWith(expect.stringContaining('بانتظار مراجعة'));
  });

  it('يرفض الإرسال دون اسم جهة', async () => {
    const user = userEvent.setup();
    render(<ProposeEditModal entry={entry()} onClose={vi.fn()} onCommitted={vi.fn()} />);

    await user.clear(screen.getByLabelText('الاسم المعتمد'));
    await user.click(screen.getByRole('button', { name: 'إرسال الاقتراح' }));

    expect(await screen.findByText('اسم الجهة مطلوب')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });
});
