import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import BaseNumbersModal from './BaseNumbersModal';

const { apiMock, errorMessageMock } = vi.hoisted(() => ({
  apiMock: { get: vi.fn() },
  errorMessageMock: vi.fn(() => 'فشل جلب الأرقام'),
}));

vi.mock('../api/client', () => ({ api: apiMock, getApiErrorMessage: errorMessageMock }));

beforeEach(() => {
  vi.clearAllMocks();
});

function entries() {
  return [
    { year: 2026, baseNumber: '900' },
    { year: 2025, baseNumber: '55' },
  ];
}

describe('BaseNumbersModal', () => {
  it('يجلب من المسار الداخلي افتراضيًا ويحفظ نوع الملف المعروض', async () => {
    apiMock.get.mockResolvedValue({ data: entries() });
    render(
      <BaseNumbersModal
        documentId={7}
        documentTitle="ملف أحمد"
        fileType="حقوق"
        onClose={vi.fn()}
      />,
    );

    expect(await screen.findByText('900')).toBeInTheDocument();
    expect(screen.getByText('55')).toBeInTheDocument();
    expect(screen.getAllByText('حقوق').length).toBeGreaterThan(0);
    expect(apiMock.get).toHaveBeenCalledWith('/documents/7/base-numbers');
  });

  it('يجلب عبر fetchUrl حين تُمرَّر (نقطة بوابة المندوب)', async () => {
    apiMock.get.mockResolvedValue({ data: entries() });
    render(
      <BaseNumbersModal documentId={7} fetchUrl="/portal/files/1/base-numbers" onClose={vi.fn()} />,
    );

    expect(await screen.findByText('900')).toBeInTheDocument();
    expect(apiMock.get).toHaveBeenCalledWith('/portal/files/1/base-numbers');
  });

  it('يعرض رسالة الفراغ والخطأ', async () => {
    apiMock.get.mockResolvedValue({ data: [] });
    const { rerender } = render(
      <BaseNumbersModal documentId={7} onClose={vi.fn()} />,
    );
    expect(await screen.findByText('لا توجد أرقام أساس مسجلة لهذا الملف')).toBeInTheDocument();

    apiMock.get.mockRejectedValue({});
    rerender(<BaseNumbersModal documentId={8} onClose={vi.fn()} />);
    expect(await screen.findByText('فشل جلب الأرقام')).toBeInTheDocument();
  });
});