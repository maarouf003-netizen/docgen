import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../../api/client';
import { formatDate } from '../../utils/dates';
import type { CorrespondenceListItemDto } from '../../types';
import CreateCorrespondenceModal from './CreateCorrespondenceModal';
import CorrespondenceImportanceBadge from './CorrespondenceImportanceBadge';
import CorrespondenceSeenBadge from './CorrespondenceSeenBadge';
import { correspondenceTitle } from './correspondenceDisplay';

/**
 * بطاقة «المراسلات» في تفاصيل الملف: مراسلات هذا الملف حصرًا مع أهميتها.
 * للمندوب تُغذَّى من مسار البوابة (ما هو طرف فيه فقط)، ولغيره من المسار الرئيسي.
 * تخفى البطاقة كليًا عن من لا يملك صلاحية الاطلاع (403/404).
 */
export default function DocumentCorrespondenceCard({
  documentId,
  documentTitle,
  canCreate,
  portal = false,
}: {
  documentId: number;
  documentTitle?: string;
  canCreate: boolean;
  portal?: boolean;
}) {
  const detailBase = portal ? '/portal/correspondence' : '/correspondence';
  const [items, setItems] = useState<CorrespondenceListItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [hidden, setHidden] = useState(false);
  const [error, setError] = useState('');
  const [createOpen, setCreateOpen] = useState(false);

  const endpoint = portal ? `/portal/files/${documentId}/correspondence` : `/correspondence/document/${documentId}`;

  const load = useCallback(
    (signal: AbortSignal) =>
      api
        .get<CorrespondenceListItemDto[]>(endpoint, { signal })
        .then((r) => {
          setItems(Array.isArray(r.data) ? r.data : []);
          setError('');
        })
        .catch((err) => {
          const status = err?.response?.status;
          if (status === 403 || status === 404) {
            setHidden(true);
            return;
          }
          if (err?.name === 'CanceledError' || err?.code === 'ERR_CANCELED') return;
          setError(getApiErrorMessage(err));
        })
        .finally(() => setLoading(false)),
    [endpoint],
  );

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  if (hidden) return null;

  return (
    <section
      id="file-correspondence"
      aria-label="مراسلات الملف"
      className="bg-white rounded-xl border border-gray-200 shadow-sm px-5 py-4 scroll-mt-24"
    >
      <div className="flex items-center justify-between gap-3 flex-wrap mb-3">
        <h3 className="font-bold text-emerald-800">المراسلات</h3>
        {canCreate && (
          <button
            type="button"
            onClick={() => setCreateOpen(true)}
            className="bg-[#800000] hover:bg-[#9e0e0e] text-white rounded-lg px-4 py-2 text-sm min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-[#800000]"
          >
            تسطير مراسلة
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
          لا توجد مراسلات على هذا الملف{canCreate ? ' — سطّر أول مراسلة' : ''}.
        </p>
      ) : (
        <ul className="divide-y divide-gray-100">
          {items.map((item) => (
            <li key={item.id}>
              <Link
                to={`${detailBase}/${item.id}`}
                className="flex items-center justify-between gap-3 flex-wrap py-3 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 rounded"
              >
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2 flex-wrap text-xs text-gray-500">
                    <span className="font-mono font-semibold text-emerald-800 tabular-nums" dir="ltr">
                      {item.correspondenceNumber}
                    </span>
                    <span aria-hidden="true">•</span>
                    <time dateTime={item.correspondenceDate} className="tabular-nums">
                      {formatDate(item.correspondenceDate)}
                    </time>
                  </div>
                  <span className="block text-sm font-medium text-gray-800 break-words mt-0.5">
                    {correspondenceTitle(item.fileContext)}
                  </span>
                  <span className="block text-xs text-gray-500 mt-0.5 truncate">
                    إلى: {item.targetName}
                  </span>
                </div>
                <div className="flex items-center gap-2 flex-wrap">
                  <CorrespondenceSeenBadge
                    viewStatus={item.viewStatus}
                    canMarkSeen={item.canMarkSeen}
                  />
                  <CorrespondenceImportanceBadge importance={item.importance} />
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}

      {createOpen && (
        <CreateCorrespondenceModal
          documentId={documentId}
          documentTitle={documentTitle}
          portal={portal}
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
