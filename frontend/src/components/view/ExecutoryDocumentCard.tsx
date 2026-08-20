import type { DocumentResponse } from '../../types';
import { isExecutedLike } from '../../utils/documentDisplay';
import { FieldCell } from './FieldCell';
import { SectionCard } from './SectionCard';
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
    <SectionCard title="بيانات السند التنفيذي">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5 items-start">
        <FieldCell label="نوع السند" value={doc.contractTypeSelector} />
        <FieldCell label={typeLabel} value={doc.contractType} />
        <FieldCell label={numberLabel} value={doc.contractNumber} />
        <FieldCell label={dateLabel} value={doc.contractDate} />
      </div>
      {(doc.annexType || doc.annexNumber || doc.annexDate) && (
        <div className="mt-2.5">
          <FieldCell
            label="ملحق العقد"
            value={[doc.annexType, doc.annexNumber, doc.annexDate].filter((v) => v && v.trim()).join(' — ')}
          />
        </div>
      )}
      {isOrdinary && (
        <div className="mt-2.5">
          <FieldCell label="خلاصة الحكم" value={doc.inclusionText} showEmpty />
        </div>
      )}
      {isExecuted ? (
        <div className="mt-2.5">
          <FieldCell
            label={isDeposit ? 'المبلغ المعروض' : 'المبلغ المطلوب دفعه من الجهة العامة'}
            value={formatRequiredAmounts(doc)}
            showEmpty
            emphasized
          />
        </div>
      ) : (
        amountWords && (
          <div className="mt-2.5">
            <FieldCell label="المبلغ المطالب به" value={amountWords} emphasized />
          </div>
        )
      )}
    </SectionCard>
  );
}