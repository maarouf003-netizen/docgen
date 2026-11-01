import type { HeadAlertSummaryDto } from '../../types';
import { AlertRow } from './AlertRow';

/**
 * جسم قائمة التنبيهات في صفحة `/alerts` — بلا حاوية تمرير وبلا تذييل:
 * الصفحة نفسها تمرّر، و`AlertsFeedFooter` يُركَّب أسفل القائمة.
 *
 * الحالات ثلاث صريحة كما في اللوحة القديمة: تحميل أول (لا «لا توجد تنبيهات»
 * كاذبة)، فراغ مشروع، أو الصفوف. و**لا فراغ تحت خطأ**: الفشل حالة جلب لا
 * دليل على انعدام البيانات، فيُترك الإعلان للافتة وتذييل «إعادة المحاولة».
 */
export function AlertList({
  alerts,
  isLoading,
  error,
  sentView = false,
  onMarkRead,
  markingKey,
  onOpen,
  onOpenRecipients,
  emptyText = 'لا توجد تنبيهات في هذا النطاق',
}: {
  alerts: HeadAlertSummaryDto[];
  isLoading: boolean;
  error: string | null;
  sentView?: boolean;
  onMarkRead?: (a: HeadAlertSummaryDto) => void;
  markingKey?: string | null;
  onOpen?: (a: HeadAlertSummaryDto) => void;
  onOpenRecipients?: (a: HeadAlertSummaryDto) => void;
  emptyText?: string;
}) {
  if (alerts.length === 0 && isLoading) {
    return (
      <div className="p-10 text-center">
        <p className="text-sm text-gray-500" role="status">
          جارٍ تحميل التنبيهات…
        </p>
      </div>
    );
  }

  if (alerts.length === 0) {
    if (error) return null;
    return (
      <div className="p-10 text-center">
        <p className="text-sm text-gray-400">{emptyText}</p>
      </div>
    );
  }

  return (
    <ul className="divide-y divide-gray-100">
      {alerts.map((alert) => (
        <AlertRow
          key={alert.id}
          alert={alert}
          sentView={sentView}
          onMarkRead={onMarkRead}
          markingKey={markingKey}
          onOpen={onOpen}
          onOpenRecipients={sentView ? onOpenRecipients : undefined}
        />
      ))}
    </ul>
  );
}
