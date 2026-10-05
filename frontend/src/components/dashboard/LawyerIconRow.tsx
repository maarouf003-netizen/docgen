import { ICONS } from './dashboardIcons';
import { IconCardGrid, type CardDef } from './IconCards';
import { arabicCount } from './dashboardFormat';

export interface LawyerIconCounts {
  unseenReplies: number;
  urgentCorrespondence: number;
  calendarAlerts: number;
  /** ملفات المحامي المحالة بانتظار تحديث بياناتها (بطاقة تفتح صفحة المعلقات — B19). */
  pendingRegistrations: number;
}

/**
 * صف الأيقونات الست للوحة المحامي (جوال أولًا: عمودان → 3 → 5):
 * كل بطاقة رابط واحد (`Link`) وبداخله الجرس
 * **شارة غير تفاعلية** (لا عناصر تفاعلية متداخلة)، بعدّادها في `aria-label`.
 */
export function LawyerIconRow({
  counts,
  showReferredFiles,
  pendingState,
}: {
  counts: LawyerIconCounts;
  /**
   * إظهار بطاقة «ملفات معلقة»: تُحجب بعد نجاح الجلب والصفر المؤكد،
   * وتبقى أثناء التحميل وعند الخطأ (fail-open) حتى لا يفقد المحامي مدخل صفحته بصمت.
   */
  showReferredFiles: boolean;
  /** حالة جلب المعلقات — لصياغة صادقة للسطر الفرعي أثناء التحميل أو عند الخطأ. */
  pendingState?: 'loading' | 'error';
}) {
  const referredSubtitle =
    counts.pendingRegistrations > 0
      ? `${counts.pendingRegistrations} بانتظار تحديث بياناتها`
      : pendingState === 'loading'
        ? 'جارِ التحميل…'
        : pendingState === 'error'
          ? 'تعذّر الجلب — افتح للتحقق'
          : 'لا معلقات';
  // مفتاح بطاقة الملفات المحالة — يُستخدم في التعريف والترشيح معًا فلا ينكسرا منفصلين.
  const REFERRED_CARD_KEY = 'referred-files';
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
          ? {
              count: counts.unseenReplies,
              label: arabicCount(counts.unseenReplies, 'رد واحد غير مقروء', 'ردود غير مقروءة'),
            }
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
          ? {
              count: counts.urgentCorrespondence,
              label: arabicCount(counts.urgentCorrespondence, 'مراسلة عاجلة واحدة', 'مراسلات عاجلة'),
            }
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
          ? {
              count: counts.calendarAlerts,
              label: arabicCount(counts.calendarAlerts, 'تذكير واحد اليوم أو متأخر', 'تذكيرات اليوم أو متأخرة'),
            }
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
    {
      key: REFERRED_CARD_KEY,
      icon: ICONS.documents,
      title: 'ملفات معلقة',
      subtitle: referredSubtitle,
      tone: 'bg-violet-100 text-violet-700',
      badge:
        counts.pendingRegistrations > 0
          ? {
              count: counts.pendingRegistrations,
              label: arabicCount(counts.pendingRegistrations, 'ملف واحد بانتظار تحديث بياناته', 'ملفات بانتظار تحديث بياناتها'),
            }
          : undefined,
      to: '/pending-registrations',
    },
  ];

  const visible = showReferredFiles ? cards : cards.filter((c) => c.key !== REFERRED_CARD_KEY);

  return <IconCardGrid label="أقسام لوحة المحامي" gridClass="sm:grid-cols-3 lg:grid-cols-5" cards={visible} />;
}
