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

  it('reload لا يُفرغ القائمة المعروضة قبل وصول الصفحة الأولى الجديدة', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 3));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    let resolveReload: ((value: unknown) => void) | undefined;
    get.mockReturnValueOnce(
      new Promise((resolve) => {
        resolveReload = resolve;
      }),
    );

    act(() => result.current.reload());
    // أثناء الجلب: القائمة القديمة باقية — لا وميض «لا توجد تنبيهات».
    expect(result.current.items).toHaveLength(2);
    expect(result.current.isLoading).toBe(true);

    await act(async () => {
      resolveReload?.(pageBody([row(9)], 1));
    });
    await waitFor(() => expect(result.current.items.map((alert) => alert.id)).toEqual([9]));
  });

  it('فشل إعادة تحميل الصفحة الأولى: «إعادة المحاولة» تعيد جلبها لا تتجاهلها', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 2));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));
    // لا «مزيد» أصلًا (2 من 2): الزر في التذييل إعادة محاولة لا تحميل صفحة تالية.
    expect(result.current.hasMore).toBe(false);

    get.mockRejectedValueOnce(new Error('تعذّر الجلب'));
    act(() => result.current.reload());
    await waitFor(() => expect(result.current.error).toBe('تعذّر الجلب'));
    expect(result.current.items).toHaveLength(2);

    get.mockResolvedValueOnce(pageBody([row(9)], 1));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items.map((alert) => alert.id)).toEqual([9]));
    expect(get).toHaveBeenLastCalledWith(
      '/alerts',
      expect.objectContaining({ params: { page: 1, pageSize: 100 } }),
    );
  });

  it('يُزيل التكرار عند إزاحة الترقيم بين صفحتين (تنبيه جديد في الأعلى)', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 4));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    // إزاحة: الصف 2 ظهر في الصفحة الأولى ثم تكرر في الثانية.
    get.mockResolvedValueOnce(pageBody([row(2), row(3)], 4, 2));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items).toHaveLength(3));

    expect(result.current.items.map((alert) => alert.id)).toEqual([1, 2, 3]);
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

  it('جسم مخالف لعقد `PagedResult` يُعلن خطأً صريحًا لا قائمة فارغة كاذبة', async () => {
    // الشكل القديم (مصفوفة مجرّدة): سابقًا كان يُترجم صفحة فارغة، فتُعرض
    // «لا توجد تنبيهات» ويُخفى أن العطل في العقد لا في البيانات.
    get.mockResolvedValueOnce({ data: [row(1), row(2)] });

    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));

    await waitFor(() => expect(result.current.error).not.toBeNull());
    expect(result.current.error).toBe(
      'تعذّر قراءة قائمة التنبيهات — أعِد تحميل الصفحة، وإن استمر الخطأ فأبلغ الدعم',
    );
    expect(result.current.items).toEqual([]);
    expect(result.current.hasMore).toBe(false);
    expect(result.current.stalled).toBe(false);
    expect(result.current.isLoading).toBe(false);
  });

  it('لا يطلب شيئًا وهو معطّل (نطاق غير متاح)', () => {
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: false }));

    expect(get).not.toHaveBeenCalled();
    expect(result.current.items).toEqual([]);
    expect(result.current.totalCount).toBe(0);
  });

  it('تمكين النطاق بعد تعطيله يستأنف الجلب مرة واحدة من الصفحة الأولى', async () => {
    get.mockResolvedValueOnce(pageBody([row(1)], 1));

    const { result, rerender } = renderHook(
      ({ enabled }: { enabled: boolean }) => useAlertsFeed('/alerts', { enabled }),
      { initialProps: { enabled: false } },
    );
    expect(get).not.toHaveBeenCalled();

    rerender({ enabled: true });

    await waitFor(() => expect(result.current.items.map((alert) => alert.id)).toEqual([1]));
    expect(get).toHaveBeenCalledTimes(1);
    expect(get).toHaveBeenCalledWith(
      '/alerts',
      expect.objectContaining({ params: { page: 1, pageSize: 100 } }),
    );
  });

  it('تعطيل النطاق أثناء الجلب يُبطل الطلب بلا خطأ ولا بيانات', async () => {
    let capturedSignal: AbortSignal | undefined;
    get.mockImplementationOnce((_url: string, config: { signal?: AbortSignal }) => {
      capturedSignal = config.signal;
      // وعد لا يُحلّ أبدًا: الإلغاء هو ما يُنهي الطلب لا الاستجابة.
      return new Promise(() => {});
    });

    const { result, rerender } = renderHook(
      ({ enabled }: { enabled: boolean }) => useAlertsFeed('/alerts', { enabled }),
      { initialProps: { enabled: true } },
    );
    await waitFor(() => expect(capturedSignal).toBeDefined());

    rerender({ enabled: false });

    await waitFor(() => expect(capturedSignal?.aborted).toBe(true));
    expect(result.current.error).toBeNull();
    expect(result.current.items).toEqual([]);
    expect(result.current.isLoading).toBe(false);
  });

  it('إزاحة بين صفحتين: صفحة ملحقة كلها مكررات تُعلن الانقطاع وتوقف الطلبات', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2), row(3)], 4));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(3));
    expect(result.current.hasMore).toBe(true);
    expect(result.current.stalled).toBe(false);

    // تنبيه جديد أُدرج في الأعلى ⇒ الإجمالي 5 والنافذة أُزيحت، فالصفحة الثانية
    // تعيد الصف الأخير من الأولى («3») وحده: تقدم صفري.
    get.mockResolvedValueOnce(pageBody([row(3)], 5, 2));
    act(() => result.current.loadMore());

    await waitFor(() => expect(result.current.stalled).toBe(true));
    expect(result.current.hasMore).toBe(false);
    expect(result.current.items.map((alert) => alert.id)).toEqual([1, 2, 3]);

    // لا صفحة ثالثة: زر «تحميل المزيد» صار بلا معنى، والمخرج `reload`.
    act(() => result.current.loadMore());
    expect(get).toHaveBeenCalledTimes(2);
  });

  it('صفحة ملحقة فارغة مع إجمالي أكبر تُعلن الانقطاع لا صفحة «لا مزيد» كاذبة', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 3));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    get.mockResolvedValueOnce(pageBody([], 3, 2));
    act(() => result.current.loadMore());

    await waitFor(() => expect(result.current.stalled).toBe(true));
    expect(result.current.hasMore).toBe(false);
    expect(result.current.error).toBeNull();
  });

  it('صفحة ملحقة فارغة والإجمالي مساوٍ للمعروض: اكتمال لا انقطاع', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 3));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    // حُذف/أُرشف تنبيه بين الجلبين فانكمش الإجمالي إلى المعروض نفسه.
    get.mockResolvedValueOnce(pageBody([], 2, 2));
    act(() => result.current.loadMore());

    await waitFor(() => expect(result.current.totalCount).toBe(2));
    expect(result.current.stalled).toBe(false);
    expect(result.current.hasMore).toBe(false);
  });

  it('reload يُصفّر الانقطاع ويعيد المتابعة من الصفحة الأولى', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 3));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    get.mockResolvedValueOnce(pageBody([row(2)], 4, 2));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.stalled).toBe(true));

    get.mockResolvedValueOnce(pageBody([row(9), row(2), row(1)], 5));
    act(() => result.current.reload());
    await waitFor(() => expect(result.current.items.map((alert) => alert.id)).toEqual([9, 2, 1]));
    expect(result.current.stalled).toBe(false);
    expect(result.current.hasMore).toBe(true);

    get.mockResolvedValueOnce(pageBody([row(4), row(5)], 5, 2));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items).toHaveLength(5));
    expect(result.current.stalled).toBe(false);
  });
});
