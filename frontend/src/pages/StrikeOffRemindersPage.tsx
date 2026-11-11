import { useState } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import ExecutionActionsModal from '../components/ExecutionActionsModal';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { useIsMobile } from '../hooks/useMediaQuery';
import { formatDate } from '../utils/dates';
import { applicantName, fullName } from '../utils/documentDisplay';
import type { DocumentResponse, PagedResult } from '../types';

/** مصدر تاريخ «آخر إجراء» المعروض — يُعلن بصدق حين لا يكون إجراءً مسجّلًا. */
function sourceLabel(source?: string): string | null {
  if (source === 'registration') return 'تاريخ القيد';
  if (source === 'created') return 'تاريخ الإدخال';
  return null;
}

/**
 * «تذكيرات الشطب»: ملفات «طالبة تنفيذ» النشطة التي مضى على آخر إجراء مسجّل فيها 5 أشهر
 * (قبل مهلة الشطب القانونية). الحساب في الخلفية وحدها؛ الصفحة تعرض وتُدير الإلغاء فقط.
 * الخطة: `docs/STRIKE_OFF_REMINDERS_PLAN.md`.
 */
export default function StrikeOffRemindersPage() {
  const isMobile = useIsMobile();
  const [page, setPage] = useState(1);
  const [confirmId, setConfirmId] = useState<number | null>(null);
  const [dismissingId, setDismissingId] = useState<number | null>(null);
  const [actionsDocId, setActionsDocId] = useState<number | null>(null);
  const [message, setMessage] = useState('');
  const [actionError, setActionError] = useState('');

  const listQuery = useCancellableRequest<PagedResult<DocumentResponse>>(
    (signal) => {
      const params = new URLSearchParams({ page: String(page), perPage: '20' });
      return api.get(`/documents/strike-off-reminders?${params.toString()}`, { signal }).then((r) => r.data);
    },
    [page],
  );

  const data = listQuery.data;

  const dismiss = async (d: DocumentResponse) => {
    setDismissingId(d.id);
    setActionError('');
    try {
      await api.post(`/documents/${d.id}/strike-off-reminder/dismiss`);
      setMessage(`أُلغي تذكير الشطب للملف «${fullName(d) || d.displayFileNumber || d.id}» — يعود عند تسجيل إجراء جديد.`);
      setConfirmId(null);
      listQuery.refetch();
    } catch (err) {
      setActionError(getApiErrorMessage(err));
    } finally {
      setDismissingId(null);
    }
  };

  const dismissButton = (d: DocumentResponse) =>
    confirmId === d.id ? (
      <div className="flex flex-wrap gap-2">
        <button
          type="button"
          onClick={() => dismiss(d)}
          disabled={dismissingId === d.id}
          className="bg-red-700 hover:bg-red-600 disabled:opacity-50 text-white rounded-lg px-3 py-1.5 text-sm font-medium min-h-11"
        >
          {dismissingId === d.id ? 'جارِ الإلغاء...' : 'تأكيد إلغاء التذكير'}
        </button>
        <button
          type="button"
          onClick={() => setConfirmId(null)}
          disabled={dismissingId === d.id}
          className="border border-gray-300 rounded-lg px-3 py-1.5 text-sm text-gray-700 min-h-11"
        >
          تراجع
        </button>
      </div>
    ) : (
      <button
        type="button"
        onClick={() => { setActionError(''); setConfirmId(d.id); }}
        className="border border-gray-300 text-gray-700 hover:bg-gray-50 rounded-lg px-3 py-1.5 text-sm font-medium min-h-11"
      >
        إلغاء التذكير
      </button>
    );

  const actionsButton = (d: DocumentResponse) => (
    <button
      type="button"
      onClick={() => setActionsDocId(d.id)}
      className="border border-emerald-700 text-emerald-800 hover:bg-emerald-50 rounded-lg px-3 py-1.5 text-sm font-medium min-h-11"
    >
      عرض الإجراءات والملاحظات
    </button>
  );

  const lastActivity = (d: DocumentResponse) => {
    const source = sourceLabel(d.lastActivitySource);
    return (
      <span className="inline-flex flex-col">
        <span className="tabular-nums text-gray-800">{formatDate(d.lastActivityDate, '—')}</span>
        {source ? <span className="text-[11px] text-gray-500">{source}</span> : null}
      </span>
    );
  };

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-6 flex-wrap gap-3">
        <div>
          <h2 className="text-2xl font-bold text-gray-800">تذكيرات الشطب</h2>
          <p className="text-sm text-gray-500 mt-1">
            ملفات مضى على آخر إجراء مسجّل فيها 5 أشهر — راجعها قبل مهلة الشطب (6 أشهر).
          </p>
        </div>
        <Link
          to="/documents"
          className="bg-white hover:bg-gray-50 text-gray-700 border border-gray-300 rounded-lg px-4 py-2 text-sm font-medium min-h-11 inline-flex items-center"
        >
          ← الملفات التنفيذية
        </Link>
      </div>

      {listQuery.error && (
        <div role="alert" className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm mb-4">
          {listQuery.error}
        </div>
      )}
      {actionError && (
        <div role="alert" className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm mb-4">
          {actionError}
        </div>
      )}
      {message && (
        <div className="text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-lg px-4 py-2 mb-4">
          {message}
        </div>
      )}

      {listQuery.isLoading && <div className="text-gray-500">جارِ التحميل...</div>}

      {data && (
        <>
          {isMobile ? (
            <div className="flex flex-col gap-4">
              {data.items.map((d) => (
                <article key={d.id} className="bg-white rounded-xl shadow p-4">
                  <div className="flex items-start justify-between gap-2 mb-2">
                    <Link to={`/documents/${d.id}`} className="text-emerald-800 font-bold text-lg hover:underline">
                      {d.displayFileNumber ?? d.fileNumber ?? `ملف ${d.id}`}
                    </Link>
                    <span className="text-xs px-2 py-1 rounded-full shrink-0 bg-amber-100 text-amber-800">
                      {d.fileType || '—'}
                    </span>
                  </div>
                  <dl className="text-sm text-gray-600 space-y-1">
                    <div><span className="font-medium text-gray-700">دارة التنفيذ: </span>{d.court || '—'}</div>
                    <div><span className="font-medium text-gray-700">طالب التنفيذ: </span>{applicantName(d) || '—'}</div>
                    <div><span className="font-medium text-gray-700">المنفذ عليه: </span>{fullName(d) || '—'}</div>
                    <div>
                      <span className="font-medium text-gray-700">تاريخ آخر إجراء: </span>
                      {lastActivity(d)}
                    </div>
                  </dl>
                  <div className="mt-3 pt-3 border-t border-gray-100 flex flex-col gap-2">
                    {actionsButton(d)}
                    {dismissButton(d)}
                  </div>
                </article>
              ))}
              {data.items.length === 0 && (
                <div className="bg-white rounded-xl shadow p-8 text-center text-gray-400">
                  لا ملفات مقاربة للشطب حاليًا.
                </div>
              )}
            </div>
          ) : (
            <div className="bg-white rounded-xl shadow overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-gray-600">
                  <tr className="text-right">
                    <th className="px-4 py-3">رقم الملف</th>
                    <th className="px-4 py-3">نوعه</th>
                    <th className="px-4 py-3">دارة التنفيذ</th>
                    <th className="px-4 py-3">طالب التنفيذ</th>
                    <th className="px-4 py-3">المنفذ عليه</th>
                    <th className="px-4 py-3">تاريخ آخر إجراء</th>
                    <th className="px-4 py-3">إجراء</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {data.items.map((d) => (
                    <tr key={d.id} className="hover:bg-gray-50 align-top">
                      <td className="px-4 py-3 font-medium whitespace-nowrap">
                        <Link to={`/documents/${d.id}`} className="text-emerald-800 hover:underline">
                          {d.displayFileNumber ?? d.fileNumber ?? `ملف ${d.id}`}
                        </Link>
                      </td>
                      <td className="px-4 py-3">{d.fileType || '—'}</td>
                      <td className="px-4 py-3">{d.court || '—'}</td>
                      <td className="px-4 py-3">{applicantName(d) || '—'}</td>
                      <td className="px-4 py-3">{fullName(d) || '—'}</td>
                      <td className="px-4 py-3 whitespace-nowrap">{lastActivity(d)}</td>
                      <td className="px-4 py-3">
                        <div className="flex flex-col gap-2 items-start">
                          {actionsButton(d)}
                          {dismissButton(d)}
                        </div>
                      </td>
                    </tr>
                  ))}
                  {data.items.length === 0 && (
                    <tr>
                      <td colSpan={7} className="px-4 py-8 text-center text-gray-400">
                        لا ملفات مقاربة للشطب حاليًا.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          )}

          <div className="flex items-center justify-between mt-4 text-sm text-gray-600 flex-wrap gap-2">
            <span>
              صفحة {data.page} من {data.totalPages || 1} ({data.totalCount} ملفًا)
            </span>
            <div className="flex gap-2">
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
                className="px-3 py-1.5 border border-gray-300 rounded-lg disabled:opacity-40 hover:bg-gray-50 min-h-11"
              >
                السابق
              </button>
              <button
                type="button"
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

      {actionsDocId !== null && (
        <ExecutionActionsModal
          documentId={actionsDocId}
          onClose={() => setActionsDocId(null)}
          onChanged={() => listQuery.refetch()}
        />
      )}
    </div>
  );
}
