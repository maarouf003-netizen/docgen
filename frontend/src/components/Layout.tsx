import { useEffect, useRef, useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useIsMobile } from '../hooks/useMediaQuery';
import NetworkStatusBanner from './NetworkStatusBanner';
import ReviewPendingBell from './review/ReviewPendingBell';
import { REVIEWS_UNSEEN_EVENT } from './review/reviewDisplay';
import nationalEmblem from '../assets/national.png';

const ROLES: Record<string, string> = {
  lawyer: 'محامي',
  head: 'رئيس قسم',
  manager: 'مدير',
  admin: 'مشرف نظام',
};

interface NavItem {
  to: string;
  label: string;
  end?: boolean;
  /** عدد عناصر تحتاج انتباه صاحب الدور (مثل ردود غير مطّلع عليها). */
  badge?: number;
}

export default function Layout() {
  const { user, logout, hasFullAccess, isHead } = useAuth();
  const isMobile = useIsMobile();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [unseenReplies, setUnseenReplies] = useState(0);
  const drawerRef = useRef<HTMLDivElement>(null);
  const drawerTriggerRef = useRef<HTMLButtonElement>(null);
  const drawerCloseRef = useRef<HTMLButtonElement>(null);
  const isLawyerUser = user?.role === 'lawyer';

  // عدّاد كتب المطالعة فيها ردّ لم يطّلع عليه المحامي — شارة حمراء على بند المطالعات،
  // تُحدَّث كل دقيقة وفورًا عند فتح كتاب بعد الاطلاع (حدث reviews:unseen-changed).
  useEffect(() => {
    if (!isLawyerUser) return undefined;
    let cancelled = false;
    const fetchCount = () =>
      api
        .get<{ count: number }>('/review-letters/unseen-replies-count')
        .then((r) => {
          if (!cancelled) setUnseenReplies(r.data.count);
        })
        .catch(() => {
          /* الشارة تبقى على آخر قيمة معروفة عند فشل التحديث */
        });
    void fetchCount();
    const timer = window.setInterval(fetchCount, 60_000);
    const onSeenChanged = () => fetchCount();
    window.addEventListener(REVIEWS_UNSEEN_EVENT, onSeenChanged);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
      window.removeEventListener(REVIEWS_UNSEEN_EVENT, onSeenChanged);
    };
  }, [isLawyerUser]);

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

  const canViewAuditLogs = hasFullAccess || isHead;
  const canManageBranchLawyers = user?.role === 'head' || user?.role === 'admin';
  const canManageUsers = user?.role === 'admin';
  const canManageEntityRegistry = hasFullAccess || isHead;
  const canManageDelegates = hasFullAccess || isHead;
  // مندوب الجهة: قائمة بوابة مختصرة فقط (ملفاتي/تصدير) دون باقي البنود (المرحلة 3).
  const isEntityManager = user?.role === 'entitymanager';

  const navItems: NavItem[] = [];

  if (isEntityManager) {
    // مندوب الجهة: قائمة بوابة مختصرة فقط (ملفاتي/تصدير) دون باقي البنود (المرحلة 3).
    navItems.push({ to: '/portal', label: 'ملفات الجهة' });
  } else {
    navItems.push(
      { to: '/', label: 'لوحة التحكم', end: true },
      { to: '/documents', label: 'الملفات التنفيذية' },
    );
    navItems.push({
      to: '/reviews',
      label: user?.role === 'lawyer' ? 'المطالعات' : 'كتب المطالعات',
      badge: isLawyerUser && unseenReplies > 0 ? unseenReplies : undefined,
    });
    if (canManageBranchLawyers) navItems.push({ to: '/branch-lawyers', label: 'محامو الفرع' });
    if (user?.role === 'head') navItems.push({ to: '/delegations/requests', label: 'طلبات الإنابة' });
    if (canManageEntityRegistry) navItems.push({ to: '/entities/registry', label: 'سجل الجهات العامة' });
    if (user?.role === 'head') navItems.push({ to: '/entities/review', label: 'مراجعة سجل الجهات' });
    if (canManageDelegates) navItems.push({ to: '/delegates', label: 'مندوبو الجهات' });
    if (canManageUsers) navItems.push({ to: '/users/manage', label: 'إدارة المستخدمين' });
    if (canManageUsers) navItems.push({ to: '/branches/manage', label: 'إدارة الفروع' });
    if (hasFullAccess) navItems.push({ to: '/users', label: 'نشاط المستخدمين' });
    if (hasFullAccess) navItems.push({ to: '/entity-change-log', label: 'سجل تغييرات الجهات' });
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
        {user?.role === 'head' && (
          <div className="flex justify-center mt-2">
            <ReviewPendingBell />
          </div>
        )}
      </div>
      <nav className="flex-1 min-h-0 p-3 overflow-y-auto" aria-label="القائمة الرئيسية">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.end}
            onClick={onNavigate}
            className={linkClass}
          >
            {renderNavLabel(item)}
          </NavLink>
        ))}
      </nav>
      <div className="p-4 border-t border-emerald-700 text-sm">
        <div className="font-medium">{user?.fullName}</div>
        <div className="text-emerald-300 text-xs mb-2">
          {user?.role ? ROLES[user.role] : ''} — {user?.branchName || 'كل الفروع'}
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
        {bottomItems.map((item) => (
          <NavLink key={item.to} to={item.to} end={item.end} className={bottomNavClass}>
            {renderNavLabel(item)}
          </NavLink>
        ))}
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
              <ReviewPendingBell className="ms-auto" />
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
    </div>
  );
}
