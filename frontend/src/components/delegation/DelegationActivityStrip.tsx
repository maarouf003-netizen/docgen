import { api } from '../../api/client';
import { readAlertsPage } from '../../api/alerts';
import { useCancellableRequest } from '../../hooks/useCancellableRequest';
import type { HeadAlertSummaryDto, PagedResult } from '../../types';
import { formatDateTime } from '../../utils/dates';

/**
 * شريط حالة الإنابة (تنبيهات المرآة/المتابعة): يُعرض في بطاقة «حالة الإنابة» للمناب
 * فقط (حُذف من بطاقة «تشعبات الملف» للمنيب توحيدًا مع سطر «بانتظار الإتمام» البنفسجي —
 * كل حالاته مغطاة هناك بالشارة والسطر والأزرار)؛ ويُغذّى من
 * GET /alerts/by-delegation/{id} المتاح لأطراف الإنابة فقط، ويعيد `PagedResult`
 * (`D1`) بملخصات بلا نص كامل — فالمعروض `messagePreview` وحدها.
 * الأحدث في الصدارة (بحد أقصى ثلاثة)، والفراغ لا يظهر شيئًا.
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

  const alerts = (query.data?.items ?? []).slice(0, 3);
  if (alerts.length === 0) return null;

  return (
    <div className="mt-3 rounded-lg bg-amber-50/70 border border-amber-200 px-3 py-2">
      <span className="block text-[11px] font-bold text-amber-800 mb-1">حالة الإنابة</span>
      <ul className="space-y-1">
        {alerts.map((a) => (
          <li key={a.id} className="flex items-start justify-between gap-2 text-xs text-amber-900">
            <span className="min-w-0 break-words">{a.messagePreview}</span>
            <span className="shrink-0 text-[10px] text-amber-700 tabular-nums whitespace-nowrap">
              {formatDateTime(a.createdAt)}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
