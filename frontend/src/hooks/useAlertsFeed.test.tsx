import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { useAlertsFeed } from './useAlertsFeed';
import { makeHeadAlertSummary } from '../test/factories';

vi.mock('../api/client', () => ({
  api: { get: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

const get = api.get as unknown as ReturnType<typeof vi.fn>;

function row(id: number) {
  return makeHeadAlertSummary({ id, messagePreview: `تنبيه ${id}` });
}

/** جسم `PagedResult` كما يصل على السلك (`perPage=100`). */
function pageBody(items: ReturnType<typeof row>[], totalCount: number, page = 1) {
  return {
    data: {
      items,
      page,
      perPage: 100,
      totalCount,
      totalPages: Math.ceil(totalCount / 100),
    },
  };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('useAlertsFeed', () => {
  it('يجلب الصفحة الأولى بـ pageSize=100 ويحسب hasMore من الإجمالي', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 137));

    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));

    await waitFor(() => expect(result.current.items).toHaveLength(2));
    expect(result.current.totalCount).toBe(137);
    expect(result.current.hasMore).toBe(true);
    expect(result.current.error).toBeNull();
    expect(get).toHaveBeenCalledWith(
      '/alerts',
      expect.objectContaining({ params: { page: 1, pageSize: 100 } }),
    );
  });

  it('loadMore يجلب الصفحة التالية ويُلحقها بالترتيب بلا تكرار', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 3));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    get.mockResolvedValueOnce(pageBody([row(3)], 3, 2));
    act(() => result.current.loadMore());

    await waitFor(() => expect(result.current.items).toHaveLength(3));
    expect(result.current.items.map((alert) => alert.id)).toEqual([1, 2, 3]);
    expect(result.current.hasMore).toBe(false);
    expect(get).toHaveBeenLastCalledWith(
      '/alerts',
      expect.objectContaining({ params: { page: 2, pageSize: 100 } }),
    );
  });

  it('لا يطلب صفحة إضافية حين لا يوجد مزيد', async () => {
    get.mockResolvedValueOnce(pageBody([row(1)], 1));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(1));

    act(() => result.current.loadMore());

    expect(get).toHaveBeenCalledTimes(1);
  });

  it('فشل صفحة إضافية يُبقي المعروض ويعلن الخطأ ويعيد المحاولة لنفس الصفحة', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 3));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    get.mockRejectedValueOnce(new Error('تعذّر الجلب'));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.error).toBe('تعذّر الجلب'));
    // المعروض لا يسقط عند فشل الصفحة التالية.
    expect(result.current.items).toHaveLength(2);

    get.mockResolvedValueOnce(pageBody([row(3)], 3, 2));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items).toHaveLength(3));
    expect(result.current.error).toBeNull();
    // إعادة المحاولة كانت للصفحة 2 نفسها، لا للصفحة 3.
    expect(get).toHaveBeenLastCalledWith(
      '/alerts',
      expect.objectContaining({ params: { page: 2, pageSize: 100 } }),
    );
  });

  it('reload يعيد من الصفحة الأولى ويستبدل ولا يُضيف', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 3));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    get.mockResolvedValueOnce(pageBody([row(3)], 3, 2));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items).toHaveLength(3));

    get.mockResolvedValueOnce(pageBody([row(9)], 1));
    act(() => result.current.reload());
    await waitFor(() => expect(result.current.items.map((alert) => alert.id)).toEqual([9]));
    expect(result.current.totalCount).toBe(1);
    expect(get).toHaveBeenLastCalledWith(
      '/alerts',
      expect.objectContaining({ params: { page: 1, pageSize: 100 } }),
    );
  });

  it('updateItems يعدّل المحمّل محليًا بلا طلب جديد', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 2));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    act(() =>
      result.current.updateItems((prev) => prev.map((alert) => ({ ...alert, isRead: true }))),
    );

    expect(result.current.items.every((alert) => alert.isRead)).toBe(true);
    expect(get).toHaveBeenCalledTimes(1);
  });

  it('لا يطلب شيئًا وهو معطّل (نطاق غير متاح)', () => {
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: false }));

    expect(get).not.toHaveBeenCalled();
    expect(result.current.items).toEqual([]);
    expect(result.current.totalCount).toBe(0);
  });
});
