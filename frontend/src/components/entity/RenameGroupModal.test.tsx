import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RenameGroupModal } from './RenameGroupModal';
import type { GroupPick } from './reviewShared';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../../api/client';

const group: GroupPick = { groupId: 7, canonicalName: 'جهة قديمة', entryCount: 3, governorates: ['دمشق'] };
const post = () => api.post as unknown as ReturnType<typeof vi.fn>;

beforeEach(() => {
  vi.clearAllMocks();
  post().mockResolvedValue({ data: {} });
});

function renderModal(onCommitted = vi.fn()) {
  return {
    onCommitted,
    ...render(<RenameGroupModal group={group} onClose={vi.fn()} onCommitted={onCommitted} />),
  };
}

describe('RenameGroupModal', () => {
  it('يعرض العنوان ويطلب المعاينة بعد كتابة التسمية', async () => {
    const user = userEvent.setup();
    post().mockResolvedValueOnce({
      data: { oldCanonicalName: 'جهة قديمة', newCanonicalName: 'جهة جديدة', affectedDocuments: 5, branches: ['الفرع الرئيسي'] },
    });
    renderModal();

    expect(screen.getByText('تعديل تسمية جهة')).toBeInTheDocument();
    await user.type(screen.getByLabelText('التسمية الجديدة'), 'جهة جديدة');
    await user.click(screen.getByRole('button', { name: 'معاينة التأثير' }));

    expect(post()).toHaveBeenCalledWith(
      '/entity-registry/groups/7/rename-preview',
      expect.objectContaining({ groupId: 7, newCanonicalName: 'جهة جديدة' }),
    );
    expect(await screen.findByText(/سيتم تعديل اسم «جهة قديمة» إلى «جهة جديدة»/)).toBeInTheDocument();
  });

  it('يرفض التنفيذ بلا مرجع مكتمل', async () => {
    const user = userEvent.setup();
    post().mockResolvedValueOnce({
      data: { oldCanonicalName: 'جهة قديمة', newCanonicalName: 'جهة جديدة', affectedDocuments: 0, branches: [] },
    });
    renderModal();

    await user.type(screen.getByLabelText('التسمية الجديدة'), 'جهة جديدة');
    await user.click(screen.getByRole('button', { name: 'معاينة التأثير' }));
    await screen.findByText(/سيتم تعديل اسم/);
    await user.click(screen.getByRole('button', { name: 'تأكيد التنفيذ' }));

    expect(await screen.findByText('نوع المرجع ورقمه وتاريخه مطلوبة')).toBeInTheDocument();
    expect(post()).toHaveBeenCalledTimes(1);
  });

  it('ينفذ عند اكتمال المرجع والتأكيد الحرفي ويبلغ بالنتيجة', async () => {
    const user = userEvent.setup();
    post()
      .mockResolvedValueOnce({
        data: { oldCanonicalName: 'جهة قديمة', newCanonicalName: 'جهة جديدة', affectedDocuments: 2, branches: [] },
      })
      .mockResolvedValueOnce({
        data: { oldCanonicalName: 'جهة قديمة', newCanonicalName: 'جهة جديدة', affectedDocuments: 2 },
      });
    const { onCommitted } = renderModal();

    await user.type(screen.getByLabelText('التسمية الجديدة'), 'جهة جديدة');
    await user.click(screen.getByRole('button', { name: 'معاينة التأثير' }));
    await screen.findByText(/سيتم تعديل اسم/);

    await user.selectOptions(screen.getByLabelText('نوع المرجع'), 'قرار');
    await user.type(screen.getByLabelText('رقم المرجع'), '77');
    await user.type(screen.getByLabelText('تاريخ المرجع'), '1/8/2026');
    await user.type(screen.getByLabelText('تأكيد كتابة التسمية الجديدة'), 'جهة جديدة');
    await user.click(screen.getByRole('button', { name: 'تأكيد التنفيذ' }));

    await waitFor(() => expect(onCommitted).toHaveBeenCalledWith(expect.stringContaining('جهة جديدة')));
    expect(post()).toHaveBeenLastCalledWith(
      '/entity-registry/groups/7/rename',
      expect.objectContaining({ decreeNumber: '77', decreeDate: '1/8/2026' }),
    );
  });

  it('يعرض خطأ الخادم عند فشل المعاينة', async () => {
    const user = userEvent.setup();
    post().mockRejectedValueOnce(new Error('boom'));
    renderModal();

    await user.type(screen.getByLabelText('التسمية الجديدة'), 'جهة جديدة');
    await user.click(screen.getByRole('button', { name: 'معاينة التأثير' }));

    expect(await screen.findByText('خطأ من الخادم')).toBeInTheDocument();
  });
});
