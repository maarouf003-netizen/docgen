import { formatDelta } from './dashboardFormat';

/**
 * شارة الدلتا المئوية عن الفترة السابقة:
 * `null` (غياب السابق أو صفره) → لا تُعرض إطلاقًا بدل رقم مضلِّل.
 */
export function DeltaBadge({ value }: { value: number | null }) {
  if (value == null) return null;

  const direction = value > 0 ? 'up' : value < 0 ? 'down' : 'flat';
  const tone =
    direction === 'up'
      ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
      : direction === 'down'
        ? 'bg-red-50 text-red-700 border-red-200'
        : 'bg-gray-50 text-gray-500 border-gray-200';
  const accessible =
    direction === 'up'
      ? `ارتفاع ${formatDelta(value)} عن الفترة السابقة`
      : direction === 'down'
        ? `انخفاض ${formatDelta(value)} عن الفترة السابقة`
        : 'بلا تغيّر عن الفترة السابقة';

  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-medium tabular-nums ${tone}`}
      dir="ltr"
      aria-label={accessible}
    >
      <svg
        viewBox="0 0 24 24"
        className="w-3 h-3"
        fill="none"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden="true"
      >
        {direction === 'up' ? (
          <path d="M7 17 17 7" />
        ) : direction === 'down' ? (
          <path d="m7 7 10 10" />
        ) : (
          <path d="M5 12h14" />
        )}
        {direction === 'up' ? <path d="M8 7h9v9" /> : direction === 'down' ? <path d="M16 17H7V8" /> : null}
      </svg>
      {formatDelta(value)}
    </span>
  );
}
