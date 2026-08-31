import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';

/**
 * بانر تنبيه للروابط القديمة المعاد توجيهها (تحسين 8.6): يظهر نبيهًا بأن الرابط
 * القديم لم يعد معتمدًا ويعرض الوجهة الجديدة، ثم يعيد التوجيه تلقائيًا بعد
 * مهلة قصيرة حتى لا يكسر الإشارات المرجعية الحالية للمستخدمين.
 */
export default function LegacyRouteBanner({
  to,
  message,
  delayMs = 3500,
}: {
  to: string;
  message: string;
  delayMs?: number;
}) {
  const navigate = useNavigate();
  const [secondsLeft, setSecondsLeft] = useState(() => Math.max(1, Math.ceil(delayMs / 1000)));

  useEffect(() => {
    const timer = window.setTimeout(() => navigate(to, { replace: true }), delayMs);
    return () => window.clearTimeout(timer);
  }, [to, delayMs, navigate]);

  useEffect(() => {
    const tick = window.setInterval(() => {
      setSecondsLeft((s) => (s > 1 ? s - 1 : 1));
    }, 1000);
    return () => window.clearInterval(tick);
  }, []);

  return (
    <div
      role="alert"
      aria-live="polite"
      className="bg-amber-100 border border-amber-300 rounded-xl shadow-sm p-4 mx-4 mt-4 flex gap-3 items-start"
    >
      <span aria-hidden="true" className="text-xl leading-none shrink-0">
        ⚠️
      </span>
      <div className="flex-1 min-w-0">
        <p className="text-amber-900 text-sm font-medium text-wrap-pretty">{message}</p>
        <p className="text-amber-800 text-xs mt-2">
          سيُعاد التوجيه تلقائيًا خلال {secondsLeft} ثانية — أو انتقل مباشرة:{' '}
          <Link to={to} className="font-bold underline underline-offset-2 hover:text-amber-950 focus-visible:ring-2 focus-visible:ring-amber-500 rounded">
            الوجهة الجديدة ›
          </Link>
        </p>
      </div>
    </div>
  );
}
