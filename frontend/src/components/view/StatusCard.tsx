import type { DocumentResponse } from '../../types';
import { SectionCard } from './SectionCard';
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
    <SectionCard title="الحالة">
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <p className="text-gray-800 flex-1 min-w-0 rounded-lg bg-gray-50 border border-gray-100 px-3 py-2.5">
          {buildStatusSummary(doc)}
        </p>
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
    </SectionCard>
  );
}