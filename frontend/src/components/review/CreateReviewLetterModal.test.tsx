import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import CreateReviewLetterModal from './CreateReviewLetterModal';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn() },
  getApiErrorMessage: () => 'حدث خطأ غير متوقع',
}));

import { api } from '../../api/client';

const noop = () => {};

describe('CreateReviewLetterModal — مستلم الكتاب العام (§10.2)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthMock.mockReturnValue({ user: { id: 3, role: 'lawyer', branchId: 1 } });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [
        { id: 3, branchId: 1, name: 'مصياف', isActive: true, circuitCount: 1, headName: 'رئيس الشعبة' },
        { id: 4, branchId: 1, name: 'بلا رئيس', isActive: true, circuitCount: 0, headName: null },
      ],
    });
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 1 } });
  });

  it('الكتاب العام يعرض منسدل المستلم بافتراضي القسم (الشعب ذات الرئيس فقط)', async () => {
    render(<CreateReviewLetterModal onClose={noop} />);

    const select = await screen.findByLabelText(/المستلم/) as HTMLSelectElement;
    expect(select.value).toBe('');
    expect(select.options[0].text).toBe('رئيس القسم');
    const values = Array.from(select.options).map((o) => o.text);
    expect(values).toContain('شعبة مصياف');
    expect(values).not.toContain('شعبة بلا رئيس');
  });

  it('الافتراضي يُرسَل null (رئيس القسم) والشعبة تُرسَل بمعرفها', async () => {
    const user = userEvent.setup();
    render(<CreateReviewLetterModal onClose={noop} />);

    await user.click(await screen.findByRole('button', { name: 'حفظ وإرسال' }));
    expect(api.post).toHaveBeenCalledWith('/review-letters', expect.objectContaining({
      documentId: null,
      recipientSectionId: null,
    }));

    await user.selectOptions(screen.getByLabelText(/المستلم/), '3');
    await user.click(screen.getByRole('button', { name: 'حفظ وإرسال' }));
    expect(api.post).toHaveBeenCalledWith('/review-letters', expect.objectContaining({
      recipientSectionId: 3,
    }));
  });

  it('المرتبط بملف بلا منسدل (تلقائي لمالك الدائرة)', async () => {
    render(<CreateReviewLetterModal documentId={10} documentTitle="ملف 10" onClose={noop} />);

    expect(screen.queryByLabelText(/المستلم/)).not.toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalled();
  });
});
