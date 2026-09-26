import { Link } from 'react-router-dom';
import type { PortalFileListItemDto } from '../../types';
import { tripleName } from '../../utils/documentDisplay';
import { publicEntityBranchLabel } from '../../utils/publicEntityBranchLabel';

/**
 * ألوان الشارة لكل قيم `DocumentStatusResolver` الثماني — النص من الخادم حصرًا
 * (`displayStatus`) فلا تصنيف هنا: الخام (`execStatus`) للفلاتر فقط.
 * القيمة الغريبة (خارج الثماني) رمادية ظاهرة لا خضراء مضلّلة.
 */
const STATUS_STYLES: Record<string, string> = {
  'متداول': 'bg-emerald-100 text-emerald-800',
  'تريث': 'bg-amber-100 text-amber-800',
  'منفذ': 'bg-sky-100 text-sky-800',
  'محال الى البداية': 'bg-purple-100 text-purple-800',
  'تحت رفع': 'bg-gray-200 text-gray-700',
  'متداول / منفذ جزئيا': 'bg-cyan-100 text-cyan-800',
  'مسترد': 'bg-fuchsia-100 text-fuchsia-800',
  'مشطوب': 'bg-gray-200 text-gray-700',
};

function statusLabel(file: PortalFileListItemDto): string {
  if (file.displayStatus) return file.displayStatus;
  // احتياط حمولات قديمة بلا displayStatus — يُحذف مع أول تنظيف لاحق.
  if (file.execStatus) return file.execStatus;
  return file.isDraft ? 'تحت رفع' : 'متداول';
}

/** تسمية فرع الجهة للسطر الثاني: «المحافظة/الفرع» (الجهة الأم بلا لاحقة فرعية). */
function branchShort(governorate: string, branchName: string): string {
  return publicEntityBranchLabel(governorate, branchName);
}

/**
 * شريط ملف تنفيذي في بوابة المندوب:
 * السطر الأول الاسم الثلاثي للمنفذ عليه، تحته فرع الجهة العامة، وشارة الحالة،
 * وفي نهاية الشريط رقم الأساس (الأحدث دائمًا مع السنة) ونوع الملف ودائرة التنفيذ
 * (تُخفى الدائرة عند فراغها — لا يعرض شيء).
 */
export default function PortalFileCard({
  file,
  canonicalName,
}: {
  file: PortalFileListItemDto;
  canonicalName?: string | null;
}) {
  const fullName = tripleName(file.borrowerName ?? undefined, file.borrowerFather ?? undefined, file.borrowerFamily ?? undefined)
    || file.borrowerName
    || file.documentType;
  const status = statusLabel(file);
  const badgeCls = STATUS_STYLES[status] ?? 'bg-gray-100 text-gray-700';

  const matched = file.matchedEntries ?? [];
  const branchText = matched.length === 0
    ? null
    : matched.slice(0, 2).map((e) => branchShort(e.governorate, e.branchName)).join(' · ')
      + (matched.length > 2 ? ` +${matched.length - 2}` : '');

  const baseText = file.displayBaseNumber
    ? `رقم الأساس: ${file.displayBaseNumber}${file.displayBaseYear ? ` لعام ${file.displayBaseYear}` : ''}`
    : null;

  // النوع: FileType أولًا ثم DocumentType احتياطًا (لا بطاقة بلا نوع عند فراغ الأول).
  const typeText = (file.fileType?.trim() || file.documentType?.trim())
    ? `نوع الملف: ${(file.fileType?.trim() || file.documentType?.trim())}`
    : null;
  // الدائرة تُخفى تمامًا عند فراغها أو بياضها — لا يعرض شيء مكانها.
  const courtText = file.court?.trim()
    ? `دائرة التنفيذ المختصة: ${file.court.trim()}`
    : null;

  const metaParts: ReadonlyArray<{ key: 'base' | 'type' | 'court'; text: string }> = [
    ...(baseText ? [{ key: 'base' as const, text: baseText }] : []),
    ...(typeText ? [{ key: 'type' as const, text: typeText }] : []),
    ...(courtText ? [{ key: 'court' as const, text: courtText }] : []),
  ];

  return (
    <li>
      <Link
        to={`/portal/files/${file.id}`}
        className="block px-4 py-3 hover:bg-emerald-50/60 min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-emerald-500"
      >
        <div className="flex flex-wrap items-start justify-between gap-2">
          <span className="min-w-0 grow">
            <span className="block font-medium text-gray-800 break-words">{fullName}</span>
            {branchText ? (
              <span className="block text-xs text-gray-500 mt-0.5 truncate">
                {canonicalName ? `${canonicalName} · ` : ''}{branchText}
              </span>
            ) : canonicalName ? (
              <span className="block text-xs text-gray-500 mt-0.5 truncate">{canonicalName}</span>
            ) : null}
          </span>
          <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ${badgeCls}`}>
            {status}
          </span>
        </div>
        {metaParts.length > 0 && (
          <div className="mt-1.5 flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-gray-600">
            {metaParts.map((part) => (
              <span key={part.key} className="tabular-nums break-words min-w-0">{part.text}</span>
            ))}
          </div>
        )}
      </Link>
    </li>
  );
}
