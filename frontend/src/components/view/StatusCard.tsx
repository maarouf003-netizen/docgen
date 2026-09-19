import type { DocumentResponse } from '../../types';
import { SectionCard } from './SectionCard';
import { buildStatusSummary } from './viewFormat';

/**
 * بطاقة «الحالة» الموحّدة في تفاصيل الملف: تُعرض لملفات «طالبة تنفيذ» وعائلة «منفذ عليه»
 * على السواء، وتحمل زر «تغيير الحالة» داخلَها لمن يملك صلاحية التغيير (المحامي) فقط،
 * وزر «إتمام الإنابة» بجانبه للملف المناب المتداول فحسب (N11/و4 — L8).
 */
export function StatusCard({
  doc,
  canChangeStatus,
  onOpenStatus,
  canCompleteDelegation = false,
  onCompleteDelegation,
  delegationSourceLabel,
}: {
  doc: DocumentResponse;
  canChangeStatus: boolean;
  onOpenStatus: () => void;
  /** زر «إتمام الإنابة»: يُعرض بجانب «تغيير الحالة» لمناب متداول مسجل أصولًا فقط (N11). */
  canCompleteDelegation?: boolean;
  onCompleteDelegation?: () => void;
  /** اسم الملف المنيب (للمناب) لملخص حالة الإنابة (L5) — يُمرَّر إن وُجد الغرضُ. */
  delegationSourceLabel?: string | null;
}) {
  return (
    <SectionCard title="الحالة">
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <p className="text-gray-800 flex-1 min-w-0 rounded-lg bg-gray-50 border border-gray-100 px-3 py-2.5">
          {buildStatusSummary(doc, { sourceLabel: delegationSourceLabel })}
        </p>
        {(canCompleteDelegation || canChangeStatus) && (
          <div className="flex gap-2 flex-wrap shrink-0">
            {canCompleteDelegation && (
              <button
                type="button"
                onClick={onCompleteDelegation}
                className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                إتمام الإنابة
              </button>
            )}
            {canChangeStatus && (
              <button
                type="button"
                onClick={onOpenStatus}
                className="bg-blue-700 hover:bg-blue-600 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                تغيير الحالة
              </button>
            )}
          </div>
        )}
      </div>
    </SectionCard>
  );
}