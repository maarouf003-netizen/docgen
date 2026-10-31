import { useState } from 'react';
import { api } from '../../api/client';
import { useCancellableRequest } from '../../hooks/useCancellableRequest';
import type { HeadAlertDto } from '../../types';

/**
 * كاشف النص الكامل للتنبيه (`D12` — المرحلة 8).
 *
 * القائمة تحمل `messagePreview` فقط (`HeadAlertSummaryDto`)، فالنص الكامل لا يوجد
 * على السلك أصلًا. هذا المكوّن لا يجلب شيئًا قبل فتح المستخدم: الجلب يبدأ عند أول
 * فتح عبر `GET /api/alerts/{id}` (العقد الوحيد الذي يحمل `message` كاملًا)، ويُعاد
 * الجلب عند كل فتح — النص قد يتغيّر على الخادم (`lastContentChangedAt`) فالعرض
 * المحفوظ يبقى ظاهرًا أثناء التحديث بلا وميض.
 *
 * لا يُعرض الزر إلا عند `previewTruncated = true` (قرار الخادم) — فالمعاينة غير
 * المقطوعة هي النص كاملًا، وجلبها طلب زائد. وحالة الجلب تُقرأ من حالة الاستعلام
 * نفسها (`error`/`data`) لا من نص الرسالة، فلا مؤشر تحميل أبدي لو كان النص فارغًا.
 */
export function AlertDetails({
  alertId,
  previewTruncated,
}: {
  alertId: number;
  /** هل المعاينة مقطوعة؟ (يحدده الخادم في `HeadAlertSummaryDto.previewTruncated`) */
  previewTruncated: boolean;
}) {
  const [open, setOpen] = useState(false);
  const detailsQuery = useCancellableRequest<HeadAlertDto>(
    (signal) => api.get<HeadAlertDto>(`/alerts/${alertId}`, { signal }).then((r) => r.data),
    [alertId],
    { enabled: open },
  );

  if (!previewTruncated) return null;

  const panelId = `alert-details-${alertId}`;

  return (
    <div className="mt-1.5">
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-controls={panelId}
        className="min-h-11 px-2 -mx-2 rounded-lg text-xs font-medium text-emerald-800 hover:bg-emerald-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600"
      >
        {open ? 'إخفاء النص الكامل' : 'عرض النص الكامل'}
      </button>
      {/* الحاوية تبقى في الشجرة كي يظل `aria-controls` صالحًا في الحالتين،
          لكن لا يُرسَم أي نص قبل الفتح (فلا ظهور لمعاينة ناقصة أو مؤشر كاذب). */}
      <div id={panelId}>
        {open && detailsQuery.error ? (
          <div className="mt-1">
            <p role="alert" className="text-sm text-red-700">
              {detailsQuery.error}
            </p>
            {/* فشل الجلب حالة قابلة للاستعادة بلا إغلاق/إعادة فتح مخفيين. */}
            <button
              type="button"
              onClick={() => detailsQuery.refetch()}
              className="min-h-11 px-2 -mx-2 rounded-lg text-xs font-medium text-emerald-800 hover:bg-emerald-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600"
            >
              إعادة المحاولة
            </button>
          </div>
        ) : open && detailsQuery.data ? (
          <p className="text-sm text-gray-800 leading-relaxed whitespace-pre-wrap break-words mt-1">
            {detailsQuery.data.message}
          </p>
        ) : open ? (
          <p className="text-sm text-gray-500 mt-1" role="status">
            جارٍ جلب النص الكامل…
          </p>
        ) : null}
      </div>
    </div>
  );
}
