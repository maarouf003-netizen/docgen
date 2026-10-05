import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import CircuitEmptyingWizard from './CircuitEmptyingWizard';
import type { ExecutionCircuitDto } from '../../types';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'حدث خطأ غير متوقع',
}));

import { api } from '../../api/client';

const noop = () => {};

function circuit(id: number, name: string, isActive = true, fileCount = 1): ExecutionCircuitDto {
  return { id, branchId: 1, name, isActive, fileCount, pendingCount: 0, version: 0 };
}

describe('CircuitEmptyingWizard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/documents') {
        return Promise.resolve({
          data: {
            items: [{ id: 11, borrowerName: 'مقترض', fileNumber: '100', fileType: 'صلح', fileYear: '2026', lawyer: 'محامي أول' }],
          },
        });
      }
      if (url === '/users/lawyers') {
        return Promise.resolve({
          data: [
            { id: 3, fullName: 'محامي نشط', isActive: true },
            { id: 4, fullName: 'محامي معطل', isActive: false },
          ],
        });
      }
      return Promise.reject(new Error('unknown'));
    });
  });

  it('يُظهر البانر الحرفي عند اكتمال الإفراغ ويُرشّح المحامين المعطلين', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { referredCount: 1, skippedCount: 0, remainingCount: 0 },
    });
    render(
      <CircuitEmptyingWizard
        source={circuit(1, 'دائرة المصدر')}
        circuits={[circuit(1, 'دائرة المصدر'), circuit(2, 'دائرة الهدف')]}
        onDone={noop}
        onCancel={noop}
      />,
    );

    await user.selectOptions(screen.getByLabelText(/الدائرة الهدف/), '2');
    // المحامي المعطل لا يظهر في القائمة أصلًا (الخادم يرفضه — لا احتكاك).
    expect(screen.queryByRole('option', { name: 'محامي معطل' })).not.toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText(/المحامي الهدف/), '3');
    await user.click(screen.getByRole('checkbox', { name: /إحالة ملف 11/ }));
    await user.click(screen.getByRole('button', { name: 'إحالة (1)' }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith(
        '/execution-circuits/1/refer-files',
        { fileIds: [11], targetLawyerId: 3, targetCircuitId: 2 },
        expect.any(Object),
      ),
    );
    expect(
      await screen.findByText('لا تنسى نقل ملفات هذه الدائرة لمحامي أو محامين اخرين ان كان لذلك مقتضى'),
    ).toBeInTheDocument();
  });

  it('الحذف يتطلب تسليح تأكيد — النقرة الأولى لا تحذف', async () => {
    // S1: الفعل الخطر لا يُنفَّذ فورًا (مخالفة سابقة) — خطوتان صريحتان.
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { referredCount: 1, skippedCount: 0, remainingCount: 0 },
    });
    const onDone = vi.fn();
    render(
      <CircuitEmptyingWizard
        source={circuit(1, 'دائرة المصدر')}
        circuits={[circuit(1, 'دائرة المصدر'), circuit(2, 'دائرة الهدف')]}
        onDone={onDone}
        onCancel={noop}
      />,
    );

    await user.selectOptions(screen.getByLabelText(/الدائرة الهدف/), '2');
    await user.selectOptions(screen.getByLabelText(/المحامي الهدف/), '3');
    await user.click(screen.getByRole('checkbox', { name: /إحالة ملف 11/ }));
    await user.click(screen.getByRole('button', { name: 'إحالة (1)' }));
    expect(
      await screen.findByText('لا تنسى نقل ملفات هذه الدائرة لمحامي أو محامين اخرين ان كان لذلك مقتضى'),
    ).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'حذف الدائرة' }));
    expect(api.delete).not.toHaveBeenCalled();
    expect(await screen.findByText(/حذف الدائرة نهائي/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'تأكيد الحذف نهائيًا' }));
    await waitFor(() => expect(api.delete).toHaveBeenCalledWith('/execution-circuits/1'));
    expect(onDone).toHaveBeenCalledTimes(1);
  });

  it('التراجع عن تسليح الحذف لا يحذف، وEscape يغلق المعالج', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { referredCount: 1, skippedCount: 0, remainingCount: 0 },
    });
    const onCancel = vi.fn();
    render(
      <CircuitEmptyingWizard
        source={circuit(1, 'دائرة المصدر')}
        circuits={[circuit(1, 'دائرة المصدر'), circuit(2, 'دائرة الهدف')]}
        onDone={noop}
        onCancel={onCancel}
      />,
    );

    await user.selectOptions(screen.getByLabelText(/الدائرة الهدف/), '2');
    await user.selectOptions(screen.getByLabelText(/المحامي الهدف/), '3');
    await user.click(screen.getByRole('checkbox', { name: /إحالة ملف 11/ }));
    await user.click(screen.getByRole('button', { name: 'إحالة (1)' }));
    await screen.findByText('لا تنسى نقل ملفات هذه الدائرة لمحامي أو محامين اخرين ان كان لذلك مقتضى');

    await user.click(screen.getByRole('button', { name: 'حذف الدائرة' }));
    await screen.findByText(/حذف الدائرة نهائي/);
    await user.click(screen.getByRole('button', { name: 'تراجع' }));
    expect(api.delete).not.toHaveBeenCalled();
    expect(screen.queryByText(/حذف الدائرة نهائي/)).not.toBeInTheDocument();

    // S6.e: Escape يغلق المعالج (بعد إلغاء التسليح لم يعد مسلّحًا).
    await user.keyboard('{Escape}');
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('يعرض «أنشئ دائرة بديلة أولًا» عند غياب هدف متاح', async () => {
    render(
      <CircuitEmptyingWizard
        source={circuit(1, 'دائرة المصدر')}
        circuits={[circuit(1, 'دائرة المصدر')]}
        onDone={noop}
        onCancel={noop}
      />,
    );

    expect(await screen.findByText('أنشئ دائرة بديلة أولًا')).toBeInTheDocument();
  });
});
