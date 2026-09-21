import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import TransferAllAppealsModal from './TransferAllAppealsModal';

const { apiMock, errorMessageMock } = vi.hoisted(() => ({
  apiMock: { get: vi.fn(), post: vi.fn() },
  errorMessageMock: vi.fn(() => 'خطأ'),
}));

vi.mock('../../api/client', () => ({ api: apiMock, getApiErrorMessage: errorMessageMock }));

function mockLawyersAndCount(count = 2) {
  apiMock.get.mockImplementation((url: string) => {
    if (url === '/users/lawyers')
      return Promise.resolve({
        data: [
          { id: 3, fullName: 'محامي أول', isActive: true },
          { id: 4, fullName: 'محامي ثانٍ', isActive: true },
        ],
      });
    if (url === '/appeals/owner/3/count') return Promise.resolve({ data: { count } });
    return Promise.resolve({ data: {} });
  });
}

describe('TransferAllAppealsModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('معاينة النقل توضح أن المنظورة فقط تُنقل', async () => {
    mockLawyersAndCount(2);
    const user = userEvent.setup();
    render(<TransferAllAppealsModal onClose={vi.fn()} onTransferred={vi.fn()} />);

    await user.selectOptions(await screen.findByLabelText('من المحامي'), '3');
    await user.selectOptions(screen.getByLabelText('إلى المحامي'), '4');
    await user.click(screen.getByRole('button', { name: 'متابعة' }));

    expect(await screen.findByText(/سيتم نقل/)).toBeInTheDocument();
    expect(screen.getByText('تُنقل الاستئنافات المنظورة فقط.')).toBeInTheDocument();
    expect(apiMock.get).toHaveBeenCalledWith('/appeals/owner/3/count');
  });
});
