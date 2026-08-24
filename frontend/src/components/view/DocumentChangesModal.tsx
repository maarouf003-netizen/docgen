import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { formatDateTime } from '../../utils/dates';
import type { DocumentChangeGroupDto, PagedResult } from '../../types';

const PER_PAGE = 10;

/** تسميات أنواع إدخالات سجل التعديلات كما تظهر للمستخدم. */
const ACTION_LABELS: Record<string, string> = {
  create: 'إنشاء الملف',
  update: 'تعديل بيانات الملف',
  status: 'تغيير الحالة التنفيذية',
  'executed-status': 'تغيير حالة الوضع',
  'restore-struck-off': 'فك الشطب وتجديد الملف',
};

function ActionLabel({ type }: { type: string }) {
  return (
    <span className="rounded-full px-2.5 py-0.5 text-xs font-bold bg-emerald-50 text-emerald-700 border border-emerald-200 whitespace-nowrap">
      {ACTION_LABELS[type] ?? type}
    </span>
  );
}

/**
 * نافذة «سجل التعديلات» للملف: مجموعات زمنية (الأحدث أولًا) تعرض لكل عملية
 * الفاعل والوقت وكل حقل تغيّر بالقيمة قبل وبعد — أداة المراجعة المؤسسية.
 */
export default function DocumentChangesModal({
  documentId,
  onClose,
}: {
  documentId: number;
  onClose: () => void;
}) {
  const [groups, setGroups] = useState<DocumentChangeGroupDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    api
      .get<PagedResult<DocumentChangeGroupDto>>(
        `/documents/${documentId}/changes?page=${page}&perPage=${PER_PAGE}`,
        { signal: controller.signal },
      )
      .then((r) => r.data)
      .then((data) => {
        setGroups(data.items);
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
  }, [documentId, page]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PER_PAGE));

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="سجل التعديلات"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-2xl max-h-[90vh] flex flex-col">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 shrink-0">
          <h3 className="text-lg font-bold text-gray-800">سجل التعديلات</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4 overflow-y-auto min-h-0">
          {error && (
            <p className="text-red-600 text-sm" role="alert">
              {error}
            </p>
          )}

          {loading ? (
            <p className="text-gray-500 text-sm">جارِ التحميل…</p>
          ) : groups.length === 0 ? (
            <p className="text-gray-400 text-sm py-6 text-center">
              لا توجد تعديلات مسجلة على هذا الملف بعد.
            </p>
          ) : (
            <ol className="space-y-4">
              {groups.map((group) => (
                <li
                  key={group.auditLogId}
                  className="bg-gray-50 rounded-xl border border-gray-200 p-3 sm:p-4"
                >
                  <div className="flex items-center justify-between gap-2 flex-wrap mb-2">
                    <ActionLabel type={group.actionType} />
                    <div className="text-xs text-gray-500 flex items-center gap-2 flex-wrap">
                      <span>{group.userName ?? '—'}</span>
                      <span aria-hidden="true">•</span>
                      <time dateTime={group.timestamp} className="tabular-nums">
                        {formatDateTime(group.timestamp)}
                      </time>
                    </div>
                  </div>
                  <ul className="divide-y divide-gray-100 bg-white rounded-lg border border-gray-100">
                    {group.changes.map((change) => (
                      <li
                        key={`${group.auditLogId}-${change.fieldKey}`}
                        className="px-3 py-2 text-sm grid gap-1 sm:grid-cols-[minmax(120px,auto)_1fr] sm:items-baseline"
                      >
                        <span className="font-medium text-gray-600">{change.fieldLabel}</span>
                        <span className="min-w-0 break-words tabular-nums">
                          <span className="text-red-600 line-through decoration-red-300">
                            {change.oldValue ?? '—'}
                          </span>
                          <span className="mx-1.5 text-gray-400" aria-hidden="true">
                            ←
                          </span>
                          <span className="text-emerald-700 font-medium">{change.newValue ?? '—'}</span>
                        </span>
                      </li>
                    ))}
                  </ul>
                </li>
              ))}
            </ol>
          )}
        </div>

        {totalPages > 1 && (
          <div className="shrink-0 border-t border-gray-100 px-5 py-3 flex items-center justify-center gap-4">
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
      </div>
    </div>
  );
}
