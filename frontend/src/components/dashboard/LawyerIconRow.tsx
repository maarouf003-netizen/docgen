import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ICONS } from './dashboardIcons';

export interface LawyerIconCounts {
  unseenReplies: number;
  urgentCorrespondence: number;
  calendarAlerts: number;
}

interface CardDef {
  key: string;
  icon: ReactNode;
  title: string;
  subtitle: string;
  tone: string;
  badge?: { count: number; label: string };
  to?: string;
  anchor?: string;
}

/**
 * صف الأيقونات الخمس للوحة المحامي (جوال أولًا: عمودان → 3 → 5):
 * كل بطاقة رابط واحد (`Link` للمسارات، `a` للمراسي) وبداخله الجرس
 * **شارة غير تفاعلية** (لا عناصر تفاعلية متداخلة)، بعدّادها في `aria-label`.
 */
export function LawyerIconRow({ counts }: { counts: LawyerIconCounts }) {
  const cards: CardDef[] = [
    {
      key: 'stats',
      icon: ICONS.chart,
      title: 'الإحصائيات',
      subtitle: 'صفحة المؤشرات',
      tone: 'bg-emerald-100 text-emerald-700',
      to: '/stats',
    },
    {
      key: 'reviews',
      icon: ICONS.reviews,
      title: 'المطالعات',
      subtitle: counts.unseenReplies > 0 ? `${counts.unseenReplies} ردود غير مقروءة` : 'لا ردود جديدة',
      tone: 'bg-sky-100 text-sky-700',
      badge:
        counts.unseenReplies > 0
          ? { count: counts.unseenReplies, label: `${counts.unseenReplies} ردود غير مقروءة` }
          : undefined,
      to: '/reviews',
    },
    {
      key: 'correspondence',
      icon: ICONS.correspondence,
      title: 'المراسلات',
      subtitle:
        counts.urgentCorrespondence > 0 ? `${counts.urgentCorrespondence} عاجلة تحتاج المشاهدة` : 'لا عاجل جديد',
      tone: 'bg-amber-100 text-amber-700',
      badge:
        counts.urgentCorrespondence > 0
          ? { count: counts.urgentCorrespondence, label: `${counts.urgentCorrespondence} مراسلات عاجلة` }
          : undefined,
      to: '/correspondence',
    },
    {
      key: 'calendar',
      icon: ICONS.calendar,
      title: 'التقويم',
      subtitle:
        counts.calendarAlerts > 0 ? `${counts.calendarAlerts} اليوم أو متأخرة` : 'لا تذكيرات عاجلة',
      tone: 'bg-violet-100 text-violet-700',
      badge:
        counts.calendarAlerts > 0
          ? { count: counts.calendarAlerts, label: `${counts.calendarAlerts} تذكيرات اليوم أو متأخرة` }
          : undefined,
      to: '/calendar',
    },
    {
      key: 'account',
      icon: ICONS.account,
      title: 'الحساب الشخصي',
      subtitle: 'الملف والتفضيلات',
      tone: 'bg-slate-100 text-slate-700',
      to: '/account',
    },
  ];

  return (
    <nav aria-label="أقسام لوحة المحامي" className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3 sm:gap-4 mb-6">
      {cards.map((card) => {
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
        return card.to ? (
          <Link key={card.key} to={card.to} aria-label={label} className={cls}>
            {inner}
          </Link>
        ) : (
          <a key={card.key} href={card.anchor} aria-label={label} className={cls}>
            {inner}
          </a>
        );
      })}
    </nav>
  );
}
