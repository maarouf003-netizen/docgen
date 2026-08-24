import { useCallback, useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import type { DelegateDto, PublicEntityEntryDto } from '../types';

interface ScopeDraft {
  mode: 'group' | 'entry';
  groupId: string;
  entryId: string;
}

const emptyScope: ScopeDraft = { mode: 'group', groupId: '', entryId: '' };

/** شاشة إدارة حسابات مندوبي الجهات وربط نطاقهم — مدير/مشرف/رئيس قسم (د11). */
export default function EntityDelegates() {
  const [delegates, setDelegates] = useState<DelegateDto[] | null>(null);
  const [entries, setEntries] = useState<PublicEntityEntryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [listError, setListError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setListError('');
    try {
      const [delegatesRes, entriesRes] = await Promise.all([
        api.get<DelegateDto[]>('/entity-portal/delegates'),
        api.get<{ items: PublicEntityEntryDto[] }>('/entity-registry/search', { params: { status: 'final', perPage: 100 } }),
      ]);
      setDelegates(Array.isArray(delegatesRes.data) ? delegatesRes.data : []);
      setEntries(entriesRes.data?.items ?? []);
    } catch (err) {
      setListError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const groups = new Map<number, string>();
  for (const e of entries) groups.set(e.groupId, e.canonicalName);

  // ── نموذج الإنشاء ──
  const [showForm, setShowForm] = useState(false);
  const [username, setUsername] = useState('');
  const [fullName, setFullName] = useState('');
  const [password, setPassword] = useState('');
  const [scope, setScope] = useState<ScopeDraft>({ ...emptyScope });
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');

  const resetForm = () => {
    setUsername(''); setFullName(''); setPassword('');
    setScope({ ...emptyScope }); setFormError(''); setShowForm(false);
  };

  const submitForm = async () => {
    if (!username.trim()) return setFormError('اسم الدخول مطلوب');
    if (!fullName.trim()) return setFormError('الاسم الكامل مطلوب');
    if (password.length < 6) return setFormError('كلمة المرور يجب أن تكون 6 أحرف على الأقل');
    if (scope.mode === 'group' && !scope.groupId) return setFormError('اختر هوية الجهة');
    if (scope.mode === 'entry' && !scope.entryId) return setFormError('اختر قيد الجهة');

    setSaving(true); setFormError('');
    try {
      await api.post('/entity-portal/delegates', {
        username: username.trim(),
        fullName: fullName.trim(),
        password,
        portalGroupId: scope.mode === 'group' ? Number(scope.groupId) : null,
        portalEntryId: scope.mode === 'entry' ? Number(scope.entryId) : null,
      });
      resetForm();
      await load();
    } catch (err) {
      setFormError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  // ── نافذة التعديل ──
  const [editing, setEditing] = useState<DelegateDto | null>(null);
  const [editName, setEditName] = useState('');
  const [editActive, setEditActive] = useState(true);
  const [editPassword, setEditPassword] = useState('');
  const [editScope, setEditScope] = useState<ScopeDraft>({ ...emptyScope });
  const [editSaving, setEditSaving] = useState(false);
  const [editError, setEditError] = useState('');

  const openEdit = (d: DelegateDto) => {
    setEditing(d);
    setEditName(d.fullName);
    setEditActive(d.isActive);
    setEditPassword('');
    setEditScope(d.portalGroupId != null
      ? { mode: 'group', groupId: String(d.portalGroupId), entryId: '' }
      : d.portalEntryId != null
        ? { mode: 'entry', groupId: '', entryId: String(d.portalEntryId) }
        : { ...emptyScope });
    setEditError('');
  };

  const saveEdit = async () => {
    if (!editing) return;
    if (!editName.trim()) return setEditError('الاسم الكامل مطلوب');
    if (editScope.mode === 'group' && !editScope.groupId) return setEditError('اختر هوية الجهة');
    if (editScope.mode === 'entry' && !editScope.entryId) return setEditError('اختر قيد الجهة');

    setEditSaving(true); setEditError('');
    try {
      await api.put(`/entity-portal/delegates/${editing.id}`, {
        fullName: editName.trim(),
        isActive: editActive,
        newPassword: editPassword || null,
        portalGroupId: editScope.mode === 'group' ? Number(editScope.groupId) : null,
        portalEntryId: editScope.mode === 'entry' ? Number(editScope.entryId) : null,
      });
      setEditing(null);
      await load();
    } catch (err) {
      setEditError(getApiErrorMessage(err));
    } finally {
      setEditSaving(false);
    }
  };

  const scopeLabel = (d: DelegateDto) =>
    d.portalGroupName ?? d.portalEntryLabel ?? '—';

  const renderScopePicker = (draft: ScopeDraft, setDraft: (s: ScopeDraft) => void, idPrefix: string, includeGroupOptions: boolean) => (
    <>
      <div className="sm:col-span-2 flex flex-wrap gap-4">
        <label className="inline-flex items-center gap-2 text-sm cursor-pointer min-h-11">
          <input type="radio" name={`${idPrefix}-mode`} checked={draft.mode === 'group'} onChange={() => setDraft({ ...draft, mode: 'group' })} />
          هوية أم (كل قيودها)
        </label>
        <label className="inline-flex items-center gap-2 text-sm cursor-pointer min-h-11">
          <input type="radio" name={`${idPrefix}-mode`} checked={draft.mode === 'entry'} onChange={() => setDraft({ ...draft, mode: 'entry' })} />
          قيد بعينه
        </label>
      </div>
      {draft.mode === 'group' ? (
        <div className="sm:col-span-2">
          <label htmlFor={`${idPrefix}-group`} className="block text-xs font-bold text-gray-600 mb-1">الهوية الأم</label>
          <select
            id={`${idPrefix}-group`}
            value={draft.groupId}
            onChange={(e) => setDraft({ ...draft, groupId: e.target.value })}
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
            <option value="">اختر الهوية…</option>
            {Array.from(groups.entries()).map(([gid, name]) => (
              <option key={gid} value={gid}>{name}</option>
            ))}
            {includeGroupOptions && draft.groupId && !groups.has(Number(draft.groupId)) && (
              <option value={draft.groupId}>{`هوية #${draft.groupId}`}</option>
            )}
          </select>
        </div>
      ) : (
        <div className="sm:col-span-2">
          <label htmlFor={`${idPrefix}-entry`} className="block text-xs font-bold text-gray-600 mb-1">قيد الجهة</label>
          <select
            id={`${idPrefix}-entry`}
            value={draft.entryId}
            onChange={(e) => setDraft({ ...draft, entryId: e.target.value })}
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
            <option value="">اختر القيد…</option>
            {entries.filter((e) => e.status === 'final').map((e) => (
              <option key={e.id} value={e.id}>{`${e.canonicalName} — ${e.governorate}/${e.branchName}`}</option>
            ))}
          </select>
        </div>
      )}
    </>
  );

  return (
    <div className="max-w-5xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-6">مندوبو الجهات</h2>

      <div className="bg-white rounded-xl shadow p-4 mb-4 flex flex-wrap items-center justify-between gap-3">
        <div className="text-sm text-gray-600">
          {loading ? 'جارِ التحميل…' : `${delegates?.length ?? 0} مندوبًا`}
        </div>
        <button
          onClick={() => setShowForm((v) => !v)}
          aria-expanded={showForm}
          className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
        >
          {showForm ? 'إلغاء' : '+ إضافة مندوب'}
        </button>
      </div>

      {listError && <div role="alert" className="text-red-600 mb-4">{listError}</div>}

      {showForm && (
        <form
          onSubmit={(e) => { e.preventDefault(); void submitForm(); }}
          className="bg-white rounded-xl shadow p-4 mb-4 grid sm:grid-cols-2 gap-4"
        >
          <div>
            <label htmlFor="dlg-username" className="block text-xs font-bold text-gray-600 mb-1">اسم الدخول</label>
            <input
              id="dlg-username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="مثال: delegate.ministry…"
              autoComplete="off"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="dlg-name" className="block text-xs font-bold text-gray-600 mb-1">الاسم الكامل</label>
            <input
              id="dlg-name"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              placeholder="مثال: أحمد الخطيب…"
              autoComplete="off"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div className="sm:col-span-2">
            <label htmlFor="dlg-password" className="block text-xs font-bold text-gray-600 mb-1">كلمة المرور</label>
            <input
              id="dlg-password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="6 أحرف على الأقل…"
              autoComplete="new-password"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          {renderScopePicker(scope, setScope, 'new', false)}
          {formError && <p role="alert" className="text-red-600 text-sm sm:col-span-2">{formError}</p>}
          <div className="sm:col-span-2">
            <button
              type="submit"
              disabled={saving}
              className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              {saving ? 'جارِ الحفظ…' : 'إنشاء المندوب'}
            </button>
          </div>
        </form>
      )}

      <div className="bg-white rounded-xl shadow overflow-hidden divide-y divide-gray-100">
        {!loading && delegates !== null && delegates.length === 0 && (
          <div className="px-4 py-8 text-center text-gray-400">لا يوجد مندوبون بعد</div>
        )}
        {(delegates ?? []).map((d) => (
          <div key={d.id} className="p-4 flex flex-wrap items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="font-medium text-gray-800 break-words">
                {d.fullName} <span className="text-gray-400 font-normal text-xs">({d.username})</span>
              </div>
              <div className="text-sm text-gray-600 mt-0.5">النطاق: {scopeLabel(d)}</div>
            </div>
            <div className="flex items-center gap-2">
              <span className={`rounded-full px-2 py-0.5 text-xs ${d.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-200 text-gray-600'}`}>
                {d.isActive ? 'مفعّل' : 'موقوف'}
              </span>
              <button
                onClick={() => openEdit(d)}
                className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 text-xs min-h-11"
              >
                تعديل
              </button>
            </div>
          </div>
        ))}
      </div>

      {editing && (
        <div
          className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
          dir="rtl"
          role="dialog"
          aria-modal="true"
          aria-label={`تعديل مندوب: ${editing.username}`}
          style={{ overscrollBehavior: 'contain' }}
        >
          <div className="bg-white rounded-xl shadow-xl w-full max-w-md max-h-[85vh] overflow-y-auto overscroll-contain p-5">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-bold text-gray-800">تعديل مندوب</h3>
              <button
                onClick={() => setEditing(null)}
                className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
                aria-label="إغلاق"
              >
                ×
              </button>
            </div>

            <div className="grid sm:grid-cols-2 gap-4">
              <div className="sm:col-span-2">
                <label htmlFor="ed-dlg-name" className="block text-xs font-bold text-gray-600 mb-1">الاسم الكامل</label>
                <input
                  id="ed-dlg-name"
                  value={editName}
                  onChange={(e) => setEditName(e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <div className="sm:col-span-2">
                <label htmlFor="ed-dlg-password" className="block text-xs font-bold text-gray-600 mb-1">كلمة مرور جديدة (اختياري)</label>
                <input
                  id="ed-dlg-password"
                  type="password"
                  value={editPassword}
                  onChange={(e) => setEditPassword(e.target.value)}
                  placeholder="اتركها فارغة للإبقاء على الحالية…"
                  autoComplete="new-password"
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              {renderScopePicker(editScope, setEditScope, 'edit', true)}
              <label className="sm:col-span-2 inline-flex items-center gap-2 text-sm cursor-pointer min-h-11">
                <input type="checkbox" checked={editActive} onChange={(e) => setEditActive(e.target.checked)} />
                الحساب مفعّل
              </label>
            </div>

            {editError && <p role="alert" className="text-red-600 text-sm mt-3">{editError}</p>}

            <div className="mt-5 flex flex-wrap gap-2 justify-end">
              <button
                onClick={saveEdit}
                disabled={editSaving}
                className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {editSaving ? 'جارِ الحفظ…' : 'حفظ التعديل'}
              </button>
              <button
                onClick={() => setEditing(null)}
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
