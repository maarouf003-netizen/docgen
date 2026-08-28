import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { UnifyNamesModal } from './UnifyNamesModal';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../../api/client';

const groupsResponse = {
  data: {
    items: [
      { groupId: 1, canonicalName: 'وزارة النقل', entityType: 'ministry', isActive: true, entryCount: 2, governorates: ['دمشق', 'حلب'] },
      { groupId: 2, canonicalName: 'وزاره النقل', entityType: 'ministry', isActive: true, entryCount: 1, governorates: ['حمص'] },
      { groupId: 3, canonicalName: 'وزارة التعليم', entityType: 'ministry', isActive: true, entryCount: 1, governorates: ['دمشق'] },
    ],
    page: 1,
    perPage: 20,
    totalCount: 3,
    totalPages: 1,
  },
};

beforeEach(() => {
  vi.clearAllMocks();
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(groupsResponse);
  (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
});

describe('UnifyNamesModal', () => {
  it('يعرض قائمة المجموعات ويحمّلها من GET groups', async () => {
    render(<UnifyNamesModal onClose={vi.fn()} onCommitted={vi.fn()} />);

    expect(await screen.findByText('وزارة النقل')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/entity-registry/groups', expect.objectContaining({ params: expect.objectContaining({ perPage: 100 }) }));
  });

  it('يحدّد الهوية الهدف مسبقًا عند تمرير initialGroupId', async () => {
    render(<UnifyNamesModal onClose={vi.fn()} onCommitted={vi.fn()} initialGroupId={2} />);

    await screen.findByText('وزارة النقل');
    const select = screen.getByLabelText('الهوية الهدف (يبقى اسمها)') as HTMLSelectElement;
    expect(select.value).toBe('2');
  });

  it('يعرض زر المعاينة بعد اختيار الهدف والممتصة', async () => {
    const user = userEvent.setup();
    render(<UnifyNamesModal onClose={vi.fn()} onCommitted={vi.fn()} />);

    await screen.findByText('وزارة النقل');
    await user.selectOptions(screen.getByLabelText('الهوية الهدف (يبقى اسمها)'), '1');
    const checkboxes = screen.getAllByRole('checkbox');
    // اختر المجموعة الممتصة الأولى غير الهدف
    await user.click(checkboxes[0]);

    expect(await screen.findByRole('button', { name: 'معاينة التوحيد' })).toBeInTheDocument();
  });

  it('يستدعي POST groups/unify-preview عند المعاينة ويعرض النتيجة', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      data: { targetName: 'وزارة النقل', absorbedGroups: [{ groupId: 2, name: 'وزاره النقل', entryCount: 1, governorates: ['حمص'] }], totalEntriesToMove: 1, warnings: [] },
    });
    render(<UnifyNamesModal onClose={vi.fn()} onCommitted={vi.fn()} />);

    await screen.findByText('وزارة النقل');
    await user.selectOptions(screen.getByLabelText('الهوية الهدف (يبقى اسمها)'), '1');
    await user.click(screen.getAllByRole('checkbox')[0]);
    await user.click(screen.getByRole('button', { name: 'معاينة التوحيد' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-registry/groups/unify-preview', expect.objectContaining({ targetGroupId: 1, absorbedGroupIds: expect.any(Array) }));
    });
    expect(await screen.findByText('معاينة توحيد التسمية')).toBeInTheDocument();
    expect(screen.getAllByText('وزاره النقل').length).toBeGreaterThanOrEqual(1);
  });

  it('يستدعي POST groups/unify عند التأكيد ويستدعي onCommitted', async () => {
    const user = userEvent.setup();
    const onCommitted = vi.fn();
    (api.post as unknown as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({
        data: { targetName: 'وزارة النقل', absorbedGroups: [{ groupId: 2, name: 'وزاره النقل', entryCount: 1, governorates: ['حمص'] }], totalEntriesToMove: 1, warnings: [] },
      })
      .mockResolvedValueOnce({
        data: { targetGroupId: 1, canonicalName: 'وزارة النقل', groupsUnified: 1, entriesMoved: 1, changeEventId: 99 },
      });

    render(<UnifyNamesModal onClose={onCommitted} onCommitted={onCommitted} />);

    await screen.findByText('وزارة النقل');
    await user.selectOptions(screen.getByLabelText('الهوية الهدف (يبقى اسمها)'), '1');
    await user.click(screen.getAllByRole('checkbox')[0]);
    await user.click(screen.getByRole('button', { name: 'معاينة التوحيد' }));
    await screen.findByText('معاينة توحيد التسمية');
    await user.click(screen.getByRole('button', { name: 'تأكيد التوحيد' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-registry/groups/unify', expect.objectContaining({ targetGroupId: 1, absorbedGroupIds: expect.any(Array) }));
    });
    expect(onCommitted).toHaveBeenCalledWith(expect.stringContaining('تم توحيد'));
  });

  it('يعرض تنبيه عدم حفظ الأسماء القديمة كبدائل', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      data: { targetName: 'وزارة النقل', absorbedGroups: [{ groupId: 2, name: 'وزاره النقل', entryCount: 1, governorates: ['حمص'] }], totalEntriesToMove: 1, warnings: [] },
    });
    render(<UnifyNamesModal onClose={vi.fn()} onCommitted={vi.fn()} />);

    await screen.findByText('وزارة النقل');
    await user.selectOptions(screen.getByLabelText('الهوية الهدف (يبقى اسمها)'), '1');
    await user.click(screen.getAllByRole('checkbox')[0]);
    await user.click(screen.getByRole('button', { name: 'معاينة التوحيد' }));

    expect(await screen.findByText(/لن تُحفظ كأسماء بديلة/)).toBeInTheDocument();
  });
});
