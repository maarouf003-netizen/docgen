import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { OccurrencesEditor } from './OccurrencesEditor';

const apiMock = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
}));

vi.mock('../../api/client', () => ({
  api: apiMock,
  getApiErrorMessage: () => 'حدث خطأ غير متوقع',
}));

import { api } from '../../api/client';

const apiPost = () => api.post as unknown as ReturnType<typeof vi.fn>;
const currentYear = new Date().getFullYear();

beforeEach(() => {
  vi.clearAllMocks();
});

async function openRenewalForm() {
  const user = userEvent.setup();
  render(
    <OccurrencesEditor
      documentId={7}
      initial={[]}
      isFileStruckOff
      generalEntitySide="executed"
      onRenewalRestored={() => {}}
    />,
  );
  await user.click(screen.getByRole('button', { name: '+ إضافة وقعة' }));
  await user.selectOptions(screen.getByLabelText('نوع الوقعة'), 'renewal');
  return user;
}

describe('OccurrencesEditor', () => {
  it('يخفي سنة الإعادة ويثبتها على سنة اليوم لعائلة «منفذ عليها» عند استعادة مشطوب', async () => {
    apiPost().mockResolvedValue({});
    const user = await openRenewalForm();

    // حقل السنة مخفي ويُعرض نص ثابت بدلًا منه.
    expect(screen.queryByRole('textbox', { name: 'سنة الإعادة' })).not.toBeInTheDocument();
    expect(screen.getByText(new RegExp(String(currentYear)))).toBeInTheDocument();

    await user.type(screen.getByLabelText('رقم الملف الجديد'), '2026/55');
    await user.click(screen.getByRole('button', { name: 'حفظ الوقعة' }));

    await waitFor(() =>
      expect(apiPost()).toHaveBeenCalledWith('/documents/7/restore-struck-off', {
        renewalFileNumber: '2026/55',
        renewalFileType: undefined,
        renewalYear: currentYear,
        renewalFileReceiptNumber: undefined,
        renewalFileReceiptDate: undefined,
        renewalDate: undefined,
      }),
    );
  });

  it('يبقي سنة الإعادة قابلة للتحرير لعائلة «طالبة تنفيذ» ويقبل الأرقام العربية', async () => {
    apiPost().mockResolvedValue({});
    const user = userEvent.setup();
    render(
      <OccurrencesEditor
        documentId={7}
        initial={[]}
        isFileStruckOff
        generalEntitySide="applicant"
        onRenewalRestored={() => {}}
      />,
    );
    await user.click(screen.getByRole('button', { name: '+ إضافة وقعة' }));
    await user.selectOptions(screen.getByLabelText('نوع الوقعة'), 'renewal');

    const yearInput = screen.getByRole('textbox', { name: 'سنة الإعادة' });
    expect(yearInput).toBeInTheDocument();
    await user.type(screen.getByLabelText('رقم الملف الجديد'), '2026/55');
    await user.type(yearInput, '٢٠٢٦');
    await user.click(screen.getByRole('button', { name: 'حفظ الوقعة' }));

    await waitFor(() =>
      expect(apiPost()).toHaveBeenCalledWith('/documents/7/restore-struck-off', {
        renewalFileNumber: '2026/55',
        renewalFileType: undefined,
        renewalYear: 2026,
        renewalFileReceiptNumber: undefined,
        renewalFileReceiptDate: undefined,
        renewalDate: undefined,
      }),
    );
  });
});
