import { useAuth } from '../auth/useAuth';
import ArchivedDocumentsList, { type ArchivedDocumentsListConfig } from '../components/ArchivedDocumentsList';
import { getDocumentBadge } from '../utils/documentStatus';
import { executedFullName, fullName, isExecutedLike } from '../utils/documentDisplay';
import { formatDate } from '../utils/dates';

export default function StruckOffDocuments() {
  const { user } = useAuth();
  // إعادة الملف المشطوب من اختصاص المحامي صاحب الملف فقط (بذات حكم المحذوفات).
  const canRestore = user?.role === 'lawyer';

  const config: ArchivedDocumentsListConfig = {
    title: 'الملفات المشطوبة',
    searchPlaceholder: 'بحث في الملفات المشطوبة...',
    emptyText: 'لا توجد ملفات مشطوبة',
    showBackLink: true,
    fetchEndpoint: '/documents/struck-off',
    restoreEndpoint: (id) => `/documents/${id}/restore-struck-off`,
    restoreButtonLabel: 'إعادة الملف',
    confirmRestoreLabel: 'تأكيد الإعادة',
    restoringLabel: 'جارِ الإعادة...',
    successMessage: (name) => `أعيد الملف "${name}" إلى المتداول`,
    dateColumnHeader: 'تاريخ الشطب',
    dateCell: (d) => formatDate(d.struckOffDate, '—'),
    cardTopRight: (d) => {
      const badge = getDocumentBadge(d);
      return <span className={`text-xs px-2 py-1 rounded-full shrink-0 ${badge.cls}`}>{badge.text}</span>;
    },
    cardBottomExtra: (d) => (
      <div className="text-xs text-gray-500 mt-1">شُطب في {formatDate(d.struckOffDate, '—')}</div>
    ),
    // الاسم المعروض: لملفات «طالبة تنفيذ» اسم المقترض الثلاثي، ولفئات «منفذ عليها»/«عرض وايداع»
    // اسم أول منفذٍ عليه — فيتسق العرض مع هوية الملف الفعلية في صفحة المشطوبة.
    displayName: (d) => (isExecutedLike(d.generalEntitySide) ? executedFullName(d) : fullName(d)),
    linkToDocument: true,
    canRestore,
    requiresRenewal: true,
  };

  return <ArchivedDocumentsList config={config} />;
}
