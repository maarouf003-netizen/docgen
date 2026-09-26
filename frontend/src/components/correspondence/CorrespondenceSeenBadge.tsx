import type { CorrespondenceViewStatus } from '../../types';
import { CORRESPONDENCE_VIEW_STATUS_LABELS } from './correspondenceDisplay';

/**
 * شارة اطلاع الطرف المستلم — تظهر بجانب شارة الأهمية في القائمة وبطاقة الملف.
 * `pending` نابضة لأنها وحدها ما يستدعي فعلًا من المستلم (توثيق)، أما المرسل
 * والمدير فحالتهما معلوماتية («بانتظار المشاهدة») بلا نابض ولا زر.
 * والحالة من الخادم: `canMarkSeen` وحده يقرر وجود أسلوب التنبيه.
 */
export default function CorrespondenceSeenBadge({
  viewStatus,
  canMarkSeen = false,
}: {
  viewStatus: CorrespondenceViewStatus;
  canMarkSeen?: boolean;
}) {
  const pending = viewStatus === 'pending';
  if (pending && canMarkSeen) {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-red-600 text-white px-2.5 py-0.5 text-[11px] font-bold whitespace-nowrap">
        <span
          className="w-1.5 h-1.5 rounded-full bg-white motion-safe:animate-pulse"
          aria-hidden="true"
        />
        {CORRESPONDENCE_VIEW_STATUS_LABELS.pending}
      </span>
    );
  }
  if (pending) {
    return (
      <span className="inline-flex items-center rounded-full bg-amber-100 text-amber-800 border border-amber-200 px-2.5 py-0.5 text-[11px] font-bold whitespace-nowrap">
        {CORRESPONDENCE_VIEW_STATUS_LABELS.pending}
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 px-2.5 py-0.5 text-[11px] font-bold whitespace-nowrap">
      <span aria-hidden="true">✓</span>
      {CORRESPONDENCE_VIEW_STATUS_LABELS.seen}
    </span>
  );
}
