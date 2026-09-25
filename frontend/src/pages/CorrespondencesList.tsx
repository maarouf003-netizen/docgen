import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { formatDate } from '../utils/dates';
import type {
  CorrespondenceImportance,
  CorrespondenceListItemDto,
  PagedResult,
} from '../types';
import CreateCorrespondenceModal from '../components/correspondence/CreateCorrespondenceModal';
import CorrespondenceImportanceBadge from '../components/correspondence/CorrespondenceImportanceBadge';
import {
  CORRESPONDENCE_IMPORTANCE_LABELS,
  correspondenceTitle,
} from '../components/correspondence/correspondenceDisplay';

const PER_PAGE = 20;

/**
 * قائمة المراسلات: الطرف يرى مراسلاته، ورئيس القسم مراسلات محافظته (فرعه +
 * العامة من مندوبيها)، والمدير/المشرف بمحافظة منتقاة إجباريًا — مع فلتر أهمية
 * وبحث بالرقم والاسم والنص. زر «+ مراسلة جديدة» عامة (غير مرتبطة بملف).
 */
export default function CorrespondencesList({ portal = false }: { portal?: boolean }) {
  const { user, hasFullAccess, isHead } = useAuth();
  const base = portal ? '/portal/correspondence' : '/correspondence';
  const detailBase = portal ? '/portal/correspondence' : '/correspondence';
  const [items, setItems] = useState<CorrespondenceListItemDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [q, setQ] = useState('');
  const [importance, setImportance] = useState('');
  const [refreshKey, setRefreshKey] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [createOpen, setCreateOpen] = useState(false);

  const canSeeGovernorate = hasFullAccess && !portal;
  const [governorate, setGovernorate] = useState('');
  const [governorates, setGovernorates] = useState<string[]>([]);

  // الكتابة: محامٍ/رئيس قسم في المسار الرئيسي، ومندوب الجهة في البوابة حصرًا —
  // تطابق CanCreateCorrespondences في الخلفية (المدير/المشرف قراءة فقط).
  const canCreate = portal
    ? user?.role === 'entitymanager'
    : user?.role === 'lawyer' || user?.role === 'head';

  // جلب خيارات فلتر المحافظة — مرة واحدة فقط للمدير/المشرف.
  useEffect(() => {
    if (!canSeeGovernorate) return;
    const controller = new AbortController();
    api
      .get<{ governorates: string[] }>('/correspondence/filter-options', {
        signal: controller.signal,
      })
      .then((r) => r.data)
      .then((data) => {
        setGovernorates(Array.isArray(data.governorates) ? data.governorates : []);
      })
      .catch(() => {});
    return () => controller.abort();
  }, [canSeeGovernorate]);

  // جلب القائمة — لا يُستدعى قبل اختيار المحافظة للمدير/المشرف.
  useEffect(() => {
    const controller = new AbortController();

    if (canSeeGovernorate && !governorate.trim()) {
      setItems([]);
      setTotalCount(0);
      setLoading(false);
      setError('');
      return () => controller.abort();
    }

    setLoading(true);
    setError('');
    const params = new URLSearchParams();
    if (q.trim()) params.set('q', q.trim());
    if (governorate.trim()) params.set('governorate', governorate.trim());
    if (importance) params.set('importance', importance);
    params.set('page', String(page));
    params.set('perPage', String(PER_PAGE));
    api
      .get<PagedResult<CorrespondenceListItemDto>>(`${base}?${params.toString()}`, {
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
  }, [page, q, importance, refreshKey, governorate, canSeeGovernorate, base]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PER_PAGE));

  return (
    <div>
      <div className="flex items-center justify-between flex-wrap gap-3 mb-6">
        <h2 className="text-xl sm:text-2xl font-bold text-gray-900">المراسلات</h2>
        {canCreate && (
          <button
            onClick={() => setCreateOpen(true)}
            className="bg-[#800000] hover:bg-[#9e0e0e] text-white rounded-lg px-4 py-2 text-sm min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-[#800000]"
          >
            + مراسلة جديدة
          </button>
        )}
      </div>

      <div className="bg-white rounded-xl shadow p-4 mb-6 space-y-3">
        {canSeeGovernorate && (
          <div>
            <label htmlFor="correspondence-governorate-filter" className="block text-xs font-medium text-gray-500 mb-1">
              المحافظة
            </label>
            <select
              id="correspondence-governorate-filter"
              name="correspondence-governorate-filter"
              aria-label="فلتر المحافظة"
              value={governorate}
              onChange={(e) => {
                setGovernorate(e.target.value);
                setPage(1);
              }}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm min-h-11 focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              <option value="">اختر المحافظة…</option>
              {governorates.map((g) => (
                <option key={g} value={g}>
                  {g}
                </option>
              ))}
            </select>
          </div>
        )}
        <div className="flex gap-3 flex-wrap">
          <div className="flex-1 min-w-0">
            <label htmlFor="correspondence-search" className="sr-only">
              بحث في المراسلات
            </label>
            <input
              id="correspondence-search"
              name="correspondence-search"
              type="search"
              autoComplete="off"
              value={q}
              onChange={(e) => {
                setQ(e.target.value);
                setPage(1);
              }}
              placeholder="بحث برقم المراسلة، اسم المنفذ عليه، أو الاسم أو النص…"
              className="w-full min-w-0 border border-gray-300 rounded-lg px-3 py-2 text-sm min-h-11 focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="correspondence-importance-filter" className="sr-only">
              فلتر الأهمية
            </label>
            <select
              id="correspondence-importance-filter"
              name="correspondence-importance-filter"
              aria-label="فلتر الأهمية"
              value={importance}
              onChange={(e) => {
                setImportance(e.target.value);
                setPage(1);
              }}
              className="border border-gray-300 rounded-lg px-3 py-2 text-sm min-h-11 focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              <option value="">أهمية المراسلة</option>
              {(Object.keys(CORRESPONDENCE_IMPORTANCE_LABELS) as CorrespondenceImportance[]).map((key) => (
                <option key={key} value={key}>
                  {CORRESPONDENCE_IMPORTANCE_LABELS[key]}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {error && <p className="text-red-600 text-sm mb-4">{error}</p>}

      {canSeeGovernorate && !governorate.trim() ? (
        <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-8 text-center">
          <p className="text-gray-500 text-sm">اختر المحافظة لعرض المراسلات…</p>
        </div>
      ) : loading ? (
        <div className="text-gray-500 text-sm">جارِ التحميل…</div>
      ) : items.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-8 text-center">
          <p className="text-gray-500 text-sm">
            {q.trim() || importance ? 'لا توجد مراسلات مطابقة للبحث' : 'لا توجد مراسلات بعد'}
          </p>
        </div>
      ) : (
        <ul className="space-y-3">
          {items.map((item) => (
            <li key={item.id}>
              <Link
                to={`${detailBase}/${item.id}`}
                className="block bg-white rounded-xl border border-gray-200 shadow-sm hover:shadow-md transition-shadow p-4 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
              >
                <div className="flex items-start justify-between gap-3 flex-wrap mb-1.5">
                  <div className="flex items-center gap-2 flex-wrap min-w-0">
                    <span className="font-mono text-sm font-semibold text-emerald-800 tabular-nums" dir="ltr">
                      {item.correspondenceNumber}
                    </span>
                    <span className="text-xs text-gray-400" aria-hidden="true">•</span>
                    <time dateTime={item.correspondenceDate} className="text-xs text-gray-500 tabular-nums">
                      {formatDate(item.correspondenceDate)}
                    </time>
                  </div>
                  <div className="flex items-center gap-2 flex-wrap">
                    {item.isUrgentUnseen && (
                      <span className="inline-flex items-center gap-1 rounded-full bg-red-600 text-white px-2.5 py-0.5 text-[11px] font-bold whitespace-nowrap">
                        <span className="w-1.5 h-1.5 rounded-full bg-white animate-pulse" aria-hidden="true" />
                        عاجل بلا مشاهدة
                      </span>
                    )}
                    <CorrespondenceImportanceBadge importance={item.importance} />
                  </div>
                </div>
                <h3 className="font-semibold text-gray-800 text-sm leading-relaxed break-words">
                  {correspondenceTitle(item.fileContext)}
                </h3>
                <p className="text-xs text-gray-500 mt-1">
                  من: <span className="font-medium text-gray-700">{item.creatorName}</span>
                  {' · '}إلى: <span className="font-medium text-gray-700">{item.targetName}</span>
                  {' · '}
                  {item.messagesCount} رسالة
                  {item.receiptsCount > 0 && ` · ${item.receiptsCount} مشاهَدة موثقة`}
                </p>
                {(hasFullAccess || isHead) && (
                  <p className="text-xs text-gray-500 mt-0.5">
                    المحافظة: <span className="font-medium text-gray-700">{item.governorate}</span>
                    {item.administrativeBranchName && (
                      <> · فرع الإدارة: <span className="font-medium text-gray-700">{item.administrativeBranchName}</span></>
                    )}
                  </p>
                )}
                {!hasFullAccess && !isHead && item.snippet && (
                  <p className="text-xs text-gray-500 mt-1 truncate">{item.snippet}</p>
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
        <CreateCorrespondenceModal
          portal={portal}
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
