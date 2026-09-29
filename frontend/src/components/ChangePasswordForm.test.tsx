import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ChangePasswordForm } from './ChangePasswordForm';

vi.mock('../api/client', () => ({
  api: { post: vi.fn() },
}));

import { api } from '../api/client';

beforeEach(() => {
  vi.clearAllMocks();
});

describe('ChangePasswordForm', () => {
  it('يعرض الحقول الثلاثة بملصقات قابلة للنقر وسمات نماذج صحيحة', () => {
    render(<ChangePasswordForm />);

    const current = screen.getByLabelText('كلمة المرور الحالية');
    expect(current).toHaveAttribute('type', 'password');
    expect(current).toHaveAttribute('autocomplete', 'current-password');
    expect(current).toHaveAttribute('name', 'currentPassword');

    const next = screen.getByLabelText('كلمة المرور الجديدة');
    expect(next).toHaveAttribute('autocomplete', 'new-password');
    expect(screen.getByLabelText('تأكيد كلمة المرور')).toHaveAttribute('autocomplete', 'new-password');
  });

  it('يرفض عدم التطابق ويركّز حقل التأكيد', async () => {
    const user = userEvent.setup();
    render(<ChangePasswordForm />);

    await user.type(screen.getByLabelText('كلمة المرور الحالية'), 'old1234');
    await user.type(screen.getByLabelText('كلمة المرور الجديدة'), 'new1234');
    await user.type(screen.getByLabelText('تأكيد كلمة المرور'), 'different');
    await user.click(screen.getByRole('button', { name: 'حفظ' }));

    expect(screen.getByRole('alert')).toHaveTextContent('غير متطابقتين');
    expect(screen.getByLabelText('تأكيد كلمة المرور')).toHaveFocus();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يرفض القصيرة ويركّز حقل الجديدة', async () => {
    const user = userEvent.setup();
    render(<ChangePasswordForm />);

    await user.type(screen.getByLabelText('كلمة المرور الحالية'), 'old1234');
    await user.type(screen.getByLabelText('كلمة المرور الجديدة'), '123');
    await user.type(screen.getByLabelText('تأكيد كلمة المرور'), '123');
    await user.click(screen.getByRole('button', { name: 'حفظ' }));

    expect(screen.getByRole('alert')).toHaveTextContent('6 أحرف');
    expect(screen.getByLabelText('كلمة المرور الجديدة')).toHaveFocus();
  });

  it('ينجح ويمسح الحقول', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});
    render(<ChangePasswordForm />);

    await user.type(screen.getByLabelText('كلمة المرور الحالية'), 'old1234');
    await user.type(screen.getByLabelText('كلمة المرور الجديدة'), 'new1234');
    await user.type(screen.getByLabelText('تأكيد كلمة المرور'), 'new1234');
    await user.click(screen.getByRole('button', { name: 'حفظ' }));

    expect(api.post).toHaveBeenCalledWith('/auth/change-password', {
      oldPassword: 'old1234',
      newPassword: 'new1234',
    });
    expect(screen.getByRole('status')).toHaveTextContent('تم تغيير كلمة المرور بنجاح');
    expect(screen.getByLabelText('كلمة المرور الحالية')).toHaveValue('');
  });

  it('يعرض خطأ الخلفية', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockRejectedValue({
      response: { data: { message: 'الحالية غير صحيحة' } },
    });
    render(<ChangePasswordForm />);

    await user.type(screen.getByLabelText('كلمة المرور الحالية'), 'wrong');
    await user.type(screen.getByLabelText('كلمة المرور الجديدة'), 'new1234');
    await user.type(screen.getByLabelText('تأكيد كلمة المرور'), 'new1234');
    await user.click(screen.getByRole('button', { name: 'حفظ' }));

    expect(screen.getByRole('alert')).toHaveTextContent('الحالية غير صحيحة');
  });
});
