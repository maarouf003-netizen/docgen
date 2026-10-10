/**
 * أيقونة دوّار صغيرة لأزرار العمليات الطويلة (كالتصدير): زخرفية بحتة
 * (`aria-hidden`) — النص المجاور يبقى هو المُعلن لقارئ الشاشة.
 * الحركة عبر `motion-safe` فقط احترامًا لـ`prefers-reduced-motion`.
 * أبعاد ثابتة (`h-4 w-4`) لمنع أي انزياح تخطيط (CLS) عند الظهور.
 */
export default function SpinnerIcon({ className = '' }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 20 20"
      fill="none"
      aria-hidden="true"
      className={`h-4 w-4 shrink-0 motion-safe:animate-spin ${className}`}
    >
      <circle cx="10" cy="10" r="8" stroke="currentColor" strokeOpacity="0.25" strokeWidth="2.5" />
      <path d="M18 10a8 8 0 0 0-8-8" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
    </svg>
  );
}
