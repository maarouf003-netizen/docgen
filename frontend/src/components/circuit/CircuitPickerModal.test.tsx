import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import CircuitPickerModal from './CircuitPickerModal';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'حدث خطأ غير متوقع',
}));

import { api } from '../../api/client';

const noop = () => {};

describe('CircuitPickerModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('يعرض دوائر الفرع ويختار منها', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [
        { id: 1, branchId: 1, name: 'دائرة دمشق الأولى', isActive: true, fileCount: 0, pendingCount: 0, version: 0 },
        { id: 2, branchId: 1, name: 'دائرة دمشق الثانية', isActive: true, fileCount: 0, pendingCount: 0, version: 0 },
      ],
    });
    const onSelect = vi.fn();
    render(
      <CircuitPickerModal fetchUrl="/execution-circuits/for-lawyer" onSelect={onSelect} onClose={noop} />,
    );

    await waitFor(() => expect(api.get).toHaveBeenCalledWith('/execution-circuits/for-lawyer'));
    expect(await screen.findByText('دائرة دمشق الأولى')).toBeInTheDocument();

    await user.type(screen.getByLabelText('البحث باسم الدائرة'), 'الثانية');
    expect(screen.queryByText('دائرة دمشق الأولى')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'اختيار دائرة دمشق الثانية' }));
    expect(onSelect).toHaveBeenCalledTimes(1);
  });

  it('حصر التركيز: Tab من الأخير يلتف للأول وShift+Tab بالعكس', async () => {
    // S6.e: التركيز لا يتسرب للخلفية أثناء فتح الحوار.
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [
        { id: 1, branchId: 1, name: 'دائرة دمشق الأولى', isActive: true, fileCount: 0, pendingCount: 0, version: 0 },
        { id: 2, branchId: 1, name: 'دائرة دمشق الثانية', isActive: true, fileCount: 0, pendingCount: 0, version: 0 },
      ],
    });
    render(
      <CircuitPickerModal fetchUrl="/execution-circuits/for-lawyer" onSelect={noop} onClose={noop} />,
    );
    await screen.findByText('دائرة دمشق الأولى');

    const closers = screen.getAllByRole('button', { name: 'إغلاق' });
    const first = closers[0];
    const last = closers[closers.length - 1];

    last.focus();
    await user.tab();
    expect(first).toHaveFocus();

    await user.tab({ shift: true });
    expect(last).toHaveFocus();
  });

  it('Escape يغلق النافذة', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });
    const onClose = vi.fn();
    render(
      <CircuitPickerModal fetchUrl="/execution-circuits/for-lawyer" onSelect={noop} onClose={onClose} />,
    );
    await screen.findByText(/لا توجد دوائر مسجلة بعد/);

    await user.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('يعرض رسالة صادقة عند فراغ السجل', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });
    render(
      <CircuitPickerModal fetchUrl="/execution-circuits/for-lawyer" onSelect={noop} onClose={noop} />,
    );
    expect(await screen.findByText(/لا توجد دوائر مسجلة بعد/)).toBeInTheDocument();
  });
});
