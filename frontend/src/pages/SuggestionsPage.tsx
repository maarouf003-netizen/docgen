import { useState } from 'react';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { api, getApiErrorMessage } from '../api/client';
import type { AppSuggestionDto, PagedResult } from '../types';

/** حجم صفحة صندوق المشرف — يطابق افتراضي الخلفية. */
const PER_PAGE = 20;

/**
 * صندوق اقتراحات التطوير للمشرف (`/suggestions` — دنيا لكن كاملة):
 * مرقّم الصفحات (الأحدث أولًا) مع المرسل، وتعليم المقروء.
 */
export default function SuggestionsPage() {
  const [page, setPage] = useState(1);
  const [markingId, setMarkingId] = useState<number | null>(null);
  const [error, setError] = useState('');

  const suggestionsQuery = useCancellableRequest<PagedResult<AppSuggestionDto>>(
    (signal) =>
      api
        .get('/app-suggestions', { params: { page, perPage: PER_PAGE }, signal })
        .then((r) => r.data),
    [page],
    { enabled: true },
  );
  const suggestions = suggestionsQuery.data?.items ?? [];
  const totalPages = suggestionsQuery.data?.totalPages ?? 0;
  const currentPage = suggestionsQuery.data?.page ?? page;
  const unread = suggestions.filter((s) => !s.isRead).length;

  const markRead = async (id: number) => {
    setMarkingId(id);
    setError('');
    try {
      await api.patch(`/app-suggestions/${id}/read`);
      suggestionsQuery.setData((prev) =>
        prev ? { ...prev, items: prev.items.map((s) => (s.id === id ? { ...s, isRead: true } : s)) } : prev,
      );
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setMarkingId(null);
    }
  };

  return (
    <div className="max-w-3xl mx-auto">
      <div className="flex items-center gap-2 mb-6">
        <h2 className="text-xl sm:text-2xl font-bold text-gray-900 text-balance">اقتراحات التطوير</h2>
        {unread > 0 ? (
          <span className="text-xs bg-red-100 text-red-800 rounded-full px-2 py-0.5 font-medium tabular-nums">
            {unread} غير مقروء
          </span>
        ) : null}
      </div>

      {error ? (
        <p role="alert" className="text-red-700 text-sm bg-red-50 border border-red-100 rounded-xl px-3 py-2 mb-4">
          {error}
        </p>
      ) : null}

      {suggestionsQuery.isLoading ? (
        <p className="text-gray-500">جارِ التحميل...</p>
      ) : suggestionsQuery.error ? (
        <p role="alert" className="text-red-700 text-sm">
          {suggestionsQuery.error}
        </p>
      ) : suggestions.length === 0 ? (
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-10 text-center">
          <p className="text-gray-400 text-sm">لا اقتراحات بعد.</p>
        </div>
      ) : (
        <>
          <ul className="bg-white rounded-2xl shadow-sm border border-gray-100 divide-y divide-gray-100 overflow-hidden">
          {suggestions.map((s) => (
            <li key={s.id} className="px-4 sm:px-5 py-3.5">
              <p className="text-sm text-gray-800 break-words">{s.message}</p>
              <p className="flex flex-wrap items-center gap-x-2 gap-y-1 mt-1.5 text-xs text-gray-400">
                <span className="font-medium text-gray-600">{s.senderName ?? '—'}</span>
                <span className="tabular-nums" dir="ltr">
                  {s.createdAt.slice(0, 10)}
                </span>
                {s.isRead ? (
                  <span className="rounded-full px-2 py-0.5 font-medium bg-emerald-100 text-emerald-700">
                    مقروء
                  </span>
                ) : (
                  <button
                    type="button"
                    onClick={() => markRead(s.id)}
                    disabled={markingId === s.id}
                    className="min-h-11 px-3 rounded-lg border border-gray-200 hover:bg-gray-50 disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-emerald-600 font-medium text-gray-700"
                  >
                    {markingId === s.id ? 'جارٍ التعليم…' : 'تعليم كمقروء'}
                  </button>
                )}
              </p>
            </li>
          ))}
          </ul>
          {totalPages > 1 ? (
            <nav aria-label="صفحات الاقتراحات" className="flex items-center justify-center gap-3 mt-4">
              <button
                type="button"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={currentPage <= 1}
                className="min-h-11 px-4 rounded-lg border border-gray-200 bg-white text-sm hover:bg-gray-50 disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
              >
                السابق
              </button>
              <span className="text-sm text-gray-600 tabular-nums">
                صفحة {currentPage} من {totalPages}
              </span>
              <button
                type="button"
                onClick={() => setPage((p) => p + 1)}
                disabled={currentPage >= totalPages}
                className="min-h-11 px-4 rounded-lg border border-gray-200 bg-white text-sm hover:bg-gray-50 disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
              >
                التالي
              </button>
            </nav>
          ) : null}
        </>
      )}
    </div>
  );
}
