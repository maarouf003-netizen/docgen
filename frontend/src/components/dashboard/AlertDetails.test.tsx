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

const FULL_MESSAGE = `${'تفصيل التنبيه '.repeat(40)}— نهاية النص الكامل التي لا تظهر في المعاينة.`;

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
    // الفشل لو عُرضت المعاينة وحدها: النص الكامل (بطوله) مطلوب حرفيًا.
    expect(await screen.findByText(FULL_MESSAGE)).toBeInTheDocument();
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

  it('يعرض خطأ الجلب بجانب التنبيه بلا كسر الصف', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('تعذّر جلب النص'));

    render(<AlertDetails alertId={7} previewTruncated />);
    await user.click(screen.getByRole('button', { name: 'عرض النص الكامل' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('تعذّر جلب النص');
    expect(screen.getByRole('button', { name: 'إخفاء النص الكامل' })).toBeInTheDocument();
  });

  it('لا يعرض زر الجلب عندما تكون المعاينة كاملة (النص الكامل موجود أصلًا)', () => {
    render(<AlertDetails alertId={7} previewTruncated={false} />);

    expect(screen.queryByRole('button', { name: 'عرض النص الكامل' })).not.toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalled();
  });
});
