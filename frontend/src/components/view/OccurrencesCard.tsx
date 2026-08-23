import type { AppealDto, DocumentResponse } from '../../types';
import { formatDate } from '../../utils/dates';
import { appealStatusBadge } from '../../utils/appealStatus';
import { FieldCell } from './FieldCell';
import { SectionCard } from './SectionCard';
import { buildOccurrenceLines } from './viewFormat';

/**
 * بطاقة «وقوعات الملف» مجزأة إلى جزئين:
 * 1) «الشطوبات»: السجل الزمني لوقوعات الملف وعلى رأسه الشطب (والتجديد وإجراءات
 *    الحالة الأخرى)، مع نافذة تفاصيلها الكاملة.
 * 2) «الاستئنافات»: كل استئناف وقع على الملف بسطر «استئناف قرار رئيس التنفيذ…»
 *    وبجانبه شارة حالته (منظور حمراء / محسوم خضراء / مشطوب رمادية)، والضغط عليه يفتح
 *    نافذة كافة تفاصيل الاستئناف بما فيها قرار الحسم.
 */
export function OccurrencesCard({
  doc,
  appeals = [],
  onOpen,
  onOpenAppeal,
}: {
  doc: DocumentResponse;
  appeals?: AppealDto[];
  onOpen: () => void;
  onOpenAppeal: (appeal: AppealDto) => void;
}) {
  const occurrences = doc.occurrences ?? [];
  const occurrenceLines = buildOccurrenceLines(occurrences);
  const legacyStruckOffDate = occurrences.length === 0 ? doc.struckOffDate : undefined;
  const hasStruckPart = occurrences.length > 0 || Boolean(legacyStruckOffDate);

  if (!hasStruckPart && appeals.length === 0) return null;

  return (
    <SectionCard title="وقوعات الملف">
      <div className="space-y-4">
        {/* الجزء الأول: الشطوبات (سجل وقوعات الملف التاريخي) */}
        <section aria-label="الشطوبات" className="space-y-2">
          <h4 className="text-sm font-bold text-gray-600">الشطوبات</h4>
          {hasStruckPart ? (
            occurrences.length > 0 ? (
              <>
                <ul className="text-gray-800 text-sm space-y-1.5">
                  {occurrenceLines.map((line, i) => (
                    <li key={i}>{line}</li>
                  ))}
                </ul>
                <button
                  type="button"
                  onClick={onOpen}
                  aria-label="عرض تفاصيل وقوعات الملف"
                  className="block w-full text-right min-h-11"
                >
                  <span className="text-emerald-800 text-xs font-medium hover:underline">
                    عرض التفاصيل ({occurrences.length})
                  </span>
                </button>
              </>
            ) : (
              <FieldCell label="تاريخ الشطب" value={formatDate(legacyStruckOffDate)} showEmpty />
            )
          ) : (
            <p className="text-gray-400 text-sm">لا توجد شطوبات.</p>
          )}
        </section>

        {/* الجزء الثاني: الاستئنافات */}
        <section aria-label="الاستئنافات" className="space-y-2">
          <h4 className="text-sm font-bold text-gray-600">الاستئنافات</h4>
          {appeals.length > 0 ? (
            <ul className="space-y-2">
              {appeals.map((appeal) => {
                const badge = appealStatusBadge(appeal.status);
                const date = formatDate(appeal.appealedDecisionDate, 'غير محدد');
                return (
                  <li key={appeal.id}>
                    <button
                      type="button"
                      onClick={() => onOpenAppeal(appeal)}
                      aria-label={`عرض تفاصيل استئناف قرار رئيس التنفيذ المؤرخ في ${date}`}
                      className="w-full flex items-center justify-between gap-3 rounded-lg border border-gray-200 hover:bg-gray-50 px-3 py-2 min-h-11 text-right focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    >
                      <span className="text-gray-800 text-sm min-w-0">
                        استئناف قرار رئيس التنفيذ في ({appeal.court ?? '—'}) المؤرخ في {date}
                      </span>
                      <span className={`shrink-0 rounded-full px-2.5 py-1 text-xs ${badge.cls}`}>
                        {badge.text}
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          ) : (
            <p className="text-gray-400 text-sm">لا توجد استئنافات.</p>
          )}
        </section>
      </div>
    </SectionCard>
  );
}
