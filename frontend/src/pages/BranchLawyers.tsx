import { useEffect, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useIsMobile } from '../hooks/useMediaQuery';
import type { BranchDto, LawyerListItem } from '../types';

export default function BranchLawyers() {
  const { user } = useAuth();
  const isAdmin = user?.role === 'admin';
  const isMobile = useIsMobile();

  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [branchId, setBranchId] = useState<number | null>(null);
  const [lawyers, setLawyers] = useState<LawyerListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [showForm, setShowForm] = useState(false);
  const [fullName, setFullName] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');

  const [busyId, setBusyId] = useState<number | null>(null);

  useEffect(() => {
    if (!isAdmin) return;
    api
      .get<BranchDto[]>('/branches')
      .then((r) => {
        setBranches(r.data);
        setBranchId(r.data[0]?.id ?? null);
      })
      .catch((err) => setError(getApiErrorMessage(err)));
  }, [isAdmin]);

  const load = (selectedBranchId: number | null) => {
    setLoading(true);
    setError('');
    api
      .get<LawyerListItem[]>('/users/lawyers', {
        params: isAdmin && selectedBranchId ? { branchId: selectedBranchId } : undefined,
      })
      .then((r) => setLawyers(r.data))
      .catch((err) => setError(getApiErrorMessage(err)))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load(branchId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branchId, isAdmin]);

  const resetForm = () => {
    setFullName('');
    setUsername('');
    setPassword('');
    setFormError('');
    setShowForm(false);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!fullName.trim()) {
      setFormError('الاسم الكامل مطلوب');
      return;
    }
    if (!username.trim() || /\s/.test(username)) {
      setFormError('اسم المستخدم مطلوب ولا يحوي مسافات');
      return;
    }
    if (password.length < 6) {
      setFormError('كلمة المرور يجب أن تكون 6 أحرف على الأقل');
      return;
    }

    setSaving(true);
    setFormError('');
    try {
      await api.post('/users/lawyers', {
        username: username.trim(),
        fullName: fullName.trim(),
        password,
        branchId: isAdmin ? branchId : null,
      });
      resetForm();
      load(branchId);
    } catch (err) {
      setFormError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const toggleActive = async (lawyer: LawyerListItem) => {
    if (!window.confirm(`هل أنت متأكد من ${lawyer.isActive ? 'إيقاف' : 'تفعيل'} ${lawyer.fullName}؟`)) return;
    setBusyId(lawyer.id);
    setError('');
    try {
      await api.patch(`/users/${lawyer.id}/active`, { isActive: !lawyer.isActive });
      load(branchId);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  };

  const activeCount = lawyers.filter((l) => l.isActive).length;

  return (
    <div className="max-w-4xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-6">محامو الفرع</h2>

      {isAdmin && (
        <div className="bg-white rounded-xl shadow p-4 mb-4 flex flex-col sm:flex-row sm:items-center gap-3">
          <label htmlFor="branch-filter" className="text-sm text-gray-600">الفرع</label>
          <select
            id="branch-filter"
            value={branchId ?? ''}
            onChange={(e) => setBranchId(e.target.value ? Number(e.target.value) : null)}
            className="border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none flex-1 min-h-11"
          >
            {branches.map((b) => (
              <option key={b.id} value={b.id}>{b.name}</option>
            ))}
          </select>
        </div>
      )}

      <div className="bg-white rounded-xl shadow p-4 mb-4 flex flex-wrap items-center justify-between gap-3">
        <div className="text-sm text-gray-600">
          {loading ? 'جارِ التحميل...' : `${lawyers.length} محامٍ (${activeCount} مفعّل)`}
        </div>
        <button
          onClick={() => setShowForm((v) => !v)}
          className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
        >
          {showForm ? 'إلغاء' : '+ إضافة محامٍ'}
        </button>
      </div>

      {showForm && (
        <form
          onSubmit={submit}
          className="bg-white rounded-xl shadow p-4 mb-4 grid sm:grid-cols-2 gap-4"
        >
          <div>
            <label htmlFor="lawyer-fullname" className="block text-xs font-medium text-gray-600 mb-1">الاسم الكامل</label>
            <input
              id="lawyer-fullname"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              placeholder="اسم المحامي..."
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="lawyer-username" className="block text-xs font-medium text-gray-600 mb-1">اسم المستخدم</label>
            <input
              id="lawyer-username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="بدون مسافات..."
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div className="sm:col-span-2">
            <label htmlFor="lawyer-password" className="block text-xs font-medium text-gray-600 mb-1">كلمة المرور (6 أحرف على الأقل)</label>
            <input
              id="lawyer-password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          {formError && <p className="text-red-600 text-sm sm:col-span-2">{formError}</p>}
          <div className="sm:col-span-2 flex gap-2">
            <button
              type="submit"
              disabled={saving}
              className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              {saving ? 'جارِ الحفظ...' : 'حفظ المحامي'}
            </button>
          </div>
        </form>
      )}

      {error && <div className="text-red-600 mb-4">{error}</div>}

      <div className="bg-white rounded-xl shadow overflow-hidden">
        {!isMobile && (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-gray-600">
              <tr className="text-right">
                <th className="px-4 py-3">الاسم</th>
                <th className="px-4 py-3">اسم المستخدم</th>
                <th className="px-4 py-3">الفرع</th>
                <th className="px-4 py-3">الحالة</th>
                <th className="px-4 py-3">إجراء</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {lawyers.map((l) => (
                <tr key={l.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-medium text-gray-800">{l.fullName}</td>
                  <td className="px-4 py-3">{l.username}</td>
                  <td className="px-4 py-3">{l.branchName || '—'}</td>
                  <td className="px-4 py-3">
                    <span
                      className={`inline-block rounded-full px-2 py-0.5 text-xs ${
                        l.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-200 text-gray-600'
                      }`}
                    >
                      {l.isActive ? 'مفعّل' : 'موقوف'}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <button
                      onClick={() => toggleActive(l)}
                      disabled={busyId === l.id}
                      className={`rounded-lg px-3 py-1.5 text-xs min-h-11 ${
                        l.isActive
                          ? 'bg-red-50 text-red-700 hover:bg-red-100'
                          : 'bg-emerald-50 text-emerald-700 hover:bg-emerald-100'
                      }`}
                    >
                      {busyId === l.id ? 'جارٍ...' : l.isActive ? 'إيقاف' : 'تفعيل'}
                    </button>
                  </td>
                </tr>
              ))}
              {!loading && lawyers.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-gray-400">لا يوجد محامون</td>
                </tr>
              )}
            </tbody>
          </table>
        )}

        {isMobile && (
          <div className="divide-y divide-gray-100">
            {!loading && lawyers.length === 0 && (
              <div className="px-4 py-8 text-center text-gray-400">لا يوجد محامون</div>
            )}
            {lawyers.map((l) => (
              <div key={l.id} className="p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="font-bold text-gray-800">{l.fullName}</div>
                  <span
                    className={`rounded-full px-2 py-0.5 text-xs ${
                      l.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-200 text-gray-600'
                    }`}
                  >
                    {l.isActive ? 'مفعّل' : 'موقوف'}
                  </span>
                </div>
                <div className="text-sm text-gray-600 mt-1">
                  {l.username}
                  {l.branchName ? <span className="text-gray-400"> · {l.branchName}</span> : null}
                </div>
                <button
                  onClick={() => toggleActive(l)}
                  disabled={busyId === l.id}
                  className={`mt-3 rounded-lg px-3 py-2 text-xs min-h-11 ${
                    l.isActive
                      ? 'bg-red-50 text-red-700 hover:bg-red-100'
                      : 'bg-emerald-50 text-emerald-700 hover:bg-emerald-100'
                  }`}
                >
                  {busyId === l.id ? 'جارٍ...' : l.isActive ? 'إيقاف الحساب' : 'تفعيل الحساب'}
                </button>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
