import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../auth/useAuth';

export default function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (window.location.search.includes('logged_out')) {
      // إزالة العلامة بعد تسجيل الخروج
      window.history.replaceState({}, '', '/login');
    }
  }, []);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setBusy(true);
    try {
      await login(username, password);
      navigate('/');
    } catch (err) {
      const message = axios.isAxiosError(err)
        ? (err.response?.data as { message?: string } | undefined)?.message
        : undefined;
      setError(message ?? 'اسم المستخدم أو كلمة المرور غير صحيحة');
    } finally {
      setBusy(false);
    }
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

        <label className="block text-sm font-medium text-gray-700 mb-1">اسم المستخدم</label>
        <input
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          required
          autoFocus
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 mb-4 focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />

        <label className="block text-sm font-medium text-gray-700 mb-1">كلمة المرور</label>
        <input
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
          {busy ? 'جاري الدخول...' : 'دخول'}
        </button>
      </form>
    </div>
  );
}
