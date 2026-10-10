import { useEffect, useState } from 'react';
import { api } from '../api/client';

export interface UseBadgeCountOptions {
  /** تفعيل الجلب (يُربط بالدور عادةً) — عند التعطيل يبقى العدّاد صفرًا. */
  enabled: boolean;
  /** إيقاع الاستطلاع بالميلي ثانية — الافتراضي 60 ثانية (إيقاع الأجراس الحالية). */
  intervalMs?: number;
  /** حدث نافذة اختياري يعيد الجلب فورًا (الحدث بلا حمولة — يُعاد الجلب فقط). */
  eventName?: string;
  /** شكل الاستجابة: `{ count }` أو قائمة يُحتسب طولها (بلا سقف ترقيم في نقطتي الرئيس). */
  shape: 'count' | 'list';
  /**
   * إيقاف الاستطلاع بخفاء التبويب (يُستأنف فور الظهور) — افتراضيًا مغلق عمدًا
   * لعزل سلوك الدعوات القائمة؛ فعّله للعدادات الجديدة فقط.
   */
  pauseWhenHidden?: boolean;
}

/**
 * عدّاد شارة لوحات التحكم: جلب عند التركيب + استطلاع دوري + تحديث حدثي
 * اختياري، مع الاحتفاظ بآخر قيمة معروفة عند فشل التحديث (لا كسر للوحة أبدًا).
 */
export function useBadgeCount(endpoint: string, options: UseBadgeCountOptions): number {
  const { enabled, intervalMs = 60_000, eventName, shape, pauseWhenHidden = false } = options;
  const [count, setCount] = useState(0);

  useEffect(() => {
    if (!enabled) {
      setCount(0);
      return undefined;
    }
    // مصدر جديد = عدّاد جديد: لا تُعرض قيمة المصدر السابق (ولو لحظة) ولا تبقى
    // للأبد عند فشل المصدر الجديد — إعادة الاشتراك لا تحدث إلا بتغيّر أحد
    // الاعتماديات البدائية، فالتصفير هنا آمن ولا يومض مع كل استطلاع.
    setCount(0);
    let cancelled = false;
    const isHidden = () =>
      pauseWhenHidden &&
      typeof document !== 'undefined' &&
      document.visibilityState === 'hidden';
    const fetchCount = () => {
      // الخفاء يجمّد الاستطلاع (لا يصفّر الشارة) — يُستأنف فور الظهور أدناه.
      if (isHidden()) return;
      if (shape === 'count') {
        api
          .get<{ count: number }>(endpoint)
          .then((r) => {
            const n = r.data?.count;
            if (!cancelled && typeof n === 'number' && n >= 0) setCount(n);
          })
          .catch(() => {
            /* الشارة تبقى على آخر قيمة معروفة عند فشل التحديث */
          });
      } else {
        api
          .get<unknown[]>(endpoint)
          .then((r) => {
            if (!cancelled && Array.isArray(r.data)) setCount(r.data.length);
          })
          .catch(() => {
            /* الشارة تبقى على آخر قيمة معروفة عند فشل التحديث */
          });
      }
    };
    void fetchCount();
    const timer = window.setInterval(fetchCount, intervalMs);
    if (eventName) window.addEventListener(eventName, fetchCount);
    const onVisible = () => {
      if (document.visibilityState === 'visible') fetchCount();
    };
    if (pauseWhenHidden) document.addEventListener('visibilitychange', onVisible);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
      if (eventName) window.removeEventListener(eventName, fetchCount);
      if (pauseWhenHidden) document.removeEventListener('visibilitychange', onVisible);
    };
  }, [endpoint, enabled, intervalMs, eventName, shape, pauseWhenHidden]);

  return count;
}
