import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { normalizeDocumentResponse } from '../utils/apiNormalization';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { fullName, identityFileNumber } from '../components/view/viewFormat';
import DocumentCorrespondenceCard from '../components/correspondence/DocumentCorrespondenceCard';
import type { DocumentResponse } from '../types';

/**
 * صفحة مراسلات الملف المستقلة (داخلية): تُفتح من زر «مراسلات» في ترويسة
 * `DocumentView` بدل التمرير للبطاقة المضمّنة (المحذوفة). تُعيد استعمال
 * `DocumentCorrespondenceCard` نقلًا لا نسخًا، مع `onAccessDenied` لعرض تنبيه
 * صريح مع رابط عودة بدل الفراغ عند غياب صلاحية المراسلات.
 */
export default function FileCorrespondence() {
  const { id } = useParams();
  const { user } = useAuth();
  const [accessDenied, setAccessDenied] = useState(false);

  // resetOnDepsChange: استعلام هوية الملف المفرد — تُصفَّر بيانات السابق
  // عند تبديل الملف فلا يُعرض ماله تحت عنوان الجديد أثناء التحميل.
  const docQuery = useCancellableRequest<DocumentResponse>(
    (signal) =>
      api
        .get<DocumentResponse>(`/documents/${id}`, { signal })
        .then((r) => normalizeDocumentResponse(r.data)),
    [id],
    { enabled: Boolean(id), resetOnDepsChange: true },
  );
  const doc = docQuery.data ?? null;

  // تبديل الملف على المسار نفسه يعيد استعمال الصفحة: يُصفَّر تنبيه الملف
  // السابق حتى لا يظهر كاذبًا (وعنوان التسطير يُشتق من الملف الجديد حصرًا).
  useEffect(() => {
    setAccessDenied(false);
  }, [id]);

  if (docQuery.error) {
    return (
      <div className="max-w-3xl mx-auto" role="alert">
        <p className="text-red-600 mb-4">{docQuery.error}</p>
        <div className="flex gap-3 flex-wrap">
          <button
            type="button"
            onClick={docQuery.refetch}
            className="min-h-11 px-4 rounded-lg border border-red-300 hover:bg-red-50 text-red-800 font-medium focus:outline-none focus-visible:ring-2 focus-visible:ring-red-500"
          >
            إعادة المحاولة
          </button>
          <Link
            to={`/documents/${id}`}
            className="min-h-11 inline-flex items-center px-4 rounded-lg border border-gray-300 hover:bg-gray-50 text-gray-700 font-medium focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            عودة إلى الملف
          </Link>
        </div>
      </div>
    );
  }

  if (!doc) {
    return <div className="max-w-3xl mx-auto text-gray-500 text-sm">جارِ التحميل…</div>;
  }

  // صلاحية التسطير: عبارة البطاقة الأصلية حرفيًا (DocumentView) — محامي الملف
  // المالك أو رئيس القسم/الشعبة.
  const canEdit = user?.role === 'lawyer';
  const canTransfer = user?.role === 'head' || user?.role === 'subhead';
  const isOwner = doc.createdById != null && doc.createdById === user?.id;
  // عنوان نافذة التسطير: عبارة البطاقة الأصلية حرفيًا (لا عبارة الترويسة).
  const documentTitle =
    fullName({ name: doc.borrowerName, father: doc.borrowerFather, family: doc.borrowerFamily }) ||
    doc.documentType ||
    undefined;

  return (
    <div className="max-w-3xl mx-auto">
      <div className="flex items-center justify-between gap-3 flex-wrap mb-2">
        <h1 className="text-xl md:text-2xl font-bold text-gray-800">مراسلات الملف</h1>
        {!accessDenied && (
          <Link
            to={`/documents/${id}`}
            className="min-h-11 inline-flex items-center px-4 rounded-lg border border-gray-300 hover:bg-gray-50 text-gray-700 text-sm font-medium focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            عودة إلى الملف
          </Link>
        )}
      </div>
      {/* سياق الملف (هوية + عنوان التسطير) — فلا تُفتح الصفحة عمياءً من رابط مباشر. */}
      <p className="mb-5 text-sm text-gray-500">
        {[documentTitle, identityFileNumber(doc)].filter(Boolean).join(' — ')}
      </p>
      {accessDenied ? (
        <div
          role="alert"
          className="bg-white rounded-xl border border-gray-200 shadow-sm px-5 py-4 flex items-center justify-between gap-3 flex-wrap"
        >
          <p className="text-sm text-gray-700">لا صلاحية لعرض مراسلات هذا الملف.</p>
          <Link
            to={`/documents/${id}`}
            className="min-h-11 inline-flex items-center px-4 rounded-lg border border-gray-300 hover:bg-gray-50 text-gray-700 text-sm font-medium focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            عودة إلى الملف
          </Link>
        </div>
      ) : (
        id !== undefined && (
          <DocumentCorrespondenceCard
            documentId={Number(id)}
            documentTitle={documentTitle}
            canCreate={(canEdit && isOwner) || canTransfer}
            onAccessDenied={() => setAccessDenied(true)}
          />
        )
      )}
    </div>
  );
}
