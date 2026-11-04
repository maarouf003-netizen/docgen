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
  /**
   * آخر صفحة وصلت كانت ممتلئة **ولا نعلم** أن القائمة انتهت: قد يوجد ما بعدها.
   * (`true` لا يعني «مزيد مُثبت»، بل «قد يوجد مزيد» — والقرار النهائي لصفحة غير ممتلئة.)
   */
  hasMore: boolean;
  /**
   * اكتمل التحميل لكن عدد المحمّل يخالف إجمالي الخادم: صفوف أُضيفت فوق المؤشر، أو
   * غادرت الفلتر (أرشفة/تعاقب)، أو غيّرت مفتاح ترتيبها. ليست خطأً — إشارة صادقة
   * تدعو إلى `reload` بدل إخفاء الفروق.
   */
  scopeDrift: boolean;
  /** تحميل الصفحة الأولى (القائمة فارغة حتى تصل). */
  isLoading: boolean;
  /** تحميل صفحة إضافية (القائمة المعروضة تبقى ظاهرة). */
  isLoadingMore: boolean;
  error: string | null;
  /**
   * الإجراء الموحّد للتذييل: يجلب الصفحة التالية عند وجود مزيد، ويعيد محاولة
   * الطلب الفاشل، أو يعيد التحميل من الأولى حين يكون الفشل حتميًا (خرق عقد
   * المؤشر، أو صفحة لم تُضف صفًا).
   */
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

/** رسالة مرتبطة بنطاقها — فلا يلتصق خطأ نطاق بنطاق آخر بعد تبديل التبويب/التصفية. */
interface ScopedMessage {
  url: string;
  message: string | null;
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
  /**
   * صفحة مؤشر لم تُضف أي صف جديد: المؤشر لن يتقدّم، فأي محاولة أخرى تعيد نفس
   * النتيجة. تُعلن للمستخدم ويُتاح لها `reload` وحده (حارس التقدّم).
   */
  stuck: boolean;
  /** اكتمل الطلب وعدد المحمّل يخالف الإجمالي — إشارة إعادة تحميل لا خطأ (انظر `AlertsFeed`). */
  scopeDrift: boolean;
}

const EMPTY_ITEMS: HeadAlertSummaryDto[] = [];

const EMPTY_FEED: FeedState = {
  url: '',
  items: EMPTY_ITEMS,
  totalCount: 0,
  hasMore: false,
  stuck: false,
  scopeDrift: false,
};

/**
 * نص إعلان توقّف التقدّم. صياغته للعرض المباشر وتذكر المخرج الفعلي (إعادة
 * التحميل) الذي يوفّره زر التذييل — لا رسالة تطلب إجراءً غير معروض.
 */
const STUCK_MESSAGE =
  'توقّف تحميل بقية التنبيهات — أعِد تحميل القائمة، وإن استمر الخطأ فأبلغ الدعم';

/**
 * يُلحق صفحة بالمحمّل ويُسقط المكررات بالمعرّف.
 *
 * التكرار غير متوقع مع مؤشر keyset (الشرط `<` صارم على `(time, id)`)، لكن
 * الإبقاء على الإسقاط يمنع مفاتيح React المكررة وصفوفًا متطابقة لو تغيّر
 * سلوك الخادم مستقبلًا. وإن لم تُضف الصفحة شيئًا تُعاد المصفوفة نفسها
 * (هوية ثابتة) بلا رندر زائد — ويقرأ المستدعي ذلك كـ«توقّف تقدّم».
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
 * (`{lastContentChangedAt}|{id}` — `buildAlertsCursor`)، فلا يتكرر صف **أبدًا**،
 * ولا يُفقد صف لم يتغيّر مفتاح ترتيبه. (صف يُعدَّل محتواه أثناء التصفّح يرتفع
 * فوق المؤشر فيخرج من بقية الجولة حتى إعادة التحميل — فقدان مؤقت معلوم.)
 *
 * **الحالة:**
 * - الصفحة الأولى (مؤشر `null`) تستبدل المحمّل، فهي بداية سلسلة جديدة
 *   (إعادة تحميل/تغيير نطاق) — بلا تفريغ أثناء الجلب (لا وميض «لا توجد تنبيهات»).
 * - `loadMore` يطلب ما بعد آخر صف ويُلحق النتيجة.
 * - `hasMore` يُشتق من **امتلاء الصفحة الأخيرة** وحده (مع حارس التقدّم): صفحة غير
 *   ممتلئة تعني نهاية القائمة حتمًا في keyset، وصفحة ممتلئة تعني «قد يوجد مزيد».
 *   **ولا يُستعمل `totalCount` بوابةً للترقيم** لأنه عدد القائمة كلها بينما `items`
 *   تراكم تحت المؤشر فقط؛ المقارنة كانت تُوقف التحميل مبكرًا عند تقلّص القائمة
 *   (أرشفة/تعاقب/حذف بين الجلبين ⇒ صفوف صالحة لا تُجلب أبدًا) وتطلب صفحة زائدة عند
 *   نموّها. الكلفة المقبولة: طلب واحد زائد عند نهاية عددها مضاعف لحجم الصفحة.
 * - `scopeDrift` يُعلن عند نهاية السلسلة إذا اختلف المحمّل عن الإجمالي: إشارة
 *   إعادة تحميل صادقة بدل قصّ صامت.
 * - فشل صفحة إضافية يُبقي المعروض، ويعيد الزر محاولة **المؤشر نفسه**.
 * - **فشل حتمي** (خرق عقد صف يمنع بناء المؤشر، أو صفحة بلا أي صف جديد) لا
 *   يُعاد معه الطلب نفسه لأنه سيفشل بالنتيجة ذاتها؛ يتحول الزر إلى إعادة
 *   تحميل من الأولى، وهي المخرج الوحيد الذي يصلح الحالة.
 *
 * **لا يوجد انقطاع ترقيم بنيويًا** هنا (المؤشر لا يزيح النافذة)، لكن يبقى
 * حارس **تقدّم** واحد: صفحة بلا صف جديد تُعلن وتوقف الطلبات.
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
  // قبل إصلاح الصف نفسه — **ومربوط بالنطاق** كبقية الحالات، فلا يظهر خطأ
  // تصفية/تبويب سابق فوق قائمة نطاق جديد سليمة.
  const [cursorErrorState, setCursorErrorState] = useState<ScopedMessage>({ url, message: null });
  const cursorError = cursorErrorState.url === url ? cursorErrorState.message : null;

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
      const isFirstPage = data.cursor === null;
      const items = isFirstPage
        ? data.result.items
        : appendUniqueRows(baseItems, data.result.items);
      // صفحة ممتلئة ⇔ قد يوجد ما بعدها؛ وصفحة مؤشر بلا صف جديد ⇔ المؤشر لا
      // يتقدّم (لا فائدة من طلب آخر) ⇒ تُعلن الحالة بدل زر صامت.
      const pageWasFull = data.result.items.length === data.result.perPage;
      const stuck = !isFirstPage && data.result.items.length > 0 && items === baseItems;
      // **البوابة الوحيدة: امتلاء الصفحة.** `totalCount` عدّ القائمة كلها (فوق
      // المؤشر وتحته) بينما `items` تراكم تحت المؤشر؛ ومقارنتهما تنهار في الحالتين:
      // (1) تقلّص القائمة (أرشفة/تعاقب/حذف بين الجلبين) يجعل المحمّل يسبق الإجمالي
      //     فتنقطع السلسلة وصفوف صالحة لا تُجلب أبدًا — وأسوأ صورها أن يتساوى
      //     العددان (صفوف خرجت مقابل صفوف لم تُجلب) فيبدو «اكتمالًا» كاذبًا؛
      // (2) نموّها (صف أُدرج فوق المؤشر) يجعل الإجمالي أكبر دائمًا فيُطلب ما لا يوجد.
      // فتُقبل الكلفة المحدودة: طلب واحد زائد في نهاية قائمة عددها مضاعف لحجم
      // الصفحة، بدل صف مخفي بلا إشارة. النهاية تُكتشف بامتلاء الصفحة لا بالعدّ.
      const hasMore = pageWasFull && !stuck;
      // نهاية السلسلة مع اختلاف العدّين = القائمة تغيّرت أثناء التصفح: إشارة
      // إعادة تحميل صادقة (ليست خطأً) بدل عرض ناقص صامت.
      const scopeDrift = !isFirstPage && !stuck && !pageWasFull
        && items.length !== data.result.totalCount;
      if (
        sameScope &&
        prev.items === items &&
        prev.totalCount === data.result.totalCount &&
        prev.hasMore === hasMore &&
        prev.stuck === stuck &&
        prev.scopeDrift === scopeDrift
      ) {
        return prev;
      }
      return { url: data.url, items, totalCount: data.result.totalCount, hasMore, stuck, scopeDrift };
    });
  }, [query.data]);

  const items = feed.items;
  const totalCount = feed.totalCount;
  const hasMore = feed.hasMore;
  const scopeDrift = feed.scopeDrift;
  const isLoading = queryIsLoading && cursor === null;
  const isLoadingMore = queryIsLoading && cursor !== null;
  const stuck = feed.stuck;
  // الأولوية: خرق عقد المؤشر ← فشل طلب (قابل لإعادة المحاولة) ← توقّف تقدّم.
  const error = cursorError ?? queryError ?? (stuck ? STUCK_MESSAGE : null);

  const setCursor = useCallback(
    (next: string | null) => {
      setCursorState((prev) =>
        prev.url === url && prev.cursor === next ? prev : { url, cursor: next },
      );
    },
    [url],
  );

  const setCursorError = useCallback(
    (message: string | null) => {
      setCursorErrorState((prev) =>
        prev.url === url && prev.message === message ? prev : { url, message },
      );
    },
    [url],
  );

  const reload = useCallback(() => {
    // لا تفريغ للعناصر هنا: القائمة المعروضة تبقى حتى تصل الصفحة الأولى الجديدة
    // (وإلا وميض «لا توجد تنبيهات» عند إعادة التحميل أمام المستخدم).
    setCursorError(null);
    setCursor(null);
    refetch();
  }, [refetch, setCursor, setCursorError]);

  const loadMore = useCallback(() => {
    if (queryIsLoading) return;
    // فشل حتمي (لا يتغيّر بتكرار الطلب نفسه): إعادة التحميل من الأولى هي المخرج.
    if (cursorError || stuck) {
      reload();
      return;
    }
    // فشل طلب عابر: إعادة محاولة **الطلب الحالي** نفسه — المؤشر لم يتغيّر، فلا
    // تخطّي لصفوف لم تُحمّل.
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
  }, [
    cursorError,
    hasMore,
    items,
    queryError,
    queryIsLoading,
    refetch,
    reload,
    setCursor,
    setCursorError,
    stuck,
  ]);

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
      scopeDrift,
      isLoading,
      isLoadingMore,
      error,
      loadMore,
      reload,
      updateItems,
    }),
    [
      items,
      totalCount,
      hasMore,
      scopeDrift,
      isLoading,
      isLoadingMore,
      error,
      loadMore,
      reload,
      updateItems,
    ],
  );
}
