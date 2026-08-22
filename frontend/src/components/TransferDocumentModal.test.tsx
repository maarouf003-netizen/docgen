import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import TransferDocumentModal from './TransferDocumentModal';
import type { LawyerListItem } from '../types';

const wait = (ms: number) => act(() => new Promise((resolve) => setTimeout(resolve, ms)));

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

function lawyer(overrides: Partial<LawyerListItem> = {}): LawyerListItem {
  return {
    id: 2,
    username: 'lawyer2',
    fullName: 'محامي ثانٍ',
    isActive: true,
    branchId: 1,
    branchName: 'دمشق',
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [lawyer()] });
});

describe('TransferDocumentModal', () => {
  it('يعرض المحامين المفعلين فقط ويستثني المالك الحالي', async () => {
    render(
      <TransferDocumentModal
        documentId={5}
        currentOwnerId={2}
        onClose={() => undefined}
      />,
    );

    expect(await screen.findByText('لا يوجد محامون مفعّلون آخرون في فرعك للنقل إليهم')).toBeInTheDocument();
  });

  it('ينقل الملف إلى المحامي المختار', async () => {
    const onClose = vi.fn();
    const onTransferred = vi.fn();
    const user = userEvent.setup();
    render(
      <TransferDocumentModal
        documentId={5}
        currentOwnerId={1}
        onClose={onClose}
        onTransferred={onTransferred}
      />,
    );

    await user.selectOptions(await screen.findByLabelText('المحامي المستهدف'), '2');
    await user.click(screen.getByRole('button', { name: 'نقل الملف' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/documents/5/transfer', { targetLawyerId: 2 });
    });
    expect(onTransferred).toHaveBeenCalled();

    expect(await screen.findByText('تم نقل الملف بنجاح')).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();

    await wait(800);

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('الإغلاق اليدوي خلال نافذة الإغلاق التلقائي يمنع النداء المتأخر المزدوج', async () => {
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const onClose = vi.fn();
    const onTransferred = vi.fn();
    const user = userEvent.setup();
    const { unmount } = render(
      <TransferDocumentModal
        documentId={5}
        currentOwnerId={1}
        onClose={onClose}
        onTransferred={onTransferred}
      />,
    );

    await user.selectOptions(await screen.findByLabelText('المحامي المستهدف'), '2');
    await user.click(screen.getByRole('button', { name: 'نقل الملف' }));
    expect(await screen.findByText('تم نقل الملف بنجاح')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'إلغاء' }));
    expect(onClose).toHaveBeenCalledTimes(1);

    unmount();

    await wait(800);

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('يعرض رسالة تحقق عند محاولة النقل دون اختيار محامٍ', async () => {
    const user = userEvent.setup();
    render(
      <TransferDocumentModal
        documentId={5}
        currentOwnerId={1}
        onClose={() => undefined}
      />,
    );

    await user.click(await screen.findByRole('button', { name: 'نقل الملف' }));

    expect(screen.getByText('اختر المحامي المستهدف')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يعرض رسالة الخطأ الواردة من الخادم عند فشل النقل', async () => {
    (api.post as unknown as ReturnType<typeof vi.fn>).mockRejectedValue({
      isAxiosError: true,
      response: { status: 400, data: { message: 'لا يمكن نقل الملف إلى محامٍ من فرع آخر' } },
    });
    const user = userEvent.setup();
    render(
      <TransferDocumentModal
        documentId={5}
        currentOwnerId={1}
        onClose={() => undefined}
      />,
    );

    await user.selectOptions(await screen.findByLabelText('المحامي المستهدف'), '2');
    await user.click(screen.getByRole('button', { name: 'نقل الملف' }));

    expect(await screen.findByText('لا يمكن نقل الملف إلى محامٍ من فرع آخر')).toBeInTheDocument();
  });
});
