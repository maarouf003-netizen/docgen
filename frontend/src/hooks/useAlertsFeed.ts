import { useCallback, useEffect, useMemo, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { ALERTS_PAGE_SIZE, buildAlertsCursor, readAlertsPage } from '../api/alerts';
import { useCancellableRequest } from './useCancellableRequest';
import type { HeadAlertSummaryDto, PagedResult } from '../types';

export interface AlertsFeed {
  /** التنبيهات المحمّلة بالترتيب (الأحدث أولًا) — بلا تكرار. */
  items: HeadAlertSummaryDto[];
  /** الإجمالي من الخادم لقائمة النطاق كله (لا لعدد الصفحات المحمّلة). */
  totalCount: number;
  /** آخر صفحة وصلت كانت ممتلئة وهناك صفوف لم تُحمّل بعد. */
  hasMore: boolean;
  /** تحميل الصفحة الأولى (القائمة فارغة حتى تصل). */
  isLoading: boolean;
  /** تحميل صفحة إضافية (القائمة المعروضة تبقى ظاهرة). */
  isLoadingMore: boolean;
  error: string | null;
  /** يجلب الصفحة التالية، أو يعيد محاولة الصفحة الفاشلة نفسها. */
  loadMore: () => void;
  /** يعيد التحميل من الصفحة الأولى (بعد إصدار تنبيه مثلًا). */
  reload: () => void;
  /** تعديل محلي للعناصر المحمّلة (تعليم مقروء) بلا إعادة جلب. */
  updateItems: (updater: (prev: HeadAlertSummaryDto[]) => HeadAlertSummaryDto[]) => void;
}

/** نطاق التحميل الحالي: المؤشر `null` يعني الصفحة الأولى. */
interface CursorState {
  url: string;
  cursor: string | null;
}

/** الصفحة الواردة مع المؤشر الذي طلبتها به — يمنع نسبة صفحة إلى نطاق آخر. */
interface LoadedPage {
  url: string;
  cursor: string | null;
  result: PagedResult<HeadAlertSummaryDto>;
}

/** التراكم المحلي لنطاق واحد. */
interface FeedState {
  url: string;
  items: HeadAlertSummaryDto[];
  totalCount: number;
  hasMore: boolean;
}

const EMPTY_ITEMS: HeadAlertSummaryDto[] = [];

const EMPTY_FEED: FeedState = { url: '', items: EMPTY_ITEMS, totalCount: 0, hasMore: false };

/**
 * يُلحق صفحة بالمحمّل ويُسقط المكررات بالمعرّف.
 *
 * التكرار غير متوقع مع مؤشر keyset (الشرط `<` صارم على `(time, id)`)، لكن
 * الإبقاء على الإسقاط يمنع مفاتيح React المكررة وصفوفًا متطابقة لو تغيّر
 * سلوك الخادم مستقبلًا. وإن لم تُضف الصفحة شيئًا تُعاد المصفوفة نفسها
 * (هوية ثابتة) بلا رندر زائد.
 */
function appendUniqueRows(
  existing: HeadAlertSummaryDto[],
  incoming: HeadAlertSummaryDto[],
): HeadAlertSummaryDto[] {
  if (incoming.length === 0) return existing;
  const seen = new Set(existing.map((row) => row.id));
  const unique = incoming.filter((row) => {
    if (seen.has(row.id)) return false;
    seen.add(row.id);
    return true;
  });
  return unique.length === 0 ? existing : [...existing, ...unique];
}

/**
 * تغذية قائمة تنبيهات بترقيم **مؤشر** (keyset) بدل الإزاحة (`offset`).
 *
 * **لماذا:** `offset` يفترض قائمة ثابتة بين الجلبين؛ إصدار تنبيه جديد في
 * الأعلى يُزيح النافذة فتعود الصفحة التالية مكررات أو فارغة (جذر `stalled`
 * السابق). المؤشر يثبّت الموضع على **آخر صف معروض فعليًا**
 * (`{lastContentChangedAt}|{id}` — `buildAlertsCursor`)، فيكمل من الأقدم منه
 * بلا تكرار ولا فجوة مهما تغيّرت القائمة.
 *
 * **الحالة:**
 * - الصفحة الأولى (مؤشر `null`) تستبدل المحمّل، فهي بداية سلسلة جديدة
 *   (إعادة تحميل/تغيير نطاق) — بلا تفريغ أثناء الجلب (لا وميض «لا توجد تنبيهات»).
 * - `loadMore` يطلب ما بعد آخر صف ويُلحق النتيجة.
 * - `hasMore` يُشتق من **امتلاء الصفحة الأخيرة** ومن بقاء صفوف في الإجمالي:
 *   مؤشر keyset لا يملك عدد صفحات ثابتًا، وصفحة غير ممتلئة تعني نهاية القائمة.
 * - فشل صفحة إضافية يُبقي المعروض، ويعيد الزر محاولة **المؤشر نفسه**.
 *
 * **لا يوجد انقطاع ترقيم بنيويًا** هنا: المؤشر لا يزيح النافذة، فحالة
 * `stalled` أُزيلت من العقد مع سببها.
 */
export function useAlertsFeed(url: string, { enabled }: { enabled: boolean }): AlertsFeed {
  // المؤشر والنطاق في حالة واحدة: تغيّر `url` (تبويب/تصفية) يبدأ من الأولى
  // تلقائيًا بلا طلب صفحة متقدمة على قائمة جديدة.
  const [cursorState, setCursorState] = useState<CursorState>({ url, cursor: null });
  const cursor = cursorState.url === url ? cursorState.cursor : null;

  const [feedState, setFeedState] = useState<FeedState>(EMPTY_FEED);
  // بيانات نطاق سابق لا تُعرض على نطاق جديد.
  const feed = feedState.url === url ? feedState : EMPTY_FEED;

  // خطأ المؤشر (خرق عقد الصف) منفصل عن خطأ الطلب حتى لا يمحوه جلب ناجح لاحق
  // قبل إصلاح الصف نفسه — ويُعرض في المسار القائم (لافتة + إعادة محاولة).
  const [cursorError, setCursorError] = useState<string | null>(null);

  const query = useCancellableRequest<LoadedPage>(
    async (signal) => {
      const response = await api.get(url, {
        signal,
        params:
          cursor === null
            ? { pageSize: ALERTS_PAGE_SIZE }
            : { pageSize: ALERTS_PAGE_SIZE, cursor },
      });
      return { url, cursor, result: readAlertsPage(response.data) };
    },
    [url, cursor],
    { enabled },
  );
  const { isLoading: queryIsLoading, error: queryError, refetch } = query;

  // الصفحة الأولى تستبدل المحمّل، والصفحات التالية تُلحق. والاستبدال يتم عند
  // وصول الاستجابة فقط، فلا تفرغ القائمة أثناء إعادة التحميل.
  useEffect(() => {
    const data = query.data;
    if (!data) return;
    setFeedState((prev) => {
      const sameScope = prev.url === data.url;
      const baseItems = sameScope ? prev.items : EMPTY_ITEMS;
      const items =
        data.cursor === null ? data.result.items : appendUniqueRows(baseItems, data.result.items);
      // صفحة كاملة + صفوف باقية في الإجمالي ⇒ يمكن طلب ما بعد آخر صف.
      const hasMore =
        data.result.items.length === data.result.perPage && items.length < data.result.totalCount;
      if (
        sameScope &&
        prev.items === items &&
        prev.totalCount === data.result.totalCount &&
        prev.hasMore === hasMore
      ) {
        return prev;
      }
      return { url: data.url, items, totalCount: data.result.totalCount, hasMore };
    });
  }, [query.data]);

  const items = feed.items;
  const totalCount = feed.totalCount;
  const hasMore = feed.hasMore;
  const isLoading = queryIsLoading && cursor === null;
  const isLoadingMore = queryIsLoading && cursor !== null;
  const error = cursorError ?? queryError;

  const setCursor = useCallback(
    (next: string | null) => {
      setCursorState((prev) =>
        prev.url === url && prev.cursor === next ? prev : { url, cursor: next },
      );
    },
    [url],
  );

  const loadMore = useCallback(() => {
    if (queryIsLoading) return;
    // أي فشل (الصفحة الأولى أثناء إعادة التحميل أو صفحة تالية) ⇒ إعادة محاولة
    // **الطلب الحالي** نفسه: المؤشر لم يتغيّر، فلا تخطّي لصفوف لم تُحمّل.
    if (queryError) {
      refetch();
      return;
    }
    if (!hasMore) return;
    const tail = items[items.length - 1];
    if (!tail) return;
    try {
      setCursorError(null);
      setCursor(buildAlertsCursor(tail));
    } catch (cause) {
      // خرق عقد الصف: يُعلن بدل إرسال مؤشر ملفّق يعود 400 بلا سبب مفهوم.
      setCursorError(getApiErrorMessage(cause));
    }
  }, [hasMore, items, queryError, queryIsLoading, refetch, setCursor]);

  const reload = useCallback(() => {
    // لا تفريغ للعناصر هنا: القائمة المعروضة تبقى حتى تصل الصفحة الأولى الجديدة
    // (وإلا وميض «لا توجد تنبيهات» عند إعادة التحميل أمام المستخدم).
    setCursorError(null);
    setCursor(null);
    refetch();
  }, [refetch, setCursor]);

  const updateItems = useCallback(
    (updater: (prev: HeadAlertSummaryDto[]) => HeadAlertSummaryDto[]) => {
      setFeedState((prev) => {
        // تحديث نطاق آخر (تبويب لم يعد ظاهرًا) يُتجاهل: لا تُزرع فيه تعديلات
        // نطاق لم يُقرأ بعد.
        if (prev.url !== url) return prev;
        const next = updater(prev.items);
        return next === prev.items ? prev : { ...prev, items: next };
      });
    },
    [url],
  );

  return useMemo(
    () => ({
      items,
      totalCount,
      hasMore,
      isLoading,
      isLoadingMore,
      error,
      loadMore,
      reload,
      updateItems,
    }),
    [items, totalCount, hasMore, isLoading, isLoadingMore, error, loadMore, reload, updateItems],
  );
}
