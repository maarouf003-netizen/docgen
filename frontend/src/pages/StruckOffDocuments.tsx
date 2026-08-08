import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useIsMobile } from '../hooks/useMediaQuery';
import { getDocumentBadge } from '../utils/documentStatus';
import type { DocumentResponse, PagedResult } from '../types';

function executedFullName(d: DocumentResponse): string {
  const person = d.executedNaturalPersons?.[0];
  const personName = person
    ? [person.name, person.father, person.family].filter(Boolean).join(' ')
    : '';
  const entity = d.executedPublicEntities?.[0]?.entityName ?? '';
  const applicant = d.applicant ?? '';
  return personName || entity || applicant || '';
}

function displayFileNumber(d: DocumentResponse) {
  if (d.isDraft) return '';
  const number = d.displayFileNumber ?? d.fileNumber ?? '';
  const type = d.fileType ?? '';
  return type ? `${number} ${type}`.trim() : number;
}

function formatStruckOffAt(value?: string) {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString('ar-SY');
}

export default function StruckOffDocuments() {
  const { user } = useAuth();
  const isMobile = useIsMobile();
  // إعادة الملف المشطوب من اختصاص المحامي صاحب الملف فقط (بذات حكم المحذوفات).
  const canRestore = user?.role === 'lawyer';
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PagedResult<DocumentResponse> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [confirmId, setConfirmId] = useState<number | null>(null);
  const [restoringId, setRestoringId] = useState<number | null>(null);

  const load = () => {
    setLoading(true);
    const params = new URLSearchParams();
    if (query) params.set('q', query);
    params.set('page', String(page));
    params.set('perPage', '20');
    api
      .get<PagedResult<DocumentResponse>>(`/documents/struck-off?${params.toString()}`)
      .then((r) => setData(r.data))
      .catch((err) => setError(getApiErrorMessage(err)))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query, page]);

  const handleRestore = async (d: DocumentResponse) => {
    setRestoringId(d.id);
    setError('');
    try {
      await api.post(`/documents/${d.id}/restore-struck-off`);
      setMessage(`أعيد الملف "${executedFullName(d) || d.id}" إلى المتداول`);
      setConfirmId(null);
      load();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setRestoringId(null);
    }
  };

  const restoreButton = (d: DocumentResponse) =>
    confirmId === d.id ? (
      <div className="flex gap-2 flex-wrap">
        <button
          type="button"
          onClick={() => handleRestore(d)}
          disabled={restoringId === d.id}
          className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-3 py-1.5 text-sm font-medium min-h-11"
        >
          {restoringId === d.id ? 'جارِ الإعادة...' : 'تأكيد الإعادة'}
        </button>
        <button
          type="button"
          onClick={() => setConfirmId(null)}
          disabled={restoringId === d.id}
          className="border border-gray-300 rounded-lg px-3 py-1.5 text-sm text-gray-700 min-h-11"
        >
          إلغاء
        </button>
      </div>
    ) : (
      <button
        type="button"
        onClick={() => setConfirmId(d.id)}
        className="border border-emerald-700 text-emerald-800 hover:bg-emerald-50 rounded-lg px-3 py-1.5 text-sm font-medium min-h-11"
      >
        إعادة الملف
      </button>
    );

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-6 flex-wrap gap-3">
        <h2 className="text-2xl font-bold text-gray-800">الملفات المشطوبة</h2>
        <Link
          to="/documents"
          className="bg-white hover:bg-gray-50 text-gray-700 border border-gray-300 rounded-lg px-4 py-2 text-sm font-medium min-h-11 inline-flex items-center"
        >
          ← الملفات التنفيذية
        </Link>
      </div>

      <div className="bg-white rounded-xl shadow p-4 mb-6 flex flex-col sm:flex-row gap-3">
        <input
          value={query}
          onChange={(e) => { setQuery(e.target.value); setPage(1); }}
          placeholder="بحث في الملفات المشطوبة..."
          className="flex-1 min-w-64 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11"
        />
      </div>

      {error && <div className="text-red-600 mb-4">{error}</div>}
      {message && <div className="text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-lg px-4 py-2 mb-4">{message}</div>}

      {loading && <div className="text-gray-500">جارِ البحث...</div>}

      {data && (
        <>
          {isMobile ? (
            <div className="flex flex-col gap-4">
              {data.items.map((d) => {
                const badge = getDocumentBadge(d);
                return (
                  <article key={d.id} className="bg-white rounded-xl shadow p-4">
                    <div className="flex items-start justify-between gap-2 mb-2">
                      <Link
                        to={`/documents/${d.id}`}
                        className="text-emerald-800 font-bold text-lg hover:underline min-h-11"
                      >
                        {executedFullName(d) || `مستند ${d.id}`}
                      </Link>
                      <span className={`text-xs px-2 py-1 rounded-full shrink-0 ${badge.cls}`}>
                        {badge.text}
                      </span>
                    </div>
                    <div className="text-sm text-gray-600">
                      {d.applicant || '—'} · {d.branchName || '—'} · {d.court || '—'}
                    </div>
                    <div className="text-sm font-medium text-gray-800 mt-1">
                      رقم الملف: {displayFileNumber(d) || '—'}
                    </div>
                    <div className="text-xs text-gray-500 mt-1">
                      شُطب في {formatStruckOffAt(d.struckOffDate)}
                    </div>
                    {canRestore && (
                      <div className="mt-3 pt-3 border-t border-gray-100">{restoreButton(d)}</div>
                    )}
                  </article>
                );
              })}
              {data.items.length === 0 && (
                <div className="bg-white rounded-xl shadow p-8 text-center text-gray-400">
                  لا توجد ملفات مشطوبة
                </div>
              )}
            </div>
          ) : (
            <div className="bg-white rounded-xl shadow overflow-hidden">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-gray-600">
                  <tr className="text-right">
                    <th className="px-4 py-3">تاريخ الشطب</th>
                    <th className="px-4 py-3">المنفذ عليه</th>
                    <th className="px-4 py-3">طالب التنفيذ</th>
                    <th className="px-4 py-3">الفرع</th>
                    <th className="px-4 py-3">دائرة التنفيذ</th>
                    <th className="px-4 py-3">رقم الملف</th>
                    <th className="px-4 py-3">إجراء</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {data.items.map((d) => (
                    <tr key={d.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3 whitespace-nowrap text-gray-600">
                        {formatStruckOffAt(d.struckOffDate)}
                      </td>
                      <td className="px-4 py-3">
                        <Link to={`/documents/${d.id}`} className="font-medium text-gray-800 hover:text-emerald-700 hover:underline inline-flex items-center min-h-11">
                          {executedFullName(d) || `مستند ${d.id}`}
                        </Link>
                      </td>
                      <td className="px-4 py-3">{d.applicant || '—'}</td>
                      <td className="px-4 py-3">{d.branchName || '—'}</td>
                      <td className="px-4 py-3">{d.court || '—'}</td>
                      <td className="px-4 py-3">{displayFileNumber(d)}</td>
                      <td className="px-4 py-3">{canRestore ? restoreButton(d) : '—'}</td>
                    </tr>
                  ))}
                  {data.items.length === 0 && (
                    <tr>
                      <td colSpan={7} className="px-4 py-8 text-center text-gray-400">
                        لا توجد ملفات مشطوبة
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          )}

          <div className="flex items-center justify-between mt-4 text-sm text-gray-600 flex-wrap gap-2">
            <span>
              صفحة {data.page} من {data.totalPages || 1} ({data.totalCount} نتيجة)
            </span>
            <div className="flex gap-2">
              <button
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
                className="px-3 py-1.5 border border-gray-300 rounded-lg disabled:opacity-40 hover:bg-gray-50 min-h-11"
              >
                السابق
              </button>
              <button
                disabled={page >= data.totalPages}
                onClick={() => setPage(page + 1)}
                className="px-3 py-1.5 border border-gray-300 rounded-lg disabled:opacity-40 hover:bg-gray-50 min-h-11"
              >
                التالي
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
