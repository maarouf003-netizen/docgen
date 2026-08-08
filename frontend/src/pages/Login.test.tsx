import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import Login from './Login';
import type { LoginResponse } from '../types';

const loginMock = vi.hoisted(() => vi.fn());
const navigateMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => ({
    user: null,
    loading: false,
    login: loginMock,
    logout: vi.fn(),
    hasFullAccess: false,
    isHead: false,
  }),
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => navigateMock,
}));

const successResponse: LoginResponse = {
  token: 'token-1',
  user: {
    id: 1,
    username: 'محمد أحمد علي',
    fullName: 'محمد أحمد علي',
    role: 'lawyer',
    branchId: 1,
    branchName: 'الفرع الرئيسي - دمشق',
  },
};

beforeEach(() => {
  vi.clearAllMocks();
  loginMock.mockReset();
  navigateMock.mockReset();
});

describe('Login', () => {
  it('ينجح الدخول العادي ويوجّه للرئيسية', async () => {
    loginMock.mockResolvedValue(successResponse);
    const user = userEvent.setup();
    render(<Login />);

    await user.type(screen.getByLabelText('اسم المستخدم'), 'lawyer1');
    await user.type(screen.getByLabelText('كلمة المرور'), '123456');
    await user.click(screen.getByRole('button', { name: 'دخول' }));

    await waitFor(() => expect(loginMock).toHaveBeenCalledWith('lawyer1', '123456', undefined));
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/'));
  });

  it('يعرض اختيار الفرع عند تكرار الاسم ثم يدخل بالفرع المختار', async () => {
    loginMock.mockResolvedValueOnce({
      requiresBranchSelection: true,
      branches: [
        { branchId: 1, branchName: 'الفرع الرئيسي - دمشق' },
        { branchId: 2, branchName: 'فرع حلب' },
      ],
    });
    loginMock.mockResolvedValueOnce(successResponse);
    const user = userEvent.setup();
    render(<Login />);

    await user.type(screen.getByLabelText('اسم المستخدم'), 'محمد أحمد علي');
    await user.type(screen.getByLabelText('كلمة المرور'), '123456');
    await user.click(screen.getByRole('button', { name: 'دخول' }));

    expect(await screen.findByText(/يوجد أكثر من حساب/)).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('الفرع'), '2');
    await user.click(screen.getByRole('button', { name: 'متابعة الدخول' }));

    await waitFor(() => expect(loginMock).toHaveBeenLastCalledWith('محمد أحمد علي', '123456', 2));
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/'));
  });

  it('يطلب اختيار الفرع قبل المتابعة إذا لم يُختار', async () => {
    loginMock.mockResolvedValueOnce({
      requiresBranchSelection: true,
      branches: [
        { branchId: 1, branchName: 'الفرع الرئيسي - دمشق' },
        { branchId: 2, branchName: 'فرع حلب' },
      ],
    });
    const user = userEvent.setup();
    render(<Login />);

    await user.type(screen.getByLabelText('اسم المستخدم'), 'محمد أحمد علي');
    await user.type(screen.getByLabelText('كلمة المرور'), '123456');
    await user.click(screen.getByRole('button', { name: 'دخول' }));

    expect(await screen.findByText(/يوجد أكثر من حساب/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'متابعة الدخول' }));

    expect(await screen.findByText('يرجى اختيار الفرع')).toBeInTheDocument();
    expect(loginMock).toHaveBeenCalledTimes(1);
  });

  it('يعرض رسالة الخطأ عند كلمة مرور خاطئة ولا يوجّه', async () => {
    loginMock.mockRejectedValue({
      isAxiosError: true,
      response: { status: 401, data: { message: 'اسم المستخدم أو كلمة المرور غير صحيحة' } },
    });
    const user = userEvent.setup();
    render(<Login />);

    await user.type(screen.getByLabelText('اسم المستخدم'), 'lawyer1');
    await user.type(screen.getByLabelText('كلمة المرور'), 'bad');
    await user.click(screen.getByRole('button', { name: 'دخول' }));

    expect(await screen.findByText('اسم المستخدم أو كلمة المرور غير صحيحة')).toBeInTheDocument();
    expect(navigateMock).not.toHaveBeenCalled();
  });
});
