import type { DocumentResponse } from '../../types';
import { formatDate } from '../../utils/dates';
import { Row } from './Row';
import { buildOccurrenceLines } from './viewFormat';

/** بطاقة «وقوعات الملف»: سرد الشطب والتجديد الحالي، مع نافذة التفاصيل الكاملة عند الضغط. */
export function OccurrencesCard({
  doc,
  onOpen,
}: {
  doc: DocumentResponse;
  onOpen: () => void;
}) {
  const occurrences = doc.occurrences ?? [];
  const occurrenceLines = buildOccurrenceLines(occurrences);
  const legacyStruckOffDate = occurrences.length === 0 ? doc.struckOffDate : undefined;

  if (occurrences.length === 0 && !legacyStruckOffDate) return null;

  return (
    <div className="bg-white rounded-xl shadow p-5">
      <h3 className="font-bold text-gray-800 mb-3">وقوعات الملف</h3>
      {occurrences.length > 0 ? (
        <button
          type="button"
          onClick={onOpen}
          aria-label="عرض تفاصيل وقوعات الملف"
          className="block w-full text-right min-h-11"
        >
          <ul className="text-gray-800 text-sm space-y-1">
            {occurrenceLines.map((line, i) => (
              <li key={i}>{line}</li>
            ))}
          </ul>
          <span className="text-emerald-800 text-xs font-medium hover:underline">
            عرض التفاصيل ({occurrences.length})
          </span>
        </button>
      ) : (
        <Row label="تاريخ الشطب" value={formatDate(legacyStruckOffDate)} showEmpty />
      )}
    </div>
  );
}
