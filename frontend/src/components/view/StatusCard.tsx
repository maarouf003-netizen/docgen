import type { DocumentResponse } from '../../types';
import { buildStatusSummary } from './viewFormat';

/**
 * بطاقة «الحالة» الموحّدة في تفاصيل الملف: تُعرض لملفات «طالبة تنفيذ» وعائلة «منفذ عليه»
 * على السواء، وتحمل زر «تغيير الحالة» داخلَها لمن يملك صلاحية التغيير (المحامي) فقط.
 */
export function StatusCard({
  doc,
  canChangeStatus,
  onOpenStatus,
}: {
  doc: DocumentResponse;
  canChangeStatus: boolean;
  onOpenStatus: () => void;
}) {
  return (
    <div className="bg-white rounded-xl shadow p-5">
      <h3 className="font-bold text-emerald-800 mb-3">الحالة</h3>
      <div className="flex items-center justify-between gap-3">
        <p className="text-gray-800 flex-1 min-w-0">{buildStatusSummary(doc)}</p>
        {canChangeStatus && (
          <button
            type="button"
            onClick={onOpenStatus}
            className="shrink-0 bg-blue-700 hover:bg-blue-600 text-white rounded-lg px-4 py-2 text-sm min-h-11"
          >
            تغيير الحالة
          </button>
        )}
      </div>
    </div>
  );
}
