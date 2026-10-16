import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../auth/useAuth';
import { getHomeForRole } from '../auth/roleHome';
import type { LoginBranchSelectionResponse, LoginResponse } from '../types';
import nationalEmblem from '../assets/national.png';
import syrianFlag from '../assets/flag.jpg';

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
        // الهبوط حسب الدور مباشرة: المندوب إلى بوابته دون المرور بلوحة لا يملكها.
        navigate(getHomeForRole(result.user.role));
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
    <div dir="rtl" className="min-h-screen bg-emerald-950 lg:grid lg:grid-cols-2">
      {/* لوحة الهوية الرسمية — الأولى في DOM فتظهر يميناً في RTL على الكمبيوتر فقط */}
      <section
        aria-label="الجمهورية العربية السورية — وزارة العدل — إدارة قضايا الدولة"
        className="relative hidden overflow-hidden lg:flex lg:min-h-screen lg:flex-col"
      >
        {/* طبقة الصورة الاختيارية: ضع الصورة الأولى باسم public/login-hero.jpg لتظهر تلقائياً، وإلا يبقى التدرج الرسمي */}
        <div aria-hidden="true" className="absolute inset-0 bg-emerald-950" />
        <div
          aria-hidden="true"
          className="absolute inset-0 bg-cover bg-center"
          style={{ backgroundImage: "url('/login-hero.jpg')" }}
        />
        <div
          aria-hidden="true"
          className="absolute inset-0 bg-gradient-to-t from-emerald-950 via-emerald-950/75 to-emerald-950/25"
        />
        <div className="relative flex flex-1 flex-col items-center justify-end px-10 pt-24 pb-12 text-center text-white">
          <img
            src={nationalEmblem}
            alt="شعار إدارة قضايا الدولة — الجمهورية العربية السورية"
            width={96}
            height={96}
            className="h-24 w-24 rounded-full bg-white p-1 shadow-xl ring-1 ring-white/40 object-cover"
          />
          <p className="mt-5 text-sm font-medium tracking-wide text-amber-200">
            الجمهورية العربية السورية
          </p>
          <p className="mt-1 text-3xl font-bold text-white text-balance">وزارة العدل</p>
          <div aria-hidden="true" className="my-4 flex w-full max-w-xs items-center gap-3">
            <span className="h-px flex-1 bg-amber-200/60" />
            <span className="h-1.5 w-1.5 rotate-45 bg-amber-200" />
            <span className="h-px flex-1 bg-amber-200/60" />
          </div>
          <p className="text-xl font-semibold text-emerald-50">إدارة قضايا الدولة</p>
          <p className="mt-3 max-w-sm text-sm leading-7 text-emerald-100/90">
            منصة إدارة الملفات التنفيذية المدعومة بمولد مستندات آلي
          </p>
        </div>
        <div aria-hidden="true" className="relative flex h-1.5">
          <span className="flex-1 bg-[#007A3D]" />
          <span className="flex-1 bg-white" />
          <span className="flex-1 bg-neutral-900" />
        </div>
      </section>

      {/* لوحة الدخول — اليسار على الكمبيوتر، وملء الشاشة على الجوال */}
      <div className="flex min-h-screen items-center justify-center bg-slate-100 px-4 py-8 sm:px-6">
        <div className="w-full max-w-md min-w-0">
          {/* هوية مضغوطة للجوال فقط (اللوحة الجانبية مخفية تحته) */}
          <div className="mb-4 text-center lg:hidden">
            <img
              src={nationalEmblem}
              alt="شعار إدارة قضايا الدولة — الجمهورية العربية السورية"
              width={80}
              height={80}
              className="mx-auto h-20 w-20 rounded-full bg-white p-1 shadow-md ring-1 ring-gray-200 object-cover"
            />
            <p className="mt-2 text-xs font-medium text-emerald-800">الجمهورية العربية السورية</p>
            <p className="text-sm font-bold text-emerald-950">وزارة العدل — إدارة قضايا الدولة</p>
          </div>

          <form onSubmit={submit} className="overflow-hidden rounded-2xl bg-white shadow-xl">
            <div aria-hidden="true" className="flex h-1.5">
              <span className="flex-1 bg-[#007A3D]" />
              <span className="flex-1 bg-white" />
              <span className="flex-1 bg-neutral-900" />
            </div>
            <div className="p-6 sm:p-8">
              <div className="mb-4 flex items-center justify-between">
                <img
                  src={syrianFlag}
                  alt="علم الجمهورية العربية السورية"
                  width={64}
                  height={32}
                  className="w-16 rounded-md shadow-md aspect-[2/1] object-cover"
                />
                <img
                  src={nationalEmblem}
                  alt="شعار إدارة قضايا الدولة — الجمهورية العربية السورية"
                  width={64}
                  height={64}
                  className="w-16 h-16 rounded-full bg-white object-cover ring-1 ring-gray-200 drop-shadow-sm"
                />
              </div>
              <h1 className="text-2xl font-bold text-emerald-900 text-center mb-1 text-balance">
                مسار
              </h1>
              <p className="text-center text-gray-500 text-sm mb-6">
                منصة إدارة الملفات التنفيذية المدعومة بمولد مستندات آلي
              </p>

              {error && (
                <div
                  role="alert"
                  className="bg-red-50 text-red-700 border border-red-200 rounded-lg p-3 mb-4 text-sm"
                >
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
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 mb-4 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 bg-white"
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
                    className="text-sm text-emerald-800 underline min-h-11 touch-manipulation"
                  >
                    تسجيل الدخول باسم آخر
                  </button>
                </div>
              )}

              <label htmlFor="login-username" className="block text-sm font-medium text-gray-700 mb-1">اسم المستخدم</label>
              <input
                id="login-username"
                name="username"
                autoComplete="username"
                value={username}
                onChange={(e) => {
                  setUsername(e.target.value);
                  if (pendingBranches) resetBranchSelection();
                }}
                required
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 mb-4 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
              />

              <label htmlFor="login-password" className="block text-sm font-medium text-gray-700 mb-1">كلمة المرور</label>
              <input
                id="login-password"
                name="password"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 mb-6 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
              />

              <button
                type="submit"
                disabled={busy}
                className="w-full bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white font-bold rounded-lg py-2.5 transition-colors min-h-11 touch-manipulation focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2"
              >
                {busy ? 'جاري الدخول...' : pendingBranches ? 'متابعة الدخول' : 'دخول'}
              </button>
            </div>
          </form>
          <p className="mt-4 text-center text-xs leading-5 text-gray-500">
            © 2026 إدارة قضايا الدولة — وزارة العدل • للاستخدام الرسمي الداخلي
          </p>
        </div>
      </div>
    </div>
  );
}
