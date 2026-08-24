import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { formatDate } from '../utils/dates';
import type { PagedResult, ReviewLetterListItemDto } from '../types';
import CreateReviewLetterModal from '../components/review/CreateReviewLetterModal';
import ReviewStatusBadge from '../components/review/ReviewStatusBadge';
import { reviewLetterTitle } from '../components/review/reviewDisplay';

const PER_PAGE = 20;

export default function ReviewsList() {
  const { user, hasFullAccess, isHead } = useAuth();
  const [items, setItems] = useState<ReviewLetterListItemDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [q, setQ] = useState('');
  const [refreshKey, setRefreshKey] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [createOpen, setCreateOpen] = useState(false);

  const isLawyer = user?.role === 'lawyer';

  // الجلب مرتبط بالبحث والصفحة ومفتاح تحديث (بعد تسطير كتاب جديد)؛
  // الإلغاء عبر AbortController يمنع سباقات الاستجابات القديمة.
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    const params = new URLSearchParams();
    if (q.trim()) params.set('q', q.trim());
    params.set('page', String(page));
    params.set('perPage', String(PER_PAGE));
    api
      .get<PagedResult<ReviewLetterListItemDto>>(`/review-letters?${params.toString()}`, {
        signal: controller.signal,
      })
      .then((r) => r.data)
      .then((data) => {
        setItems(data.items);
        setTotalCount(data.totalCount);
      })
      .catch((err) => {
        if (err?.name === 'CanceledError' || err?.code === 'ERR_CANCELED') return;
        setError(getApiErrorMessage(err));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [page, q, refreshKey]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PER_PAGE));

  return (
    <div>
      <div className="flex items-center justify-between flex-wrap gap-3 mb-6">
        <h2 className="text-xl sm:text-2xl font-bold text-gray-900">
          {isLawyer ? 'المطالعات' : 'كتب المطالعات'}
        </h2>
        {isLawyer && (
          <button
            onClick={() => setCreateOpen(true)}
            className="bg-[#800000] hover:bg-[#9e0e0e] text-white rounded-lg px-4 py-2 text-sm min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-[#800000]"
          >
            + تسطير مطالعة
          </button>
        )}
      </div>

      <div className="bg-white rounded-xl shadow p-4 mb-6">
        <label htmlFor="reviews-search" className="sr-only">
          بحث في كتب المطالعة
        </label>
        <input
          id="reviews-search"
          name="reviews-search"
          type="search"
          autoComplete="off"
          value={q}
          onChange={(e) => {
            setQ(e.target.value);
            setPage(1);
          }}
          placeholder="بحث برقم الكتاب، اسم المنفذ عليه، أو نص المطالعة…"
          className="w-full min-w-0 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />
      </div>

      {error && <p className="text-red-600 text-sm mb-4">{error}</p>}

      {loading ? (
        <div className="text-gray-500 text-sm">جارِ التحميل…</div>
      ) : items.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-8 text-center">
          <p className="text-gray-500 text-sm">
            {q.trim() ? 'لا توجد كتب مطالعة مطابقة للبحث' : 'لا توجد كتب مطالعة بعد'}
          </p>
        </div>
      ) : (
        <ul className="space-y-3">
          {items.map((item) => (
            <li key={item.id}>
              <Link
                to={`/reviews/${item.id}`}
                className="block bg-white rounded-xl border border-gray-200 shadow-sm hover:shadow-md transition-shadow p-4 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
              >
                <div className="flex items-start justify-between gap-3 flex-wrap mb-1.5">
                  <div className="flex items-center gap-2 flex-wrap min-w-0">
                    <span className="font-mono text-sm font-semibold text-emerald-800 tabular-nums" dir="ltr">
                      {item.letterNumber}
                    </span>
                    <span className="text-xs text-gray-400" aria-hidden="true">•</span>
                    <time dateTime={item.letterDate} className="text-xs text-gray-500 tabular-nums">
                      {formatDate(item.letterDate)}
                    </time>
                  </div>
                  <div className="flex items-center gap-2 flex-wrap">
                    {isLawyer && item.hasUnseenReply && (
                      <span className="inline-flex items-center gap-1 rounded-full bg-red-600 text-white px-2.5 py-0.5 text-[11px] font-bold whitespace-nowrap">
                        <span className="w-1.5 h-1.5 rounded-full bg-white animate-pulse" aria-hidden="true" />
                        رد جديد
                      </span>
                    )}
                    <ReviewStatusBadge isAnswered={item.isAnswered} />
                  </div>
                </div>
                <h3 className="font-semibold text-gray-800 text-sm leading-relaxed break-words">
                  {reviewLetterTitle(item.fileContext)}
                </h3>
                {!hasFullAccess && !isHead && item.snippet && (
                  <p className="text-xs text-gray-500 mt-1 truncate">{item.snippet}</p>
                )}
                {(hasFullAccess || isHead) && (
                  <p className="text-xs text-gray-500 mt-1">
                    سطّره: <span className="font-medium text-gray-700">{item.lawyerName}</span>
                    {' · '}
                    {item.messagesCount} رسالة
                  </p>
                )}
              </Link>
            </li>
          ))}
        </ul>
      )}

      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-4 mt-6">
          <button
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page <= 1 || loading}
            className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-40 min-h-11"
            aria-label="الصفحة السابقة"
          >
            السابق
          </button>
          <span className="text-sm text-gray-600 tabular-nums">
            {page} / {totalPages}
          </span>
          <button
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={page >= totalPages || loading}
            className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-40 min-h-11"
            aria-label="الصفحة التالية"
          >
            التالي
          </button>
        </div>
      )}

      {createOpen && (
        <CreateReviewLetterModal
          onClose={() => setCreateOpen(false)}
          onCreated={() => {
            setPage(1);
            setRefreshKey((k) => k + 1);
          }}
        />
      )}
    </div>
  );
}
