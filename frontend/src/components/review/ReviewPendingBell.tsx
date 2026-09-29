import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../api/client';
import { useAuth } from '../../auth/useAuth';
import { MessageBellIcon } from '../MessageBellIcon';

/**
 * مؤشر كتب المطالعة لرئيس القسم (أيقونة رسالة حديثة): أحمر مع عدد الكتب التي
 * لم يُرد عليها، ويُحدَّث كل دقيقة. لا يظهر إلا لرئيس القسم.
 */
export default function ReviewPendingBell({ className = '' }: { className?: string }) {
  const { isHead } = useAuth();
  const [count, setCount] = useState(0);

  useEffect(() => {
    if (!isHead) return undefined;
    let cancelled = false;
    const fetchCount = () =>
      api
        .get<{ count: number }>('/review-letters/pending-count')
        .then((r) => {
          if (!cancelled) setCount(r.data.count);
        })
        .catch(() => {
          /* الجرس يبقى على آخر قيمة معروفة عند فشل التحديث */
        });
    void fetchCount();
    const timer = window.setInterval(fetchCount, 60_000);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [isHead]);

  if (!isHead) return null;

  const hasPending = count > 0;

  return (
    <Link
      to="/reviews"
      aria-label={
        hasPending ? `كتب مطالعة بانتظار الرد: ${count}` : 'لا توجد كتب مطالعة بانتظار الرد'
      }
      className={`relative inline-flex items-center justify-center min-h-11 min-w-11 rounded-lg transition-colors ${className}`}
    >
      <MessageBellIcon className={hasPending ? 'w-6 h-6 text-red-500' : 'w-6 h-6 text-emerald-300'} />
      {hasPending && (
        <span className="absolute -top-0.5 -left-0.5 min-w-5 h-5 px-1 rounded-full bg-red-600 border-2 border-emerald-900 text-white text-[11px] font-bold flex items-center justify-center tabular-nums">
          {count > 99 ? '+99' : count}
        </span>
      )}
    </Link>
  );
}
