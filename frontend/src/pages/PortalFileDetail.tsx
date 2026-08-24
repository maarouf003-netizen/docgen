import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import type { DocumentResponse, PortalAppealDto } from '../types';

const STATUS_LABELS: Record<string, string> = {
  pending: 'معلّق',
  decided: 'حُسم',
  'struck-off': 'مشطوب',
};

/** تفاصيل قرائية لملف داخل نطاق بوابة الجهة، مع بطاقة استئنافات القراءة فقط. */
export default function PortalFileDetail() {
  const { id } = useParams();
  const documentId = Number(id);

  const [file, setFile] = useState<DocumentResponse | null>(null);
  const [appeals, setAppeals] = useState<PortalAppealDto[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    if (!Number.isFinite(documentId)) return;
    setLoading(true);
    setError('');
    try {
      const res = await api.get<DocumentResponse>(`/portal/files/${documentId}`);
      setFile(res.data);
      const appealsRes = await api.get<PortalAppealDto[]>(`/portal/files/${documentId}/appeals`);
      setAppeals(Array.isArray(appealsRes.data) ? appealsRes.data : []);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [documentId]);

  useEffect(() => {
    void load();
  }, [load]);

  if (error) {
    return (
      <div className="max-w-3xl mx-auto" role="alert">
        <p className="text-red-600 mb-4">{error}</p>
        <Link to="/portal" className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 min-h-11 inline-block">
          رجوع إلى ملفات الجهة
        </Link>
      </div>
    );
  }

  if (loading || !file) {
    return <div className="max-w-3xl mx-auto text-gray-500 text-sm">جارِ التحميل…</div>;
  }

  const amountLine = `${(file.amountNumeric ?? 0).toLocaleString('ar-SY')} ${file.currency ?? ''}`.trim();

  return (
    <div className="max-w-3xl mx-auto">
      <Link to="/portal" className="inline-block text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 min-h-11 mb-4">
        ← رجوع إلى ملفات الجهة
      </Link>

      <h2 className="text-2xl font-bold text-gray-800 mb-1 break-words">
        {file.borrowerName || file.documentType}
      </h2>
      <p className="text-sm text-gray-500 mb-6">{file.documentType}</p>

      {/* بطاقة المعلومات الأساسية */}
      <section aria-labelledby="pf-basic" className="bg-white rounded-xl shadow p-5 mb-4">
        <h3 id="pf-basic" className="font-bold text-gray-800 mb-3">المعلومات الأساسية</h3>
        <dl className="grid sm:grid-cols-2 gap-y-2 gap-x-6 text-sm">
          <div><dt className="inline text-gray-500">الحالة: </dt><dd className="inline font-medium text-gray-800">{file.execStatus || (file.isDraft ? 'تحت رفع' : 'متداول')}</dd></div>
          <div><dt className="inline text-gray-500">دائرة التنفيذ: </dt><dd className="inline font-medium text-gray-800">{file.court || '—'}</dd></div>
          <div><dt className="inline text-gray-500">رقم الملف: </dt><dd className="inline font-medium text-gray-800 tabular-nums">{file.fileNumber || '—'}</dd></div>
          <div><dt className="inline text-gray-500">سنة الملف: </dt><dd className="inline font-medium text-gray-800 tabular-nums">{file.fileYear || '—'}</dd></div>
          <div><dt className="inline text-gray-500">المبلغ: </dt><dd className="inline font-medium text-gray-800 tabular-nums">{amountLine}</dd></div>
          <div><dt className="inline text-gray-500">رقم العقد: </dt><dd className="inline font-medium text-gray-800 tabular-nums">{file.contractNumber || '—'}</dd></div>
          <div><dt className="inline text-gray-500">تاريخ العقد: </dt><dd className="inline font-medium text-gray-800">{file.contractDate || '—'}</dd></div>
        </dl>
      </section>

      {/* بطاقة الأطراف */}
      <section aria-labelledby="pf-parties" className="bg-white rounded-xl shadow p-5 mb-4">
        <h3 id="pf-parties" className="font-bold text-gray-800 mb-3">الأطراف</h3>
        {file.applicant && (
          <p className="text-sm text-gray-700"><span className="text-gray-500">طالب التنفيذ: </span>{file.applicant}</p>
        )}
        {(file.executedPublicEntities ?? []).length > 0 ? (
          <ul className="mt-2 space-y-1">
            {(file.executedPublicEntities ?? []).map((e) => (
              <li key={e.id} className="text-sm text-gray-700 break-words">
                {e.entityName}
                {e.entityBranch ? ` — ${e.entityBranch}` : ''}
                {e.governorate ? ` (${e.governorate})` : ''}
              </li>
            ))}
          </ul>
        ) : (
          !file.applicant && <p className="text-sm text-gray-400">لا توجد جهات مسجلة</p>
        )}
      </section>

      {/* بطاقة الاستئنافات القرائية */}
      <section aria-labelledby="pf-appeals" className="bg-white rounded-xl shadow p-5">
        <h3 id="pf-appeals" className="font-bold text-gray-800 mb-3">الاستئنافات</h3>
        {appeals === null || appeals.length === 0 ? (
          <p className="text-sm text-gray-400">لا توجد استئنافات على هذا الملف</p>
        ) : (
          <ul className="divide-y divide-gray-100">
            {appeals.map((a) => (
              <li key={a.id} className="py-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="font-medium text-gray-800 break-words">
                    {a.appealTypeLabel || 'استئناف'}
                    {a.appealBaseNumber && (
                      <span className="text-xs text-gray-500 tabular-nums"> · أساس {a.appealBaseNumber}{a.appealYear ? `/${a.appealYear}` : ''}</span>
                    )}
                  </span>
                  <span className="rounded-full bg-gray-100 text-gray-700 px-2 py-0.5 text-xs whitespace-nowrap">
                    {STATUS_LABELS[a.status] ?? a.status}
                  </span>
                </div>
                {a.decisionRuling && (
                  <p className="text-xs text-gray-500 mt-1 break-words">القرار: {a.decisionRuling}</p>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      <p className="mt-4 text-xs text-gray-400">عرض قرائي عبر بوابة الجهة العامة.</p>
    </div>
  );
}
