import { Row } from './Row';
import type { PartyModal } from './viewTypes';

/** نافذة منبثقة بتفاصيل منفذٍ عليه (هوية كاملة) أو ورثة المتوفى أو جهة عامة. */
export function PartyDetailsModal({ modal, onClose }: { modal: PartyModal; onClose: () => void }) {
  const title =
    modal.kind === 'person'
      ? modal.title
      : modal.kind === 'entity'
        ? 'الجهة العامة'
        : `ورثة المتوفى (${modal.deceasedName})`;

  // صفوف الشخص الطبيعي المعروضة: المدخلة فعلاً فقط (القيم الفارغة/البيضاء لا تُعرض).
  const visibleRows = modal.kind === 'person' ? modal.rows.filter((row) => nonEmpty(row.value)) : [];

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={title}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[85vh] flex flex-col">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 shrink-0">
          <h3 className="text-lg font-bold text-gray-800">{title}</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4 overflow-y-auto">
          {modal.kind === 'person' &&
            (visibleRows.length === 0 ? (
              <p className="text-gray-400 text-sm">لا توجد بيانات مدخلة لهذا الطرف</p>
            ) : (
              visibleRows.map((row) => <Row key={row.label} label={row.label} value={row.value} />)
            ))}

          {modal.kind === 'entity' && (
            <>
              {nonEmpty(modal.name) && <Row label="اسم الجهة" value={modal.name} />}
              {nonEmpty(modal.branch) && <Row label="الفرع" value={modal.branch} />}
              {nonEmpty(modal.governorate) && <Row label="المحافظة" value={modal.governorate} />}
            </>
          )}

          {modal.kind === 'heirs' && (
            <div>
              {modal.lines.length === 0 && <p className="text-gray-400 text-sm">لا يوجد ورثة</p>}
              {modal.lines.map((line, i) => (
                <div key={i} className="py-2 border-b border-gray-100 last:border-0 text-sm text-gray-800">
                  {line.detail ? `${line.name} — ${line.detail}` : line.name}
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="px-5 py-4 border-t border-gray-200 flex justify-end shrink-0">
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

/** القيمة «مُدخلة» فعلاً: أي قيمة غير الخالية/البياض تُعدّ معلومات يعرضها؛ وإلا تُهمل. */
function nonEmpty(value?: string): boolean {
  return (value ?? '').trim() !== '';
}
