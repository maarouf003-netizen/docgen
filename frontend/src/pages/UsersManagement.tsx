import { useEffect, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useIsMobile } from '../hooks/useMediaQuery';
import type { BranchDto, Role, UserListItem } from '../types';

const ROLE_LABELS: Record<Role, string> = {
  lawyer: 'محامي',
  head: 'رئيس قسم',
  manager: 'مدير',
  admin: 'مشرف نظام',
};

const BRANCH_ROLES: Role[] = ['lawyer', 'head'];

function branchRequired(role: Role): boolean {
  return BRANCH_ROLES.includes(role);
}

export default function UsersManagement() {
  const { user: me } = useAuth();
  const isMobile = useIsMobile();

  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [showForm, setShowForm] = useState(false);
  const [fullName, setFullName] = useState('');
  const [role, setRole] = useState<Role>('lawyer');
  const [branchId, setBranchId] = useState<number | null>(null);
  const [password, setPassword] = useState('');
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');

  const [editing, setEditing] = useState<UserListItem | null>(null);
  const [editFullName, setEditFullName] = useState('');
  const [editRole, setEditRole] = useState<Role>('lawyer');
  const [editBranchId, setEditBranchId] = useState<number | null>(null);
  const [editActive, setEditActive] = useState(true);
  const [editPassword, setEditPassword] = useState('');
  const [editSaving, setEditSaving] = useState(false);
  const [editError, setEditError] = useState('');

  const load = () => {
    setLoading(true);
    setError('');
    api
      .get<UserListItem[]>('/users')
      .then((r) => setUsers(r.data))
      .catch((err) => setError(getApiErrorMessage(err)))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    api
      .get<BranchDto[]>('/branches')
      .then((r) => setBranches(r.data))
      .catch(() => undefined);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const resetForm = () => {
    setFullName('');
    setRole('lawyer');
    setBranchId(null);
    setPassword('');
    setFormError('');
    setShowForm(false);
  };

  const validate = (pw: string): string => {
    if (!fullName.trim()) return 'الاسم الثلاثي مطلوب';
    if (pw.length < 6) return 'كلمة المرور يجب أن تكون 6 أحرف على الأقل';
    return '';
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const problem = validate(password);
    if (problem) {
      setFormError(problem);
      return;
    }
    if (branchRequired(role) && branchId === null) {
      setFormError('يجب تحديد الفرع لهذا الدور');
      return;
    }

    setSaving(true);
    setFormError('');
    try {
      const name = fullName.trim();
      await api.post('/users', {
        username: name,
        fullName: name,
        role,
        branchId: branchRequired(role) ? branchId : null,
        password,
      });
      resetForm();
      load();
    } catch (err) {
      setFormError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const openEdit = (u: UserListItem) => {
    setEditing(u);
    setEditFullName(u.fullName);
    setEditRole(u.role);
    setEditBranchId(u.branchId);
    setEditActive(u.isActive);
    setEditPassword('');
    setEditError('');
  };

  const closeEdit = () => {
    setEditing(null);
    setEditPassword('');
    setEditError('');
  };

  const saveEdit = async () => {
    if (!editing) return;
    if (!editFullName.trim()) {
      setEditError('الاسم الكامل مطلوب');
      return;
    }
    if (branchRequired(editRole) && editBranchId === null) {
      setEditError('يجب تحديد الفرع لهذا الدور');
      return;
    }
    if (editPassword && editPassword.length < 6) {
      setEditError('كلمة المرور الجديدة يجب أن تكون 6 أحرف على الأقل');
      return;
    }

    setEditSaving(true);
    setEditError('');
    try {
      await api.put(`/users/${editing.id}`, {
        fullName: editFullName.trim(),
        role: editRole,
        branchId: branchRequired(editRole) ? editBranchId : null,
        isActive: editActive,
        password: editPassword || null,
      });
      closeEdit();
      load();
    } catch (err) {
      setEditError(getApiErrorMessage(err));
    } finally {
      setEditSaving(false);
    }
  };

  const isSelf = (u: UserListItem) => u.id === me?.id;

  return (
    <div className="max-w-5xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-6">إدارة المستخدمين</h2>

      <div className="bg-white rounded-xl shadow p-4 mb-4 flex flex-wrap items-center justify-between gap-3">
        <div className="text-sm text-gray-600">
          {loading ? 'جارِ التحميل...' : `${users.length} مستخدم`}
        </div>
        <button
          onClick={() => setShowForm((v) => !v)}
          className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
        >
          {showForm ? 'إلغاء' : '+ إضافة مستخدم'}
        </button>
      </div>

      {showForm && (
        <form
          onSubmit={submit}
          className="bg-white rounded-xl shadow p-4 mb-4 grid sm:grid-cols-2 gap-4"
        >
          <div className="sm:col-span-2">
            <label htmlFor="user-fullname" className="block text-xs font-medium text-gray-600 mb-1">الاسم الثلاثي (اسم الدخول)</label>
            <input
              id="user-fullname"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              placeholder="مثال: محمد أحمد علي"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="user-role" className="block text-xs font-medium text-gray-600 mb-1">الدور</label>
            <select
              id="user-role"
              value={role}
              onChange={(e) => setRole(e.target.value as Role)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none"
            >
              {(Object.keys(ROLE_LABELS) as Role[]).map((r) => (
                <option key={r} value={r}>{ROLE_LABELS[r]}</option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="user-branch" className="block text-xs font-medium text-gray-600 mb-1">الفرع</label>
            <select
              id="user-branch"
              value={branchId ?? ''}
              onChange={(e) => setBranchId(e.target.value ? Number(e.target.value) : null)}
              disabled={!branchRequired(role)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none disabled:bg-gray-50 disabled:text-gray-400"
            >
              <option value="">{branchRequired(role) ? 'اختر الفرع...' : 'غير مطلوب'}</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
          </div>
          <div className="sm:col-span-2">
            <label htmlFor="user-password" className="block text-xs font-medium text-gray-600 mb-1">كلمة المرور (6 أحرف على الأقل)</label>
            <input
              id="user-password"
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
              {saving ? 'جارِ الحفظ...' : 'إنشاء المستخدم'}
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
                <th className="px-4 py-3">الدور</th>
                <th className="px-4 py-3">الفرع</th>
                <th className="px-4 py-3">الحالة</th>
                <th className="px-4 py-3">إجراء</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {users.map((u) => (
                <tr key={u.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-medium text-gray-800">{u.fullName}</td>
                  <td className="px-4 py-3">{u.username}</td>
                  <td className="px-4 py-3">{ROLE_LABELS[u.role] ?? u.role}</td>
                  <td className="px-4 py-3">{u.branchName || '—'}</td>
                  <td className="px-4 py-3">
                    <span
                      className={`inline-block rounded-full px-2 py-0.5 text-xs ${
                        u.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-200 text-gray-600'
                      }`}
                    >
                      {u.isActive ? 'مفعّل' : 'موقوف'}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <button
                      onClick={() => openEdit(u)}
                      className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 text-xs min-h-11"
                    >
                      تعديل
                    </button>
                  </td>
                </tr>
              ))}
              {!loading && users.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-gray-400">لا يوجد مستخدمون</td>
                </tr>
              )}
            </tbody>
          </table>
        )}

        {isMobile && (
          <div className="divide-y divide-gray-100">
            {!loading && users.length === 0 && (
              <div className="px-4 py-8 text-center text-gray-400">لا يوجد مستخدمون</div>
            )}
            {users.map((u) => (
              <div key={u.id} className="p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="font-bold text-gray-800">{u.fullName}</div>
                  <span
                    className={`rounded-full px-2 py-0.5 text-xs ${
                      u.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-200 text-gray-600'
                    }`}
                  >
                    {u.isActive ? 'مفعّل' : 'موقوف'}
                  </span>
                </div>
                <div className="text-sm text-gray-600 mt-1">
                  {u.username} · {ROLE_LABELS[u.role] ?? u.role}
                  {u.branchName ? <span className="text-gray-400"> · {u.branchName}</span> : null}
                </div>
                <button
                  onClick={() => openEdit(u)}
                  className="mt-3 text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 text-xs min-h-11"
                >
                  تعديل
                </button>
              </div>
            ))}
          </div>
        )}
      </div>

      {editing && (
        <div
          className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
          dir="rtl"
          role="dialog"
          aria-modal="true"
          aria-label="تعديل مستخدم"
        >
          <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[85vh] overflow-y-auto p-5">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-bold text-gray-800">تعديل مستخدم</h3>
              <button
                onClick={closeEdit}
                className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
                aria-label="إغلاق"
              >
                ×
              </button>
            </div>

            <div className="grid sm:grid-cols-2 gap-4">
              <div>
                <label htmlFor="edit-fullname" className="block text-xs font-medium text-gray-600 mb-1">الاسم الثلاثي (اسم الدخول)</label>
                <input
                  id="edit-fullname"
                  value={editFullName}
                  onChange={(e) => setEditFullName(e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
                <p className="text-[11px] text-gray-400 mt-1">تعديل الاسم يحدّث اسم الدخول تلقائياً.</p>
              </div>
              <div>
                <label htmlFor="edit-role" className="block text-xs font-medium text-gray-600 mb-1">الدور</label>
                <select
                  id="edit-role"
                  value={editRole}
                  onChange={(e) => setEditRole(e.target.value as Role)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none"
                >
                  {(Object.keys(ROLE_LABELS) as Role[]).map((r) => (
                    <option key={r} value={r}>{ROLE_LABELS[r]}</option>
                  ))}
                </select>
              </div>
              <div>
                <label htmlFor="edit-branch" className="block text-xs font-medium text-gray-600 mb-1">الفرع</label>
                <select
                  id="edit-branch"
                  value={editBranchId ?? ''}
                  onChange={(e) => setEditBranchId(e.target.value ? Number(e.target.value) : null)}
                  disabled={!branchRequired(editRole)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none disabled:bg-gray-50 disabled:text-gray-400"
                >
                  <option value="">{branchRequired(editRole) ? 'اختر الفرع...' : 'غير مطلوب'}</option>
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>{b.name}</option>
                  ))}
                </select>
              </div>
              <div>
                <label htmlFor="edit-password" className="block text-xs font-medium text-gray-600 mb-1">كلمة مرور جديدة (اختياري)</label>
                <input
                  id="edit-password"
                  type="password"
                  value={editPassword}
                  onChange={(e) => setEditPassword(e.target.value)}
                  placeholder="اتركها فارغة للإبقاء"
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
            </div>

            <label className="mt-4 inline-flex items-center gap-2 text-sm cursor-pointer min-h-11">
              <input
                type="checkbox"
                checked={editActive}
                disabled={isSelf(editing)}
                onChange={(e) => setEditActive(e.target.checked)}
              />
              الحساب مفعّل
              {isSelf(editing) && <span className="text-xs text-gray-400">(لا يمكنك إيقاف حسابك)</span>}
            </label>

            {editError && <p className="text-red-600 text-sm mt-3">{editError}</p>}

            <div className="mt-5 flex flex-wrap gap-2">
              <button
                onClick={saveEdit}
                disabled={editSaving}
                className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {editSaving ? 'جارِ الحفظ...' : 'حفظ التعديل'}
              </button>
              <button
                onClick={closeEdit}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
              >
                إلغاء
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
