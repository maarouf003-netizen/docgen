import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AlertDetailsSheet } from './AlertDetailsSheet';
import { makeHeadAlertDetail } from '../../test/factories';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../../api/client';

const MESSAGE = 'نص التنبيه الكامل المعروض في حوار التفاصيل.';

function mockDetail(id = 7, message = MESSAGE) {
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
    data: makeHeadAlertDetail({ id, message }),
  });
}

beforeEach(() => {
  vi.clearAllMocks();
});

/**
 * حراسة وصول حوار نص التنبيه (الخطة §18:3119): مخرج `Escape`، وزر إغلاق مسمّى،
 * و`role=dialog`/`aria-modal`/`aria-labelledby` صحيحة، وقفل تمرير داخلي
 * (`overscroll-contain`) بلا تمرير أفقي. لا يُختبر «حبس تركيز» لأنه غير منفّذ
 * في المكوّن — لا نخترع سلوكًا غير موجود.
 */
describe('AlertDetailsSheet (D12)', () => {
  it('لا يُركَّب ولا يجلب شيئًا حين لا تنبيه مفتوح', () => {
    render(<AlertDetailsSheet alertId={null} onClose={vi.fn()} />);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalled();
  });

  it('يجلب `GET /alerts/{id}` عند الفتح ويعرض النص الكامل', async () => {
    mockDetail();

    render(<AlertDetailsSheet alertId={7} onClose={vi.fn()} />);

    expect(api.get).toHaveBeenCalledTimes(1);
    expect(api.get).toHaveBeenCalledWith('/alerts/7', expect.any(Object));
    expect(await screen.findByText(MESSAGE)).toBeInTheDocument();
  });

  it('Escape يستدعي onClose', async () => {
    mockDetail();
    const onClose = vi.fn();

    render(<AlertDetailsSheet alertId={7} onClose={onClose} />);
    await screen.findByText(MESSAGE);

    fireEvent.keyDown(window, { key: 'Escape' });

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('زر الإغلاق يستدعي onClose ويحمل aria-label وهدف لمس كافيًا', async () => {
    mockDetail();
    const onClose = vi.fn();
    const user = userEvent.setup();

    render(<AlertDetailsSheet alertId={7} onClose={onClose} />);
    const close = screen.getByRole('button', { name: 'إغلاق' });

    expect(close).toHaveClass('min-h-11');
    expect(close).toHaveClass('min-w-11');

    await user.click(close);

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('role=dialog وaria-modal وaria-labelledby يشير إلى عنوان موجود', async () => {
    mockDetail();

    render(<AlertDetailsSheet alertId={7} onClose={vi.fn()} />);
    await screen.findByText(MESSAGE);

    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');

    const labelledBy = dialog.getAttribute('aria-labelledby');
    expect(labelledBy).toBeTruthy();
    const heading = document.getElementById(labelledBy!);
    expect(heading).not.toBeNull();
    expect(heading).toHaveTextContent('نص التنبيه');
  });

  it('الحاوية الداخلية تقفل التمرير (overscroll-contain) ولا تمرير أفقي في الحوار', async () => {
    mockDetail();

    render(<AlertDetailsSheet alertId={7} onClose={vi.fn()} />);
    await screen.findByText(MESSAGE);

    const dialog = screen.getByRole('dialog');
    const scroll = dialog.querySelector('.overscroll-contain');

    expect(scroll).not.toBeNull();
    expect(scroll).toHaveClass('overflow-y-auto');
    expect(scroll).toHaveClass('min-h-0');
    expect(scroll).toHaveClass('flex-1');

    // لا قصّ/تمرير أفقي: لا عنصر داخل الحوار يفرض `overflow-x`.
    for (const element of dialog.querySelectorAll<HTMLElement>('*')) {
      expect(element.className).not.toMatch(/overflow-x-(auto|scroll)/);
    }
  });

  it('يعرض نصًا فعليًا بطول 12,000 حرف كاملًا بلا قصّ', async () => {
    const longMessage = 'ا'.repeat(12_000);
    mockDetail(7, longMessage);

    render(<AlertDetailsSheet alertId={7} onClose={vi.fn()} />);

    const paragraph = await screen.findByText(longMessage);
    expect(paragraph.textContent).toHaveLength(12_000);
    expect(paragraph).toHaveClass('break-words');
    expect(paragraph).toHaveClass('whitespace-pre-wrap');
  });

  it('فشل الجلب يعرض تنبيهًا وزر إعادة محاولة يعمل', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockRejectedValueOnce(
      new Error('تعذّر جلب النص'),
    );
    const user = userEvent.setup();

    render(<AlertDetailsSheet alertId={7} onClose={vi.fn()} />);

    expect(await screen.findByRole('alert')).toHaveTextContent('تعذّر جلب النص');
    const retry = screen.getByRole('button', { name: 'إعادة المحاولة' });
    expect(retry).toHaveClass('min-h-11');

    mockDetail();
    await user.click(retry);

    expect(api.get).toHaveBeenCalledTimes(2);
    expect(await screen.findByText(MESSAGE)).toBeInTheDocument();
  });
});
