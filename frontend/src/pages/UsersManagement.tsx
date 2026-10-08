import { useEffect, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useIsMobile } from '../hooks/useMediaQuery';
import type { BranchDto, Role, SectionDto, UserListItem } from '../types';
import { ROLE_LABELS } from '../auth/roleLabels';

const BRANCH_ROLES: Role[] = ['lawyer', 'head', 'subhead'];

function branchRequired(role: Role): boolean {
  return BRANCH_ROLES.includes(role);
}

function sectionRequired(role: Role): boolean {
  return role === 'subhead';
}

function isHeadRole(role: Role): boolean {
  return role === 'head' || role === 'subhead';
}

const userCountFormatter = new Intl.NumberFormat('ar-SY');

export default function UsersManagement() {
  const { user: me } = useAuth();
  // `BQ-001د`: المدير لا يرى دور المشرف أصلًا في القوائم ولا يحرر حساباته.
  const isManager = me?.role === 'manager';
  const selectableRoles = (Object.keys(ROLE_LABELS) as Role[]).filter((r) => !isManager || r !== 'admin');
  const isMobile = useIsMobile();

  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [showForm, setShowForm] = useState(false);
  const [fullName, setFullName] = useState('');
  const [role, setRole] = useState<Role>('lawyer');
  const [branchId, setBranchId] = useState<number | null>(null);
  const [sectionId, setSectionId] = useState<number | null>(null);
  const [sections, setSections] = useState<SectionDto[]>([]);
  const [sectionsLoading, setSectionsLoading] = useState(false);
  const [password, setPassword] = useState('');
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');

  const [editing, setEditing] = useState<UserListItem | null>(null);
  const [editFullName, setEditFullName] = useState('');
  const [editRole, setEditRole] = useState<Role>('lawyer');
  const [editBranchId, setEditBranchId] = useState<number | null>(null);
  const [editSectionId, setEditSectionId] = useState<number | null>(null);
  const [editSections, setEditSections] = useState<SectionDto[]>([]);
  const [editSectionsLoading, setEditSectionsLoading] = useState(false);
  const [editActive, setEditActive] = useState(true);
  const [editSuccessorId, setEditSuccessorId] = useState<number | null>(null);
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

  useEffect(() => {
    if (!showForm || !sectionRequired(role) || branchId === null) {
      setSections([]);
      return;
    }
    let cancelled = false;
    setSectionsLoading(true);
    api
      .get<SectionDto[]>(`/sections?branchId=${branchId}`)
      .then((r) => {
        if (!cancelled) setSections(r.data);
      })
      .catch(() => {
        if (!cancelled) setSections([]);
      })
      .finally(() => {
        if (!cancelled) setSectionsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [showForm, role, branchId]);

  useEffect(() => {
    if (!editing || !sectionRequired(editRole) || editBranchId === null) {
      setEditSections([]);
      return;
    }
    let cancelled = false;
    setEditSectionsLoading(true);
    api
      .get<SectionDto[]>(`/sections?branchId=${editBranchId}`)
      .then((r) => {
        if (!cancelled) setEditSections(r.data);
      })
      .catch(() => {
        if (!cancelled) setEditSections([]);
      })
      .finally(() => {
        if (!cancelled) setEditSectionsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [editing, editRole, editBranchId]);

  const resetForm = () => {
    setFullName('');
    setRole('lawyer');
    setBranchId(null);
    setSectionId(null);
    setSections([]);
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
    if (sectionRequired(role) && sectionId === null) {
      setFormError('الشعبة إلزامية لرئيس الشعبة');
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
        sectionId: sectionRequired(role) ? sectionId : null,
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
    setEditSectionId(u.sectionId ?? null);
    setEditSections([]);
    setEditActive(u.isActive);
    setEditSuccessorId(null);
    setEditPassword('');
    setEditError('');
  };

  const closeEdit = () => {
    setEditing(null);
    setEditPassword('');
    setEditSuccessorId(null);
    setEditError('');
  };

  const editDeactivating = Boolean(editing && editing.isActive && !editActive && isHeadRole(editRole));
  const successorCandidates = users.filter(
    (u) =>
      editing &&
      u.id !== editing.id &&
      u.isActive &&
      u.branchId !== null &&
      u.branchId === editBranchId &&
      (u.role === 'lawyer' || u.role === 'head' || u.role === 'subhead'),
  );

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
    if (sectionRequired(editRole) && editSectionId === null) {
      setEditError('الشعبة إلزامية لرئيس الشعبة');
      return;
    }
    if (editDeactivating && editSuccessorId === null) {
      setEditError('التعطيل يتطلب خلفًا إجباريًا — حدد الخلف');
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
        sectionId: sectionRequired(editRole) ? editSectionId : null,
        isActive: editActive,
        successorId: editDeactivating ? editSuccessorId : null,
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
          {loading ? 'جارِ التحميل...' : `${userCountFormatter.format(users.length)} مستخدم`}
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
              {selectableRoles.map((r) => (
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
          {sectionRequired(role) && (
            <div>
              <label htmlFor="user-section" className="block text-xs font-medium text-gray-600 mb-1">الشعبة (إلزامية لرئيس الشعبة)</label>
              <select
                id="user-section"
                value={sectionId ?? ''}
                onChange={(e) => setSectionId(e.target.value ? Number(e.target.value) : null)}
                disabled={branchId === null || sectionsLoading}
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 disabled:bg-gray-50 disabled:text-gray-400"
              >
                <option value="">
                  {branchId === null ? 'اختر الفرع أولًا...' : sectionsLoading ? 'جارِ تحميل الشعب...' : 'اختر الشعبة...'}
                </option>
                {sections.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}{s.headName ? ` — ${s.headName}` : ''}
                  </option>
                ))}
              </select>
            </div>
          )}
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
                <th className="px-4 py-3">الشعبة</th>
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
                  <td className="px-4 py-3 break-words">{u.sectionName ?? '—'}</td>
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
                    {isManager && u.role === 'admin' ? (
                      <span className="text-xs text-gray-400">مشرف</span>
                    ) : (
                      <button
                        onClick={() => openEdit(u)}
                        className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 text-xs min-h-11"
                      >
                        تعديل
                      </button>
                    )}
                  </td>
                </tr>
              ))}
              {!loading && users.length === 0 && (
                <tr>
                  <td colSpan={7} className="px-4 py-8 text-center text-gray-400">لا يوجد مستخدمون</td>
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
                  {u.sectionName ? <span className="text-gray-400"> · {u.sectionName}</span> : null}
                </div>
                {isManager && u.role === 'admin' ? (
                  <span className="mt-3 inline-block text-xs text-gray-400">مشرف</span>
                ) : (
                  <button
                    onClick={() => openEdit(u)}
                    className="mt-3 text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 text-xs min-h-11"
                  >
                    تعديل
                  </button>
                )}
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
                  {selectableRoles.map((r) => (
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
              {sectionRequired(editRole) && (
                <div>
                  <label htmlFor="edit-section" className="block text-xs font-medium text-gray-600 mb-1">الشعبة (إلزامية لرئيس الشعبة)</label>
                  <select
                    id="edit-section"
                    value={editSectionId ?? ''}
                    onChange={(e) => setEditSectionId(e.target.value ? Number(e.target.value) : null)}
                    disabled={editBranchId === null || editSectionsLoading}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 disabled:bg-gray-50 disabled:text-gray-400"
                  >
                    <option value="">
                      {editBranchId === null ? 'اختر الفرع أولًا...' : editSectionsLoading ? 'جارِ تحميل الشعب...' : 'اختر الشعبة...'}
                    </option>
                    {editSections.map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.name}{s.headName ? ` — ${s.headName}` : ''}
                      </option>
                    ))}
                  </select>
                </div>
              )}
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

            {editDeactivating && (
              <div className="mt-4">
                <label htmlFor="edit-successor" className="block text-xs font-medium text-gray-600 mb-1">
                  الخلف الإجباري (يحل محل الرئيس المعطّل في كل شيء)
                </label>
                <select
                  id="edit-successor"
                  value={editSuccessorId ?? ''}
                  onChange={(e) => setEditSuccessorId(e.target.value ? Number(e.target.value) : null)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  <option value="">اختر الخلف...</option>
                  {successorCandidates.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.fullName} — {ROLE_LABELS[c.role] ?? c.role}
                    </option>
                  ))}
                </select>
              </div>
            )}

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
