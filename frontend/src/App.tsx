import { lazy, Suspense } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { useAuth } from './auth/useAuth';
import ErrorBoundary from './components/ErrorBoundary';
import Layout from './components/Layout';

const Login = lazy(() => import('./pages/Login'));
const Dashboard = lazy(() => import('./pages/Dashboard'));
const DocumentsList = lazy(() => import('./pages/DocumentsList'));
const DeletedDocuments = lazy(() => import('./pages/DeletedDocuments'));
const StruckOffDocuments = lazy(() => import('./pages/StruckOffDocuments'));
const ExecutedDocuments = lazy(() => import('./pages/ExecutedDocuments'));
const Rotation = lazy(() => import('./pages/Rotation'));
const DocumentForm = lazy(() => import('./pages/DocumentForm'));
const DocumentView = lazy(() => import('./pages/DocumentView'));
const UsersActivity = lazy(() => import('./pages/UsersActivity'));
const BranchLawyers = lazy(() => import('./pages/BranchLawyers'));
const DelegationRequests = lazy(() => import('./pages/DelegationRequests'));
const UsersManagement = lazy(() => import('./pages/UsersManagement'));
const BranchesManagement = lazy(() => import('./pages/BranchesManagement'));
const AuditLogs = lazy(() => import('./pages/AuditLogs'));
const ChangePassword = lazy(() => import('./pages/ChangePassword'));

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
    return <Navigate to="/" replace state={{ from: location }} />;
  return <>{children}</>;
}

export default function App() {
  return (
    <ErrorBoundary>
      <AuthProvider>
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
            <Route path="/" element={<Dashboard />} />
            <Route path="/documents" element={<DocumentsList />} />
            <Route path="/documents/deleted" element={<DeletedDocuments />} />
            <Route path="/documents/struck-off" element={<StruckOffDocuments />} />
            <Route path="/documents/executed" element={<ExecutedDocuments />} />
            <Route
              path="/documents/rotate"
              element={
                <RequireRole allowed={(role) => role === 'lawyer'}>
                  <Rotation />
                </RequireRole>
              }
            />
            <Route path="/documents/new" element={<DocumentForm />} />
            <Route path="/documents/:id" element={<DocumentView />} />
            <Route path="/documents/:id/edit" element={<DocumentForm />} />
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
            <Route path="/change-password" element={<ChangePassword />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </Suspense>
      </AuthProvider>
    </ErrorBoundary>
  );
}
