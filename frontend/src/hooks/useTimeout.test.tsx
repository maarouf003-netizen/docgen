import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, act } from '@testing-library/react';
import type { ReactNode } from 'react';
import { useTimeout } from './useTimeout';

let fired = 0;

function Probe({ callback, delay }: { callback: () => void; delay: number | null }): ReactNode {
  useTimeout(callback, delay);
  return null;
}

beforeEach(() => {
  vi.useFakeTimers();
  fired = 0;
});

afterEach(() => {
  vi.useRealTimers();
});

describe('useTimeout', () => {
  it('ينفذ الدالة مرة واحدة بعد انقضاء المدة', () => {
    render(<Probe callback={() => { fired += 1; }} delay={700} />);

    act(() => {
      vi.advanceTimersByTime(699);
    });
    expect(fired).toBe(0);

    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(fired).toBe(1);

    act(() => {
      vi.advanceTimersByTime(5000);
    });
    expect(fired).toBe(1);
  });

  it('لا يفعل شيئًا عندما تكون المدة null', () => {
    render(<Probe callback={() => { fired += 1; }} delay={null} />);

    act(() => {
      vi.advanceTimersByTime(10000);
    });
    expect(fired).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
  });

  it('يلغي المؤقت عند إلغاء التركيب فلا تنفذ الدالة', () => {
    const { unmount } = render(<Probe callback={() => { fired += 1; }} delay={700} />);

    unmount();
    act(() => {
      vi.advanceTimersByTime(10000);
    });
    expect(fired).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
  });

  it('العودة إلى null تلغي جدولة قائمة', () => {
    const { rerender } = render(<Probe callback={() => { fired += 1; }} delay={null} />);

    rerender(<Probe callback={() => { fired += 1; }} delay={700} />);
    expect(vi.getTimerCount()).toBe(1);

    rerender(<Probe callback={() => { fired += 1; }} delay={null} />);
    expect(vi.getTimerCount()).toBe(0);

    act(() => {
      vi.advanceTimersByTime(10000);
    });
    expect(fired).toBe(0);
  });

  it('يستخدم أحدث نسخة من الدالة عند التنفيذ', () => {
    const first = vi.fn();
    const second = vi.fn();

    const { rerender } = render(<Probe callback={first} delay={700} />);
    rerender(<Probe callback={second} delay={700} />);

    act(() => {
      vi.advanceTimersByTime(700);
    });
    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledTimes(1);
  });
});
