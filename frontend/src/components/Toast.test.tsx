import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Toast } from './Toast';

describe('Toast', () => {
  it('يعرض رسالة خطأ بدور alert مع زر إغلاق', () => {
    render(<Toast type="error" message="رسالة خطأ" onClose={() => {}} />);

    expect(screen.getByRole('alert')).toHaveTextContent('رسالة خطأ');
    expect(screen.getByRole('button', { name: 'إغلاق الرسالة' })).toBeInTheDocument();
  });

  it('يعرض رسالة نجاح بنصها', () => {
    render(<Toast type="success" message="تم بنجاح" onClose={() => {}} />);

    expect(screen.getByRole('alert')).toHaveTextContent('تم بنجاح');
  });

  it('يستدعي onClose عند النقر على زر الإغلاق', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<Toast type="error" message="رسالة" onClose={onClose} />);

    await user.click(screen.getByRole('button', { name: 'إغلاق الرسالة' }));

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('يستدعي onClose تلقائياً بعد المدة المحددة', () => {
    vi.useFakeTimers();
    try {
      const onClose = vi.fn();
      render(<Toast type="success" message="رسالة" onClose={onClose} />);

      vi.advanceTimersByTime(5000);

      expect(onClose).toHaveBeenCalledTimes(1);
    } finally {
      vi.useRealTimers();
    }
  });
});
