import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MergeBranchesModal } from './MergeBranchesModal';
import type { GroupPick } from './reviewShared';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../../api/client';

const selected: GroupPick[] = [
  { groupId: 1, canonicalName: 'جهة الناجية', entryCount: 2, governorates: ['دمشق'] },
  { groupId: 2, canonicalName: 'جهة الممتصة', entryCount: 1, governorates: ['دمشق'] },
];
const post = () => api.post as unknown as ReturnType<typeof vi.fn>;

beforeEach(() => {
  vi.clearAllMocks();
  post().mockResolvedValue({ data: {} });
});

describe('MergeBranchesModal', () => {
  it('يعرض الناجية والملغاة وخيار الاسم الجديد', () => {
    render(<MergeBranchesModal selected={selected} onClose={vi.fn()} onCommitted={vi.fn()} />);

    expect(screen.getByText('دمج جهات عامة')).toBeInTheDocument();
    expect(screen.getByLabelText(/الجهة العامة التي سيتم الدمج معها/)).toBeInTheDocument();
    expect(screen.getByText('جهة الممتصة')).toBeInTheDocument();
    expect(screen.getByLabelText('الاسم الجديد للجهة العامة (اختياري)')).toBeInTheDocument();
  });

  it('يرفض التنفيذ بلا مرجع مكتمل', async () => {
    const user = userEvent.setup();
    render(<MergeBranchesModal selected={selected} onClose={vi.fn()} onCommitted={vi.fn()} />);

    await user.type(screen.getByLabelText('تأكيد كتابة اسم الهدف'), 'جهة الناجية');
    await user.click(screen.getByRole('button', { name: 'تأكيد الدمج' }));

    expect(await screen.findByText('نوع المرجع ورقمه وتاريخه مطلوبة')).toBeInTheDocument();
    expect(post()).not.toHaveBeenCalled();
  });

  it('يرفض التنفيذ عند اختلاف التأكيد عن اسم الناجية', async () => {
    const user = userEvent.setup();
    render(<MergeBranchesModal selected={selected} onClose={vi.fn()} onCommitted={vi.fn()} />);

    await user.selectOptions(screen.getByLabelText('نوع المرجع'), 'مرسوم');
    await user.type(screen.getByLabelText('رقم المرجع'), '5');
    await user.type(screen.getByLabelText('تاريخ المرجع'), '2/8/2026');
    await user.type(screen.getByLabelText('تأكيد كتابة اسم الهدف'), 'اسم خاطئ');
    await user.click(screen.getByRole('button', { name: 'تأكيد الدمج' }));

    expect(await screen.findByText('أكّد بكتابة اسم الهوية الناجية للمتابعة')).toBeInTheDocument();
    expect(post()).not.toHaveBeenCalled();
  });

  it('ينفذ الدمج عند اكتمال المرجع والتأكيد ويبلغ بالنتيجة', async () => {
    const user = userEvent.setup();
    post().mockResolvedValueOnce({
      data: { absorbedGroupsCount: 1, entriesMigrated: 1, totalAffectedDocuments: 4 },
    });
    const onCommitted = vi.fn();
    render(<MergeBranchesModal selected={selected} onClose={onCommitted} onCommitted={onCommitted} />);

    await user.selectOptions(screen.getByLabelText('نوع المرجع'), 'مرسوم');
    await user.type(screen.getByLabelText('رقم المرجع'), '5');
    await user.type(screen.getByLabelText('تاريخ المرجع'), '2/8/2026');
    await user.type(screen.getByLabelText('تأكيد كتابة اسم الهدف'), 'جهة الناجية');
    await user.click(screen.getByRole('button', { name: 'تأكيد الدمج' }));

    await waitFor(() => expect(onCommitted).toHaveBeenCalledWith(expect.stringContaining('جهة الناجية')));
    expect(post()).toHaveBeenCalledWith(
      '/entity-registry/merge-commit',
      expect.objectContaining({ survivorGroupId: 1, absorbedGroupIds: [2], decreeKind: 'مرسوم' }),
    );
  });
});
