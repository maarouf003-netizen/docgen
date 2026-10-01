import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';

export interface CardDef {
  key: string;
  icon: ReactNode;
  title: string;
  subtitle: string;
  tone: string;
  badge?: { count: number; label: string };
  to: string;
}

/**
 * بطاقة أيقونة واحدة: رابط واحد يحتضن شارة غير تفاعلية (لا عناصر
 * تفاعلية متداخلة)، والعدّاد في `aria-label` الأب لا في الشارة نفسها.
 */
export function IconCard({ card }: { card: CardDef }) {
  const inner = (
    <>
      <span className="flex items-center gap-2.5 min-w-0">
        <span className={`shrink-0 rounded-xl p-2.5 ${card.tone}`} aria-hidden="true">
          {card.icon}
        </span>
        <span className="min-w-0">
          <span className="block font-bold text-gray-900 text-sm truncate">{card.title}</span>
          <span className="block text-xs text-gray-500 truncate">{card.subtitle}</span>
        </span>
      </span>
      {card.badge ? (
        <span
          className="shrink-0 min-w-6 h-6 px-1.5 rounded-full bg-red-600 text-white text-xs font-bold inline-flex items-center justify-center tabular-nums"
          aria-hidden="true"
        >
          {card.badge.count > 99 ? '+99' : card.badge.count}
        </span>
      ) : (
        <span className="shrink-0 text-gray-300" aria-hidden="true">
          ←
        </span>
      )}
    </>
  );
  const label = card.badge ? `${card.title} — ${card.badge.label}` : card.title;
  const cls =
    'relative flex items-center justify-between gap-2 bg-white rounded-2xl shadow-sm border border-gray-100 p-3 sm:p-4 min-h-11 hover:shadow-md hover:border-emerald-200 transition-shadow focus-visible:ring-2 focus-visible:ring-emerald-600';
  return (
    <Link to={card.to} aria-label={label} className={cls}>
      {inner}
    </Link>
  );
}

/**
 * شبكة صف الأيقونات (جوال أولًا: عمودان، ثم `gridClass` للشاشات الأكبر).
 */
export function IconCardGrid({
  label,
  gridClass,
  cards,
}: {
  label: string;
  gridClass: string;
  cards: CardDef[];
}) {
  return (
    <nav aria-label={label} className={`grid grid-cols-2 ${gridClass} gap-3 sm:gap-4 mb-6`}>
      {cards.map((card) => (
        <IconCard key={card.key} card={card} />
      ))}
    </nav>
  );
}
