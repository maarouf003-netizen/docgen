import { ICONS } from './dashboardIcons';
import { IconCardGrid, type CardDef } from './IconCards';

/**
 * صف أيقونات لوحة المدير/مشرف النظام (جوال أولًا: عمودان → 3 → 3):
 * نفس نمط `LawyerIconRow` و`HeadIconRow` — الشريط الجانبي يقتصر على
 * (لوحة + ملفات + منتدى + مكتبة)، وبقية الأقسام تُفتح من بطاقات اللوحة.
 *
 * - المدير يرى 10 بطاقات (بلا «محامو الفرع» — صلاحية المشرف وحده).
 * - المشرف يرى 11 بطاقة (مع «محامو الفرع»).
 * - الإحصائيات بطاقة تفتح `/stats` مثل المحامي ورئيس القسم (لا عرض مباشر في اللوحة).
 * - «الحساب الشخصي» بطاقة للمدير والمشرف مثل المحامي ورئيس القسم —
 *   صفحة الحساب تعرض الملف وكلمة المرور والملخص فقط، بلا إرسال اقتراحات
 *   (المشرف/المدير يستقبلانها في `/suggestions` — الإرسال محظور خلفيًا).
 */
export function ManagerIconRow({ isAdmin = false }: { isAdmin?: boolean }) {
  const cards: CardDef[] = [
    {
      key: 'stats',
      icon: ICONS.chart,
      title: 'الإحصائيات',
      subtitle: 'مؤشرات عامة',
      tone: 'bg-emerald-100 text-emerald-700',
      to: '/stats',
    },
    {
      key: 'reviews',
      icon: ICONS.reviews,
      title: 'كتب المطالعات',
      subtitle: 'المطالعات الواردة',
      tone: 'bg-sky-100 text-sky-700',
      to: '/reviews',
    },
    {
      key: 'correspondence',
      icon: ICONS.correspondence,
      title: 'المراسلات',
      subtitle: 'مراسلات الملفات',
      tone: 'bg-amber-100 text-amber-700',
      to: '/correspondence',
    },
    {
      key: 'suggestions',
      icon: ICONS.forum,
      title: 'اقتراحات التطوير',
      subtitle: 'صندوق الاقتراحات',
      tone: 'bg-violet-100 text-violet-700',
      to: '/suggestions',
    },
    {
      key: 'entity-review-management',
      icon: ICONS.entity,
      title: 'مراجعة سجل الجهات العامة',
      subtitle: 'إدارة سجل الجهات',
      tone: 'bg-indigo-100 text-indigo-700',
      to: '/entities/review-management',
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
      key: 'users-manage',
      icon: ICONS.borrowers,
      title: 'إدارة المستخدمين',
      subtitle: 'الحسابات والصلاحيات',
      tone: 'bg-slate-100 text-slate-700',
      to: '/users/manage',
    },
    {
      key: 'branches-manage',
      icon: ICONS.home,
      title: 'إدارة الفروع',
      subtitle: 'فروع الإدارة',
      tone: 'bg-orange-100 text-orange-700',
      to: '/branches/manage',
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

  if (isAdmin) {
    cards.splice(6, 0, {
      key: 'branch-lawyers',
      icon: ICONS.borrowers,
      title: 'محامو الفرع',
      subtitle: 'إدارة محامي الفرع',
      tone: 'bg-teal-100 text-teal-700',
      to: '/branch-lawyers',
    });
  }

  return (
    <IconCardGrid
      label={isAdmin ? 'أقسام لوحة مشرف النظام' : 'أقسام لوحة المدير'}
      gridClass="sm:grid-cols-3"
      cards={cards}
    />
  );
}
