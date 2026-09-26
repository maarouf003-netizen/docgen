import { lazy, Suspense } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { useAuth } from './auth/useAuth';
import { getHomeForRole } from './auth/roleHome';
import CurrentYearProvider from './components/CurrentYearProvider';
import ErrorBoundary from './components/ErrorBoundary';
import Layout from './components/Layout';
import LegacyRouteBanner from './components/LegacyRouteBanner';

const Login = lazy(() => import('./pages/Login'));
const Dashboard = lazy(() => import('./pages/Dashboard'));
const DocumentsList = lazy(() => import('./pages/DocumentsList'));
const AppealsList = lazy(() => import('./pages/AppealsList'));
const AppealDetail = lazy(() => import('./pages/AppealDetail'));
const DeletedDocuments = lazy(() => import('./pages/DeletedDocuments'));
const StruckOffDocuments = lazy(() => import('./pages/StruckOffDocuments'));
const ExecutedDocuments = lazy(() => import('./pages/ExecutedDocuments'));
const ReferredToStartDocuments = lazy(() => import('./pages/ReferredToStartDocuments'));
const Rotation = lazy(() => import('./pages/Rotation'));
const DocumentForm = lazy(() => import('./pages/DocumentForm'));
const DocumentView = lazy(() => import('./pages/DocumentView'));
const UsersActivity = lazy(() => import('./pages/UsersActivity'));
const BranchLawyers = lazy(() => import('./pages/BranchLawyers'));
const DelegationRequests = lazy(() => import('./pages/DelegationRequests'));
const UsersManagement = lazy(() => import('./pages/UsersManagement'));
const BranchesManagement = lazy(() => import('./pages/BranchesManagement'));
const EntityRegistryReviewManagement = lazy(() => import('./pages/EntityRegistryReviewManagement'));
const EntityRegistryReview = lazy(() => import('./pages/EntityRegistryReview'));
const EntityDelegates = lazy(() => import('./pages/EntityDelegates'));
const PortalFiles = lazy(() => import('./pages/PortalFiles'));
const PortalStats = lazy(() => import('./pages/PortalStats'));
const PortalFileDetail = lazy(() => import('./pages/PortalFileDetail'));
const AuditLogs = lazy(() => import('./pages/AuditLogs'));
const ChangePassword = lazy(() => import('./pages/ChangePassword'));
const ReviewsList = lazy(() => import('./pages/ReviewsList'));
const ReviewDetail = lazy(() => import('./pages/ReviewDetail'));
const CorrespondencesList = lazy(() => import('./pages/CorrespondencesList'));
const CorrespondenceDetail = lazy(() => import('./pages/CorrespondenceDetail'));

function PageLoader() {
  return <div className="min-h-screen flex items-center justify-center text-gray-500">جارِ التحميل...</div>;
}

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) return <div className="min-h-screen flex items-center justify-center text-gray-500">جارِ التحميل...</div>;
  if (!user) return <Navigate to="/login" replace state={{ from: location }} />;
  return <>{children}</>;
}

function RequireRole({
  allowed,
  children,
}: {
  allowed: (role: string | undefined, hasFullAccess: boolean, isHead: boolean) => boolean;
  children: React.ReactNode;
}) {
  const { user, hasFullAccess, isHead, loading } = useAuth();
  const location = useLocation();

  if (loading) return <div className="min-h-screen flex items-center justify-center text-gray-500">جارِ التحميل...</div>;
  if (!user || !allowed(user.role, hasFullAccess, isHead))
    return <Navigate to={getHomeForRole(user?.role)} replace state={{ from: location }} />;
  return <>{children}</>;
}

/**
 * المسارات التشغيلية الداخلية: مسموحة للأدوار الداخلية صراحةً (قائمة
 * سماح مغلقة الفشل)، ومحجوبة عن مندوب الجهة الذي بوابته القرائية فقط —
 * الرفض يرتد به إلى وطنه عبر `getHomeForRole` (بوابته لا اللوحة).
 * `/change-password` يبقى خارجها (حق شخصي لكل الأدوار بما فيها المندوب).
 */
const allowInternal = (role: string | undefined) =>
  role === 'lawyer' || role === 'head' || role === 'manager' || role === 'admin';

/**
 * مسار الجذر حسب الدور: مندوب الجهة لا يملك لوحة تحكم إطلاقًا فيُحوَّل
 * إلى بوابته القرائية قبل تحميل `Dashboard` (فلا تنطلق استعلاماتها
 * المرفوضة بـ403 ولا تظهر شاشتها) — بقية الأدوار ترى اللوحة.
 * يغطي هذا الزيارة المباشرة لـ`/` وارتدادات `RequireRole` معًا.
 */
function RootRoute() {
  const { user, loading } = useAuth();
  if (loading) return <PageLoader />;
  const home = getHomeForRole(user?.role);
  if (home !== '/') return <Navigate to={home} replace />;
  return <Dashboard />;
}

export default function App() {
  return (
    <ErrorBoundary>
      <AuthProvider>
        <CurrentYearProvider>
        <Suspense fallback={<PageLoader />}>
          <Routes>
          <Route path="/login" element={<Login />} />
          <Route
            element={
              <RequireAuth>
                <Layout />
              </RequireAuth>
            }
          >
            <Route path="/" element={<RootRoute />} />
            <Route
              path="/documents"
              element={
                <RequireRole allowed={allowInternal}>
                  <DocumentsList />
                </RequireRole>
              }
            />
            <Route
              path="/reviews"
              element={
                <RequireRole allowed={allowInternal}>
                  <ReviewsList />
                </RequireRole>
              }
            />
            <Route
              path="/reviews/:id"
              element={
                <RequireRole allowed={allowInternal}>
                  <ReviewDetail />
                </RequireRole>
              }
            />
            <Route
              path="/correspondence"
              element={
                <RequireRole allowed={allowInternal}>
                  <CorrespondencesList />
                </RequireRole>
              }
            />
            <Route
              path="/correspondence/:id"
              element={
                <RequireRole allowed={allowInternal}>
                  <CorrespondenceDetail />
                </RequireRole>
              }
            />
          <Route
            path="/appeals"
            element={
              <RequireRole allowed={allowInternal}>
                <AppealsList />
              </RequireRole>
            }
          />
          <Route
            path="/appeals/:id"
            element={
              <RequireRole allowed={allowInternal}>
                <AppealDetail />
              </RequireRole>
            }
          />
            <Route
              path="/documents/deleted"
              element={
                <RequireRole allowed={allowInternal}>
                  <DeletedDocuments />
                </RequireRole>
              }
            />
            <Route
              path="/documents/struck-off"
              element={
                <RequireRole allowed={allowInternal}>
                  <StruckOffDocuments />
                </RequireRole>
              }
            />
            <Route
              path="/documents/executed"
              element={
                <RequireRole allowed={allowInternal}>
                  <ExecutedDocuments />
                </RequireRole>
              }
            />
            <Route
              path="/documents/referred-to-start"
              element={
                <RequireRole allowed={allowInternal}>
                  <ReferredToStartDocuments />
                </RequireRole>
              }
            />
            <Route
              path="/documents/rotate"
              element={
                <RequireRole allowed={(role) => role === 'lawyer'}>
                  <Rotation />
                </RequireRole>
              }
            />
            <Route
              path="/documents/new"
              element={
                <RequireRole allowed={allowInternal}>
                  <DocumentForm />
                </RequireRole>
              }
            />
            <Route
              path="/documents/:id"
              element={
                <RequireRole allowed={allowInternal}>
                  <DocumentView />
                </RequireRole>
              }
            />
            <Route
              path="/documents/:id/edit"
              element={
                <RequireRole allowed={allowInternal}>
                  <DocumentForm />
                </RequireRole>
              }
            />
            <Route
              path="/branch-lawyers"
              element={
                <RequireRole allowed={(role) => role === 'head' || role === 'admin'}>
                  <BranchLawyers />
                </RequireRole>
              }
            />
            <Route
              path="/delegations/requests"
              element={
                <RequireRole allowed={(role) => role === 'head'}>
                  <DelegationRequests />
                </RequireRole>
              }
            />
            <Route
              path="/users/manage"
              element={
                <RequireRole allowed={(role) => role === 'admin'}>
                  <UsersManagement />
                </RequireRole>
              }
            />
            <Route
              path="/branches/manage"
              element={
                <RequireRole allowed={(role) => role === 'admin'}>
                  <BranchesManagement />
                </RequireRole>
              }
            />
            <Route
              path="/entities/registry"
              element={
                <RequireRole allowed={(_role, hasFullAccess) => hasFullAccess}>
                  <LegacyRouteBanner
                    to="/entities/review-management?tab=add"
                    message="هذا الرابط القديم لسجل الجهات لم يعد معتمدًا — انتقل إلى «إدارة سجل الجهات»"
                  />
                </RequireRole>
              }
            />
            <Route
              path="/entities/review-management"
              element={
                <RequireRole allowed={(_role, hasFullAccess) => hasFullAccess}>
                  <EntityRegistryReviewManagement />
                </RequireRole>
              }
            />
            <Route
              path="/entities/review"
              element={
                <RequireRole allowed={(_role, hasFullAccess, isHead) => hasFullAccess || isHead}>
                  <EntityRegistryReview />
                </RequireRole>
              }
            />
            <Route
              path="/portal"
              element={
                <RequireRole allowed={(role) => role === 'entitymanager'}>
                  <Navigate to="/portal/stats" replace />
                </RequireRole>
              }
            />
            <Route
              path="/portal/stats"
              element={
                <RequireRole allowed={(role) => role === 'entitymanager'}>
                  <PortalStats />
                </RequireRole>
              }
            />
            <Route
              path="/portal/files"
              element={
                <RequireRole allowed={(role) => role === 'entitymanager'}>
                  <PortalFiles />
                </RequireRole>
              }
            />
            <Route
              path="/portal/files/:id"
              element={
                <RequireRole allowed={(role) => role === 'entitymanager'}>
                  <PortalFileDetail />
                </RequireRole>
              }
            />
            <Route
              path="/portal/correspondence"
              element={
                <RequireRole allowed={(role) => role === 'entitymanager'}>
                  <CorrespondencesList portal />
                </RequireRole>
              }
            />
            <Route
              path="/portal/correspondence/:id"
              element={
                <RequireRole allowed={(role) => role === 'entitymanager'}>
                  <CorrespondenceDetail portal />
                </RequireRole>
              }
            />
            <Route
              path="/delegates"
              element={
                <RequireRole allowed={(_role, hasFullAccess, isHead) => hasFullAccess || isHead}>
                  <EntityDelegates />
                </RequireRole>
              }
            />
            <Route
              path="/users"
              element={
                <RequireRole allowed={(_role, hasFullAccess) => hasFullAccess}>
                  <UsersActivity />
                </RequireRole>
              }
            />
            <Route
              path="/audit-logs"
              element={
                <RequireRole allowed={(_role, hasFullAccess, isHead) => hasFullAccess || isHead}>
                  <AuditLogs />
                </RequireRole>
              }
            />
            <Route
              path="/entity-change-log"
              element={
                <RequireRole allowed={(_role, hasFullAccess) => hasFullAccess}>
                  <LegacyRouteBanner
                    to="/entities/review-management?tab=log"
                    message="هذا الرابط القديم لسجل تغييرات الجهات لم يعد معتمدًا — انتقل إلى «سجل تغييرات الجهات»"
                  />
                </RequireRole>
              }
            />
            <Route path="/change-password" element={<ChangePassword />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </Suspense>
        </CurrentYearProvider>
      </AuthProvider>
    </ErrorBoundary>
  );
}
