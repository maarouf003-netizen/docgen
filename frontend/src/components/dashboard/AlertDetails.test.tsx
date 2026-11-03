import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AlertDetails } from './AlertDetails';
import { makeHeadAlertDetail } from '../../test/factories';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../../api/client';

const FULL_MESSAGE_HEAD = 'بداية النص الكامل — ';
const FULL_MESSAGE_TAIL = ' — نهاية النص الكامل التي لا تظهر في المعاينة.';
// البند §18:3118: النص الطويل الفعلي 12,000 حرف (لا معاينة قصيرة مُكبَّرة).
const FULL_MESSAGE = `${FULL_MESSAGE_HEAD}${'ا'.repeat(
  12_000 - FULL_MESSAGE_HEAD.length - FULL_MESSAGE_TAIL.length,
)}${FULL_MESSAGE_TAIL}`;

beforeEach(() => {
  vi.clearAllMocks();
});

describe('AlertDetails (D12)', () => {
  it('لا يجلب شيئًا قبل الفتح — القائمة تحمل المعاينة فقط', () => {
    render(<AlertDetails alertId={7} previewTruncated />);

    expect(api.get).not.toHaveBeenCalled();
    // لا نص كامل مبكر (ولا مؤشر تحميل) قبل أن يطلب المستخدم.
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'عرض النص الكامل' }),
    ).toHaveAttribute('aria-expanded', 'false');
  });

  it('يجلب `GET /alerts/{id}` عند الفتح ويعرض النص الكامل لا المعاينة', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: makeHeadAlertDetail({ id: 7, message: FULL_MESSAGE }),
    });

    render(<AlertDetails alertId={7} previewTruncated />);
    await user.click(screen.getByRole('button', { name: 'عرض النص الكامل' }));

    expect(api.get).toHaveBeenCalledTimes(1);
    expect(api.get).toHaveBeenCalledWith('/alerts/7', expect.any(Object));
    // الفشل لو عُرضت المعاينة وحدها: النص الكامل (بطوله الحقيقي) مطلوب حرفيًا.
    const message = await screen.findByText(FULL_MESSAGE);
    expect(message).toBeInTheDocument();
    expect(FULL_MESSAGE).toHaveLength(12_000);
    expect(message.textContent).toHaveLength(12_000);

    // لا قصّ ولا تمرير أفقي: النص يلتف داخل الحاوية (`break-words`) ويحفظ
    // الأسطر (`whitespace-pre-wrap`)، ولا سلف يفرض `overflow-x`.
    expect(message).toHaveClass('break-words');
    expect(message).toHaveClass('whitespace-pre-wrap');
    for (let node = message.parentElement; node; node = node.parentElement) {
      expect(node.className).not.toMatch(/overflow-x-(auto|scroll)/);
    }

    expect(screen.getByRole('button', { name: 'إخفاء النص الكامل' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );
  });

  it('يخفي النص بعد الإغلاق ويعيد الزر إلى حالته المطوية', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: makeHeadAlertDetail({ id: 7, message: FULL_MESSAGE }),
    });

    render(<AlertDetails alertId={7} previewTruncated />);
    await user.click(screen.getByRole('button', { name: 'عرض النص الكامل' }));
    expect(await screen.findByText(FULL_MESSAGE)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'إخفاء النص الكامل' }));
    expect(screen.queryByText(FULL_MESSAGE)).not.toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'عرض النص الكامل' }),
    ).toHaveAttribute('aria-expanded', 'false');
  });

  it('يعرض خطأ الجلب مع «إعادة المحاولة» ويستعيد النص عند النجاح', async () => {
    const user = userEvent.setup();
    const get = api.get as unknown as ReturnType<typeof vi.fn>;
    get.mockRejectedValueOnce(new Error('تعذّر جلب النص'));
    get.mockResolvedValueOnce({ data: makeHeadAlertDetail({ id: 7, message: FULL_MESSAGE }) });

    render(<AlertDetails alertId={7} previewTruncated />);
    await user.click(screen.getByRole('button', { name: 'عرض النص الكامل' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('تعذّر جلب النص');
    await user.click(screen.getByRole('button', { name: 'إعادة المحاولة' }));

    expect(api.get).toHaveBeenCalledTimes(2);
    expect(await screen.findByText(FULL_MESSAGE)).toBeInTheDocument();
    // الخطأ السابق لا يبقى معلنًا بعد النجاح.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('لا يعرض زر الجلب عندما تكون المعاينة كاملة (النص الكامل موجود أصلًا)', () => {
    render(<AlertDetails alertId={7} previewTruncated={false} />);

    expect(screen.queryByRole('button', { name: 'عرض النص الكامل' })).not.toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalled();
  });
});
