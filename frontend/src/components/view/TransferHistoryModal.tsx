import type { DocumentAssignmentDto } from '../../types';
import { formatDate } from '../../utils/dates';

/**
 * نافذة «سجل التعاقب على الملف»: منشئ الملف ثم كل محامٍ حُمّل عليه الملف مع تاريخ الإحالة
 * ومن قام بها — تُفتح عند الضغط على «المحامي المختص» في بطاقة بيانات الملف.
 */
export function TransferHistoryModal({
  assignments,
  onClose,
}: {
  assignments: DocumentAssignmentDto[];
  onClose: () => void;
}) {
  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="سجل التعاقب على الملف"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[85vh] overflow-y-auto">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 sticky top-0 bg-white">
          <h3 className="text-lg font-bold text-gray-800">سجل التعاقب على الملف</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4">
          {assignments.length === 0 && <p className="text-gray-400 text-sm">لا يوجد سجل تعاقب</p>}
          <ol className="relative border-r border-gray-200 mr-2">
            {assignments.map((a) => (
              <li key={a.id} className="mb-5 last:mb-0">
                <span className="absolute -right-[7px] top-1 h-3 w-3 rounded-full bg-emerald-700" aria-hidden="true" />
                <div className="flex items-center gap-2 mb-1">
                  <span
                    className={`inline-block rounded-full px-2 py-0.5 text-xs font-medium ${
                      a.kind === 'create' ? 'bg-sky-100 text-sky-800' : 'bg-emerald-100 text-emerald-800'
                    }`}
                  >
                    {a.kind === 'create' ? 'منشئ الملف' : 'إحالة'}
                  </span>
                  <span className="text-xs text-gray-400">{a.assignedAt ? formatDate(a.assignedAt) : ''}</span>
                </div>
                <p className="text-gray-800 text-sm font-medium">{a.lawyerName || '—'}</p>
                {a.kind === 'transfer' && a.assignedByName ? (
                  <p className="text-xs text-gray-500">أحالها: {a.assignedByName}</p>
                ) : null}
              </li>
            ))}
          </ol>
        </div>

        <div className="px-5 py-4 border-t border-gray-200 flex justify-end">
          <button
            onClick={onClose}
            className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
          >
            إغلاق
          </button>
        </div>
      </div>
    </div>
  );
}
