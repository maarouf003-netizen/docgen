import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import PendingRegistrationsPage from './PendingRegistrationsPage';

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

describe('PendingRegistrationsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [
        {
          documentId: 7,
          circuitId: 1,
          circuitName: 'دائرة الهدف',
          borrowerName: 'مقترض',
          oldFileNumber: '100',
          oldFileType: 'صلح',
          oldFileYear: '2025',
        },
      ],
    });
  });

  it('يعرض «المعروض حاليًا (قديم)» ويحفظ ذريًا بالأرقام المطبّعة', async () => {
    const user = userEvent.setup();
    render(<PendingRegistrationsPage />);

    expect(await screen.findByText('دائرة: دائرة الهدف')).toBeInTheDocument();
    expect(screen.getByText(/100 صلح 2025/)).toBeInTheDocument();

    await user.type(screen.getByLabelText('الرقم الجديد للملف 7'), '١٢٣');
    await user.clear(screen.getByLabelText('سنة الملف 7'));
    await user.type(screen.getByLabelText('سنة الملف 7'), '٢٠٢٦');
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { completedCount: 1 } });
    await user.click(screen.getByRole('button', { name: 'حفظ الكل (ذري)' }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith('/documents/complete-registrations', {
        entries: [{ documentId: 7, fileNumber: '123', fileType: 'صلح', fileYear: '2026' }],
      }),
    );
  });

  it('يعرض عنوان «ملفات محالة حديثًا» مع سطر التحديث', async () => {
    render(<PendingRegistrationsPage />);

    expect(await screen.findByRole('heading', { name: 'ملفات محالة حديثًا' })).toBeInTheDocument();
    expect(screen.getByText('بانتظار تحديث بياناتها في الدوائر المُحال إليها')).toBeInTheDocument();
  });

  it('يعرض نص الفراغ الجديد عند عدم وجود محالات', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });
    render(<PendingRegistrationsPage />);

    expect(await screen.findByText('لا توجد ملفات محالة حديثًا بانتظار تحديث بياناتها')).toBeInTheDocument();
  });

  it('يرفض الرقم الفارغ أماميًا مع تركيز أول خطأ', async () => {
    const user = userEvent.setup();
    render(<PendingRegistrationsPage />);
    await screen.findByText('دائرة: دائرة الهدف');
    await user.click(screen.getByRole('button', { name: 'حفظ الكل (ذري)' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('الرقم الجديد إلزامي');
    expect(api.post).not.toHaveBeenCalled();
  });
});
