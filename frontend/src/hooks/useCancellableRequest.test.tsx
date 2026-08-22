import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, act, waitFor } from '@testing-library/react';
import { CanceledError } from 'axios';
import type { ReactNode } from 'react';
import { useCancellableRequest, type CancellableRequestResult } from './useCancellableRequest';

vi.mock('../api/client', () => ({
  api: {},
  getApiErrorMessage: (error: unknown) => {
    const message = (error as { message?: string } | null)?.message;
    return typeof message === 'string' && message ? message : 'تعذر تنفيذ الطلب، حاول مجدداً';
  },
}));

let latest: CancellableRequestResult<number> | undefined;

interface ProbeProps {
  fetcher: (signal: AbortSignal) => Promise<number>;
  deps: readonly unknown[];
  enabled?: boolean;
}

function Probe({ fetcher, deps, enabled }: ProbeProps): ReactNode {
  latest = useCancellableRequest<number>(fetcher, deps, enabled === undefined ? {} : { enabled });
  return null;
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (cause: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

describe('useCancellableRequest', () => {
  beforeEach(() => {
    latest = undefined;
  });

  it('يجلب البيانات ويرفع حالة التحميل عند النجاح', async () => {
    const fetcher = vi.fn(() => Promise.resolve(42));

    render(<Probe fetcher={fetcher} deps={[]} />);

    await waitFor(() => expect(latest?.data).toBe(42));
    expect(latest?.isLoading).toBe(false);
    expect(latest?.error).toBeNull();
    expect(fetcher).toHaveBeenCalledTimes(1);
  });

  it('يمرر إشارة إلغاء صالحة إلى الدالة الجالبة', async () => {
    let received: AbortSignal | undefined;
    render(
      <Probe
        fetcher={(signal) => {
          received = signal;
          return Promise.resolve(1);
        }}
        deps={[]}
      />,
    );

    await waitFor(() => expect(latest?.data).toBe(1));
    expect(received).toBeInstanceOf(AbortSignal);
    expect(received?.aborted).toBe(false);
  });

  it('يعرض رسالة الخطأ الموحدة عند فشل الجلب', async () => {
    const fetcher = () => Promise.reject(new Error('فشل الشبكة'));

    render(<Probe fetcher={fetcher} deps={[]} />);

    await waitFor(() => expect(latest?.error).toBe('فشل الشبكة'));
    expect(latest?.data).toBeNull();
    expect(latest?.isLoading).toBe(false);
  });

  it('يتجاهل أخطاء الإلغاء ولا يعاملها كخطأ للمستخدم', async () => {
    const cancelError = new CanceledError('canceled');
    const fetcher = () => Promise.reject(cancelError);

    render(<Probe fetcher={fetcher} deps={[]} />);

    await waitFor(() => expect(latest?.isLoading).toBe(false));
    expect(latest?.error).toBeNull();
  });

  it('يغيّر التبعيات فيجهل الاستجابة المتأخرة للطلب السابق ويعتمد الأحدث', async () => {
    const calls: Array<ReturnType<typeof deferred<number>>> = [];
    const fetcher = vi.fn(() => {
      const d = deferred<number>();
      calls.push(d);
      return d.promise;
    });

    const { rerender } = render(<Probe fetcher={fetcher} deps={['a']} />);
    expect(calls).toHaveLength(1);

    rerender(<Probe fetcher={fetcher} deps={['b']} />);
    expect(calls).toHaveLength(2);

    await act(async () => {
      calls[0].resolve(111);
      calls[1].resolve(222);
    });

    expect(latest?.data).toBe(222);
    expect(calls[0].promise).toMatchObject({});
  });

  it('يُلغي الطلب السابق فعليًا عند تغيّر التبعيات', async () => {
    const signals: AbortSignal[] = [];
    const fetcher = vi.fn((signal: AbortSignal) => {
      signals.push(signal);
      return new Promise<number>(() => {});
    });

    const { rerender } = render(<Probe fetcher={fetcher} deps={[1]} />);
    rerender(<Probe fetcher={fetcher} deps={[2]} />);

    expect(signals).toHaveLength(2);
    expect(signals[0].aborted).toBe(true);
    expect(signals[1].aborted).toBe(false);
  });

  it('يُلغي الطلب الجاري عند إلغاء التركيب', async () => {
    const signals: AbortSignal[] = [];
    const fetcher = vi.fn((signal: AbortSignal) => {
      signals.push(signal);
      return new Promise<number>(() => {});
    });

    const { unmount } = render(<Probe fetcher={fetcher} deps={[]} />);
    unmount();

    expect(signals).toHaveLength(1);
    expect(signals[0].aborted).toBe(true);
  });

  it('refetch يعيد الجلب بنفس التبعيات', async () => {
    const fetcher = vi.fn(() => Promise.resolve(7));

    render(<Probe fetcher={fetcher} deps={['static']} />);

    await waitFor(() => expect(latest?.data).toBe(7));

    await act(async () => {
      latest?.refetch();
    });

    expect(fetcher).toHaveBeenCalledTimes(2);
    await waitFor(() => expect(latest?.data).toBe(7));
  });

  it('enabled=false يمنع الجلب ولا يشغّل حالة التحميل', () => {
    const fetcher = vi.fn(() => Promise.resolve(5));

    render(<Probe fetcher={fetcher} deps={[]} enabled={false} />);

    expect(fetcher).not.toHaveBeenCalled();
    expect(latest?.isLoading).toBe(false);
    expect(latest?.data).toBeNull();
    expect(latest?.error).toBeNull();
  });

  it('تفعيل enabled من false إلى true يبدأ الجلب', async () => {
    const fetcher = vi.fn(() => Promise.resolve(9));

    const { rerender } = render(<Probe fetcher={fetcher} deps={[]} enabled={false} />);
    rerender(<Probe fetcher={fetcher} deps={[]} enabled={true} />);

    await waitFor(() => expect(latest?.data).toBe(9));
    expect(fetcher).toHaveBeenCalledTimes(1);
  });

  it('يستخدم أحدث نسخة من دالة الجلب عند إعادة الجلب دون تبعيات', async () => {
    const firstFetcher = vi.fn(() => Promise.resolve(1));
    const secondFetcher = vi.fn(() => Promise.resolve(2));

    const { rerender } = render(<Probe fetcher={firstFetcher} deps={['k']} />);
    await waitFor(() => expect(latest?.data).toBe(1));

    rerender(<Probe fetcher={secondFetcher} deps={['k']} />);
    await act(async () => {
      latest?.refetch();
    });

    await waitFor(() => expect(latest?.data).toBe(2));
    expect(secondFetcher).toHaveBeenCalledTimes(1);
  });

  it('يدعم setData بشكل دالّي مبنيًا على القيمة السابقة', async () => {
    render(<Probe fetcher={() => Promise.resolve(10)} deps={[]} />);
    await waitFor(() => expect(latest?.data).toBe(10));

    await act(async () => {
      latest?.setData((prev) => (prev ?? 0) + 5);
    });

    expect(latest?.data).toBe(15);
  });

  it('يدعم setData بقيمة مباشرة', async () => {
    render(<Probe fetcher={() => Promise.resolve(10)} deps={[]} />);
    await waitFor(() => expect(latest?.data).toBe(10));

    await act(async () => {
      latest?.setData(null);
    });

    expect(latest?.data).toBeNull();
  });
});
