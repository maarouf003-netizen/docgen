import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ComingSoonToast, COMING_SOON_TIMEOUT_MS } from './ComingSoonToast';

afterEach(() => {
  vi.useRealTimers();
});

describe('ComingSoonToast', () => {
  it('يعرض اسم الميزة مع رسالة قيد البناء', () => {
    render(<ComingSoonToast feature="المنتدى" onClose={() => {}} />);

    expect(screen.getByRole('status')).toHaveTextContent('المنتدى');
    expect(screen.getByRole('status')).toHaveTextContent('الميزة قيد البناء حاليا');
  });

  it('يُغلق بزر الإغلاق', () => {
    const onClose = vi.fn();
    render(<ComingSoonToast feature="المكتبة" onClose={onClose} />);

    fireEvent.click(screen.getByRole('button', { name: 'إغلاق التنبيه' }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('يُغلق بمفتاح Escape', () => {
    const onClose = vi.fn();
    render(<ComingSoonToast feature="المكتبة" onClose={onClose} />);

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('يختفي تلقائيًا بعد المهلة', () => {
    vi.useFakeTimers();
    const onClose = vi.fn();
    render(<ComingSoonToast feature="المنتدى" onClose={onClose} />);

    expect(onClose).not.toHaveBeenCalled();
    vi.advanceTimersByTime(COMING_SOON_TIMEOUT_MS);
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
