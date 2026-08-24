import { Link } from 'react-router-dom';
import type { HeadAlertDto } from '../../types';
import { TARGET_TYPE_BADGES, appealAwareTypeLabel } from './dashboardFormat';
import { formatDateTime } from '../../utils/dates';

/**
 * صف تنبيه واحد. يظهر زر «تمت القراءة» للمحامي فقط عبر onMarkRead،
 * وتظهر العدادات (غير مقروء/المجموع) لرئيس القسم فقط عند توفرها.
 */
export function AlertRow({
  alert,
  onMarkRead,
  markingKey,
}: {
  alert: HeadAlertDto;
  onMarkRead?: (a: HeadAlertDto) => void;
  markingKey?: string | null;
}) {
  const isMarking = markingKey === String(alert.id);
  return (
    <li key={alert.id}>
      <div className="flex items-center gap-3 px-4 sm:px-5 py-3">
        <span
          className={`shrink-0 w-2 h-2 rounded-full ${alert.isRead ? 'bg-gray-300' : 'bg-red-500'}`}
          aria-hidden="true"
        />
        <div className="min-w-0 flex-1">
          {/* التنبيهات المرتبطة بكتاب مطالعة تفتح الكتاب، وباستئناف تفتح تفاصيله، وسواهر تفتح الملف. */}
          {alert.reviewLetterId ? (
            <Link
              to={`/reviews/${alert.reviewLetterId}`}
              className="block font-medium text-gray-800 hover:text-emerald-700 truncate"
            >
              {alert.message}
            </Link>
          ) : alert.appealId ? (
            <Link
              to={`/appeals/${alert.appealId}`}
              className="block font-medium text-gray-800 hover:text-emerald-700 truncate"
            >
              {alert.message}
            </Link>
          ) : alert.documentId ? (
            <Link
              to={`/documents/${alert.documentId}`}
              className="block font-medium text-gray-800 hover:text-emerald-700 truncate"
            >
              {alert.message}
            </Link>
          ) : (
            <p className="font-medium text-gray-800 leading-snug">{alert.message}</p>
          )}
          <p className="text-sm text-gray-500 mt-0.5 truncate">
            {alert.createdByName ?? 'رئيس القسم'} · {formatDateTime(alert.createdAt)}
          </p>
          <div className="flex flex-wrap gap-1.5 mt-1.5">
            <span
              className={`text-[11px] px-2 py-0.5 rounded-full border ${
                TARGET_TYPE_BADGES[alert.targetType] ?? 'bg-gray-100 text-gray-700 border-gray-200'
              }`}
            >
              {appealAwareTypeLabel(alert)}
            </span>
            {alert.targetLawyerName ? (
              <span className="text-[11px] px-2 py-0.5 rounded-full bg-gray-100 text-gray-600 border border-gray-200">
                إلى: {alert.targetLawyerName}
              </span>
            ) : null}
          </div>
        </div>
        <div className="shrink-0 flex flex-col items-end gap-1.5">
          {onMarkRead && !alert.isRead ? (
            <button
              type="button"
              onClick={() => onMarkRead(alert)}
              disabled={isMarking}
              className="min-h-11 px-3 rounded-lg text-xs font-medium border border-gray-200 text-gray-600 hover:text-emerald-700 hover:border-emerald-200 hover:bg-emerald-50 disabled:opacity-50 transition-colors"
            >
              {isMarking ? 'جارِ التحديث...' : 'تمت القراءة'}
            </button>
          ) : onMarkRead && alert.isRead ? (
            <span className="text-[11px] px-2 py-0.5 rounded-full bg-gray-100 text-gray-500 border border-gray-200">
              مقروء
            </span>
          ) : null}
          {alert.recipientCount != null ? (
            <span className="text-[11px] px-2 py-0.5 rounded-full border border-gray-200 text-gray-600">
              غير مقروء: {alert.unreadCount ?? 0} / {alert.recipientCount}
            </span>
          ) : null}
        </div>
      </div>
    </li>
  );
}
