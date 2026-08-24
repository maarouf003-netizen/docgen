import type { DocumentResponse } from '../../types';
import { formatDate } from '../../utils/dates';
import { isExecutedLike } from '../../utils/documentDisplay';
import { FieldCell } from './FieldCell';
import { RowTriple } from './RowTriple';
import { SectionCard } from './SectionCard';
import { formatFileNumber, formatPaidAmounts } from './viewFormat';

/** بطاقة «بيانات الملف» الموحّدة: دائرة التنفيذ، الفرع/المحامي حسب الصلاحية، رقم الملف وأرقام الأساس، وحقول كل صفة. */
export function FileDataCard({
  doc,
  isLawyer,
  showBranch,
  showLawyer,
  canViewChanges,
  onOpenBaseNumbers,
  onOpenAssignments,
  onOpenChanges,
}: {
  doc: DocumentResponse;
  isLawyer: boolean;
  showBranch: boolean;
  showLawyer: boolean;
  /** سجل التعديلات أداة مراجعة داخلية: صاحب الملف ورئيس القسم والإدارة فقط. */
  canViewChanges: boolean;
  onOpenBaseNumbers: () => void;
  onOpenAssignments: () => void;
  onOpenChanges: () => void;
}) {
  const isExecuted = isExecutedLike(doc.generalEntitySide);
  const fileNumber = formatFileNumber(doc);
  const paidAmounts = isExecuted ? formatPaidAmounts(doc) : '';

  const interactiveTile =
    'w-full flex items-center justify-between gap-3 text-right rounded-lg border border-gray-200 bg-gray-50 hover:bg-emerald-50 hover:border-emerald-200 px-3 py-2 min-h-11';

  return (
    <SectionCard title="بيانات الملف">
      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-2.5 items-start">
        <FieldCell label="دائرة التنفيذ المختصة" value={doc.court} />
        {showBranch && <FieldCell label="فرع الملف" value={doc.branchName} />}
      </div>

      {showLawyer ? (
        <div className="mt-2.5">
          <button
            type="button"
            onClick={onOpenAssignments}
            aria-label="عرض سجل التعاقب على الملف"
            className={interactiveTile}
          >
            <span className="min-w-0">
              <span className="block text-xs text-gray-500">المحامي المختص</span>
              <span className="block font-medium text-emerald-800 text-sm truncate">
                {doc.lawyer || '—'}
              </span>
            </span>
            <span className="text-gray-400 text-sm shrink-0" aria-hidden="true">←</span>
          </button>
        </div>
      ) : (
        isLawyer &&
        doc.referredFromLawyer &&
        doc.referredAt && (
          <div className="mt-2.5 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
            <span className="text-amber-800 text-xs block font-medium">إحالة الملف</span>
            <span className="text-amber-900 text-sm">
              أُحيل لك هذا الملف من {doc.referredFromLawyer} بتاريخ {formatDate(doc.referredAt)}
            </span>
          </div>
        )
      )}

      <div className="mt-2.5">
        <button
          type="button"
          onClick={onOpenBaseNumbers}
          aria-label="عرض أرقام الأساس للسنوات السابقة"
          className={interactiveTile}
        >
          <span className="min-w-0">
            <span className="block text-xs text-gray-500">
              رقم الملف ونوعه لعام {doc.fileYear}
            </span>
            <span
              className={`block text-sm truncate ${
                doc.needsRotation ? 'text-red-600 font-medium' : 'text-gray-800'
              }`}
            >
              {fileNumber || '—'}
            </span>
          </span>
          <span className="text-gray-400 text-sm shrink-0" aria-hidden="true">←</span>
        </button>
      </div>

      {canViewChanges && (
        <div className="mt-2.5">
          <button
            type="button"
            onClick={onOpenChanges}
            aria-label="عرض سجل التعديلات على مستوى الحقول"
            className={interactiveTile}
          >
            <span className="min-w-0">
              <span className="block text-xs text-gray-500">سجل التعديلات</span>
              <span className="block text-sm truncate text-emerald-800 font-medium">
                تتبع تغيّرات الحقول (قبل/بعد)
              </span>
            </span>
            <span className="text-gray-400 text-sm shrink-0" aria-hidden="true">←</span>
          </button>
        </div>
      )}

      {isExecuted ? (
        <div className="mt-2.5 grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-2.5 items-start">
          <FieldCell label="رقم ورود الاخطار التنفيذي" value={doc.fileReceiptNumber} />
          <FieldCell label="تاريخ ورود الاخطار التنفيذي" value={formatDate(doc.fileReceiptDate)} />
          <FieldCell label="كيفية تنفيذ الملف" value={doc.executedDescription} showEmpty />
          {paidAmounts && (
            <FieldCell
              label={
                doc.generalEntitySide === 'deposit'
                  ? 'المبلغ المودع'
                  : 'المبلغ الذي دفعته الجهة العامة'
              }
              value={paidAmounts}
              showEmpty
              emphasized
            />
          )}
        </div>
      ) : (
        <>
          <div className="mt-2.5">
            <RowTriple
              firstLabel="رقم كتاب الجهة العامة"
              firstValue={doc.fileIncoming}
              secondLabel="تاريخ كتاب الجهة العامة"
              secondValue={doc.fileIncomingDate}
              thirdLabel="رقم ورود الملف"
              thirdValue={doc.fileArrivalNumber}
            />
          </div>
          <div className="mt-2.5 grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-2.5 items-start">
            <FieldCell label="تاريخ ورود الملف" value={doc.fileArrivalDate} />
            <FieldCell label="رقم تحت رفع" value={doc.underFilingNumber} />
            <FieldCell label="تاريخ قيد الملف" value={doc.fileRegistrationDate} />
            <FieldCell label="تاريخ القاء حجز المنظومة" value={doc.seizureDate} />
          </div>
        </>
      )}

      {!isLawyer && (
        <div className="mt-2.5">
          <FieldCell label="منشئ المستند" value={doc.createdByName} />
        </div>
      )}
    </SectionCard>
  );
}