import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CreateAlertForm } from './CreateAlertForm';

function renderForm(overrides: Partial<Parameters<typeof CreateAlertForm>[0]> = {}) {
  const onSubmit = vi.fn((event: { preventDefault: () => void }) => event.preventDefault());
  const props = {
    targetType: 'branch' as const,
    onTargetTypeChange: vi.fn(),
    lawyers: [
      { id: 7, username: 'samer', fullName: 'سامر الحلبي', isActive: true, branchId: 1 },
    ] as Parameters<typeof CreateAlertForm>[0]['lawyers'],
    lawyerId: '',
    onLawyerIdChange: vi.fn(),
    message: '',
    onMessageChange: vi.fn(),
    submitting: false,
    error: { field: null, text: '' },
    onSubmit,
    onCancel: vi.fn(),
    ...overrides,
  };
  render(<CreateAlertForm {...props} />);
  return { props, onSubmit };
}

describe('CreateAlertForm — عقد إمكانية الوصول (`P6-03a`)', () => {
  it('نص التنبيه له تسمية واسم حقل ودلالة إلزام', () => {
    renderForm();

    const message = screen.getByLabelText('نص التنبيه');
    expect(message.tagName).toBe('TEXTAREA');
    expect(message).toHaveAttribute('name', 'message');
    expect(message).toHaveAttribute('aria-required', 'true');
  });

  it('حقل المحامي يظهر بنوع محدد فقط وله تسمية واسم حقل', () => {
    renderForm({ targetType: 'lawyer' });

    const lawyer = screen.getByLabelText('المحامي');
    expect(lawyer.tagName).toBe('SELECT');
    expect(lawyer).toHaveAttribute('name', 'targetLawyerId');
    expect(lawyer).toHaveAttribute('aria-required', 'true');
    expect(screen.getByRole('option', { name: 'سامر الحلبي' })).toBeInTheDocument();
  });

  it('رسالة الخطأ مرتبطة بالحقل عبر role=alert وaria-describedby', () => {
    renderForm({ error: { field: 'message', text: 'نص التنبيه مطلوب' } });

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('نص التنبيه مطلوب');
    expect(alert).toHaveAttribute('id', 'alert-form-error');

    const message = screen.getByLabelText('نص التنبيه');
    expect(message).toHaveAttribute('aria-invalid', 'true');
    expect(message).toHaveAttribute('aria-describedby', 'alert-form-error');
  });

  it('أزرار نوع التنبيه تعبّر عن الحالة بـ aria-pressed ونوع button', async () => {
    const user = userEvent.setup();
    const { props } = renderForm();

    const branch = screen.getByRole('button', { name: 'تعميم للفرع' });
    expect(branch).toHaveAttribute('type', 'button');
    expect(branch).toHaveAttribute('aria-pressed', 'true');

    await user.click(screen.getByRole('button', { name: 'رسالة لمحامٍ' }));
    expect(props.onTargetTypeChange).toHaveBeenCalledWith('lawyer');
  });

  it('زر الإرسال والغاء نوعهما صريح ولا يوجد زر إرسال ضمني', () => {
    renderForm();

    expect(screen.getByRole('button', { name: 'إرسال التنبيه' })).toHaveAttribute('type', 'submit');
    expect(screen.getByRole('button', { name: 'إلغاء' })).toHaveAttribute('type', 'button');
  });
});
