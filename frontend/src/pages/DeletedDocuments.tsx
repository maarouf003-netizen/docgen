import { useAuth } from '../auth/useAuth';
import ArchivedDocumentsList, { type ArchivedDocumentsListConfig } from '../components/ArchivedDocumentsList';
import { formatDateTime } from '../utils/dates';
import { fullName } from '../utils/documentDisplay';

export default function DeletedDocuments() {
  const { user } = useAuth();
  // الاستعادة من اختصاص المحامي صاحب الملف فقط؛ والرئيس/المشرف يعرضون فقط.
  const canRestore = user?.role === 'lawyer';

  const config: ArchivedDocumentsListConfig = {
    title: 'الملفات المحذوفة',
    searchPlaceholder: 'بحث بالاسم الثنائي أو الثلاثي لأحد المنفذ عليهم أو ورثة المتوفى، رقم العقد، دائرة التنفيذ...',
    emptyText: 'لا توجد ملفات محذوفة',
    showBackLink: false,
    fetchEndpoint: '/documents/deleted',
    restoreEndpoint: (id) => `/documents/${id}/restore`,
    restoreButtonLabel: 'استعادة',
    confirmRestoreLabel: 'تأكيد الاستعادة',
    restoringLabel: 'جارِ الاستعادة...',
    successMessage: (name) => `تمت استعادة الملف "${name}"`,
    dateColumnHeader: 'تاريخ الحذف',
    dateCell: (d) => formatDateTime(d.deletedAt, '—'),
    cardTopRight: (d) => (
      <span className="text-xs text-gray-500 shrink-0 whitespace-nowrap">
        حُذف في {formatDateTime(d.deletedAt, '—')}
      </span>
    ),
    displayName: fullName,
    linkToDocument: false,
    canRestore,
  };

  return <ArchivedDocumentsList config={config} />;
}
