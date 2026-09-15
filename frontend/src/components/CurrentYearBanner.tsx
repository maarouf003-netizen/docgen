import { useCurrentYear } from '../hooks/useCurrentYear';

/**
 * شريط رفيع يظهر أثناء جلب سنة النظام من الخادم، لمنع الاعتماد على سنة غير مؤكدة
 * في شاشات السنة الحاسمة (تدوير الأرقام، الإعادة، الوقوعات) قبل اكتمال الجلب.
 * بعد الاكتمال أو الفشل يختفي ويأخذ المكان القيمة الفعلية أو الاحتياطية.
 */
export default function CurrentYearBanner() {
  const { isLoading } = useCurrentYear();
  if (!isLoading) return null;
  return (
    <div
      role="status"
      className="text-xs text-emerald-900 bg-emerald-50 border-b border-emerald-200 px-4 py-1.5 text-center"
    >
      جارِ تحديد سنة النظام…
    </div>
  );
}