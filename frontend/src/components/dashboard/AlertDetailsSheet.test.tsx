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
    // ملاحظة دليل: هذا حارس أصناف لا قياس تخطيط (`jsdom` بلا تخطيط)، و`overflow-auto`
    // و`overflow-scroll` يفرضان المحورين أيضًا فيُمنعان صراحةً. القياس الفعلي للجوال
    // (`scrollWidth <= clientWidth`) يحتاج متصفحًا حقيقيًا.
    for (const element of dialog.querySelectorAll<HTMLElement>('*')) {
      expect(element.className).not.toMatch(/overflow-(x-)?(auto|scroll)/);
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

  // ── إمكانية الوصول الفعلية (لا الموثَّقة): تركيز، حصر، قفل تمرير، مخارج ──

  it('ينقل التركيز إلى زر الإغلاق عند الفتح ويعيده إلى المشغّل عند الإغلاق', async () => {
    const trigger = document.createElement('button');
    trigger.type = 'button';
    trigger.textContent = 'فتح التنبيه';
    document.body.appendChild(trigger);
    trigger.focus();

    mockDetail();
    const { rerender } = render(<AlertDetailsSheet alertId={null} onClose={vi.fn()} />);
    rerender(<AlertDetailsSheet alertId={7} onClose={vi.fn()} />);

    await screen.findByText(MESSAGE);
    expect(screen.getByRole('button', { name: 'إغلاق' })).toHaveFocus();

    rerender(<AlertDetailsSheet alertId={null} onClose={vi.fn()} />);
    expect(trigger).toHaveFocus();

    trigger.remove();
  });

  it('يحصر Tab داخل الحوار: من الأخير إلى الأول وبالعكس', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('تعذّر الجلب'));

    render(<AlertDetailsSheet alertId={7} onClose={vi.fn()} />);

    const close = screen.getByRole('button', { name: 'إغلاق' });
    const retry = await screen.findByRole('button', { name: 'إعادة المحاولة' });
    const dialog = screen.getByRole('dialog');

    retry.focus();
    fireEvent.keyDown(dialog, { key: 'Tab' });
    expect(close).toHaveFocus();

    fireEvent.keyDown(dialog, { key: 'Tab', shiftKey: true });
    expect(retry).toHaveFocus();
  });

  it('يقفل تمرير الصفحة الخلفية أثناء الفتح ويعيد الحالة عند الإغلاق', async () => {
    mockDetail();
    const { rerender } = render(<AlertDetailsSheet alertId={7} onClose={vi.fn()} />);

    await screen.findByText(MESSAGE);
    expect(document.body.style.overflow).toBe('hidden');

    rerender(<AlertDetailsSheet alertId={null} onClose={vi.fn()} />);
    expect(document.body.style.overflow).toBe('');
  });

  it('النقر على الغلاف يغلق، والنقر داخل البطاقة لا يغلق', async () => {
    mockDetail();
    const onClose = vi.fn();

    render(<AlertDetailsSheet alertId={7} onClose={onClose} />);
    await screen.findByText(MESSAGE);

    const dialog = screen.getByRole('dialog');
    fireEvent.click(dialog);
    expect(onClose).not.toHaveBeenCalled();

    fireEvent.click(dialog.parentElement!);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('يزيل مستمع Escape عند إزالة المكوّن', async () => {
    mockDetail();
    const onClose = vi.fn();

    const { unmount } = render(<AlertDetailsSheet alertId={7} onClose={onClose} />);
    await screen.findByText(MESSAGE);

    unmount();
    fireEvent.keyDown(window, { key: 'Escape' });

    expect(onClose).not.toHaveBeenCalled();
  });

  it('تبديل المعرّف لا يعرض نص التنبيه السابق', async () => {
    const get = api.get as unknown as ReturnType<typeof vi.fn>;
    get.mockImplementation((url: string) =>
      Promise.resolve({
        data: makeHeadAlertDetail({
          id: Number(url.split('/').pop()),
          message: url.endsWith('/7') ? 'نص التنبيه الأول' : 'نص التنبيه الثاني',
        }),
      }),
    );

    const { rerender } = render(<AlertDetailsSheet alertId={7} onClose={vi.fn()} />);
    expect(await screen.findByText('نص التنبيه الأول')).toBeInTheDocument();

    rerender(<AlertDetailsSheet alertId={8} onClose={vi.fn()} />);
    expect(await screen.findByText('نص التنبيه الثاني')).toBeInTheDocument();
    expect(screen.queryByText('نص التنبيه الأول')).not.toBeInTheDocument();
  });
});
