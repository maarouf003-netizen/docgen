import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, act, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { api } from '../api/client';
import { useBadgeCount } from './useBadgeCount';

vi.mock('../api/client', () => ({
  api: { get: vi.fn() },
  getApiErrorMessage: () => 'تعذر تنفيذ الطلب، حاول مجدداً',
}));

const apiGet = api.get as unknown as ReturnType<typeof vi.fn>;

let latest = 0;

function Probe({
  endpoint,
  options,
}: {
  endpoint: string;
  options: { enabled: boolean; intervalMs?: number; eventName?: string; shape: 'count' | 'list' };
}): ReactNode {
  latest = useBadgeCount(endpoint, options);
  return null;
}

describe('useBadgeCount', () => {
  beforeEach(() => {
    latest = 0;
    apiGet.mockReset();
  });

  it('يجلب `{ count }` عند التركيب ويبقى على آخر قيمة عند الفشل', async () => {
    // النداء الثاني فقط يفشل — البقية تنجح (بلا `undefined` الذي يُسقط الفاصل بخطأ غير مُلتقط).
    let calls = 0;
    apiGet.mockImplementation(() =>
      ++calls === 2 ? Promise.reject(new Error('boom')) : Promise.resolve({ data: { count: 4 } }),
    );
    const { unmount } = render(
      <Probe endpoint="/review-letters/pending-count" options={{ enabled: true, intervalMs: 30, shape: 'count' }} />,
    );
    await waitFor(() => expect(latest).toBe(4));
    // الاستطلاع الثاني يفشل — العدّاد يبقى على آخر قيمة معروفة.
    await waitFor(() => expect(apiGet.mock.calls.length).toBeGreaterThanOrEqual(2));
    expect(latest).toBe(4);
    unmount();
  });

  it('يحتسب طول القائمة ويعيد الجلب عند الحدث المخصص', async () => {
    apiGet.mockResolvedValue({ data: [{ id: 1 }, { id: 2 }, { id: 3 }] });
    const { unmount } = render(
      <Probe
        endpoint="/delegations/pending"
        options={{ enabled: true, intervalMs: 60_000, eventName: 'test:refresh', shape: 'list' }}
      />,
    );
    await waitFor(() => expect(latest).toBe(3));

    await act(async () => {
      window.dispatchEvent(new Event('test:refresh'));
    });
    await waitFor(() => expect(apiGet).toHaveBeenCalledTimes(2));
    unmount();
  });

  it('لا يجلب شيئًا عند التعطيل ويبقى صفرًا', () => {
    const { unmount } = render(
      <Probe endpoint="/review-letters/pending-count" options={{ enabled: false, shape: 'count' }} />,
    );
    expect(apiGet).not.toHaveBeenCalled();
    expect(latest).toBe(0);
    unmount();
  });

  it('يتجاهل الاستجابات غير الصالحة بلا كسر', async () => {
    apiGet.mockResolvedValueOnce({ data: { count: -1 } });
    const { unmount } = render(
      <Probe endpoint="/review-letters/pending-count" options={{ enabled: true, shape: 'count' }} />,
    );
    await act(async () => {
      await Promise.resolve();
    });
    expect(latest).toBe(0);
    unmount();
  });

  it('يزيل مستمع الحدث عند الفك فلا يجلب بعده', async () => {
    apiGet.mockResolvedValue({ data: { count: 1 } });
    const { unmount } = render(
      <Probe
        endpoint="/review-letters/pending-count"
        options={{ enabled: true, intervalMs: 60_000, eventName: 'test:gone', shape: 'count' }}
      />,
    );
    await waitFor(() => expect(latest).toBe(1));
    expect(apiGet).toHaveBeenCalledTimes(1);

    unmount();
    await act(async () => {
      window.dispatchEvent(new Event('test:gone'));
    });
    expect(apiGet).toHaveBeenCalledTimes(1);
  });

  it('يصفّر العدّاد عند تغيّر المصدر ولا يُبقي قيمة المصدر القديم', async () => {
    apiGet.mockImplementation((url: string) => {
      if (url === '/a') return Promise.resolve({ data: { count: 5 } });
      // المصدر الجديد معلّق — نتحقق أن القديم لا يبقى ظاهرًا أثناء الانتظار.
      return new Promise(() => {});
    });
    const { rerender, unmount } = render(
      <Probe endpoint="/a" options={{ enabled: true, intervalMs: 60_000, shape: 'count' }} />,
    );
    await waitFor(() => expect(latest).toBe(5));

    rerender(
      <Probe endpoint="/b" options={{ enabled: true, intervalMs: 60_000, shape: 'count' }} />,
    );
    await waitFor(() => expect(latest).toBe(0));
    unmount();
  });

  it('يوقف الفاصل الزمني عند الفك (بلا اعتماد على التوقيت)', async () => {
    // بلا `waitFor` عمدًا: مؤقتاتها الداخلية تلوث عدّ `clearInterval`.
    const clearSpy = vi.spyOn(globalThis, 'clearInterval');
    apiGet.mockResolvedValue({ data: { count: 1 } });
    const { unmount } = render(
      <Probe endpoint="/x" options={{ enabled: true, intervalMs: 60_000, shape: 'count' }} />,
    );
    await act(async () => {});
    expect(latest).toBe(1);

    const callsBefore = clearSpy.mock.calls.length;
    unmount();
    expect(clearSpy.mock.calls.length).toBe(callsBefore + 1);
    clearSpy.mockRestore();
  });
});
