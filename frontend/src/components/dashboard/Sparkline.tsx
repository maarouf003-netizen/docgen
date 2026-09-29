import { useId } from 'react';
import { MONTHS } from './dashboardFormat';

export interface SparkPoint {
  year: number;
  month: number;
  count: number;
}

const WIDTH = 132;
const HEIGHT = 40;
const PAD = 4;

/**
 * خط اتجاه مصغّر (SVG خفيف بلا مكتبات) لسلسلة شهرية متصلة:
 * يُخفى عن قارئ الشاشة ويُوصف نصيًا عبر `description` (الاتجاه + القيم).
 */
export function Sparkline({ points, description }: { points: SparkPoint[]; description: string }) {
  const id = useId();
  if (points.length === 0) return null;

  const max = Math.max(...points.map((p) => p.count), 1);
  const step = points.length > 1 ? (WIDTH - PAD * 2) / (points.length - 1) : 0;
  const coords = points.map((p, i) => {
    const x = PAD + i * step;
    const y = HEIGHT - PAD - (p.count / max) * (HEIGHT - PAD * 2);
    return { x, y, p };
  });
  const path = coords.map((c, i) => `${i === 0 ? 'M' : 'L'}${c.x.toFixed(1)},${c.y.toFixed(1)}`).join(' ');
  const last = coords[coords.length - 1];
  const firstLabel = `${MONTHS[points[0].month - 1]} ${points[0].year}`;
  const lastLabel = `${MONTHS[last.p.month - 1]} ${last.p.year}`;

  return (
    <figure className="mt-2" aria-describedby={id}>
      <svg
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        className="w-full max-w-36 h-10 text-emerald-600"
        aria-hidden="true"
        focusable="false"
      >
        <path d={path} fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
        <circle cx={last.x} cy={last.y} r="3" fill="currentColor" />
      </svg>
      <figcaption id={id} className="sr-only">
        {description}: من {firstLabel} إلى {lastLabel}
      </figcaption>
    </figure>
  );
}
