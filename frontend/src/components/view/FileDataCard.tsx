import type { DocumentResponse } from '../../types';
import { formatDate } from '../../utils/dates';
import { isExecutedLike } from '../../utils/documentDisplay';
import { Row } from './Row';
import { RowTriple } from './RowTriple';
import { formatFileNumber, formatPaidAmounts } from './viewFormat';

/** بطاقة «بيانات الملف» الموحّدة: دائرة التنفيذ، الفرع/المحامي حسب الصلاحية، رقم الملف وأرقام الأساس، وحقول كل صفة. */
export function FileDataCard({
  doc,
  isLawyer,
  showBranch,
  showLawyer,
  onOpenBaseNumbers,
  onOpenAssignments,
}: {
  doc: DocumentResponse;
  isLawyer: boolean;
  showBranch: boolean;
  showLawyer: boolean;
  onOpenBaseNumbers: () => void;
  onOpenAssignments: () => void;
}) {
  const isExecuted = isExecutedLike(doc.generalEntitySide);
  const fileNumber = formatFileNumber(doc);
  const paidAmounts = isExecuted ? formatPaidAmounts(doc) : '';

  return (
    <div className="bg-white rounded-xl shadow p-5">
      <h3 className="font-bold text-emerald-800 mb-3">بيانات الملف</h3>

      <Row label="دائرة التنفيذ المختصة" value={doc.court} />
      {showBranch && <Row label="فرع الملف" value={doc.branchName} />}

      {showLawyer ? (
        <div className="py-2 border-b border-gray-100">
          <span className="text-gray-500 text-xs block">المحامي المختص</span>
          <button
            type="button"
            onClick={onOpenAssignments}
            aria-label="عرض سجل التعاقب على الملف"
            className="w-full flex items-center justify-between gap-3 text-right text-emerald-800 font-medium text-sm hover:underline min-h-11"
          >
            <span>{doc.lawyer || '—'}</span>
            <span className="text-gray-400 text-sm shrink-0" aria-hidden="true">←</span>
          </button>
        </div>
      ) : (
        isLawyer &&
        doc.referredFromLawyer &&
        doc.referredAt && (
          <div className="py-2 border-b border-gray-100 bg-amber-50 rounded px-3 -mx-3 mt-1">
            <span className="text-amber-800 text-xs block font-medium">إحالة الملف</span>
            <span className="text-amber-900 text-sm">
              أُحيل لك هذا الملف من {doc.referredFromLawyer} بتاريخ {formatDate(doc.referredAt)}
            </span>
          </div>
        )
      )}

      <div className="py-2 border-b border-gray-100">
        <span className="text-gray-500 text-xs block">رقم الملف ونوعه لعام {doc.fileYear}</span>
        {fileNumber ? (
          <button
            type="button"
            onClick={onOpenBaseNumbers}
            aria-label="عرض أرقام الأساس للسنوات السابقة"
            className="w-full flex items-center justify-between gap-3 text-right hover:underline min-h-11"
          >
            <span className={doc.needsRotation ? 'text-red-600 font-medium text-sm' : 'text-gray-800 text-sm'}>
              {fileNumber}
            </span>
            <span className="text-gray-400 text-sm shrink-0" aria-hidden="true">←</span>
          </button>
        ) : (
          <span className="text-gray-800 text-sm">—</span>
        )}
      </div>

      {isExecuted ? (
        <>
          <Row label="رقم ورود الاخطار التنفيذي" value={doc.fileReceiptNumber} />
          <Row label="تاريخ ورود الاخطار التنفيذي" value={formatDate(doc.fileReceiptDate)} />
          <Row label="كيفية تنفيذ الملف" value={doc.executedDescription} showEmpty />
          {paidAmounts && (
            <Row
              label={doc.generalEntitySide === 'deposit' ? 'المبلغ المودع' : 'المبلغ الذي دفعته الجهة العامة'}
              value={paidAmounts}
              showEmpty
            />
          )}
        </>
      ) : (
        <>
          <RowTriple
            firstLabel="رقم كتاب الجهة العامة"
            firstValue={doc.fileIncoming}
            secondLabel="تاريخ كتاب الجهة العامة"
            secondValue={doc.fileIncomingDate}
            thirdLabel="رقم ورود الملف"
            thirdValue={doc.fileArrivalNumber}
          />
          <Row label="تاريخ ورود الملف" value={doc.fileArrivalDate} />
          <Row label="رقم تحت رفع" value={doc.underFilingNumber} />
          <Row label="تاريخ قيد الملف" value={doc.fileRegistrationDate} />
          <Row label="تاريخ القاء حجز المنظومة" value={doc.seizureDate} />
        </>
      )}

      {!isLawyer && <Row label="منشئ المستند" value={doc.createdByName} />}
    </div>
  );
}
