import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../api/client';
import { useAuth } from '../../auth/useAuth';
import { CORRESPONDENCE_UNSEEN_EVENT } from './correspondenceDisplay';

interface CorrespondenceBellProps {
  /** مسار البوابة للمندوب (يبدأ بـ /api/portal) أم المسار الرئيسي. */
  portal?: boolean;
  className?: string;
  /**
   * عدد مُغذَّى من الأب (Layout يستطلع مرة واحدة للشارة والجرس معًا) —
   * عند تمريره يتوقف الجرس عن الاستطلاع الذاتي؛ بلا تمرير يستطلع بنفسه.
   */
  count?: number;
}

/**
 * جرس المراسلات العاجلة: أحمر مع عدد المراسلات العاجلة التي لم يؤكد المستخدم
 * مشاهدتها، ويُحدَّث كل دقيقة وفورًا عند تأكيد المشاهدة. للمحامي ورئيس القسم
 * والمندوب (كلٌّ يرى عاجلَه كطرف) — لا يظهر للمدير/المشرف.
 */
export default function CorrespondenceBell({ portal = false, className = '', count: fedCount }: CorrespondenceBellProps) {
  const { user } = useAuth();
  const [selfCount, setSelfCount] = useState(0);

  const canHaveUrgent =
    user?.role === 'lawyer' || user?.role === 'head' || user?.role === 'entitymanager';
  const base = portal ? '/portal/correspondence' : '/correspondence';

  useEffect(() => {
    if (!canHaveUrgent || fedCount !== undefined) return undefined;
    let cancelled = false;
    const fetchCount = () =>
      api
        .get<{ count: number }>(`${base}/urgent-unseen-count`)
        .then((r) => {
          if (!cancelled) setSelfCount(r.data.count);
        })
        .catch(() => {
          /* الجرس يبقى على آخر قيمة معروفة عند فشل التحديث */
        });
    void fetchCount();
    const timer = window.setInterval(fetchCount, 60_000);
    const onSeenChanged = () => fetchCount();
    window.addEventListener(CORRESPONDENCE_UNSEEN_EVENT, onSeenChanged);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
      window.removeEventListener(CORRESPONDENCE_UNSEEN_EVENT, onSeenChanged);
    };
  }, [canHaveUrgent, base, fedCount]);

  if (!canHaveUrgent) return null;

  const count = fedCount ?? selfCount;
  const hasUrgent = count > 0;
  const to = portal ? '/portal/correspondence' : '/correspondence';

  return (
    <Link
      to={to}
      aria-label={hasUrgent ? `مراسلات عاجلة بلا مشاهدة: ${count}` : 'لا توجد مراسلات عاجلة بلا مشاهدة'}
      className={`relative inline-flex items-center justify-center min-h-11 min-w-11 rounded-lg transition-colors ${className}`}
    >
      <svg
        viewBox="0 0 24 24"
        className={hasUrgent ? 'w-6 h-6 text-red-500' : 'w-6 h-6 text-emerald-300'}
        fill="currentColor"
        aria-hidden="true"
        focusable="false"
      >
        <path d="M12 22a2.5 2.5 0 0 0 2.45-2h-4.9A2.5 2.5 0 0 0 12 22Zm8-4v1H4v-1l2-2v-5a6 6 0 0 1 4-5.66V5a2 2 0 1 1 4 0v.34A6 6 0 0 1 18 11v5l2 2Z" />
      </svg>
      {hasUrgent && (
        <span className="absolute -top-0.5 -left-0.5 min-w-5 h-5 px-1 rounded-full bg-red-600 border-2 border-emerald-900 text-white text-[11px] font-bold flex items-center justify-center tabular-nums">
          {count > 99 ? '+99' : count}
        </span>
      )}
    </Link>
  );
}
