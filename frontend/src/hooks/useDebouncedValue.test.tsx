import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, act } from '@testing-library/react';
import type { ReactNode } from 'react';
import { useDebouncedValue } from './useDebouncedValue';

let shown: string | undefined;
let timerCount = 0;

function Probe({ value, delay }: { value: string; delay?: number }): ReactNode {
  shown = useDebouncedValue(value, delay);
  return null;
}

beforeEach(() => {
  vi.useFakeTimers();
  shown = undefined;
});

afterEach(() => {
  vi.useRealTimers();
});

describe('useDebouncedValue', () => {
  it('يعيد القيمة الأولى فورًا دون انتظار', () => {
    render(<Probe value="أول" />);
    expect(shown).toBe('أول');
  });

  it('يؤجل تحديث القيمة حتى انقضاء المدة كاملة', () => {
    const { rerender } = render(<Probe value="أول" />);

    rerender(<Probe value="ثانٍ" />);
    expect(shown).toBe('أول');

    act(() => {
      vi.advanceTimersByTime(299);
    });
    expect(shown).toBe('أول');

    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(shown).toBe('ثانٍ');
  });

  it('التغييرات المتتابعة السريعة تنتج تحديثًا واحدًا بالقيمة الأخيرة', () => {
    const { rerender } = render(<Probe value="a" />);

    rerender(<Probe value="a1" />);
    act(() => {
      vi.advanceTimersByTime(100);
    });
    rerender(<Probe value="a2" />);
    act(() => {
      vi.advanceTimersByTime(100);
    });
    rerender(<Probe value="a3" />);
    act(() => {
      vi.advanceTimersByTime(299);
    });
    expect(shown).toBe('a');

    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(shown).toBe('a3');
  });

  it('ينظّف المؤقت عند إلغاء التركيب فلا يحدث أي تحديث بعده', () => {
    const { rerender, unmount } = render(<Probe value="x" />);
    rerender(<Probe value="xy" />);
    expect(vi.getTimerCount()).toBe(1);

    unmount();
    expect(vi.getTimerCount()).toBe(0);

    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(shown).toBe('x');
  });

  it('يحترم مدة مخصصة', () => {
    const { rerender } = render(<Probe value="1" delay={50} />);

    rerender(<Probe value="2" delay={50} />);
    act(() => {
      vi.advanceTimersByTime(49);
    });
    expect(shown).toBe('1');

    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(shown).toBe('2');
  });

  it('لا يبقي مؤقتات معلقة بعد استقرار القيمة', () => {
    const { rerender } = render(<Probe value="s" />);
    rerender(<Probe value="s2" />);
    act(() => {
      vi.advanceTimersByTime(300);
    });
    expect(timerCount).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
  });
});
