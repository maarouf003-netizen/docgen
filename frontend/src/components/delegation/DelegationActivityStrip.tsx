import { api } from '../../api/client';
import { useCancellableRequest } from '../../hooks/useCancellableRequest';
import type { HeadAlertDto } from '../../types';
import { formatDateTime } from '../../utils/dates';

/**
 * شريط نشاط الإنابة (تنبيهات المرآة/المتابعة): يُعرض في بطاقتي «معلومات الملف المنيب»
 * و«تشعبات الملف» ويُغذّى من GET /alerts/by-delegation/{id} المتاح لأطراف الإنابة فقط.
 * الأحدث في الصدارة (بحد أقصى ثلاثة)، والخطأ/الفراغ لا يظهر شيئًا.
 */
export function DelegationActivityStrip({ delegationId }: { delegationId: number }) {
  const query = useCancellableRequest<HeadAlertDto[]>(
    (signal) =>
      api
        .get<HeadAlertDto[]>(`/alerts/by-delegation/${delegationId}`, { signal })
        .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [delegationId],
  );

  const alerts = (query.data ?? []).slice(0, 3);
  if (query.error || alerts.length === 0) return null;

  return (
    <div className="mt-3 rounded-lg bg-amber-50/70 border border-amber-200 px-3 py-2">
      <span className="block text-[11px] font-bold text-amber-800 mb-1">نشاط الإنابة</span>
      <ul className="space-y-1">
        {alerts.map((a) => (
          <li key={a.id} className="flex items-start justify-between gap-2 text-xs text-amber-900">
            <span className="min-w-0 break-words">{a.message}</span>
            <span className="shrink-0 text-[10px] text-amber-700 tabular-nums whitespace-nowrap">
              {formatDateTime(a.createdAt)}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}