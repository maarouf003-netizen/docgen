import type { DocumentResponse } from '../../types';
import { isExecutedLike } from '../../utils/documentDisplay';
import { FieldCell } from './FieldCell';
import { SectionCard } from './SectionCard';
import { formatClaimedAmounts, formatRequiredAmounts } from './viewFormat';

/** بطاقة «بيانات السند التنفيذي» الموحّدة لكل الصفات: نوع السند، المحكمة/العقد، الرقم، التاريخ، والمبلغ حسب الصفة. */
export function ExecutoryDocumentCard({ doc, isMirror }: { doc: DocumentResponse; isMirror?: boolean }) {
  const isExecuted = isExecutedLike(doc.generalEntitySide);
  const isDeposit = doc.generalEntitySide === 'deposit';
  const isOrdinary = doc.contractTypeSelector === 'عادي';
  const typeLabel = isOrdinary ? 'المحكمة مصدرة القرار' : 'نوع العقد';
  const numberLabel = isOrdinary ? 'رقم القرار' : 'رقم العقد';
  const dateLabel = isOrdinary ? 'تاريخ القرار' : 'تاريخ العقد';
  // المبلغ رقمًا لا تفقيطًا في بطاقة العرض: التفقيط مشتق خادميًا للتوليد،
  // وعرضه وحده كان يُخفي تعديل الرقم ويُظهر نصًا قديمًا بعد التعديل.
  const claimedAmounts = formatClaimedAmounts(doc);

  return (
    <SectionCard title="بيانات السند التنفيذي">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5 items-start">
        <FieldCell label="نوع السند" value={doc.contractTypeSelector} />
        <FieldCell label={typeLabel} value={doc.contractType} />
        <FieldCell label={numberLabel} value={doc.contractNumber} />
        <FieldCell label={dateLabel} value={doc.contractDate} />
      </div>
      {(() => {
        const stored = (doc.annexes ?? []).filter(
          (a) => (a.annexType ?? '').trim() || (a.annexNumber ?? '').trim() || (a.annexDate ?? '').trim(),
        );
        const legacy =
          !stored.length && (doc.annexType || doc.annexNumber || doc.annexDate)
            ? [{ annexType: doc.annexType, annexNumber: doc.annexNumber, annexDate: doc.annexDate }]
            : [];
        const rows = stored.length ? stored : legacy;
        if (!rows.length) return null;
        return (
          <div className="mt-2.5 space-y-1.5">
            {rows.map((a, i) => (
              <FieldCell
                key={i}
                label={rows.length > 1 ? `ملحق ${i + 1}` : 'ملحق العقد'}
                value={[a.annexType, a.annexNumber, a.annexDate]
                  .filter((v) => v && v.trim())
                  .join(' — ')}
              />
            ))}
          </div>
        );
      })()}
      {isOrdinary && (
        <div className="mt-2.5">
          <FieldCell label="خلاصة الحكم" value={doc.inclusionText} showEmpty />
        </div>
      )}
      {(doc.inclusionNotes ?? '').trim() && (
        <div className="mt-2.5">
          <FieldCell label="ملاحظات" value={doc.inclusionNotes} />
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
        claimedAmounts && (
          <div className="mt-2.5">
            <FieldCell label="المبلغ المطالب به" value={claimedAmounts} emphasized />
            {isMirror && (
              <p className="mt-1 text-xs text-gray-500">
                مُزامَن تلقائيًا من الملف المنيب (الملف المناب مرآةً للمنيب) — تعديله من الملف المنيب حصرًا.
              </p>
            )}
          </div>
        )
      )}
    </SectionCard>
  );
}