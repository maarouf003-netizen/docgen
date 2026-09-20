import type { AssetDto, DelegationDto } from '../../types';
import { formatDate } from '../../utils/dates';
import { assetDisplayName } from '../../utils/assetDisplay';
import { delegationAssetLabel, delegationAssetsLine } from '../../utils/delegationAssets';
import { DELEGATION_STATUS_EXECUTED, delegationStatusBadge } from '../../utils/delegationStatus';

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
 * sourceAssets (اختياري — تُمرَّر في سياق المنيب فقط): تُكشف بها اللقطات اليتيمة
 * (C2 — لا تطابق أي أصل حالي) بتحذير مميز عن تحذير «عُدِّلت البيانات».
 */
export function DelegationDetails({ d, sourceAssets }: { d: DelegationDto; sourceAssets?: AssetDto[] }) {
  const badge = delegationStatusBadge(d.status);
  const books: string[] = [
    bookRow('كتاب الإيداع', d.depositBookNumber, d.depositBookDate),
  ].filter(Boolean);
  const assetsLine = delegationAssetsLine(d);
  const snapshotsAdjusted = d.assets.some((a) => a.snapshotAdjusted);
  // اليتيمة: لقطة إنابة غير منفذة لا تطابق أي أصل حالي (نوع + وصف) — حُذف أصلها أو
  // تغيّر جذريًا مع تجميد النقل التلقائي (C2)؛ الأصل الجديد حرّ فعليًا — راجع التسطير.
  const orphanLabels =
    sourceAssets == null || d.status === DELEGATION_STATUS_EXECUTED
      ? []
      : d.assets
          .filter(
            (s) =>
              !s.snapshotAdjusted &&
              !sourceAssets.some(
                (a) => a.assetKind === s.assetKind && assetDisplayName(a) === delegationAssetLabel(s),
              ),
          )
          .map(delegationAssetLabel);
  const targetNumber = [d.targetFileNumber, d.targetFileYear].filter(Boolean).join('/');

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

      {targetNumber && (
        <p className="text-sm text-gray-700">
          <span className="text-gray-500 text-xs block">الملف المناب</span>
          {targetNumber}
        </p>
      )}

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

      {orphanLabels.length > 0 && (
        <p className="text-xs font-medium text-red-800 bg-red-50 border border-red-200 rounded-lg px-3 py-2 break-words">
          {`أصل موضوع الإنابة لم يعد في الملف المنيب (حُذف أو تغيّر جذريًا): ${orphanLabels.join('، ')} — راجع التسطير.`}
        </p>
      )}

      {assetsLine && (
        <p className="text-sm text-gray-700 break-words">
          <span className="text-gray-500 text-xs block">الأموال موضوع الإنابة</span>
          {assetsLine}
        </p>
      )}

      {d.saleCoversFullDebt !== null && d.saleCoversFullDebt !== undefined && (
        <p
          className={`text-xs font-bold rounded-lg px-3 py-2 border ${d.saleCoversFullDebt ? 'bg-emerald-50 border-emerald-200 text-emerald-800' : 'bg-amber-50 border-amber-200 text-amber-800'}`}
        >
          {d.saleCoversFullDebt ? 'البدل غطى كامل المديونية' : 'البدل لم يغطِ كامل المديونية'}
        </p>
      )}
    </div>
  );
}
