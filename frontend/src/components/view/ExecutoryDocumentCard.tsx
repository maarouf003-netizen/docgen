import type { DocumentResponse } from '../../types';
import { isExecutedLike } from '../../utils/documentDisplay';
import { Row } from './Row';
import { RowPair } from './RowPair';
import { formatRequiredAmounts } from './viewFormat';

function joinAmountWords(...parts: Array<string | null | undefined>): string {
  return parts.filter((p) => p && p.trim()).join(' و ');
}

/** بطاقة «بيانات السند التنفيذي» الموحّدة لكل الصفات: نوع السند، المحكمة/العقد، الرقم، التاريخ، والمبلغ حسب الصفة. */
export function ExecutoryDocumentCard({ doc }: { doc: DocumentResponse }) {
  const isExecuted = isExecutedLike(doc.generalEntitySide);
  const isDeposit = doc.generalEntitySide === 'deposit';
  const isOrdinary = doc.contractTypeSelector === 'عادي';
  const typeLabel = isOrdinary ? 'المحكمة مصدرة القرار' : 'نوع العقد';
  const numberLabel = isOrdinary ? 'رقم القرار' : 'رقم العقد';
  const dateLabel = isOrdinary ? 'تاريخ القرار' : 'تاريخ العقد';
  const amountWords = joinAmountWords(
    isOrdinary ? doc.inclusionAmountWords : doc.amountWords,
    isOrdinary ? doc.inclusionAmount2Words : doc.amount2Words,
    isOrdinary ? doc.inclusionAmount3Words : doc.amount3Words,
  );

  return (
    <div className="bg-white rounded-xl shadow p-5">
      <h3 className="font-bold text-emerald-800 mb-3">بيانات السند التنفيذي</h3>
      <RowPair
        firstLabel="نوع السند"
        firstValue={doc.contractTypeSelector}
        secondLabel={typeLabel}
        secondValue={doc.contractType}
      />
      <RowPair
        firstLabel={numberLabel}
        firstValue={doc.contractNumber}
        secondLabel={dateLabel}
        secondValue={doc.contractDate}
      />
      {(doc.annexType || doc.annexNumber || doc.annexDate) && (
        <Row
          label="ملحق العقد"
          value={[doc.annexType, doc.annexNumber, doc.annexDate].filter((v) => v && v.trim()).join(' — ')}
        />
      )}
      {isOrdinary && <Row label="خلاصة الحكم" value={doc.inclusionText} showEmpty />}
      {isExecuted ? (
        <Row
          label={isDeposit ? 'المبلغ المعروض' : 'المبلغ المطلوب دفعه من الجهة العامة'}
          value={formatRequiredAmounts(doc)}
          showEmpty
        />
      ) : (
        amountWords && <Row label="المبلغ المطالب به" value={amountWords} />
      )}
    </div>
  );
}
