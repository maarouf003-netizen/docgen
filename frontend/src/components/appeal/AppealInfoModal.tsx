import { appealStatusBadge } from '../../utils/appealStatus';
import AppealDetailsBody from './AppealDetailsBody';
import type { AppealDto } from '../../types';

/**
 * نافذة تفاصيل الاستئناف الكاملة — غلاف حواري فوق جسم التفاصيل المشترك
 * (`AppealDetailsBody`) المعاد استخدامه في صفحة تفاصيل الاستئناف أيضًا.
 */
export default function AppealInfoModal({
  appeal,
  onClose,
}: {
  appeal: AppealDto | null;
  onClose: () => void;
}) {
  if (!appeal) return null;

  const badge = appealStatusBadge(appeal.status);

  return (
    <div
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={`تفاصيل الاستئناف رقم ${appeal.id}`}
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4 overscroll-contain"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
        <div className="sticky top-0 bg-white flex justify-between items-start px-5 py-4 border-b border-gray-200 rounded-t-xl z-10">
          <h3 className="text-lg font-bold text-emerald-800 flex items-center gap-2 flex-wrap min-w-0">
            <span>استئناف قرار رئيس التنفيذ</span>
            <span className={`rounded-full px-3 py-1 text-sm ${badge.cls}`}>{badge.text}</span>
          </h3>
          <button
            type="button"
            onClick={onClose}
            aria-label="إغلاق"
            className="text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg w-11 h-11 inline-flex items-center justify-center text-xl focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4">
          <AppealDetailsBody appeal={appeal} />
        </div>
      </div>
    </div>
  );
}
