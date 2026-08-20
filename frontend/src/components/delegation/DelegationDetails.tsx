import type { DelegationDto } from '../../types';
import { formatDate } from '../../utils/dates';
import { delegationAssetsLine } from '../../utils/delegationAssets';
import { delegationStatusBadge } from '../../utils/delegationStatus';

/** صف «كتب» اختياري: «كتاب الإيداع رقم X بتاريخ Y» (يظهر متى وُجد أحد حقلَيه). */
function bookRow(label: string, number?: string | null, date?: string | null): string {
  if (!number && !date) return '';
  const parts = [label];
  if (number) parts.push(`رقم ${number}`);
  if (date) parts.push(`بتاريخ ${formatDate(date)}`);
  return parts.join(' ');
}

/**
 * تفاصيل إنابة واحدة (مشتركة بين «تشعبات الملف» و«معلومات الملف المنيب» و«طلبات الإنابة»):
 * الدائرة المنابة وحالتها، داخليتها/خارجيتها، تاريخها ونصها، كتبها، محاميها وأموالها.
 */
export function DelegationDetails({ d }: { d: DelegationDto }) {
  const badge = delegationStatusBadge(d.status);
  const books: string[] = [
    bookRow('كتاب الإيداع', d.depositBookNumber, d.depositBookDate),
  ].filter(Boolean);
  const assetsLine = delegationAssetsLine(d);
  const snapshotsAdjusted = d.assets.some((a) => a.snapshotAdjusted);

  return (
    <div className="space-y-2 min-w-0">
      <div className="flex items-start justify-between gap-2">
        <p className="font-bold text-gray-800 text-sm leading-snug">{d.delegatedCourt || '—'}</p>
        <span className={`rounded-full px-2.5 py-0.5 text-xs whitespace-nowrap shrink-0 ${badge.cls}`}>
          {badge.text}
        </span>
      </div>

      <p className="text-xs text-gray-500">
        {d.isExternal
          ? `إنابة خارجية — الفرع المناب: ${d.externalBranchName ?? '—'}`
          : 'إنابة داخلية'}
      </p>

      {d.delegationDate && (
        <p className="text-sm text-gray-700">
          <span className="text-gray-500 text-xs block">تاريخ الإنابة</span>
          {formatDate(d.delegationDate)}
        </p>
      )}

      {d.delegationText && (
        <p className="text-sm text-gray-700 whitespace-pre-line break-words">{d.delegationText}</p>
      )}

      {books.length > 0 && (
        <ul className="space-y-1">
          {books.map((book) => (
            <li key={book} className="text-sm text-gray-700">
              {book}
            </li>
          ))}
        </ul>
      )}

      {d.assignedLawyerName && (
        <p className="text-sm text-gray-700">
          <span className="text-gray-500 text-xs block">المحامي المختص</span>
          {d.assignedLawyerName}
        </p>
      )}

      {snapshotsAdjusted && (
        <p className="text-xs font-medium text-amber-800 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
          عُدِّلت بيانات بعض الأموال في الملف المنيب بعد التسطير — حُدِّثت لقطة الإنابة تلقائيًا.
        </p>
      )}

      {assetsLine && (
        <p className="text-sm text-gray-700 break-words">
          <span className="text-gray-500 text-xs block">الأموال موضوع الإنابة</span>
          {assetsLine}
        </p>
      )}
    </div>
  );
}
