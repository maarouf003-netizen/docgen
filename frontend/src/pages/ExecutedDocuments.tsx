import ArchivedDocumentsList, { type ArchivedDocumentsListConfig } from '../components/ArchivedDocumentsList';
import { getDocumentBadge } from '../utils/documentStatus';
import { fullName } from '../utils/documentDisplay';
import { formatDate } from '../utils/dates';
import type { DocumentResponse } from '../types';

/** تاريخ تنفيذ الملف بحسب شقيته: حقلا حالة «منفذ عليها»/«عرض وايداع» (تاريخا التنفيذ/الإيداع)، وحقل حالة «طالبة تنفيذ» (براءة الذمة أو الإحالة القطعية). */
function executedDateOf(d: DocumentResponse): string {
  if (d.generalEntitySide === 'executed') return formatDate(d.executedExecutionDate, '—');
  if (d.generalEntitySide === 'deposit') return formatDate(d.executedDepositDate, '—');
  if (d.execStatus === 'منفذ بالتسوية') return d.baraetDate || '—';
  if (d.execStatus === 'منفذ جبريا') return d.forcedExecutionDate || '—';
  return '—';
}

export default function ExecutedDocuments() {
  const config: ArchivedDocumentsListConfig = {
    title: 'الملفات المنفذة',
    searchPlaceholder: 'بحث في الملفات المنفذة...',
    emptyText: 'لا توجد ملفات منفذة',
    showBackLink: true,
    fetchEndpoint: '/documents/executed',
    dateColumnHeader: 'تاريخ التنفيذ',
    dateCell: executedDateOf,
    cardTopRight: (d) => {
      const badge = getDocumentBadge(d);
      return <span className={`text-xs px-2 py-1 rounded-full shrink-0 ${badge.cls}`}>{badge.text}</span>;
    },
    cardBottomExtra: (d) => (
      <div className="text-xs text-gray-500 mt-1">نُفذ في {executedDateOf(d)}</div>
    ),
    // الاسم المعروض: لملفات «طالبة تنفيذ» اسم المقترض الثلاثي، ولفئات «منفذ عليها»/«عرض وايداع»
    // اسم أول منفذٍ عليه — فيتسق العرض مع هوية الملف الفعلية في صفحة الملفات المنفذة.
    displayName: fullName,
    linkToDocument: true,
    canRestore: false,
  };

  return <ArchivedDocumentsList config={config} />;
}
