import { useEffect } from 'react';
import { api } from '../../api/client';
import { readAlertRecipients } from '../../api/alerts';
import { useCancellableRequest } from '../../hooks/useCancellableRequest';
import type { HeadAlertRecipientDto } from '../../types';
import { ROLE_LABELS } from '../../auth/roleLabels';
import { formatDateTime } from '../../utils/dates';

/**
 * مستلمو تنبيه أصدره الرئيس/الشعبة وحالة قراءة كلٍّ منهم.
 *
 * **العقد قائم ولا يُخترع:** `GET /alerts/{id}/recipients` يعيد
 * `HeadAlertRecipientDto[]` للمنشئ أو المدير/المشرف فقط، وغير ذلك `404` بلا
 * تسريب وجود. لذلك يُمرَّر مدخل هذا الحوار من الصفحة وفق الدور/التبويب:
 * «مرسلة» للمُصدِر، و**القائمة الإشرافية** للمدير/المشرف (لا تبويب «مرسلة» له)،
 * ولا يُمرَّر للمحامي ولا لرئيس في «مستلمة». وأي `404`/`403` يظهر كخطأ معلن
 * مع «إعادة المحاولة» لا كقائمة فارغة كاذبة.
 */
export function RecipientsModal({
  alertId,
  onClose,
}: {
  /** معرّف التنبيه المفتوح، أو `null` حين لا حوار. */
  alertId: number | null;
  onClose: () => void;
}) {
  const recipientsQuery = useCancellableRequest<HeadAlertRecipientDto[]>(
    (signal) =>
      api
        .get(`/alerts/${alertId}/recipients`, { signal })
        .then((response) => readAlertRecipients(response.data)),
    [alertId],
    { enabled: alertId != null, resetOnDepsChange: true },
  );

  useEffect(() => {
    if (alertId == null) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [alertId, onClose]);

  if (alertId == null) return null;

  const headingId = `alert-recipients-title-${alertId}`;
  const recipients = recipientsQuery.data ?? [];
  const unread = recipients.filter((recipient) => !recipient.isRead).length;

  return (
    <div
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center bg-black/50 p-0 sm:p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby={headingId}
    >
      <div className="flex max-h-[85vh] w-full max-w-lg flex-col overflow-hidden rounded-t-2xl bg-white shadow-xl sm:rounded-2xl">
        <div className="flex items-start justify-between gap-3 border-b border-gray-200 px-4 py-3 sm:px-5">
          <div className="min-w-0">
            <h2 id={headingId} className="text-base font-bold text-gray-900">
              مستلمو التنبيه
            </h2>
            {!recipientsQuery.error && recipients.length > 0 ? (
              <p className="mt-0.5 text-xs text-gray-500 tabular-nums">
                غير مقروء: {unread} من {recipients.length}
              </p>
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

        <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain">
          {recipientsQuery.error ? (
            <div className="space-y-3 px-4 py-4 sm:px-5">
              <p role="alert" className="text-sm text-red-700">
                {recipientsQuery.error}
              </p>
              <button
                type="button"
                onClick={() => recipientsQuery.refetch()}
                className="min-h-11 rounded-lg border border-gray-300 px-4 text-sm font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600"
              >
                إعادة المحاولة
              </button>
            </div>
          ) : recipientsQuery.isLoading && recipients.length === 0 ? (
            <p className="px-4 py-6 text-center text-sm text-gray-500 sm:px-5" role="status">
              جارٍ تحميل المستلمين…
            </p>
          ) : recipients.length === 0 ? (
            <p className="px-4 py-6 text-center text-sm text-gray-500 sm:px-5">لا مستلمين لهذا التنبيه</p>
          ) : (
            <ul className="divide-y divide-gray-100">
              {recipients.map((recipient) => (
                <li key={recipient.id} className="flex items-start justify-between gap-3 px-4 py-3 sm:px-5">
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-gray-800">{recipient.fullName}</p>
                    <p className="text-xs text-gray-500 break-words">
                      {(ROLE_LABELS as Record<string, string>)[recipient.role] ?? recipient.role}
                    </p>
                  </div>
                  <div className="shrink-0 text-end">
                    {recipient.isRead ? (
                      <span className="rounded-full border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-[11px] text-emerald-800">
                        مقروء
                      </span>
                    ) : (
                      <span className="rounded-full border border-red-200 bg-red-50 px-2 py-0.5 text-[11px] text-red-700">
                        غير مقروء
                      </span>
                    )}
                    {recipient.isRead && recipient.readAt ? (
                      <p className="mt-1 text-[11px] text-gray-400 tabular-nums">
                        {formatDateTime(recipient.readAt)}
                      </p>
                    ) : null}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}
