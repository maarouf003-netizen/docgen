import { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import { useIsMobile } from '../hooks/useMediaQuery';
import NetworkStatusBanner from './NetworkStatusBanner';

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
}

export default function Layout() {
  const { user, logout, hasFullAccess, isHead } = useAuth();
  const isMobile = useIsMobile();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const canViewAuditLogs = hasFullAccess || isHead;
  const canManageBranchLawyers = user?.role === 'head' || user?.role === 'admin';
  const canManageUsers = user?.role === 'admin';

  const navItems: NavItem[] = [
    { to: '/', label: 'لوحة التحكم', end: true },
    { to: '/documents', label: 'الملفات التنفيذية' },
  ];
  if (canManageBranchLawyers) navItems.push({ to: '/branch-lawyers', label: 'محامو الفرع' });
  if (canManageUsers) navItems.push({ to: '/users/manage', label: 'إدارة المستخدمين' });
  if (canManageUsers) navItems.push({ to: '/branches/manage', label: 'إدارة الفروع' });
  if (hasFullAccess) navItems.push({ to: '/users', label: 'نشاط المستخدمين' });
  if (canViewAuditLogs) navItems.push({ to: '/audit-logs', label: 'سجل التدقيق' });

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    'block rounded-lg px-4 py-2.5 mb-1 transition-colors min-h-11 ' +
    (isActive ? 'bg-emerald-700 text-white' : 'text-emerald-100 hover:bg-emerald-700/40');

  const bottomNavClass = ({ isActive }: { isActive: boolean }) =>
    'flex-1 flex flex-col items-center justify-center gap-0.5 py-2 min-h-14 text-xs transition-colors ' +
    (isActive ? 'bg-emerald-700 text-white' : 'text-emerald-100 hover:bg-emerald-700/40');

  const renderSidebarContent = (onNavigate?: () => void) => (
    <>
      <div className="p-5 border-b border-emerald-700">
        <h1 className="text-lg font-bold">مولد المستندات التنفيذية</h1>
        <p className="text-xs text-emerald-300 mt-1">النظام الإلكتروني</p>
      </div>
      <nav className="flex-1 p-3 overflow-y-auto" aria-label="القائمة الرئيسية">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.end}
            onClick={onNavigate}
            className={linkClass}
          >
            {item.label}
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

  const renderBottomNav = () => (
    <nav
      className="fixed bottom-0 inset-x-0 z-40 bg-emerald-900 text-white flex border-t border-emerald-700 pb-[env(safe-area-inset-bottom)]"
      aria-label="التنقل السفلي"
    >
      {navItems.map((item) => (
        <NavLink key={item.to} to={item.to} end={item.end} className={bottomNavClass}>
          <span>{item.label}</span>
        </NavLink>
      ))}
    </nav>
  );

  return (
    <div className="min-h-screen flex flex-col" dir="rtl">
      <NetworkStatusBanner />
      <div className="flex flex-1">
        {!isMobile && (
          <aside className="w-64 bg-emerald-900 text-white flex flex-col shrink-0 min-h-screen">
            {renderSidebarContent()}
          </aside>
        )}
        <main className="flex-1 bg-gray-100 p-4 lg:p-6 overflow-y-auto pb-20 lg:pb-6">
          {isMobile && (
            <div className="mb-4 flex items-center gap-2">
              <button
                onClick={() => setDrawerOpen(true)}
                aria-label="فتح القائمة"
                className="bg-emerald-800 text-white rounded-lg px-3 py-2 min-h-11 min-w-11"
              >
                ☰
              </button>
              <h1 className="text-lg font-bold text-emerald-900">مولد المستندات التنفيذية</h1>
            </div>
          )}
          <Outlet />
        </main>
      </div>

      {isMobile && drawerOpen && (
        <div
          className="fixed inset-0 z-50"
          role="dialog"
          aria-modal="true"
          aria-label="قائمة التنقل"
        >
          <button
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
