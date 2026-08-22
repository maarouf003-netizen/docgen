import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import FileAlertModal from './FileAlertModal';

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn() },
  getApiErrorMessage: (error: unknown) => {
    const e = error as {
      isAxiosError?: boolean;
      response?: { status?: number; data?: { message?: string } };
    };
    if (e?.isAxiosError) {
      if (e.response?.data?.message) return e.response.data.message;
      if (e.response?.status === 403) return 'لا تملك صلاحية تنفيذ هذا الإجراء';
      return 'تعذر الاتصال بالخادم. تحقق من الاتصال وأعد المحاولة';
    }
    return 'حدث خطأ غير متوقع';
  },
}));

import { api } from '../api/client';

const wait = (ms: number) => act(() => new Promise((resolve) => setTimeout(resolve, ms)));

beforeEach(() => {
  vi.clearAllMocks();
});

describe('FileAlertModal', () => {
  it('يعرض عنوان الملف والمستلم ويطلب نص التنبيه قبل الإرسال', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(
      <FileAlertModal
        documentId={7}
        documentTitle="متداول - سامر حسن"
        recipientName="المحامي المختص"
        onClose={onClose}
      />,
    );

    expect(screen.getByText('متداول - سامر حسن')).toBeInTheDocument();
    expect(screen.getByText('المحامي المختص')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'إرسال التنبيه' }));

    expect(screen.getByText('نص التنبيه مطلوب')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('يرسل التنبيه بنجاح ثم يغلق تلقائيًا مرة واحدة', async () => {
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const user = userEvent.setup();
    const onClose = vi.fn();
    const onSent = vi.fn();
    render(<FileAlertModal documentId={7} onClose={onClose} onSent={onSent} />);

    await user.type(screen.getByLabelText('نص التنبيه'), 'مراجعة عاجلة');
    await user.click(screen.getByRole('button', { name: 'إرسال التنبيه' }));

    expect(await screen.findByText('تم توجيه التنبيه إلى المحامي المختص')).toBeInTheDocument();
    expect(api.post).toHaveBeenCalledWith('/alerts', {
      targetType: 'document',
      documentId: 7,
      targetLawyerId: null,
      message: 'مراجعة عاجلة',
    });
    expect(onSent).toHaveBeenCalledTimes(1);
    expect(onClose).not.toHaveBeenCalled();

    await wait(800);

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('الإغلاق اليدوي خلال نافذة الإغلاق التلقائي يمنع النداء المتأخر المزدوج', async () => {
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const user = userEvent.setup();
    const onClose = vi.fn();
    const { unmount } = render(<FileAlertModal documentId={7} onClose={onClose} />);

    await user.type(screen.getByLabelText('نص التنبيه'), 'مراجعة عاجلة');
    await user.click(screen.getByRole('button', { name: 'إرسال التنبيه' }));
    expect(await screen.findByText('تم توجيه التنبيه إلى المحامي المختص')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'إلغاء' }));
    expect(onClose).toHaveBeenCalledTimes(1);

    unmount();

    await wait(800);

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('يعرض رسالة الخطأ الواردة من الخادم عند فشل الإرسال ولا يغلق', async () => {
    (api.post as unknown as ReturnType<typeof vi.fn>).mockRejectedValue({
      isAxiosError: true,
      response: { status: 400, data: { message: 'نص التنبيه طويل جدًا' } },
    });
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<FileAlertModal documentId={7} onClose={onClose} />);

    await user.type(screen.getByLabelText('نص التنبيه'), 'مراجعة');
    await user.click(screen.getByRole('button', { name: 'إرسال التنبيه' }));

    expect(await screen.findByText('نص التنبيه طويل جدًا')).toBeInTheDocument();

    await wait(800);

    expect(onClose).not.toHaveBeenCalled();
  });
});
