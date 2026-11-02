import type { AlertsFeed } from '../../hooks/useAlertsFeed';

/**
 * تذييل قائمة التنبيهات: يمنع القصّ الصامت (`pageSize=100`) بلا زر فقط.
 *
 * - فشل الصفحة الأولى (بلا عناصر): «إعادة المحاولة» تعيد التحميل من الأولى.
 * - فشل لاحق (وعناصر ظاهرة): المعروض يبقى، والزر يصبح «إعادة المحاولة» فيعيد
 *   محاولة الطلب نفسه، أو التحميل من الأولى حين يكون الفشل حتميًا (خرق عقد
 *   المؤشر أو توقّف التقدّم) — ويظهر حتى لو لم يبقَ «مزيد» (وإلا بقي الخطأ بلا مخرج).
 * - حالة سليمة وهناك مزيد: عدّاد «يُعرض N من أصل M» + «تحميل المزيد».
 * - اكتمل التحميل: لا تذييل.
 *
 * لا فرع لانقطاع الترقيم: الترقيم بالمؤشر (keyset) لا يزيح النافذة، فالصفحة
 * التالية تبدأ دائمًا بعد آخر صف معروض بلا تكرار ولا فجوة. ويبقى حارس تقدّم
 * واحد: صفحة مؤشر بلا صف جديد تُعلنها `useAlertsFeed` عبر `error` فيظهر الزر.
 */
export function AlertsFeedFooter({ feed }: { feed: AlertsFeed }) {
  if (feed.error && feed.items.length === 0) {
    return (
      <div className="px-4 sm:px-5 py-3 border-t border-gray-100">
        <button
          type="button"
          onClick={feed.reload}
          className="min-h-11 px-3 rounded-lg border border-gray-200 text-xs font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600"
        >
          إعادة المحاولة
        </button>
      </div>
    );
  }

  if (!feed.hasMore && !feed.isLoadingMore && !feed.error) return null;

  return (
    <div className="flex flex-wrap items-center justify-between gap-2 px-4 sm:px-5 py-3 border-t border-gray-100">
      <span className="text-xs text-gray-500 tabular-nums">
        يُعرض {feed.items.length} من أصل {feed.totalCount} تنبيهًا
      </span>
      {feed.error || feed.hasMore ? (
        <button
          type="button"
          onClick={feed.loadMore}
          disabled={feed.isLoadingMore}
          className="min-h-11 px-3 rounded-lg border border-gray-200 text-xs font-medium text-emerald-800 hover:bg-emerald-50 disabled:opacity-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600"
        >
          {feed.isLoadingMore ? 'جارٍ التحميل…' : feed.error ? 'إعادة المحاولة' : 'تحميل المزيد'}
        </button>
      ) : null}
    </div>
  );
}
