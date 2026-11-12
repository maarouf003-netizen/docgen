import type { DocumentInterventionDto } from '../../types';
import { formatDate } from '../../utils/dates';
import { useFocusTrap } from '../../hooks/useFocusTrap';
import { Row } from './Row';
import {
  buildHeirLines,
  legalPartyRows,
  personRows,
  representativeRows,
} from './viewFormat';

/**
 * نافذة تفاصيل المتدخل (عرض فقط): هوية الطرف حسب صفته مع العنوان/الوكيل والورثة
 * والممثل الشرعي، ثم بيانات طلب التدخل الخمسة. تُفتح من بطاقة الأطراف ومن
 * سطر «طلب تدخل» البنفسجي في وقوعات الملف.
 */
export function InterventionDetailsModal({
  intervention,
  onClose,
}: {
  intervention: DocumentInterventionDto;
  onClose: () => void;
}) {
  const title = `المتدخل — ${intervention.natureLabel || ''}`.trim();
  const dialogRef = useFocusTrap<HTMLDivElement>();
  const heirs = intervention.nature === 'natural' ? buildHeirLines(intervention.heirs) : [];
  const identityRows =
    intervention.nature === 'legal'
      ? legalPartyRows({
          name: intervention.entityName,
          registrationNumber: intervention.registrationNumber,
          representedBy: intervention.representedBy,
          addressType: intervention.addressType,
          address: intervention.address,
          governorate: intervention.governorate,
        })
      : intervention.nature === 'public'
        ? [
            { label: 'اسم الجهة العامة', value: intervention.entityName ?? '' },
            { label: 'الفرع', value: intervention.entityBranch ?? '' },
            { label: 'المحافظة', value: intervention.governorate ?? '' },
          ]
        : [
            ...personRows({
              name: intervention.name,
              father: intervention.father,
              family: intervention.family,
              mother: intervention.mother,
              birth: intervention.birth,
              register: intervention.register,
              nationalId: intervention.nationalId,
              addressType: intervention.addressType,
              address: intervention.address,
            }),
            ...representativeRows(intervention),
          ];

  return (
    <div
      className="fixed inset-0 bg-black/50 z-50 flex items-end sm:items-center justify-center sm:p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={title}
    >
      <div ref={dialogRef} className="bg-white w-full sm:max-w-2xl max-h-[95vh] sm:max-h-[90vh] rounded-t-2xl sm:rounded-xl shadow-xl flex flex-col overscroll-contain">
        <div className="flex items-start justify-between gap-3 px-4 sm:px-5 py-3 border-b border-gray-200 shrink-0">
          <div className="min-w-0">
            <h3 className="text-lg font-bold text-gray-800">{title}</h3>
            <p className="text-sm text-gray-600 break-words">{intervention.displayName || '—'}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-2xl leading-none px-2 min-h-11 min-w-11 rounded-lg focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-4 sm:px-5 py-4 overflow-y-auto">
          <section aria-label="بيانات المتدخل">
            <h4 className="text-sm font-bold text-emerald-800 mb-1">بيانات المتدخل</h4>
            {identityRows.map((row) => (
              <Row key={row.label} label={row.label} value={row.value} />
            ))}

            {heirs.length > 0 && (
              <div className="mt-3">
                <h5 className="text-xs font-bold text-gray-600 mb-1">ورثة المتدخل</h5>
                <div className="rounded-lg border border-gray-200 px-3 py-1">
                  {heirs.map((line, i) => (
                    <div
                      key={`${line.name}-${i}`}
                      className="py-2 border-b border-gray-100 last:border-0 text-sm text-gray-800 break-words"
                    >
                      {line.detail ? `${line.name} — ${line.detail}` : line.name}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {intervention.nature === 'natural' && heirs.length === 0 && (
              <p className="text-xs text-gray-400 mt-1">لا يوجد ورثة مسجلون لهذا المتدخل.</p>
            )}
          </section>

          <section aria-label="بيانات طلب التدخل" className="mt-5">
            <h4 className="text-sm font-bold text-purple-700 mb-1">بيانات طلب التدخل</h4>
            <Row label="رقم ورود طلب التدخل" value={intervention.receiptNumber} />
            <Row
              label="تاريخ ورود طلب التدخل"
              value={intervention.receiptDate ? formatDate(intervention.receiptDate) : undefined}
            />
            <Row
              label="تاريخ قبول طلب التدخل"
              value={
                intervention.acceptanceDate ? formatDate(intervention.acceptanceDate) : undefined
              }
            />
            <TextRow label="موضوع التدخل" value={intervention.subject} />
            <TextRow label="ملاحظات" value={intervention.notes} />
          </section>
        </div>

        <div className="px-4 sm:px-5 py-3 border-t border-gray-200 flex justify-end shrink-0">
          <button
            type="button"
            onClick={onClose}
            className="w-full sm:w-auto border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
          >
            إغلاق
          </button>
        </div>
      </div>
    </div>
  );
}

/** نص طويل متعدد الأسطر (الموضوع/الملاحظات) — يحافظ على فواصل الأسطر ولا يتجاوز الحاوية. */
function TextRow({ label, value }: { label: string; value?: string }) {
  const text = (value ?? '').trim();
  if (!text) return null;
  return (
    <div className="py-2 border-b border-gray-100 last:border-0">
      <span className="text-gray-500 text-xs block">{label}</span>
      <span className="text-gray-800 whitespace-pre-line break-words">{text}</span>
    </div>
  );
}
