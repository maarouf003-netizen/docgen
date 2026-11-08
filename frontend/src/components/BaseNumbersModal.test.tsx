import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import BaseNumbersModal from './BaseNumbersModal';

const { apiMock, errorMessageMock } = vi.hoisted(() => ({
  apiMock: { get: vi.fn(), put: vi.fn(), delete: vi.fn() },
  errorMessageMock: vi.fn(() => 'حدث خطأ في التحميل'),
}));

vi.mock('../api/client', () => ({ api: apiMock, getApiErrorMessage: errorMessageMock }));

beforeEach(() => {
  vi.clearAllMocks();
});

function entries() {
  return [
    {
      id: 2,
      year: 2026,
      baseNumber: '900',
      source: 'rotation',
      circuitId: 1,
      circuitName: 'دائرة أ',
      isVoided: false,
      isEffective: true,
    },
    {
      id: 1,
      year: 2025,
      baseNumber: '55',
      source: 'registration',
      circuitId: 1,
      circuitName: 'دائرة أ',
      isVoided: false,
      isEffective: false,
    },
  ];
}

describe('BaseNumbersModal', () => {
  it('يعرض السجل الكامل بالدائرة والمصدر والحالة', async () => {
    apiMock.get.mockResolvedValue({ data: entries() });
    render(
      <BaseNumbersModal
        documentId={7}
        documentTitle="ملف تجريبي"
        fileType="عادي"
        onClose={vi.fn()}
      />,
    );

    expect((await screen.findAllByText('900')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('55').length).toBeGreaterThan(0);
    expect(screen.getAllByText('دائرة أ').length).toBeGreaterThan(0);
    expect(screen.getAllByText('فعّال').length).toBeGreaterThan(0);
    expect(screen.getAllByText('سابق').length).toBeGreaterThan(0);
    expect(apiMock.get).toHaveBeenCalledWith('/documents/7/base-numbers');
  });

  it('يستخدم fetchUrl عند تمريره (بوابة المندوب) ويكون للقراءة فقط', async () => {
    apiMock.get.mockResolvedValue({ data: entries() });
    render(
      <BaseNumbersModal documentId={7} fetchUrl="/portal/files/1/base-numbers" onClose={vi.fn()} />,
    );

    expect((await screen.findAllByText('900')).length).toBeGreaterThan(0);
    expect(apiMock.get).toHaveBeenCalledWith('/portal/files/1/base-numbers');
    expect(screen.queryByRole('button', { name: 'تصحيح' })).not.toBeInTheDocument();
  });

  it('يصحح الصف الفعّال موضعيًا عبر PUT', async () => {
    const user = userEvent.setup();
    apiMock.get.mockResolvedValue({ data: entries() });
    apiMock.put.mockResolvedValue({ data: null });
    render(<BaseNumbersModal documentId={7} onClose={vi.fn()} />);

    await screen.findAllByText('900');
    await user.click(screen.getAllByRole('button', { name: 'تصحيح' })[0]);
    const input = screen.getByLabelText('رقم الأساس الجديد');
    await user.clear(input);
    await user.type(input, '901');
    await user.click(screen.getByRole('button', { name: 'حفظ التصحيح' }));

    await waitFor(() =>
      expect(apiMock.put).toHaveBeenCalledWith('/documents/7/base-numbers/2026', {
        baseNumber: '901',
        rowId: 2,
      }),
    );
  });

  it('يرفض التراجع عن التدوير قبل التأكيد ثم ينفذ DELETE بعد التأكيد', async () => {
    const user = userEvent.setup();
    apiMock.get.mockResolvedValue({ data: entries() });
    apiMock.delete.mockResolvedValue({ data: null });
    render(<BaseNumbersModal documentId={7} onClose={vi.fn()} />);

    await screen.findAllByText('900');
    await user.click(screen.getAllByRole('button', { name: 'تراجع' })[0]);
    expect(apiMock.delete).not.toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: 'تأكيد التراجع' }));

    await waitFor(() =>
      expect(apiMock.delete).toHaveBeenCalledWith('/documents/7/base-numbers/2026'),
    );
  });

  it('يعرض الحالة الفارغة والخطأ', async () => {
    apiMock.get.mockResolvedValue({ data: [] });
    const { rerender } = render(<BaseNumbersModal documentId={7} onClose={vi.fn()} />);
    expect(
      await screen.findByText('لا توجد أرقام أساس مسجلة لهذا الملف'),
    ).toBeInTheDocument();

    apiMock.get.mockRejectedValue({});
    rerender(<BaseNumbersModal documentId={8} onClose={vi.fn()} />);
    expect(await screen.findByText('حدث خطأ في التحميل')).toBeInTheDocument();
  });
});
