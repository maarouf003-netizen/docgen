import { useCallback, useEffect, useMemo, useState } from 'react';
import { api } from '../api/client';
import { ALERTS_PAGE_SIZE, readAlertsPage } from '../api/alerts';
import { useCancellableRequest } from './useCancellableRequest';
import type { HeadAlertSummaryDto, PagedResult } from '../types';

export interface AlertsFeed {
  /** الصفحات المحمّلة بالترتيب (صفحة 1 ثم 2 …) — بلا تكرار عند إعادة الجلب. */
  items: HeadAlertSummaryDto[];
  /** الإجمالي من الخادم لقائمة النطاق كله (لا لعناصر الصفحة). */
  totalCount: number;
  /** يوجد ما لم يُحمَّل بعد ويمكن الوصول إليه (`totalCount > items.length` بلا انقطاع). */
  hasMore: boolean;
  /**
   * انقطع الترقيم قبل نهاية القائمة: صفحة ملحقة لم تُضف أي صف جديد (كلها مكررات
   * أو فارغة) لأن `offset` أُزيح بصدور/أرشفة تنبيه بين الجلبين — أي أن الإجمالي
   * ما زال يتجاوز المعروض ولا يمكن بلوغ الباقي بطلب صفحات أبعد.
   *
   * عندها `hasMore = false` (لا طلبات عقيمة) والمسار الوحيد `reload`، ويُصفَّر
   * تلقائيًا بوصول الصفحة الأولى (إعادة التحميل تستبدل الخريطة كلها).
   */
  stalled: boolean;
  /** تحميل الصفحة الأولى (القائمة فارغة حتى تصل). */
  isLoading: boolean;
  /** تحميل صفحة إضافية (القائمة المعروضة تبقى ظاهرة). */
  isLoadingMore: boolean;
  error: string | null;
  /** يجلب الصفحة التالية، أو يعيد محاولة الصفحة الفاشلة نفسها (ولا يطلب شيئًا عند انقطاع الترقيم). */
  loadMore: () => void;
  /** يعيد التحميل من الصفحة الأولى (بعد إصدار تنبيه مثلًا). */
  reload: () => void;
  /** تعديل محلي للعناصر المحمّلة (تعليم مقروء) بلا إعادة جلب. */
  updateItems: (updater: (prev: HeadAlertSummaryDto[]) => HeadAlertSummaryDto[]) => void;
}

/** نتيجة صفحة واحدة مع رقمها ونطاقها — يمنع نسبة بيانات صفحة/مرشّح قديم إلى آخر. */
interface LoadedPage {
  url: string;
  page: number;
  result: PagedResult<HeadAlertSummaryDto>;
}

/** حالة الصفحة المطلوبة، مربوطة بالنطاق: تغيّر النطاق يبدأ من الصفحة الأولى. */
interface PageState {
  url: string;
  page: number;
}

/** صفحات محمّلة لنطاق واحد — فصل البيانات بين المرشّحات يمنع عرض قائمة سابقة. */
interface LoadedPages {
  url: string;
  pages: ReadonlyMap<number, HeadAlertSummaryDto[]>;
}

const EMPTY_PAGES: ReadonlyMap<number, HeadAlertSummaryDto[]> = new Map();

/**
 * تغذية قائمة تنبيهات مرقّمة بلا قصّ صامت: تجلب الصفحة الأولى (`pageSize=100`)،
 * و`loadMore` يجلب التالية ويُلحقها، و`reload` يعيد من الأولى.
 *
 * التراكم بمفتاح رقم الصفحة (`Map`): إعادة جلب صفحة تستبدلها ولا تُضاعف عناصرها،
 * والصفحة تُقرأ من نتيجة الجلب نفسها لا من حالة الصفحة الحالية (فلا تُنسب بيانات
 * صفحة سابقة إلى صفحة لم تصل بعد). فشل صفحة إضافية يُبقي المعروض ويُعلن الخطأ،
 * وإعادة الضغط تعيد محاولة **الصفحة نفسها** لا التي بعدها.
 *
 * **انقطاع الإزاحة (`stalled`):** `offset` يفترض قائمة ثابتة بين الجلبين. إصدار
 * تنبيه جديد في الأعلى بعد الصفحة الأولى يُزيح النافذة، فقد تعود الصفحة التالية
 * كلها مكررات أو فارغة بينما `totalCount` ما زال أكبر من المعروض — وهنا يتوقف
 * الطلب (`hasMore = false`) ويُعلن الانقطاع بدل وعد ترقيم لا يُغلق وطلبات عقيمة،
 * ويبقى `reload` هو المخرج. الحل الجذري (مؤشر `(LastContentChangedAt, Id)`)
 * قرار معماري في `PR-6`.
 */
export function useAlertsFeed(url: string, { enabled }: { enabled: boolean }): AlertsFeed {
  // الصفحة والنطاق في حالة واحدة: تغيّر `url` (تبويب/تصفية) يُعيد الترقيم إلى
  // الأولى تلقائيًا بلا طلب صفحة ثالثة على قائمة جديدة، وبلا تفريغ يدوي للعناصر.
  const [pageState, setPageState] = useState<PageState>({ url, page: 1 });
  const page = pageState.url === url ? pageState.page : 1;
  const setPage = useCallback(
    (updater: (current: number) => number) => {
      setPageState((prev) => ({ url, page: updater(prev.url === url ? prev.page : 1) }));
    },
    [url],
  );

  const [loadedState, setLoadedState] = useState<LoadedPages>({ url, pages: EMPTY_PAGES });
  // بيانات نطاق سابق لا تُعرض على نطاق جديد: الصفحة تبدأ تحميلًا نظيفًا.
  const loaded = loadedState.url === url ? loadedState.pages : EMPTY_PAGES;

  const query = useCancellableRequest<LoadedPage>(
    async (signal) => {
      const response = await api.get(url, {
        signal,
        params: { page, pageSize: ALERTS_PAGE_SIZE },
      });
      return { url, page, result: readAlertsPage(response.data) };
    },
    [url, page],
    { enabled },
  );
  const { isLoading: queryIsLoading, error: queryError, refetch } = query;

  // التخزين بمفتاح الصفحة الواردة نفسها: مفتاح لا يتغيّر مع تغيّر `page`.
  // ووصول الصفحة الأولى يعني بداية سلسلة جديدة (إعادة تحميل/إصدار تنبيه): تُستبدل الخريطة
  // كلها بها فتُسقَط الصفحات اللاحقة التي أزاحها التغيير — بلا تفريغ القائمة أثناء الجلب.
  useEffect(() => {
    const data = query.data;
    if (!data) return;
    setLoadedState((prev) => {
      const pages = prev.url === data.url ? prev.pages : EMPTY_PAGES;
      if (data.page === 1) {
        const existing = pages.get(1);
        if (prev.url === data.url && pages.size === 1 && existing === data.result.items) return prev;
        return { url: data.url, pages: new Map([[1, data.result.items]]) };
      }
      const existing = pages.get(data.page);
      if (existing === data.result.items) return prev;
      const next = new Map(pages);
      next.set(data.page, data.result.items);
      return { url: data.url, pages: next };
    });
  }, [query.data]);

  // الدمج يُزيل التكرار بالمعرّف: إصدار/أرشفة تنبيه بين صفحتين يُزيح الترقيم فيظهر
  // الصف نفسه في صفحتين — ومفتاح React المكرر يُنتج تحذيرًا وصفّين متطابقين.
  // وصفحة ملحقة لا تُضيف صفًا جديدًا (`added === 0`) دليل أن الإزاحة أخلّت النافذة:
  // الباقي في أعلى النطاق ولا يُبلغ بطلب صفحات أبعد ⇒ تُعلَّم انقطاعًا.
  const merged = useMemo(() => {
    if (loaded.size === 0) return { items: [] as HeadAlertSummaryDto[], stalledPage: false };
    const pages = [...loaded.keys()].sort((a, b) => a - b);
    const seen = new Set<number>();
    const items: HeadAlertSummaryDto[] = [];
    let stalledPage = false;
    for (const pageNumber of pages) {
      let added = 0;
      for (const alert of loaded.get(pageNumber) ?? []) {
        if (seen.has(alert.id)) continue;
        seen.add(alert.id);
        items.push(alert);
        added += 1;
      }
      if (pageNumber > 1 && added === 0) stalledPage = true;
    }
    return { items, stalledPage };
  }, [loaded]);
  const items = merged.items;

  // بيانات النطاق الحالي وحدها تُقرأ: نتيجة نطاق سابق لا تُنسَب إلى نطاق جديد
  // (وإلا ظهر إجمالي المرشّح السابق تحت قائمة فارغة أثناء جلب الجديد).
  const currentData = query.data && query.data.url === url ? query.data : null;
  const totalCount = currentData?.result.totalCount ?? 0;
  // «انقطاع» فعلي حين تبقى فجوة بين الإجمالي والمعروض مع صفحة ملحقة بلا صف جديد.
  // أما صفحة ملحقة فارغة والإجمالي مساوٍ للمعروض (حذف/أرشفة بين الجلبين) فقائمة
  // مكتملة لا انقطاع — فلا رسالة مضللة.
  const stalled = merged.stalledPage && totalCount > items.length;
  const hasMore = totalCount > items.length && !stalled;
  const isLoading = queryIsLoading && page === 1;
  const isLoadingMore = queryIsLoading && page > 1;
  // آخر صفحة وصلت فعلًا في هذا النطاق — يمنع طلب صفحة تتجاوز التي لم تُحمّل بعد.
  const lastLoadedPage = currentData?.page ?? 0;

  const loadMore = useCallback(() => {
    if (queryIsLoading) return;
    // أي فشل (الصفحة الأولى أثناء إعادة التحميل أو صفحة تالية) ⇒ إعادة محاولة
    // **الصفحة الحالية** نفسها، لا الانتقال إلى التي بعدها (وإلا صار الزر بلا أثر
    // حين لا يوجد «مزيد» أصلًا، أو تخطّى صفحة لم تُحمّل).
    if (queryError) {
      refetch();
      return;
    }
    if (!hasMore) return;
    if (page > lastLoadedPage) return;
    setPage((current) => current + 1);
  }, [hasMore, lastLoadedPage, page, queryError, queryIsLoading, refetch, setPage]);

  const reload = useCallback(() => {
    // لا تفريغ للعناصر هنا: القائمة المعروضة تبقى حتى تصل الصفحة الأولى الجديدة
    // (وإلا وميض «لا توجد تنبيهات» عند إعادة التحميل أمام المستخدم).
    setPage(() => 1);
    refetch();
  }, [refetch, setPage]);

  const updateItems = useCallback(
    (updater: (prev: HeadAlertSummaryDto[]) => HeadAlertSummaryDto[]) => {
      setLoadedState((prev) => {
        // تحديث نطاق آخر (تبويب لم يعد ظاهرًا) يُتجاهل: لا تُزرع فيه تعديلات
        // نطاق لم يُقرأ بعد.
        if (prev.url !== url) return prev;
        let changed = false;
        const next = new Map<number, HeadAlertSummaryDto[]>();
        for (const [pageNumber, rows] of prev.pages) {
          const updated = updater(rows);
          if (updated !== rows) changed = true;
          next.set(pageNumber, updated);
        }
        return changed ? { url: prev.url, pages: next } : prev;
      });
    },
    [url],
  );

  return {
    items,
    totalCount,
    hasMore,
    stalled,
    isLoading,
    isLoadingMore,
    error: queryError,
    loadMore,
    reload,
    updateItems,
  };
}
