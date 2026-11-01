import { useEffect } from 'react';
import { api } from '../../api/client';
import { readAlertDetail } from '../../api/alerts';
import { useCancellableRequest } from '../../hooks/useCancellableRequest';
import type { HeadAlertDto } from '../../types';
import { formatDateTime } from '../../utils/dates';
import { alertKindLabel, isKnownAlertKind } from './alertKinds';

/**
 * حوار نص التنبيه الكامل (`D12` — الجلب عند الطلب فقط).
 *
 * القوائم تحمل `messagePreview` وحدها (`HeadAlertSummaryDto`)، فالنص الكامل لا
 * يوجد على السلك قبل الفتح. هذا الحوار لا يُركَّب إلا عند اختيار صف
 * (`alertId != null`)، ويطلب `GET /alerts/{id}` عند الإنشاء فقط.
 *
 * **لماذا حوار لا كشف سطري؟** صفحة `/alerts` تعرض قوائم طويلة على الجوال،
 * والكشف السطري كان يدفع الصفوف ويُخرج زر الإخفاء من المشهد. الحوار قابل
 * للتمرير داخليًا (`overscroll-contain`) وبلا اقتصاص، ويُغلق بـ`Escape` أو
 * بالزر، ويُعيد التركيز إلى الصفحة طبيعيًا بإزالته من الشجرة.
 */
export function AlertDetailsSheet({
  alertId,
  onClose,
}: {
  /** معرّف التنبيه المفتوح، أو `null` حين لا حوار. */
  alertId: number | null;
  onClose: () => void;
}) {
  const detailsQuery = useCancellableRequest<HeadAlertDto>(
    (signal) => api.get(`/alerts/${alertId}`, { signal }).then((response) => readAlertDetail(response.data)),
    [alertId],
    // تصفير عند تبديل التنبيه: لا يظهر نص تنبيه سابق تحت عنوان جديد.
    { enabled: alertId != null, resetOnDepsChange: true },
  );

  // مخرج لوحة مفاتيح: `Escape` يغلق الحوار (قاعدة إمكانية الوصول).
  useEffect(() => {
    if (alertId == null) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [alertId, onClose]);

  if (alertId == null) return null;

  const headingId = `alert-details-title-${alertId}`;
  const detail = detailsQuery.data;

  return (
    <div
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center bg-black/50 p-0 sm:p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby={headingId}
    >
      <div className="flex max-h-[85vh] w-full max-w-2xl flex-col overflow-hidden rounded-t-2xl bg-white shadow-xl sm:rounded-2xl">
        <div className="flex items-start justify-between gap-3 border-b border-gray-200 px-4 py-3 sm:px-5">
          <div className="min-w-0">
            <h2 id={headingId} className="text-base font-bold text-gray-900">
              نص التنبيه
            </h2>
            {detail && isKnownAlertKind(detail.kind) ? (
              <p className="mt-0.5 text-xs text-gray-500 break-words">{alertKindLabel(detail.kind)}</p>
            ) : null}
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="إغلاق"
            className="min-h-11 min-w-11 shrink-0 rounded-lg text-xl leading-none text-gray-500 hover:bg-gray-100 hover:text-gray-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            ×
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-4 py-4 sm:px-5">
          {detailsQuery.error ? (
            <div className="space-y-3">
              <p role="alert" className="text-sm text-red-700">
                {detailsQuery.error}
              </p>
              <button
                type="button"
                onClick={() => detailsQuery.refetch()}
                className="min-h-11 rounded-lg border border-gray-300 px-4 text-sm font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600"
              >
                إعادة المحاولة
              </button>
            </div>
          ) : detail ? (
            <>
              <p className="whitespace-pre-wrap break-words text-sm leading-relaxed text-gray-800">
                {detail.message}
              </p>
              <dl className="mt-4 grid grid-cols-1 gap-2 border-t border-gray-100 pt-3 text-xs text-gray-500 sm:grid-cols-2">
                <div className="flex gap-1">
                  <dt className="font-medium text-gray-600">المرسِل:</dt>
                  <dd className="min-w-0 break-words">{detail.createdByName ?? 'رئيس القسم'}</dd>
                </div>
                <div className="flex gap-1">
                  <dt className="font-medium text-gray-600">التاريخ:</dt>
                  <dd className="tabular-nums">{formatDateTime(detail.createdAt)}</dd>
                </div>
                <div className="flex gap-1">
                  <dt className="font-medium text-gray-600">آخر تحديث للمحتوى:</dt>
                  <dd className="tabular-nums">{formatDateTime(detail.lastContentChangedAt)}</dd>
                </div>
                {detail.documentTitle ? (
                  <div className="flex min-w-0 gap-1">
                    <dt className="font-medium text-gray-600">الملف:</dt>
                    <dd className="min-w-0 break-words">{detail.documentTitle}</dd>
                  </div>
                ) : null}
              </dl>
            </>
          ) : (
            <p className="text-sm text-gray-500" role="status">
              جارٍ جلب النص الكامل…
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
