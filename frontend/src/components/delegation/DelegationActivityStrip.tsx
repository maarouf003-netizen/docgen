import { useEffect, useState } from 'react';
import { api } from '../../api/client';
import { readAlertsPage } from '../../api/alerts';
import { useCancellableRequest } from '../../hooks/useCancellableRequest';
import type { HeadAlertSummaryDto, PagedResult } from '../../types';
import { formatDateTime } from '../../utils/dates';

/** عدد المعاينات الظاهرة ابتداءً — بقيّة الصفحة تُكشف عند الطلب لا تُخفى صامتة. */
const INITIAL_VISIBLE = 3;

/** مقدار الكشف في كل ضغطة: يبقي DOM صغيرًا بدل إدراج 100 صف دفعة واحدة. */
const REVEAL_STEP = 20;

/**
 * شريط حالة الإنابة (تنبيهات المرآة/المتابعة): يُعرض في بطاقة «حالة الإنابة» للمناب
 * فقط (حُذف من بطاقة «تشعبات الملف» للمنيب توحيدًا مع سطر «بانتظار الإتمام» البنفسجي —
 * كل حالاته مغطاة هناك بالشارة والسطر والأزرار)؛ ويُغذّى من
 * GET /alerts/by-delegation/{id} المتاح لأطراف الإنابة فقط، ويعيد `PagedResult`
 * (`D1`) بملخصات بلا نص كامل — فالمعروض `messagePreview` وحدها.
 * الأحدث في الصدارة (معاينات ثلاثة ابتداءً)، والفراغ لا يظهر شيئًا.
 *
 * **لا قصّ صامت (الخطة §9.1، الدين التقني 3):** الصفحة الواحدة تحمل حتى 100 صف
 * (`AlertPageRequest.MaxPageSize`)، فإخفاء ما بعد الثالث كان يُفقد المستخدم أحداث
 * إنابة بلا أي إشارة. الآن: زر «عرض المزيد» يكشف بقية الصفحة تدريجيًا، وإن كان
 * الإجمالي (`totalCount`) يتجاوز المحمّل يُعلَن ذلك نصًا صريحًا.
 *
 * **لماذا لا رابط «عرض كل التنبيهات» هنا؟** لأن `/api/alerts?delegationId=` مقيَّد
 * بكون الفاعل **مستلمًا** للصف، بينما مسار `by-delegation` متاح لكل **طرف** في
 * الإنابة — فقد يعرض الرابط أقل مما يعرضه الشريط. إعلان السقف هنا أصدق من رابط
 * يُقصّ بهدوء؛ وتوسيع المسار (مؤشر + سطح كامل) عمل منفصل مسجَّل.
 *
 * **الخطأ لا يُكتَم:** فشل الجلب يُعلن في الشريط نفسه بخطابٍ صريح وزر «إعادة المحاولة»،
 * لأن الشريط ملحق اختياري في البطاقة — فإخفاؤه الصامت يُفقد المستخدم معلومة إنابة
 * بلا أي إشارة (خلاف مبدأ «لا صمت»).
 */
export function DelegationActivityStrip({ delegationId }: { delegationId: number }) {
  const query = useCancellableRequest<PagedResult<HeadAlertSummaryDto>>(
    (signal) =>
      api
        .get(`/alerts/by-delegation/${delegationId}`, { signal })
        .then((r) => readAlertsPage(r.data)),
    [delegationId],
  );

  // معرّف إنابة آخر في البطاقة نفسها يبدأ من العدد الابتدائي، لا يورث كشفًا سابقًا.
  const [visibleCount, setVisibleCount] = useState(INITIAL_VISIBLE);
  useEffect(() => {
    setVisibleCount(INITIAL_VISIBLE);
  }, [delegationId]);

  if (query.error) {
    return (
      <div className="mt-3 rounded-lg bg-red-50 border border-red-200 px-3 py-2">
        <span className="block text-[11px] font-bold text-red-800 mb-1">حالة الإنابة</span>
        <p role="alert" className="text-[11px] text-red-700">
          {query.error}
        </p>
        <button
          type="button"
          onClick={query.refetch}
          className="min-h-11 px-1 -mx-1 text-[11px] font-medium text-emerald-800 hover:underline focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600"
        >
          إعادة المحاولة
        </button>
      </div>
    );
  }

  const items = query.data?.items ?? [];
  if (items.length === 0) return null;

  const totalCount = query.data?.totalCount ?? items.length;
  const shown = items.slice(0, visibleCount);
  const hidden = items.length - shown.length;
  // السقف الخادمي (100) لا يُخفى: الإجمالي قد يتجاوز الصفحة المحمّلة.
  const unfetched = Math.max(0, totalCount - items.length);

  return (
    <div className="mt-3 rounded-lg bg-amber-50/70 border border-amber-200 px-3 py-2">
      <span className="block text-[11px] font-bold text-amber-800 mb-1">حالة الإنابة</span>
      <ul className="space-y-1">
        {shown.map((a) => (
          <li key={a.id} className="flex items-start justify-between gap-2 text-xs text-amber-900">
            <span className="min-w-0 break-words">{a.messagePreview}</span>
            <span className="shrink-0 text-[10px] text-amber-700 tabular-nums whitespace-nowrap">
              {formatDateTime(a.createdAt)}
            </span>
          </li>
        ))}
      </ul>

      {hidden > 0 ? (
        <button
          type="button"
          onClick={() => setVisibleCount((count) => count + REVEAL_STEP)}
          className="mt-2 min-h-11 rounded-lg border border-amber-300 bg-white/70 px-3 text-[11px] font-medium text-amber-900 hover:bg-amber-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-amber-600"
        >
          عرض المزيد
        </button>
      ) : null}

      {unfetched > 0 ? (
        <p className="mt-2 text-[10px] text-amber-700 tabular-nums">
          تُعرض أحدث {items.length} من أصل {totalCount} تنبيهًا
        </p>
      ) : null}
    </div>
  );
}
