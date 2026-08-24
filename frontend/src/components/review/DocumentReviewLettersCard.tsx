import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../../api/client';
import { formatDate } from '../../utils/dates';
import type { ReviewLetterListItemDto } from '../../types';
import CreateReviewLetterModal from './CreateReviewLetterModal';
import { reviewLetterTitle } from './reviewDisplay';
import ReviewStatusBadge from './ReviewStatusBadge';

/**
 * بطاقة «كتب المطالعات» في تفاصيل الملف: كل كتب هذا الملف حصرًا مع حالتها،
 * وزر «تسطير كتاب مطالعة» يربط الكتاب الجديد بهذا الملف.
 * تخفى البطاقة كليًا عن من لا يملك صلاحية الاطلاع (403).
 */
export default function DocumentReviewLettersCard({
  documentId,
  documentTitle,
  canCreate,
}: {
  documentId: number;
  documentTitle?: string;
  canCreate: boolean;
}) {
  const [items, setItems] = useState<ReviewLetterListItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [hidden, setHidden] = useState(false);
  const [error, setError] = useState('');
  const [createOpen, setCreateOpen] = useState(false);

  const load = useCallback(
    (signal: AbortSignal) =>
      api
        .get<ReviewLetterListItemDto[]>(`/review-letters/document/${documentId}`, { signal })
        .then((r) => {
          setItems(Array.isArray(r.data) ? r.data : []);
          setError('');
        })
        .catch((err) => {
          const status = err?.response?.status;
          if (status === 403) {
            setHidden(true);
            return;
          }
          if (err?.name === 'CanceledError' || err?.code === 'ERR_CANCELED') return;
          setError(getApiErrorMessage(err));
        })
        .finally(() => setLoading(false)),
    [documentId],
  );

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  if (hidden) return null;

  return (
    <section className="bg-white rounded-xl border border-gray-200 shadow-sm px-5 py-4">
      <div className="flex items-center justify-between gap-3 flex-wrap mb-3">
        <h3 className="font-bold text-emerald-800">كتب المطالعة</h3>
        {canCreate && (
          <button
            type="button"
            onClick={() => setCreateOpen(true)}
            className="bg-[#800000] hover:bg-[#9e0e0e] text-white rounded-lg px-4 py-2 text-sm min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-[#800000]"
          >
            تسطير كتاب مطالعة
          </button>
        )}
      </div>

      {error && (
        <p className="text-red-600 text-sm" role="alert">
          {error}
        </p>
      )}

      {loading ? (
        <p className="text-gray-500 text-sm">جارِ التحميل…</p>
      ) : items.length === 0 ? (
        <p className="text-gray-400 text-sm">
          لا توجد كتب مطالعة على هذا الملف{canCreate ? ' — سطّر أول كتاب' : ''}.
        </p>
      ) : (
        <ul className="divide-y divide-gray-100">
          {items.map((item) => (
            <li key={item.id}>
              <Link
                to={`/reviews/${item.id}`}
                className="flex items-center justify-between gap-3 flex-wrap py-3 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 rounded"
              >
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2 flex-wrap text-xs text-gray-500">
                    <span className="font-mono font-semibold text-emerald-800 tabular-nums" dir="ltr">
                      {item.letterNumber}
                    </span>
                    <span aria-hidden="true">•</span>
                    <time dateTime={item.letterDate} className="tabular-nums">
                      {formatDate(item.letterDate)}
                    </time>
                  </div>
                  <span className="block text-sm font-medium text-gray-800 break-words mt-0.5">
                    {reviewLetterTitle(item.fileContext)}
                  </span>
                </div>
                <ReviewStatusBadge isAnswered={item.isAnswered} />
              </Link>
            </li>
          ))}
        </ul>
      )}

      {createOpen && (
        <CreateReviewLetterModal
          documentId={documentId}
          documentTitle={documentTitle}
          onClose={() => setCreateOpen(false)}
          onCreated={() => {
            const controller = new AbortController();
            void load(controller.signal);
          }}
        />
      )}
    </section>
  );
}
