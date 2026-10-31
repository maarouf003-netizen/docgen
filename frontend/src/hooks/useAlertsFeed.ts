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
  /** يوجد ما لم يُحمَّل بعد (`totalCount > items.length`). */
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

/** نتيجة صفحة واحدة مع رقمها — يمنع نسبة بيانات صفحة قديمة إلى صفحة جديدة. */
interface LoadedPage {
  page: number;
  result: PagedResult<HeadAlertSummaryDto>;
}

/**
 * تغذية قائمة تنبيهات مرقّمة بلا قصّ صامت: تجلب الصفحة الأولى (`pageSize=100`)،
 * و`loadMore` يجلب التالية ويُلحقها، و`reload` يعيد من الأولى.
 *
 * التراكم بمفتاح رقم الصفحة (`Map`): إعادة جلب صفحة تستبدلها ولا تُضاعف عناصرها،
 * والصفحة تُقرأ من نتيجة الجلب نفسها لا من حالة الصفحة الحالية (فلا تُنسب بيانات
 * صفحة سابقة إلى صفحة لم تصل بعد). فشل صفحة إضافية يُبقي المعروض ويُعلن الخطأ،
 * وإعادة الضغط تعيد محاولة **الصفحة نفسها** لا التي بعدها.
 */
export function useAlertsFeed(url: string, { enabled }: { enabled: boolean }): AlertsFeed {
  const [page, setPage] = useState(1);
  const [loaded, setLoaded] = useState<ReadonlyMap<number, HeadAlertSummaryDto[]>>(new Map());

  const query = useCancellableRequest<LoadedPage>(
    async (signal) => {
      const response = await api.get(url, {
        signal,
        params: { page, pageSize: ALERTS_PAGE_SIZE },
      });
      return { page, result: readAlertsPage(response.data) };
    },
    [url, page],
    { enabled },
  );
  const { isLoading: queryIsLoading, error: queryError, refetch } = query;

  // التخزين بمفتاح الصفحة الواردة نفسها: مفتاح لا يتغيّر مع تغيّر `page`.
  useEffect(() => {
    const data = query.data;
    if (!data) return;
    setLoaded((prev) => {
      const existing = prev.get(data.page);
      if (existing === data.result.items) return prev;
      const next = new Map(prev);
      next.set(data.page, data.result.items);
      return next;
    });
  }, [query.data]);

  const items = useMemo(() => {
    if (loaded.size === 0) return [];
    const pages = [...loaded.keys()].sort((a, b) => a - b);
    return pages.flatMap((pageNumber) => loaded.get(pageNumber) ?? []);
  }, [loaded]);

  const totalCount = query.data?.result.totalCount ?? 0;
  const hasMore = totalCount > items.length;
  const isLoading = queryIsLoading && page === 1;
  const isLoadingMore = queryIsLoading && page > 1;
  // آخر صفحة وصلت فعلًا — يمنع طلب صفحة تتجاوز التي لم تُحمّل بعد.
  const lastLoadedPage = query.data?.page ?? 0;

  const loadMore = useCallback(() => {
    if (queryIsLoading) return;
    // فشل الصفحة الحالية (رقمها > 1): إعادة المحاولة لنفسها لا للتي بعدها.
    if (queryError && page > 1) {
      refetch();
      return;
    }
    if (!hasMore) return;
    if (page > lastLoadedPage) return;
    setPage((current) => current + 1);
  }, [hasMore, lastLoadedPage, page, queryError, queryIsLoading, refetch]);

  const reload = useCallback(() => {
    setLoaded(new Map());
    setPage(1);
    refetch();
  }, [refetch]);

  const updateItems = useCallback(
    (updater: (prev: HeadAlertSummaryDto[]) => HeadAlertSummaryDto[]) => {
      setLoaded((prev) => {
        let changed = false;
        const next = new Map<number, HeadAlertSummaryDto[]>();
        for (const [pageNumber, rows] of prev) {
          const updated = updater(rows);
          if (updated !== rows) changed = true;
          next.set(pageNumber, updated);
        }
        return changed ? next : prev;
      });
    },
    [],
  );

  return { items, totalCount, hasMore, isLoading, isLoadingMore, error: queryError, loadMore, reload, updateItems };
}
