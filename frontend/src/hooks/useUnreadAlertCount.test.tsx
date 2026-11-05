import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { api } from '../api/client';
import { ALERTS_UNREAD_SCOPE, useUnreadAlertCount } from './useUnreadAlertCount';

vi.mock('../api/client', () => ({
  api: { get: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

const apiGet = api.get as unknown as ReturnType<typeof vi.fn>;

describe('useUnreadAlertCount', () => {
  beforeEach(() => {
    apiGet.mockReset();
  });

  it('يطلب عدّاد النطاق الشخصي `user` عند التفعيل', async () => {
    apiGet.mockResolvedValue({ data: { count: 4 } });

    const { result } = renderHook(() => useUnreadAlertCount(true));

    await waitFor(() => expect(result.current.data).toEqual({ count: 4 }));
    expect(apiGet).toHaveBeenCalledTimes(1);
    expect(apiGet).toHaveBeenCalledWith(
      '/alerts/unread-count',
      expect.objectContaining({ params: { scope: ALERTS_UNREAD_SCOPE } }),
    );
    expect(ALERTS_UNREAD_SCOPE).toBe('user');
  });

  it('لا يطلب شيئًا عند التعطيل', () => {
    const { result } = renderHook(() => useUnreadAlertCount(false));

    expect(apiGet).not.toHaveBeenCalled();
    expect(result.current.data).toBeNull();
    expect(result.current.error).toBeNull();
  });

  it('يعرض الخطأ ويُبقي آخر بيانات معروفة عند فشل التحديث', async () => {
    apiGet
      .mockResolvedValueOnce({ data: { count: 6 } })
      .mockRejectedValueOnce(new Error('تعذر الجلب'));

    const { result } = renderHook(() => useUnreadAlertCount(true));
    await waitFor(() => expect(result.current.data).toEqual({ count: 6 }));

    act(() => result.current.refetch());

    await waitFor(() => expect(result.current.error).toBe('تعذر الجلب'));
    expect(result.current.data).toEqual({ count: 6 });
  });

  it('يسمح بإنقاص العدّاد عبر `setData` بعد تعليم المقروء', async () => {
    apiGet.mockResolvedValue({ data: { count: 2 } });

    const { result } = renderHook(() => useUnreadAlertCount(true));
    await waitFor(() => expect(result.current.data).toEqual({ count: 2 }));

    act(() => {
      result.current.setData((prev) => (prev ? { count: Math.max(0, prev.count - 1) } : prev));
    });
    expect(result.current.data).toEqual({ count: 1 });

    act(() => {
      result.current.setData((prev) => (prev ? { count: Math.max(0, prev.count - 1) } : prev));
    });
    act(() => {
      result.current.setData((prev) => (prev ? { count: Math.max(0, prev.count - 1) } : prev));
    });
    expect(result.current.data).toEqual({ count: 0 });
  });
});
