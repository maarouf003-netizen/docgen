import ArchivedDocumentsList, { type ArchivedDocumentsListConfig } from '../components/ArchivedDocumentsList';
import { getDocumentBadge } from '../utils/documentStatus';
import { fullName } from '../utils/documentDisplay';
import { formatDate } from '../utils/dates';
import type { DocumentResponse } from '../types';

/** تاريخ إحالة الملف إلى قسم البداية. */
function referredDateOf(d: DocumentResponse): string {
  return formatDate(d.startReferralDate, '—');
}

/** صفحة «الملفات المحالة الى البداية»: ملفات «طالبة تنفيذ» بحالة «محال الى البداية» فقط
 * (ومنها القادم من «منفذ جبريا» المحال بجزئيته) — ظاهرة لجميع الأدوار كصفحة الملفات المنفذة. */
export default function ReferredToStartDocuments() {
  const config: ArchivedDocumentsListConfig = {
    title: 'الملفات المحالة الى البداية',
    searchPlaceholder: 'بحث في الملفات المحالة الى البداية...',
    emptyText: 'لا توجد ملفات محالة الى البداية',
    showBackLink: true,
    fetchEndpoint: '/documents/referred-to-start',
    dateColumnHeader: 'تاريخ الإحالة',
    dateCell: referredDateOf,
    cardTopRight: (d) => {
      const badge = getDocumentBadge(d);
      return <span className={`text-xs px-2 py-1 rounded-full shrink-0 ${badge.cls}`}>{badge.text}</span>;
    },
    cardBottomExtra: (d) => (
      <div className="text-xs text-gray-500 mt-1">أُحيل في {referredDateOf(d)}</div>
    ),
    displayName: fullName,
    linkToDocument: true,
    canRestore: false,
  };

  return <ArchivedDocumentsList config={config} />;
}