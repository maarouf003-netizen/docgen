import { useEffect, useRef, useState, type ReactNode } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useIsMobile } from '../hooks/useMediaQuery';
import NetworkStatusBanner from './NetworkStatusBanner';
import CurrentYearBanner from './CurrentYearBanner';
import CorrespondenceBell from './correspondence/CorrespondenceBell';
import { CORRESPONDENCE_UNSEEN_EVENT } from './correspondence/correspondenceDisplay';
import { ComingSoonToast } from './ComingSoonToast';
import { ICONS } from './dashboard/dashboardIcons';
import nationalEmblem from '../assets/national.png';
import { ROLE_LABELS } from '../auth/roleLabels';

interface NavItem {
  to: string;
  label: string;
  end?: boolean;
  /** عدد عناصر تحتاج انتباه صاحب الدور (مثل ردود غير مطّلع عليها). */
  badge?: number;
  /** أيقونة البطاقة/البند (بنفس لغة `dashboardIcons`). */
  icon?: ReactNode;
  /** بند مؤجل: يُعرض زرًا يفتح تنبيه «قيد البناء» بدل رابط (لا مسار له). */
  comingSoon?: boolean;
}

export default function Layout() {
  const { user, logout, hasFullAccess, isHead } = useAuth();
  const isMobile = useIsMobile();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const drawerRef = useRef<HTMLDivElement>(null);
  const drawerTriggerRef = useRef<HTMLButtonElement>(null);
  const drawerCloseRef = useRef<HTMLButtonElement>(null);
  const isLawyerUser = user?.role === 'lawyer';
  // رئيس القسم يجلب عدّاده من لوحته (بطاقة المراسلات) — هذا الاستطلاع للمحامي والمندوب فقط.
  const canHaveCorrespondenceUrgent =
    user?.role === 'lawyer' || user?.role === 'entitymanager';
  const [urgentCorrespondence, setUrgentCorrespondence] = useState(0);

  // عدّاد المراسلات العاجلة بلا تأكيد مشاهدة — الاستطلاع الوحيد: يغذّي شارة بند
  // المراسلات والأجراس معًا (تُمرَّر count للأجراس فلا تستطلع بنفسها)، يُحدَّث كل
  // دقيقة وفورًا عند تأكيد المشاهدة (حدث correspondence:unseen-changed).
  // المندوب يُغذَّى من مسار البوابة، وغيره من المسار الرئيسي.
  useEffect(() => {
    if (!canHaveCorrespondenceUrgent) return undefined;
    const endpoint =
      user?.role === 'entitymanager'
        ? '/portal/correspondence/urgent-unseen-count'
        : '/correspondence/urgent-unseen-count';
    let cancelled = false;
    const fetchCount = () =>
      api
        .get<{ count: number }>(endpoint)
        .then((r) => {
          if (!cancelled) setUrgentCorrespondence(r.data.count);
        })
        .catch(() => {
          /* الشارة تبقى على آخر قيمة معروفة عند فشل التحديث */
        });
    void fetchCount();
    const timer = window.setInterval(fetchCount, 60_000);
    const onSeenChanged = () => fetchCount();
    window.addEventListener(CORRESPONDENCE_UNSEEN_EVENT, onSeenChanged);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
      window.removeEventListener(CORRESPONDENCE_UNSEEN_EVENT, onSeenChanged);
    };
  }, [canHaveCorrespondenceUrgent, user?.role]);

  // نمط WAI-ARIA Dialog: Escape يغلق، والتركيز محصور داخل الدرج أثناء فتحه،
  // ويعاد إلى زر الفتح عند الإغلاق — دورة تركيز كاملة لمستخدمي لوحة المفاتيح.
  useEffect(() => {
    if (!isMobile || !drawerOpen) return;
    const previouslyFocused = document.activeElement as HTMLElement | null;
    drawerCloseRef.current?.focus();

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        setDrawerOpen(false);
        return;
      }
      if (e.key !== 'Tab' || !drawerRef.current) return;
      const focusables = Array.from(
        drawerRef.current.querySelectorAll<HTMLElement>(
          'a[href], button:not([disabled]), input, select, textarea, [tabindex]:not([tabindex="-1"])',
        ),
      ).filter((el) => el.offsetParent !== null);
      if (focusables.length === 0) return;
      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      const active = document.activeElement as HTMLElement | null;
      if (e.shiftKey && (active === first || !drawerRef.current.contains(active))) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && active === last) {
        e.preventDefault();
        first.focus();
      }
    };

    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      previouslyFocused?.focus?.();
    };
  }, [isMobile, drawerOpen]);

  // الفرع الأخير يخدم المدير/المشرف فقط (المحامي ورئيس القسم والمندوب فُرزوا أعلاه) —
  // فلا شروط أدوار أخرى هنا عمدًا.
  const canViewAuditLogs = hasFullAccess;
  const canManageBranchLawyers = user?.role === 'admin';
  const canManageUsers = user?.role === 'admin';
  const canManageDelegates = hasFullAccess;
  // مندوب الجهة: الإحصائيات + الملفات التنفيذية + المراسلات دون باقي البنود (بوابة قرائية).
  const isEntityManager = user?.role === 'entitymanager';

  const navItems: NavItem[] = [];

  if (isEntityManager) {
    // مندوب الجهة: الإحصائيات + الملفات التنفيذية + المراسلات (لا شيء سواها).
    navItems.push({ to: '/portal/stats', label: 'الإحصائيات' });
    navItems.push({ to: '/portal/files', label: 'الملفات التنفيذية' });
    navItems.push({
      to: '/portal/correspondence',
      label: 'المراسلات',
      badge: urgentCorrespondence > 0 ? urgentCorrespondence : undefined,
    });
  } else if (isLawyerUser || isHead) {
    // المحامي ورئيس القسم: نفس البنود الأربعة عمدًا (لوحة + ملفات + قيد البناء) —
    // شرط واحد حتى لا ينحرفا عن بعضهما؛ بقية الأقسام تُفتح من بطاقات اللوحة.
    navItems.push(
      { to: '/', label: 'لوحة التحكم', end: true, icon: ICONS.home },
      { to: '/documents', label: 'الملفات التنفيذية', icon: ICONS.documents },
      { to: '/forum', label: 'المنتدى', icon: ICONS.forum, comingSoon: true },
      { to: '/library', label: 'المكتبة', icon: ICONS.library, comingSoon: true },
    );
  } else {
    navItems.push(
      { to: '/', label: 'لوحة التحكم', end: true },
      { to: '/documents', label: 'الملفات التنفيذية' },
    );
    navItems.push({
      to: '/reviews',
      label: 'كتب المطالعات',
    });
    navItems.push({
      to: '/correspondence',
      label: 'المراسلات',
    });
    if (canManageBranchLawyers) navItems.push({ to: '/branch-lawyers', label: 'محامو الفرع' });
    if (hasFullAccess) navItems.push({ to: '/entities/review-management', label: 'مراجعة سجل الجهات العامة' });
    if (canManageDelegates) navItems.push({ to: '/delegates', label: 'مندوبو الجهات' });
    if (canManageUsers) navItems.push({ to: '/users/manage', label: 'إدارة المستخدمين' });
    if (canManageUsers) navItems.push({ to: '/branches/manage', label: 'إدارة الفروع' });
    if (hasFullAccess) navItems.push({ to: '/users', label: 'نشاط المستخدمين' });
    if (canViewAuditLogs) navItems.push({ to: '/audit-logs', label: 'سجل التدقيق' });
  }

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    'block rounded-lg px-4 py-2.5 mb-1 transition-colors min-h-11 ' +
    (isActive ? 'bg-emerald-700 text-white' : 'text-emerald-100 hover:bg-emerald-700/40');

  const bottomNavClass = ({ isActive }: { isActive: boolean }) =>
    'flex-1 flex flex-col items-center justify-center gap-0.5 py-2 min-h-14 text-xs transition-colors ' +
    (isActive ? 'bg-emerald-700 text-white' : 'text-emerald-100 hover:bg-emerald-700/40');

  const renderNavLabel = (item: NavItem) => (
    <span className="inline-flex items-center gap-1.5 min-w-0 max-w-full">
      {item.icon ? (
        <span className="shrink-0 opacity-90" aria-hidden="true">
          {item.icon}
        </span>
      ) : null}
      <span className="truncate">{item.label}</span>
      {item.badge != null && (
        <span
          className="shrink-0 min-w-5 h-5 px-1 rounded-full bg-red-600 border border-red-400/60 text-white text-[11px] font-bold inline-flex items-center justify-center tabular-nums"
          aria-label={`${item.label}: ${item.badge} تحتاج انتباهك`}
        >
          {item.badge > 99 ? '+99' : item.badge}
        </span>
      )}
    </span>
  );

  const [comingSoonFeature, setComingSoonFeature] = useState<string | null>(null);

  // بند مؤجل (قيد البناء) يُعرض زرًا يفتح التنبيه — لا مسار له ولا يُكسر التنقل.
  // الروابط الحقيقية تستلم دالة الصنف كما هي للحفاظ على تمييز البند النشط.
  const renderNavItem = (
    item: NavItem,
    className: ((props: { isActive: boolean }) => string) | string,
    onNavigate?: () => void,
  ) => {
    if (item.comingSoon) {
      const staticClass = typeof className === 'string' ? className : className({ isActive: false });
      return (
        <button
          key={item.to}
          type="button"
          onClick={() => {
            onNavigate?.();
            setComingSoonFeature(item.label);
          }}
          className={`${staticClass} w-full text-right opacity-90`}
          aria-label={`${item.label} — الميزة قيد البناء حاليا`}
        >
          {renderNavLabel(item)}
        </button>
      );
    }
    return (
      <NavLink key={item.to} to={item.to} end={item.end} onClick={onNavigate} className={className}>
        {renderNavLabel(item)}
      </NavLink>
    );
  };

  const renderSidebarContent = (onNavigate?: () => void) => (
    <>
      <div className="p-4 border-b border-emerald-700 text-center">
        <img
          src={nationalEmblem}
          alt="شعار نسر صلاح الدين"
          className="w-14 h-14 mx-auto mb-1 drop-shadow-md"
        />
        <h1 className="text-xl font-bold">مسار</h1>
        <p className="text-xs text-emerald-300 mt-1">
          مساعد محامي الدولة الذكي في إدارة الملفات التنفيذية
        </p>
        {canHaveCorrespondenceUrgent && (
          <div className="flex justify-center mt-2">
            <CorrespondenceBell portal={isEntityManager} count={urgentCorrespondence} />
          </div>
        )}
      </div>
      <nav className="flex-1 min-h-0 p-3 overflow-y-auto" aria-label="القائمة الرئيسية">
        {navItems.map((item) => renderNavItem(item, linkClass, onNavigate))}
      </nav>
      <div className="p-4 border-t border-emerald-700 text-sm">
        {isLawyerUser || isHead ? (
          <NavLink
            to="/account"
            onClick={onNavigate}
            className="flex items-center gap-2.5 rounded-xl p-2 -m-1 hover:bg-emerald-700/40 focus-visible:ring-2 focus-visible:ring-emerald-300 min-h-11"
            aria-label={`الحساب الشخصي: ${user?.fullName ?? ''}`}
          >
            <span
              className="shrink-0 w-9 h-9 rounded-full bg-emerald-700 text-white inline-flex items-center justify-center font-bold"
              aria-hidden="true"
            >
              {(user?.fullName ?? '').trim().slice(0, 2) || '؟'}
            </span>
            <span className="min-w-0">
              <span className="block font-medium truncate">{user?.fullName}</span>
              <span className="block text-emerald-300 text-xs truncate">
                {user?.role ? ROLE_LABELS[user.role] : ''} — {user?.branchName || 'كل الفروع'}
              </span>
            </span>
          </NavLink>
        ) : (
          <>
            <div className="font-medium">{user?.fullName}</div>
            <div className="text-emerald-300 text-xs mb-2">
              {user?.role ? ROLE_LABELS[user.role] : ''} — {user?.branchName || 'كل الفروع'}
            </div>
            <button
              onClick={logout}
              className="block w-full text-right text-emerald-100 hover:text-white hover:underline text-xs mb-1 min-h-11"
            >
              تسجيل الخروج
            </button>
            <NavLink
              to="/change-password"
              className="block text-emerald-100 hover:text-white hover:underline text-xs min-h-11"
            >
              تغيير كلمة المرور
            </NavLink>
          </>
        )}
      </div>
    </>
  );

  const renderBottomNav = () => {
    // شريط سفلي بأهداف لمس مريحة: أول 4 بنود فقط، والبقية عبر درج «المزيد».
    const BOTTOM_NAV_LIMIT = 4;
    const bottomItems = navItems.slice(0, BOTTOM_NAV_LIMIT);
    const hasMore = navItems.length > BOTTOM_NAV_LIMIT;
    const moreClass =
      'flex-1 flex flex-col items-center justify-center gap-0.5 py-2 min-h-14 text-xs transition-colors text-emerald-100 hover:bg-emerald-700/40';
    return (
      <nav
        className="fixed bottom-0 inset-x-0 z-40 bg-emerald-900 text-white flex border-t border-emerald-700 pb-[env(safe-area-inset-bottom)]"
        aria-label="التنقل السفلي"
      >
        {bottomItems.map((item) => renderNavItem(item, bottomNavClass))}
        {hasMore && (
          <button
            type="button"
            onClick={() => setDrawerOpen(true)}
            className={moreClass}
            aria-haspopup="dialog"
            aria-label={`المزيد: ${navItems.length - bottomItems.length} بنودًا إضافية`}
          >
            المزيد ⋯
          </button>
        )}
      </nav>
    );
  };

  return (
    <div className="h-dvh flex flex-col" dir="rtl">
      <NetworkStatusBanner />
      <div className="flex flex-1 min-h-0">
        {!isMobile && (
          <aside className="w-64 bg-emerald-900 text-white flex flex-col shrink-0 h-full">
            {renderSidebarContent()}
          </aside>
        )}
        <main className="flex-1 min-h-0 bg-gray-100 p-4 lg:p-6 overflow-y-auto pb-20 lg:pb-6">
          <CurrentYearBanner />
          {isMobile && (
            <div className="mb-4 flex items-center gap-2">
              <button
                ref={drawerTriggerRef}
                onClick={() => setDrawerOpen(true)}
                aria-label="فتح القائمة"
                className="bg-emerald-800 text-white rounded-lg px-3 py-2 min-h-11 min-w-11"
              >
                ☰
              </button>
              <img
                src={nationalEmblem}
                alt=""
                aria-hidden="true"
                className="w-9 h-9 shrink-0"
              />
              <h1 className="text-lg font-bold text-emerald-900">مسار</h1>
              {canHaveCorrespondenceUrgent && (
                <CorrespondenceBell
                  portal={isEntityManager}
                  count={urgentCorrespondence}
                  className="ms-auto"
                />
              )}
            </div>
          )}
          <Outlet />
        </main>
      </div>

      {isMobile && drawerOpen && (
        <div
          ref={drawerRef}
          className="fixed inset-0 z-50"
          role="dialog"
          aria-modal="true"
          aria-label="قائمة التنقل"
        >
          <button
            ref={drawerCloseRef}
            autoFocus
            onClick={() => setDrawerOpen(false)}
            aria-label="إغلاق القائمة"
            className="absolute inset-0 bg-black/50 w-full h-full cursor-default min-h-11"
          />
          <aside className="absolute top-0 right-0 h-full w-72 max-w-[85%] bg-emerald-900 text-white flex flex-col shadow-xl">
            {renderSidebarContent(() => setDrawerOpen(false))}
          </aside>
        </div>
      )}

      {isMobile && renderBottomNav()}

      {comingSoonFeature ? (
        <ComingSoonToast feature={comingSoonFeature} onClose={() => setComingSoonFeature(null)} />
      ) : null}
    </div>
  );
}
