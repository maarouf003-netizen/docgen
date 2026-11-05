import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import FloatingFormAlert from './FloatingFormAlert';

describe('FloatingFormAlert', () => {
  it('يعرض رسالة الخطأ كتنبيه عائم مستقل عن تخطيط الصفحة', () => {
    render(
      <FloatingFormAlert
        message="دائرة التنفيذ مطلوبة — اختر من السجل"
        onDismiss={() => {}}
      />,
    );

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('دائرة التنفيذ مطلوبة — اختر من السجل');
    // التنبيه داخل حاوية عائمة ثابتة: يظهر أينما كان موضع المستخدم في الصفحة الطويلة.
    expect(alert.closest('.fixed')).not.toBeNull();
  });

  it('لا يختفي تلقائيًا ويُغلق بزر الإغلاق المسمّى', async () => {
    const user = userEvent.setup();
    const onDismiss = vi.fn();
    render(<FloatingFormAlert message="خطأ" onDismiss={onDismiss} />);

    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(onDismiss).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'إغلاق رسالة الخطأ' }));
    expect(onDismiss).toHaveBeenCalledTimes(1);
  });
});
