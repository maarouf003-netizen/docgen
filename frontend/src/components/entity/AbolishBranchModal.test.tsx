import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AbolishBranchModal } from './AbolishBranchModal';
import type { GroupPick } from './reviewShared';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../../api/client';

const selected: GroupPick[] = [
  { groupId: 9, canonicalName: 'جهة ملغاة', entryCount: 1, governorates: ['حمص'] },
];
const post = () => api.post as unknown as ReturnType<typeof vi.fn>;

beforeEach(() => {
  vi.clearAllMocks();
  post().mockResolvedValue({ data: {} });
});

describe('AbolishBranchModal', () => {
  it('يحمّل المعاينة ويعرض أثرها', async () => {
    const user = userEvent.setup();
    post().mockResolvedValueOnce({
      data: { affectedDocuments: 6, activeEntries: 2, delegatesToReassign: 1, branches: ['فرع حمص'] },
    });
    render(<AbolishBranchModal selected={selected} onClose={vi.fn()} onCommitted={vi.fn()} />);

    expect(screen.getByText('حلول جهة عامة')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'معاينة التأثير' }));

    expect(post()).toHaveBeenCalledWith(
      '/entity-registry/groups/abolish-preview',
      expect.objectContaining({ abolishedGroupIds: [9] }),
    );
    expect(await screen.findByText(/مندوبون بحاجة لإعادة توجيه/)).toBeInTheDocument();
  });

  it('يرفض التنفيذ بلا اسم جهة جديدة', async () => {
    const user = userEvent.setup();
    render(<AbolishBranchModal selected={selected} onClose={vi.fn()} onCommitted={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: 'تأكيد الحلول' }));

    expect(await screen.findByText('اسم الجهة الجديدة مطلوب')).toBeInTheDocument();
    expect(post()).not.toHaveBeenCalled();
  });

  it('ينفذ الحلول عند اكتمال الحقول ويبلغ بالنتيجة', async () => {
    const user = userEvent.setup();
    post().mockResolvedValueOnce({
      data: { newCanonicalName: 'جهة بديلة', abolishedGroups: 1, affectedDocuments: 6 },
    });
    const onCommitted = vi.fn();
    render(<AbolishBranchModal selected={selected} onClose={vi.fn()} onCommitted={onCommitted} />);

    await user.type(screen.getByLabelText('اسم الجهة الجديدة'), 'جهة بديلة');
    await user.selectOptions(screen.getByLabelText('المحافظة'), 'دمشق');
    await user.selectOptions(screen.getByLabelText('نوع المرجع'), 'قرار');
    await user.type(screen.getByLabelText('رقم المرجع'), '9');
    await user.type(screen.getByLabelText('تاريخ المرجع'), '3/4/2026');
    await user.type(screen.getByLabelText('تأكيد كتابة اسم الجهة الجديدة'), 'جهة بديلة');
    await user.click(screen.getByRole('button', { name: 'تأكيد الحلول' }));

    await waitFor(() => expect(onCommitted).toHaveBeenCalledWith(expect.stringContaining('جهة بديلة')));
    expect(post()).toHaveBeenCalledWith(
      '/entity-registry/groups/abolish-and-replace',
      expect.objectContaining({ newCanonicalName: 'جهة بديلة', governorate: 'دمشق' }),
    );
  });
});
