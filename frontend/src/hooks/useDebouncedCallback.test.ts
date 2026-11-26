import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useDebouncedCallback } from './useDebouncedCallback';

describe('useDebouncedCallback', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('يستدعي آخر قيمة فقط بعد انقضاء المهلة', () => {
    const fn = vi.fn();
    const { result } = renderHook(() => useDebouncedCallback(fn, 300));

    result.current('أ');
    result.current('ب');
    expect(fn).not.toHaveBeenCalled();

    vi.advanceTimersByTime(299);
    expect(fn).not.toHaveBeenCalled();

    vi.advanceTimersByTime(1);
    expect(fn).toHaveBeenCalledTimes(1);
    expect(fn).toHaveBeenCalledWith('ب');
  });

  it('يبقي هوية الدالة مستقرة ما دامت المهلة ثابتة', () => {
    const fn = vi.fn();
    const { result, rerender } = renderHook(() => useDebouncedCallback(fn, 300));
    const first = result.current;
    rerender();
    expect(result.current).toBe(first);
  });

  it('يستخدم أحدث دالة بعد إعادة الرسم دون إعادة جدولة', () => {
    const first = vi.fn();
    const second = vi.fn();
    const { result, rerender } = renderHook(({ fn }) => useDebouncedCallback(fn, 300), {
      initialProps: { fn: first },
    });

    result.current();
    rerender({ fn: second });
    vi.advanceTimersByTime(300);

    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledTimes(1);
  });

  it('يلغي المؤقّت عند إزالة المكوّن', () => {
    const fn = vi.fn();
    const { result, unmount } = renderHook(() => useDebouncedCallback(fn, 300));

    result.current();
    unmount();
    vi.advanceTimersByTime(300);

    expect(fn).not.toHaveBeenCalled();
  });
});
