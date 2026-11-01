import { Link } from 'react-router-dom';
import type { HeadAlertSummaryDto } from '../../types';
import { TARGET_TYPE_BADGES, appealAwareTypeLabel } from './dashboardFormat';
import { formatDateTime } from '../../utils/dates';
import { AlertDetails } from './AlertDetails';
import { alertKindLabel, alertRoute, isKnownAlertKind } from './alertKinds';

/**
 * صف تنبيه واحد من عقد الملخص (`HeadAlertSummaryDto`): يعرض `messagePreview` وحدها،
 * والنص الكامل يُجلب عند الطلب من `AlertDetails` (`D12`). يظهر زر «تمت القراءة»
 * للمحامي فقط عبر onMarkRead، وتظهر العدادات (غير مقروء/المجموع) لرئيس القسم فقط.
 * في عرض الصادر (`sentView` — تبويب «أرسلتها») تُستبدل شارة «مقروء» بالعدّاد الصفري
 * حتى لا يُقرأ «غير مقروء: 0» كغير مقروء، وتُحذف نقطة القراءة كليًا لأن `isRead`
 * حالة **المستلم** لا المُرسل (قيمتها `null` هنا).
 *
 * **لا `truncate` على نص التنبيه:** قصّ CSS لسطر واحد كان يخفي نهاية أي معاينة
 * غير مقطوعة من الخادم (`previewTruncated=false`) بلا أي زر «عرض النص الكامل» —
 * أي قصًّا صامتًا من نوع العيب الذي تُصلحه هذه المرحلة. المعاينة تُلَفّ كاملة
 * (`break-words`)، والمرجع الوحيد لـ«يوجد نص أطول» هو `previewTruncated` من الخادم.
 */
export function AlertRow({
  alert,
  onMarkRead,
  markingKey,
  sentView = false,
  onOpen,
  onOpenRecipients,
}: {
  alert: HeadAlertSummaryDto;
  onMarkRead?: (a: HeadAlertSummaryDto) => void;
  markingKey?: string | null;
  sentView?: boolean;
  /**
   * فتح النص الكامل في حوار (صفحة `/alerts`). تمريره يُلغي الكشف السطري
   * (`AlertDetails`): وظيفة واحدة بمسار واحد، والحوار هو المسار على الصفحة.
   */
  onOpen?: (a: HeadAlertSummaryDto) => void;
  /** فتح قائمة المستلمين وحالة قراءتهم — في عرض الصادر فقط. */
  onOpenRecipients?: (a: HeadAlertSummaryDto) => void;
}) {
  const isMarking = markingKey === String(alert.id);
  // الوجهة من الكتالوج الدلالي، ثم سلسلة احتياط المعرّفات (AlertKinds.alertRoute).
  const href = alertRoute(alert);
  return (
    <li>
      <div className="flex items-start gap-3 px-4 sm:px-5 py-3">
        {/* نقطة القراءة **تُحذف** في عرض الصادر: `isRead` حالة المستلم وهي `null` للمُرسل،
            فرسمها حمراء يقرأ «غير مقروء» كاذبًا، وإبقاؤها شفافة يحجز فراغًا بلا معنى.
            حالة المستلمين يعلنها عدّاد الصف («غير مقروء: N / M» / «مقروء من الجميع»). */}
        {sentView ? null : (
          <span
            className={`shrink-0 w-2 h-2 mt-2 rounded-full ${alert.isRead ? 'bg-gray-300' : 'bg-red-500'}`}
            aria-hidden="true"
          />
        )}
        <div className="min-w-0 flex-1">
          {href ? (
            <Link to={href} className="block font-medium text-gray-800 hover:text-emerald-700 break-words">
              {alert.messagePreview}
            </Link>
          ) : (
            <p className="font-medium text-gray-800 leading-snug break-words">{alert.messagePreview}</p>
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
            {/* تسمية النوع من الكتالوج — تُعرض للمعروف فقط: الرمز المجهول بلا تسمية
                صادقة، وإظهار «تنبيه» العامة لكل صف تاريخي لا يضيف معلومة. */}
            {isKnownAlertKind(alert.kind) ? (
              <span className="text-[11px] px-2 py-0.5 rounded-full bg-slate-50 text-slate-600 border border-slate-200 break-words">
                {alertKindLabel(alert.kind)}
              </span>
            ) : null}
            {alert.targetLawyerName ? (
              <span className="text-[11px] px-2 py-0.5 rounded-full bg-gray-100 text-gray-600 border border-gray-200">
                إلى: {alert.targetLawyerName}
              </span>
            ) : null}
          </div>
          {/* الكشف السطري للمعاينات المقطوعة — داخل اللوحة القديمة. على الصفحة
              يُمرَّر `onOpen` فيُستبدل بالحوار كي لا تتكرر الوظيفة بمسارين. */}
          {alert.previewTruncated && !onOpen ? (
            <AlertDetails alertId={alert.id} previewTruncated />
          ) : null}
        </div>
        <div className="shrink-0 flex flex-col items-end gap-1.5">
          {onOpen ? (
            <button
              type="button"
              onClick={() => onOpen(alert)}
              className="min-h-11 px-3 rounded-lg text-xs font-medium border border-gray-200 text-gray-600 hover:text-emerald-700 hover:border-emerald-200 hover:bg-emerald-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600"
            >
              عرض التنبيه
            </button>
          ) : null}
          {onOpenRecipients && alert.recipientCount != null ? (
            <button
              type="button"
              onClick={() => onOpenRecipients(alert)}
              className="min-h-11 px-3 rounded-lg text-xs font-medium border border-gray-200 text-gray-600 hover:text-emerald-700 hover:border-emerald-200 hover:bg-emerald-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600"
            >
              المستلمون
            </button>
          ) : null}
          {onMarkRead && !alert.isRead ? (
            <button
              type="button"
              onClick={() => onMarkRead(alert)}
              disabled={isMarking}
              className="min-h-11 px-3 rounded-lg text-xs font-medium border border-gray-200 text-gray-600 hover:text-emerald-700 hover:border-emerald-200 hover:bg-emerald-50 disabled:opacity-50 transition-colors"
            >
              {isMarking ? 'جارٍ التحديث…' : 'تمت القراءة'}
            </button>
          ) : onMarkRead && alert.isRead ? (
            <span className="text-[11px] px-2 py-0.5 rounded-full bg-gray-100 text-gray-500 border border-gray-200">
              مقروء
            </span>
          ) : null}
          {alert.recipientCount != null && !(sentView && (alert.unreadCount ?? 0) === 0) ? (
            <span className="text-[11px] px-2 py-0.5 rounded-full border border-gray-200 text-gray-600 tabular-nums">
              غير مقروء: {alert.unreadCount ?? 0} / {alert.recipientCount}
            </span>
          ) : null}
          {sentView && alert.recipientCount != null && (alert.unreadCount ?? 0) === 0 ? (
            <span className="text-[11px] px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-800 border border-emerald-200">
              مقروء من الجميع
            </span>
          ) : null}
        </div>
      </div>
    </li>
  );
}
