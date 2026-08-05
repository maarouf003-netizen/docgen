import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { useAuth } from './auth/useAuth';
import ErrorBoundary from './components/ErrorBoundary';
import Layout from './components/Layout';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import DocumentsList from './pages/DocumentsList';
import DeletedDocuments from './pages/DeletedDocuments';
import DocumentForm from './pages/DocumentForm';
import DocumentView from './pages/DocumentView';
import UsersActivity from './pages/UsersActivity';
import BranchLawyers from './pages/BranchLawyers';
import UsersManagement from './pages/UsersManagement';
import AuditLogs from './pages/AuditLogs';
import ChangePassword from './pages/ChangePassword';

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) return <div className="min-h-screen flex items-center justify-center text-gray-500">جارِ التحميل...</div>;
  if (!user) return <Navigate to="/login" replace state={{ from: location }} />;
  return <>{children}</>;
}

export default function App() {
  return (
    <ErrorBoundary>
      <AuthProvider>
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
            <Route path="/documents/new" element={<DocumentForm />} />
            <Route path="/documents/:id" element={<DocumentView />} />
            <Route path="/documents/:id/edit" element={<DocumentForm />} />
            <Route path="/branch-lawyers" element={<BranchLawyers />} />
            <Route path="/users/manage" element={<UsersManagement />} />
            <Route path="/users" element={<UsersActivity />} />
            <Route path="/audit-logs" element={<AuditLogs />} />
            <Route path="/change-password" element={<ChangePassword />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </ErrorBoundary>
  );
}
