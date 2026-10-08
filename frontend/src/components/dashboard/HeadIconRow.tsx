import { ICONS } from './dashboardIcons';
import { IconCardGrid, type CardDef } from './IconCards';
import { arabicCount } from './dashboardFormat';

export interface HeadIconCounts {
  reviewsPending: number;
  urgentCorrespondence: number;
  delegationsPending: number;
  entityPending: number;
  /** ملفات بانتظار إعادة القيد في دوائر الفرع (شارة بطاقة الدوائر — B44). */
  circuitsPending: number;
}

/**
 * صف الأيقونات العشر للوحة رئيس القسم (جوال أولًا: عمودان → 3 → 3):
 * الشارة الحمراء بجانب الأيقونة التي فيها تنبيه فقط (مطالعات/مراسلات/
 * إنابات/سجل جهات)، وبقية البطاقات بلا شارة — بنفس عقد `CardDef`.
 *
 * رئيس الشعبة يرى الصف نفسه بلا بطاقة «سجل التدقيق» (نطاقه الدائري §2.23
 * يتطلب `ownerSectionId` خلفيًا قبل فتحه له — حتى ذلك الحين لا رابط مكسور).
 */
export function HeadIconRow({
  counts,
  scopeLabel = 'مؤشرات الفرع',
  hideAudit = false,
}: {
  counts: HeadIconCounts;
  scopeLabel?: string;
  hideAudit?: boolean;
}) {
  const cards: CardDef[] = [
    {
      key: 'stats',
      icon: ICONS.chart,
      title: 'الإحصائيات',
      subtitle: scopeLabel,
      tone: 'bg-emerald-100 text-emerald-700',
      to: '/stats',
    },
    {
      key: 'reviews',
      icon: ICONS.reviews,
      title: 'المطالعات',
      subtitle:
        counts.reviewsPending > 0 ? `${counts.reviewsPending} بانتظار الرد` : 'لا معلّق جديد',
      tone: 'bg-sky-100 text-sky-700',
      badge:
        counts.reviewsPending > 0
          ? {
              count: counts.reviewsPending,
              label: arabicCount(counts.reviewsPending, 'كتاب مطالعة واحد بانتظار الرد', 'كتب مطالعة بانتظار الرد'),
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
      key: 'branch-lawyers',
      icon: ICONS.borrowers,
      title: 'محامو الفرع',
      subtitle: 'إدارة محامي الفرع',
      tone: 'bg-teal-100 text-teal-700',
      to: '/branch-lawyers',
    },
    {
      key: 'delegations',
      icon: ICONS.delegation,
      title: 'طلبات الإنابة',
      subtitle:
        counts.delegationsPending > 0 ? `${counts.delegationsPending} بانتظار الاعتماد` : 'لا طلبات معلّقة',
      tone: 'bg-orange-100 text-orange-700',
      badge:
        counts.delegationsPending > 0
          ? {
              count: counts.delegationsPending,
              label: arabicCount(counts.delegationsPending, 'طلب إنابة معلّق واحد', 'طلبات إنابة معلّقة'),
            }
          : undefined,
      to: '/delegations/requests',
    },
    {
      key: 'entity-review',
      icon: ICONS.entity,
      title: 'مراجعة سجل الجهات',
      subtitle:
        counts.entityPending > 0 ? `${counts.entityPending} بانتظار المراجعة` : 'السجل محدّث',
      tone: 'bg-indigo-100 text-indigo-700',
      badge:
        counts.entityPending > 0
          ? {
              count: counts.entityPending,
              label: arabicCount(counts.entityPending, 'جهة واحدة بانتظار المراجعة', 'جهات بانتظار المراجعة'),
            }
          : undefined,
      to: '/entities/review',
    },
    {
      key: 'delegates',
      icon: ICONS.delegate,
      title: 'مندوبو الجهات',
      subtitle: 'حسابات المندوبين',
      tone: 'bg-cyan-100 text-cyan-700',
      to: '/delegates',
    },
    {
      key: 'circuits',
      icon: ICONS.chart,
      title: 'إدارة دوائر التنفيذ',
      subtitle:
        counts.circuitsPending > 0 ? `${counts.circuitsPending} ملفات محالة` : 'سجل دوائر التنفيذ',
      tone: 'bg-violet-100 text-violet-700',
      badge:
        counts.circuitsPending > 0
          ? {
              count: counts.circuitsPending,
              label: arabicCount(counts.circuitsPending, 'ملف محال واحد', 'ملفات محالة'),
            }
          : undefined,
      to: '/execution-circuits',
    },
    {
      key: 'audit',
      icon: ICONS.audit,
      title: 'سجل التدقيق',
      subtitle: 'تتبع الإجراءات',
      tone: 'bg-rose-100 text-rose-700',
      to: '/audit-logs',
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

  const visibleCards = hideAudit ? cards.filter((c) => c.key !== 'audit') : cards;

  return (
    <IconCardGrid
      label={hideAudit ? 'أقسام لوحة رئيس الشعبة' : 'أقسام لوحة رئيس القسم'}
      gridClass="sm:grid-cols-3"
      cards={visibleCards}
    />
  );
}
