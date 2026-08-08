import { useEffect, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useIsMobile } from '../hooks/useMediaQuery';
import type { BranchDto } from '../types';

export default function BranchesManagement() {
  const isMobile = useIsMobile();

  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [address, setAddress] = useState('');
  const [phone, setPhone] = useState('');
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');

  const [editing, setEditing] = useState<BranchDto | null>(null);
  const [editName, setEditName] = useState('');
  const [editCode, setEditCode] = useState('');
  const [editAddress, setEditAddress] = useState('');
  const [editPhone, setEditPhone] = useState('');
  const [editActive, setEditActive] = useState(true);
  const [editSaving, setEditSaving] = useState(false);
  const [editError, setEditError] = useState('');
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const load = () => {
    setLoading(true);
    setError('');
    api
      .get<BranchDto[]>('/branches')
      .then((r) => setBranches(r.data))
      .catch((err) => setError(getApiErrorMessage(err)))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const resetForm = () => {
    setName('');
    setCode('');
    setAddress('');
    setPhone('');
    setFormError('');
    setShowForm(false);
  };

  const validate = (nameValue: string, codeValue: string): string => {
    if (!nameValue.trim()) return 'اسم الفرع مطلوب';
    if (!codeValue.trim()) return 'كود الفرع مطلوب';
    return '';
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const problem = validate(name, code);
    if (problem) {
      setFormError(problem);
      return;
    }

    setSaving(true);
    setFormError('');
    try {
      await api.post('/branches', {
        name: name.trim(),
        code: code.trim(),
        address: address.trim() || null,
        phone: phone.trim() || null,
      });
      resetForm();
      load();
    } catch (err) {
      setFormError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const openEdit = (b: BranchDto) => {
    setEditing(b);
    setEditName(b.name);
    setEditCode(b.code);
    setEditAddress(b.address ?? '');
    setEditPhone(b.phone ?? '');
    setEditActive(b.isActive ?? true);
    setEditError('');
    setConfirmingDelete(false);
  };

  const closeEdit = () => {
    setEditing(null);
    setEditError('');
    setConfirmingDelete(false);
  };

  const saveEdit = async () => {
    if (!editing) return;
    const problem = validate(editName, editCode);
    if (problem) {
      setEditError(problem);
      return;
    }

    setEditSaving(true);
    setEditError('');
    try {
      await api.put(`/branches/${editing.id}`, {
        name: editName.trim(),
        code: editCode.trim(),
        address: editAddress.trim() || null,
        phone: editPhone.trim() || null,
        isActive: editActive,
      });
      closeEdit();
      load();
    } catch (err) {
      setEditError(getApiErrorMessage(err));
    } finally {
      setEditSaving(false);
    }
  };

  const deleteBranch = async () => {
    if (!editing) return;
    setDeleting(true);
    setEditError('');
    try {
      await api.delete(`/branches/${editing.id}`);
      closeEdit();
      load();
    } catch (err) {
      setEditError(getApiErrorMessage(err));
      setConfirmingDelete(false);
    } finally {
      setDeleting(false);
    }
  };

  const statusBadge = (b: BranchDto) => (
    <span
      className={`inline-block rounded-full px-2 py-0.5 text-xs ${
        (b.isActive ?? true) ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-200 text-gray-600'
      }`}
    >
      {(b.isActive ?? true) ? 'مفعّل' : 'موقوف'}
    </span>
  );

  const usageLine = (b: BranchDto) =>
    `${b.userCount ?? 0} مستخدم · ${b.documentCount ?? 0} مستند`;

  return (
    <div className="max-w-5xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-6">إدارة الفروع</h2>

      <div className="bg-white rounded-xl shadow p-4 mb-4 flex flex-wrap items-center justify-between gap-3">
        <div className="text-sm text-gray-600">
          {loading ? 'جارِ التحميل...' : `${branches.length} فرع`}
        </div>
        <button
          onClick={() => setShowForm((v) => !v)}
          className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
        >
          {showForm ? 'إلغاء' : '+ إضافة فرع'}
        </button>
      </div>

      {showForm && (
        <form
          onSubmit={submit}
          className="bg-white rounded-xl shadow p-4 mb-4 grid sm:grid-cols-2 gap-4"
        >
          <div>
            <label htmlFor="branch-name" className="block text-xs font-medium text-gray-600 mb-1">اسم الفرع</label>
            <input
              id="branch-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="مثال: فرع دمشق..."
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="branch-code" className="block text-xs font-medium text-gray-600 mb-1">كود الفرع</label>
            <input
              id="branch-code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              placeholder="مثال: DAM..."
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="branch-address" className="block text-xs font-medium text-gray-600 mb-1">العنوان</label>
            <input
              id="branch-address"
              value={address}
              onChange={(e) => setAddress(e.target.value)}
              placeholder="اختياري..."
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="branch-phone" className="block text-xs font-medium text-gray-600 mb-1">الهاتف</label>
            <input
              id="branch-phone"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              placeholder="اختياري..."
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
              {saving ? 'جارِ الحفظ...' : 'إنشاء الفرع'}
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
                <th className="px-4 py-3">الكود</th>
                <th className="px-4 py-3">العنوان</th>
                <th className="px-4 py-3">الاستخدام</th>
                <th className="px-4 py-3">الحالة</th>
                <th className="px-4 py-3">إجراء</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {branches.map((b) => (
                <tr key={b.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-medium text-gray-800">{b.name}</td>
                  <td className="px-4 py-3">{b.code}</td>
                  <td className="px-4 py-3 text-gray-500">{b.address || '—'}</td>
                  <td className="px-4 py-3 text-gray-500">{usageLine(b)}</td>
                  <td className="px-4 py-3">{statusBadge(b)}</td>
                  <td className="px-4 py-3">
                    <button
                      onClick={() => openEdit(b)}
                      className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 text-xs min-h-11"
                    >
                      تعديل
                    </button>
                  </td>
                </tr>
              ))}
              {!loading && branches.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-gray-400">لا توجد فروع</td>
                </tr>
              )}
            </tbody>
          </table>
        )}

        {isMobile && (
          <div className="divide-y divide-gray-100">
            {!loading && branches.length === 0 && (
              <div className="px-4 py-8 text-center text-gray-400">لا توجد فروع</div>
            )}
            {branches.map((b) => (
              <div key={b.id} className="p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="font-bold text-gray-800">
                    {b.name} <span className="text-gray-400 font-normal">({b.code})</span>
                  </div>
                  {statusBadge(b)}
                </div>
                <div className="text-sm text-gray-600 mt-1">
                  {b.address || '—'} · {usageLine(b)}
                </div>
                <button
                  onClick={() => openEdit(b)}
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
          aria-label="تعديل فرع"
        >
          <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[85vh] overflow-y-auto p-5">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-bold text-gray-800">تعديل فرع</h3>
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
                <label htmlFor="edit-branch-name" className="block text-xs font-medium text-gray-600 mb-1">اسم الفرع</label>
                <input
                  id="edit-branch-name"
                  value={editName}
                  onChange={(e) => setEditName(e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <div>
                <label htmlFor="edit-branch-code" className="block text-xs font-medium text-gray-600 mb-1">كود الفرع</label>
                <input
                  id="edit-branch-code"
                  value={editCode}
                  onChange={(e) => setEditCode(e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <div>
                <label htmlFor="edit-branch-address" className="block text-xs font-medium text-gray-600 mb-1">العنوان</label>
                <input
                  id="edit-branch-address"
                  value={editAddress}
                  onChange={(e) => setEditAddress(e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <div>
                <label htmlFor="edit-branch-phone" className="block text-xs font-medium text-gray-600 mb-1">الهاتف</label>
                <input
                  id="edit-branch-phone"
                  value={editPhone}
                  onChange={(e) => setEditPhone(e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
            </div>

            <label className="mt-4 inline-flex items-center gap-2 text-sm cursor-pointer min-h-11">
              <input
                type="checkbox"
                checked={editActive}
                onChange={(e) => setEditActive(e.target.checked)}
              />
              الفرع مفعّل
            </label>

            {editError && <p className="text-red-600 text-sm mt-3">{editError}</p>}

            {confirmingDelete ? (
              <div className="mt-5 border border-red-200 bg-red-50 rounded-lg p-4">
                <p className="text-sm text-red-700 mb-3">هل أنت متأكد من حذف هذا الفرع نهائياً؟</p>
                <div className="flex flex-wrap gap-2">
                  <button
                    onClick={deleteBranch}
                    disabled={deleting}
                    className="bg-red-600 hover:bg-red-500 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                  >
                    {deleting ? 'جارِ الحذف...' : 'تأكيد الحذف'}
                  </button>
                  <button
                    onClick={() => setConfirmingDelete(false)}
                    className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
                  >
                    إلغاء
                  </button>
                </div>
              </div>
            ) : (
              <div className="mt-5 flex flex-wrap gap-2 justify-between">
                <button
                  onClick={saveEdit}
                  disabled={editSaving}
                  className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                >
                  {editSaving ? 'جارِ الحفظ...' : 'حفظ التعديل'}
                </button>
                <div className="flex gap-2">
                  <button
                    onClick={() => setConfirmingDelete(true)}
                    className="border border-red-200 text-red-600 hover:bg-red-50 rounded-lg px-4 py-2 text-sm min-h-11"
                  >
                    حذف الفرع
                  </button>
                  <button
                    onClick={closeEdit}
                    className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
                  >
                    إلغاء
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
