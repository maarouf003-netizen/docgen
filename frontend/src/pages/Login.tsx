import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../auth/useAuth';
import type { LoginBranchSelectionResponse, LoginResponse } from '../types';

const isBranchSelection = (
  result: LoginResponse | LoginBranchSelectionResponse,
): result is LoginBranchSelectionResponse => 'requiresBranchSelection' in result;

export default function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [pendingBranches, setPendingBranches] = useState<LoginBranchSelectionResponse['branches'] | null>(null);
  const [selectedBranch, setSelectedBranch] = useState('');

  useEffect(() => {
    if (window.location.search.includes('logged_out')) {
      // إزالة العلامة بعد تسجيل الخروج
      window.history.replaceState({}, '', '/login');
    }
  }, []);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    if (pendingBranches && selectedBranch === '') {
      setError('يرجى اختيار الفرع');
      return;
    }
    setBusy(true);
    try {
      const result = await login(
        username,
        password,
        pendingBranches ? Number(selectedBranch) : undefined,
      );
      if (isBranchSelection(result)) {
        setPendingBranches(result.branches);
        setSelectedBranch(
          result.branches.length === 1 ? String(result.branches[0].branchId ?? 0) : '',
        );
      } else {
        navigate('/');
      }
    } catch (err) {
      const message = axios.isAxiosError(err)
        ? (err.response?.data as { message?: string } | undefined)?.message
        : undefined;
      setError(message ?? 'اسم المستخدم أو كلمة المرور غير صحيحة');
    } finally {
      setBusy(false);
    }
  };

  const resetBranchSelection = () => {
    setPendingBranches(null);
    setSelectedBranch('');
    setError('');
  };

  return (
    <div className="min-h-screen bg-emerald-900 flex items-center justify-center p-4" dir="rtl">
      <form onSubmit={submit} className="bg-white rounded-2xl shadow-xl p-8 w-full max-w-sm">
        <h1 className="text-2xl font-bold text-emerald-900 text-center mb-1">
          مولد المستندات التنفيذية
        </h1>
        <p className="text-center text-gray-500 text-sm mb-6">تسجيل الدخول</p>

        {error && (
          <div className="bg-red-50 text-red-700 border border-red-200 rounded-lg p-3 mb-4 text-sm">
            {error}
          </div>
        )}

        {pendingBranches && (
          <div className="mb-4">
            <p className="text-sm text-amber-800 bg-amber-50 border border-amber-200 rounded-lg p-3 mb-3">
              يوجد أكثر من حساب بهذا الاسم في فروع مختلفة. اختر الفرع ثم تابع الدخول.
            </p>
            <label htmlFor="login-branch" className="block text-sm font-medium text-gray-700 mb-1">الفرع</label>
            <select
              id="login-branch"
              value={selectedBranch}
              onChange={(e) => setSelectedBranch(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 mb-4 focus:outline-none focus:ring-2 focus:ring-emerald-500 bg-white"
            >
              <option value="" disabled>
                اختر الفرع
              </option>
              {pendingBranches.map((branch) => (
                <option key={branch.branchId ?? 0} value={String(branch.branchId ?? 0)}>
                  {branch.branchName ?? 'بدون فرع'}
                </option>
              ))}
            </select>
            <button
              type="button"
              onClick={resetBranchSelection}
              className="text-sm text-emerald-800 underline"
            >
              تسجيل الدخول باسم آخر
            </button>
          </div>
        )}

        <label htmlFor="login-username" className="block text-sm font-medium text-gray-700 mb-1">اسم المستخدم</label>
        <input
          id="login-username"
          value={username}
          onChange={(e) => {
            setUsername(e.target.value);
            if (pendingBranches) resetBranchSelection();
          }}
          required
          autoFocus
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 mb-4 focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />

        <label htmlFor="login-password" className="block text-sm font-medium text-gray-700 mb-1">كلمة المرور</label>
        <input
          id="login-password"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 mb-6 focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />

        <button
          type="submit"
          disabled={busy}
          className="w-full bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white font-bold rounded-lg py-2.5 transition-colors min-h-11"
        >
          {busy ? 'جاري الدخول...' : pendingBranches ? 'متابعة الدخول' : 'دخول'}
        </button>
      </form>
    </div>
  );
}
