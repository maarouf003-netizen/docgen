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

/**
 * حجم صفحة اختباري صغير: `perPage` في الجسم هو ما يعكسه الخادم، والحارس يشتق
 * «مزيد» من امتلاء الصفحة **كما وردت** لا من ثابت الواجهة — فصفحة من صفين
 * بـ`perPage=2` تسلك سلوك صفحة ممتلئة بلا توليد 100 صف في كل اختبار.
 */
const PAGE = 2;

function row(id: number, lastContentChangedAt?: string) {
  return makeHeadAlertSummary({
    id,
    messagePreview: `تنبيه ${id}`,
    ...(lastContentChangedAt ? { lastContentChangedAt } : {}),
  });
}

/** جسم `PagedResult` كما يصل على السلك في طلب مؤشر (`page` يعيده الخادم 1). */
function pageBody(items: ReturnType<typeof row>[], totalCount: number, perPage = PAGE) {
  return {
    data: {
      items,
      page: 1,
      perPage,
      totalCount,
      totalPages: perPage <= 0 ? 0 : Math.ceil(totalCount / perPage),
    },
  };
}

/** مؤشر الصف كما يبنيه الخادم ويُرسل في `params.cursor`. */
function cursorOf(id: number) {
  return `2026-08-02T10:00:00Z|${id}`;
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('useAlertsFeed', () => {
  it('الصفحة الأولى: بلا `page` وبلا مؤشر، و`hasMore` من امتلاء الصفحة والإجمالي', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 137));

    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));

    await waitFor(() => expect(result.current.items).toHaveLength(2));
    expect(result.current.totalCount).toBe(137);
    expect(result.current.hasMore).toBe(true);
    expect(result.current.error).toBeNull();
    expect(result.current.isLoading).toBe(false);
    expect(get).toHaveBeenCalledWith(
      '/alerts',
      expect.objectContaining({ params: { pageSize: 100 } }),
    );
  });

  it('loadMore يطلب ما بعد آخر صف بالمؤشر ويُلحق النتيجة', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 3));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    get.mockResolvedValueOnce(pageBody([row(3)], 3));
    act(() => result.current.loadMore());

    await waitFor(() => expect(result.current.items).toHaveLength(3));
    expect(result.current.items.map((alert) => alert.id)).toEqual([1, 2, 3]);
    expect(result.current.hasMore).toBe(false);
    expect(get).toHaveBeenLastCalledWith(
      '/alerts',
      expect.objectContaining({ params: { pageSize: 100, cursor: cursorOf(2) } }),
    );
  });

  it('لا يطلب صفحة إضافية حين لم تكن الصفحة ممتلئة', async () => {
    get.mockResolvedValueOnce(pageBody([row(1)], 1));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(1));

    act(() => result.current.loadMore());

    expect(get).toHaveBeenCalledTimes(1);
  });

  it('صفحة ممتلئة والإجمالي مساوٍ للمعروض: القرار للامتلاء لا للعدّ ⇒ طلب تحقّق واحد يُنهي السلسلة', async () => {
    // الإجمالي قد يتقلّص بين الجلبين (أرشفة/تعاقب)، فتساوي العددين ليس دليل نهاية؛
    // البوابة الوحيدة صفحة غير ممتلئة. الكلفة: طلب واحد زائد عند النهاية بالضبط.
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 2));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    expect(result.current.hasMore).toBe(true);

    get.mockResolvedValueOnce(pageBody([], 2));
    act(() => result.current.loadMore());

    await waitFor(() => expect(result.current.hasMore).toBe(false));
    expect(result.current.scopeDrift).toBe(false);
    expect(result.current.error).toBeNull();
    expect(result.current.items).toHaveLength(2);
  });

  it('فشل صفحة إضافية يُبقي المعروض ويعلن الخطأ ويعيد المحاولة للمؤشر نفسه', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 3));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    get.mockRejectedValueOnce(new Error('تعذّر الجلب'));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.error).toBe('تعذّر الجلب'));
    // المعروض لا يسقط عند فشل الصفحة التالية.
    expect(result.current.items).toHaveLength(2);

    get.mockResolvedValueOnce(pageBody([row(3)], 3));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items).toHaveLength(3));
    expect(result.current.error).toBeNull();
    // إعادة المحاولة كانت للمؤشر نفسه لا لموضع أبعد.
    expect(get).toHaveBeenLastCalledWith(
      '/alerts',
      expect.objectContaining({ params: { pageSize: 100, cursor: cursorOf(2) } }),
    );
  });

  it('تنبيه جديد بين الجلبين لا يُكرر صفًا ولا يُسقط صفًا، والنهاية لا تُنذر كذبًا', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 4));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    // أُصدر تنبيه 99 في الأعلى: الإزاحة كانت ستُعيد [2,3] أو تُفقد صفًا،
    // والمؤشر يكمل من بعد الصف 2 فيعطي [3,4] والإجمالي صار 5.
    get.mockResolvedValueOnce(pageBody([row(3), row(4)], 5));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items).toHaveLength(4));
    expect(result.current.items.map((alert) => alert.id)).toEqual([1, 2, 3, 4]);

    // الصف الجديد (99) فوق المؤشر، فلا يُجلب في هذه الجولة؛ والانقطاع لا يُفترض
    // من الإجمالي — الصفحة ممتلئة فنُكمل حتى تُثبت صفحة غير ممتلئة النهاية.
    expect(result.current.hasMore).toBe(true);
    get.mockResolvedValueOnce(pageBody([], 5));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.hasMore).toBe(false));

    // النهاية هادئة بلا إشارة: الإجمالي الأكبر (5) يعني صفوفًا **أحدث فوق المؤشر**،
    // وليست مفقودة تحت المؤشر — فإشعار «أعِد التحميل» هنا يصير ضجيجًا عند كل تنبيه
    // جديد في فرع نشط. الإشارة محفوظة لاحتمال الفقدان (المحمّل يتجاوز الإجمالي) فقط.
    expect(result.current.error).toBeNull();
    expect(result.current.items.map((alert) => alert.id)).toEqual([1, 2, 3, 4]);
    expect(result.current.scopeDrift).toBe(false);
    expect(result.current.hasMore).toBe(false);

    // الصف الجديد يظهر بإعادة التحميل من الأولى فقط.
    get.mockResolvedValueOnce(pageBody([row(99), row(1)], 6));
    act(() => result.current.reload());
    await waitFor(() => expect(result.current.items.map((alert) => alert.id)).toEqual([99, 1]));
    expect(result.current.scopeDrift).toBe(false);
    expect(get).toHaveBeenLastCalledWith(
      '/alerts',
      expect.objectContaining({ params: { pageSize: 100 } }),
    );
  });

  it('تقلّص القائمة بين الجلبين لا يقطع التحميل: لا صفّ صالح يُفقد بصمت', async () => {
    // السيناريو الذي كان يُنتج إيقافًا مبكرًا: صفحة ثانية **ممتلئة** وعدد المحمّل
    // يساوي الإجمالي الجديد تمامًا (صفوف خرجت من الفلتر مقابل صفوف لم تُجلب بعد).
    // المقارنة القديمة `items.length < totalCount` كانت تُنهي هنا وتُخفي [5,6].
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 6));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));
    expect(result.current.hasMore).toBe(true);

    // أُرشف الصفان 1 و2 (خرجا من «النشط») ⇒ الإجمالي 4 وبقيت تحت المؤشر [3,4,5,6].
    get.mockResolvedValueOnce(pageBody([row(3), row(4)], 4));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items).toHaveLength(4));

    // لا توقّف: الصفحة ممتلئة ⇒ قد يوجد مزيد (وكان يوجد فعلًا).
    expect(result.current.hasMore).toBe(true);
    get.mockResolvedValueOnce(pageBody([row(5), row(6)], 4));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items).toHaveLength(6));
    expect(result.current.items.map((alert) => alert.id)).toEqual([1, 2, 3, 4, 5, 6]);

    // النهاية تُعلن التغيّر (المحمّل 6 والإجمالي 4) بدل اختفاء صامت للتذييل.
    get.mockResolvedValueOnce(pageBody([], 4));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.hasMore).toBe(false));
    expect(result.current.scopeDrift).toBe(true);
    expect(result.current.error).toBeNull();
  });

  it('صفحة مؤشر فارغة تُنهي السلسلة بهدوء: لا «توقّف» ولا خطأ ولا تغيّر مزعوم', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 2));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    get.mockResolvedValueOnce(pageBody([], 2));
    act(() => result.current.loadMore());

    await waitFor(() => expect(result.current.hasMore).toBe(false));
    // `stuck` مشروط بصفحة تحمل صفوفًا مكررة، فلا يُعلن هنا خطأ توقّف.
    expect(result.current.error).toBeNull();
    expect(result.current.scopeDrift).toBe(false);
    expect(result.current.items.map((alert) => alert.id)).toEqual([1, 2]);
  });

  it('يُسقط أي تكرار بالمعرّف لو أعاده الخادم في صفحة لاحقة', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 4));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    get.mockResolvedValueOnce(pageBody([row(2), row(3)], 4));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items).toHaveLength(3));

    expect(result.current.items.map((alert) => alert.id)).toEqual([1, 2, 3]);
  });

  it('reload يعيد من الصفحة الأولى ويستبدل ولا يُضيف', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 3));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    get.mockResolvedValueOnce(pageBody([row(3)], 3));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items).toHaveLength(3));

    get.mockResolvedValueOnce(pageBody([row(9)], 1));
    act(() => result.current.reload());
    await waitFor(() => expect(result.current.items.map((alert) => alert.id)).toEqual([9]));
    expect(result.current.totalCount).toBe(1);
    expect(get).toHaveBeenLastCalledWith(
      '/alerts',
      expect.objectContaining({ params: { pageSize: 100 } }),
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
    // الصفحة ممتلئة ⇒ «قد يوجد مزيد» (القرار للامتلاء لا للعدّ).
    expect(result.current.hasMore).toBe(true);

    get.mockRejectedValueOnce(new Error('تعذّر الجلب'));
    act(() => result.current.reload());
    await waitFor(() => expect(result.current.error).toBe('تعذّر الجلب'));
    expect(result.current.items).toHaveLength(2);

    get.mockResolvedValueOnce(pageBody([row(9)], 1));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items.map((alert) => alert.id)).toEqual([9]));
    expect(get).toHaveBeenLastCalledWith(
      '/alerts',
      expect.objectContaining({ params: { pageSize: 100 } }),
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
    expect(result.current.isLoading).toBe(false);
  });

  it('صف بطابع زمني غير UTC صريح: يُعلن خرق العقد ولا يُرسل مؤشرًا ملفّقًا', async () => {
    // الصف الأخير هو أساس المؤشر، فطابعه الزمني الغامض يمنع بناء أي مؤشر.
    get.mockResolvedValueOnce(pageBody([row(2), row(1, '2026-08-02T10:00:00')], 5));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));
    expect(result.current.hasMore).toBe(true);

    act(() => result.current.loadMore());

    await waitFor(() => expect(result.current.error).not.toBeNull());
    expect(result.current.error).toContain('تعذّر تحميل بقية التنبيهات');
    expect(get).toHaveBeenCalledTimes(1);
  });

  it('فشل المؤشر الحتمي: الضغط التالي يعيد التحميل من الأولى لا تكرار الطلب الفاشل', async () => {
    get.mockResolvedValueOnce(pageBody([row(2), row(1, '2026-08-02T10:00:00')], 5));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.error).not.toBeNull());

    // إعادة المحاولة الحتمية = إعادة تحميل من الأولى (بلا مؤشر).
    get.mockResolvedValueOnce(pageBody([row(9)], 1));
    act(() => result.current.loadMore());

    await waitFor(() => expect(result.current.items.map((alert) => alert.id)).toEqual([9]));
    expect(result.current.error).toBeNull();
    expect(get).toHaveBeenLastCalledWith(
      '/alerts',
      expect.objectContaining({ params: { pageSize: 100 } }),
    );
  });

  it('صفحة مؤشر بلا أي صف جديد: توقّف معلن لا زر صامت، والمخرج إعادة التحميل', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 4));
    const { result } = renderHook(() => useAlertsFeed('/alerts', { enabled: true }));
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    // الخادم يعيد الصفين نفسيهما (خلل مؤشر): لا تقدّم ⇒ لا «مزيد» ولا طلب آخر.
    get.mockResolvedValueOnce(pageBody([row(2), row(1)], 4));
    act(() => result.current.loadMore());

    await waitFor(() => expect(result.current.error).toContain('توقّف تحميل بقية التنبيهات'));
    expect(result.current.hasMore).toBe(false);
    expect(result.current.items.map((alert) => alert.id)).toEqual([1, 2]);

    get.mockResolvedValueOnce(pageBody([row(9)], 4));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items.map((alert) => alert.id)).toEqual([9]));
    expect(result.current.error).toBeNull();
  });

  it('خطأ المؤشر لا يلتصق بنطاق آخر: تغيير التصفية يصفي الخطأ ويجلب من الأولى', async () => {
    get.mockResolvedValueOnce(pageBody([row(2), row(1, '2026-08-02T10:00:00')], 5));
    const { result, rerender } = renderHook(
      ({ url }: { url: string }) => useAlertsFeed(url, { enabled: true }),
      { initialProps: { url: '/alerts?archived=active' } },
    );
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.error).not.toBeNull());

    get.mockResolvedValueOnce(pageBody([row(7)], 1));
    rerender({ url: '/alerts?archived=all' });

    await waitFor(() => expect(result.current.items.map((alert) => alert.id)).toEqual([7]));
    expect(result.current.error).toBeNull();
    expect(get).toHaveBeenLastCalledWith(
      '/alerts?archived=all',
      expect.objectContaining({ params: { pageSize: 100 } }),
    );
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
      expect.objectContaining({ params: { pageSize: 100 } }),
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

  it('تغيّر النطاق (تبويب/تصفية) يعيد من الأولى ولا يحمل مؤشر النطاق السابق', async () => {
    get.mockResolvedValueOnce(pageBody([row(1), row(2)], 300));
    const { result, rerender } = renderHook(
      ({ url }: { url: string }) => useAlertsFeed(url, { enabled: true }),
      { initialProps: { url: '/alerts' } },
    );
    await waitFor(() => expect(result.current.items).toHaveLength(2));

    // ننتقل إلى الصفحة الثانية قبل تغيير النطاق — المؤشر يجب ألا يرافقه.
    get.mockResolvedValueOnce(pageBody([row(3)], 300));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.items).toHaveLength(3));

    get.mockResolvedValueOnce(pageBody([row(9)], 40));
    rerender({ url: '/alerts?archived=all' });

    await waitFor(() => expect(result.current.items.map((alert) => alert.id)).toEqual([9]));
    expect(get).toHaveBeenLastCalledWith(
      '/alerts?archived=all',
      expect.objectContaining({ params: { pageSize: 100 } }),
    );
    expect(result.current.totalCount).toBe(40);
  });

  it('لا يعرض عناصر النطاق السابق أثناء جلب النطاق الجديد', async () => {
    get.mockResolvedValueOnce(pageBody([row(1)], 1));
    const { result, rerender } = renderHook(
      ({ url }: { url: string }) => useAlertsFeed(url, { enabled: true }),
      { initialProps: { url: '/alerts' } },
    );
    await waitFor(() => expect(result.current.items).toHaveLength(1));

    let resolveNext: (value: unknown) => void = () => {};
    get.mockReturnValueOnce(new Promise((resolve) => { resolveNext = resolve; }));
    rerender({ url: '/alerts/sent' });

    // النطاق الجديد يبدأ فارغًا مع تحميل صريح، لا بقائمة النطاق السابق وإجماليه.
    await waitFor(() => expect(result.current.items).toHaveLength(0));
    expect(result.current.isLoading).toBe(true);
    expect(result.current.totalCount).toBe(0);

    resolveNext(pageBody([row(5)], 1));
    await waitFor(() => expect(result.current.items.map((alert) => alert.id)).toEqual([5]));
  });
});
