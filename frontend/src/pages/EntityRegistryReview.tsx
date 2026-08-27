import { useCallback, useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { GOVERNORATES } from '../utils/governorate';
import {
  CITATION_FORMULA_OPTIONS,
  ENTITY_TYPE_OPTIONS,
  entityTypeLabel,
  formatEntityCoverage,
} from '../utils/entityRegistry';
import type { PublicEntityEntryDto } from '../types';

/**
 * «مراجعة سجل الجهات العامة الممثلة» — نموذج الحوكمة الجديد:
 * ما يُدخله المحامي يُعتمد نهائيًا فورًا لكنه يبقى هنا بانتظار المراجعة؛
 * رئيس القسم يرى محافظته حصرًا (اعتماد / تعديل تسمية يبلّغ المُدخِل)،
 * والمدير/المشرف يرىان كل السجل ويمكنهما إدخال جهات مسبقة لأي محافظة.
 */
export default function EntityRegistryReview() {
  const { user } = useAuth();
  const isHead = user?.role === 'head';

  const [items, setItems] = useState<PublicEntityEntryDto[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [listError, setListError] = useState('');
  const [busyId, setBusyId] = useState<number | null>(null);
  const [successMsg, setSuccessMsg] = useState('');

  // نافذة التعديل (تعديل التسمية أثناء المراجعة يوجّه إشعارًا للمُدخِل تلقائيًا).
  const [editing, setEditing] = useState<PublicEntityEntryDto | null>(null);
  const [editName, setEditName] = useState('');
  const [editType, setEditType] = useState<PublicEntityEntryDto['entityType']>('ministry');
  const [editGovernorate, setEditGovernorate] = useState('');
  const [editBranch, setEditBranch] = useState('');
  const [savingEdit, setSavingEdit] = useState(false);
  const [editError, setEditError] = useState('');

  // نموذج الإدخال المسبق.
  const [showCreate, setShowCreate] = useState(false);
  const [newName, setNewName] = useState('');
  const [newType, setNewType] = useState<PublicEntityEntryDto['entityType']>('ministry');
  const [newCitation, setNewCitation] = useState<'add-to-job' | 'add-to-position'>('add-to-job');
  const [newGov, setNewGov] = useState('');
  const [newBranch, setNewBranch] = useState('الفرع الرئيسي');
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setListError('');
    try {
      const res = await api.get<PublicEntityEntryDto[]>('/entity-registry/pending-review');
      setItems(Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      setListError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const approve = async (entry: PublicEntityEntryDto) => {
    setBusyId(entry.id);
    setSuccessMsg('');
    try {
      await api.post(`/entity-registry/${entry.id}/approve-review`);
      setSuccessMsg(`تم اعتماد «${entry.canonicalName}» دون تعديل`);
      await load();
    } catch (err) {
      setSuccessMsg('');
      setListError(getApiErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  };

  const openEdit = (entry: PublicEntityEntryDto) => {
    setEditing(entry);
    setEditName(entry.canonicalName);
    setEditType(entry.entityType);
    setEditGovernorate(entry.governorate);
    setEditBranch(entry.branchName);
    setEditError('');
  };

  const saveEdit = async () => {
    if (!editing) return;
    if (!editName.trim()) {
      setEditError('اسم الجهة مطلوب');
      return;
    }
    if (!editGovernorate.trim()) {
      setEditError('المحافظة مطلوبة');
      return;
    }

    setSavingEdit(true);
    setEditError('');
    try {
      await api.put(`/entity-registry/${editing.id}`, {
        canonicalName: editName.trim(),
        entityType: editType,
        governorate: editGovernorate.trim(),
        branchName: editBranch.trim(),
      });
      const renamed = editName.trim() !== editing.canonicalName;
      setEditing(null);
      setSuccessMsg(renamed
        ? `تم تعديل اسم الجهة من «${editing.canonicalName}» إلى «${editName.trim()}» وأُرسل تنبيه للمحامي المُدخِل`
        : 'تم حفظ التعديل وإقفال مراجعة القيد');
      await load();
    } catch (err) {
      setEditError(getApiErrorMessage(err));
    } finally {
      setSavingEdit(false);
    }
  };

  const submitCreate = async () => {
    if (!newName.trim()) {
      setCreateError('اسم الجهة مطلوب');
      return;
    }
    if (!newGov) {
      setCreateError('المحافظة مطلوبة');
      return;
    }

    setCreating(true); setCreateError('');
    try {
      await api.post('/entity-registry', {
        canonicalName: newName.trim(),
        entityType: newType,
        governorate: newGov,
        branchName: newBranch.trim(),
        citationFormula: newCitation,
      });
      setShowCreate(false);
      setSuccessMsg(`أُضيفت «${newName.trim()}» إلى السجل كجهة متاحة للمحامين`);
      setNewName('');
      await load();
    } catch (err) {
      setCreateError(getApiErrorMessage(err));
    } finally {
      setCreating(false);
    }
  };

  return (
    <div className="max-w-4xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-2">مراجعة سجل الجهات العامة الممثلة</h2>
      <p className="text-sm text-gray-500 mb-4">
        {isHead
          ? 'تراجع هنا الجهات التي أدخلها المحامون في محافظة فرعك. اعتمادها لا يبلّغ أحدًا، وتعديل تسميتها يوجّه تنبيهًا للمُدخِل بالاسمين.'
          : 'تشمل مراجعتك كل المحافظات، ويمكنك إدخال جهات مسبقة لأي محافظة.'}
      </p>

      {successMsg && (
        <p role="status" className="mb-4 bg-emerald-50 border border-emerald-100 text-emerald-800 rounded-lg p-3 text-sm">
          {successMsg}
        </p>
      )}
      {listError && (
        <div role="alert" className="mb-4 bg-red-50 border border-red-100 rounded-lg p-3 text-sm text-red-700 flex items-center justify-between gap-3">
          <span>{listError}</span>
          <button onClick={() => void load()} className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 min-h-11">
            إعادة المحاولة
          </button>
        </div>
      )}

      {/* إدخال جهة مسبقة */}
      {!showCreate && (
        <button
          onClick={() => setShowCreate(true)}
          aria-expanded={showCreate}
          className="mb-4 bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
        >
          + إدخال جهة عامة مسبقًا
        </button>
      )}
      {showCreate && (
        <form
          onSubmit={(e) => { e.preventDefault(); void submitCreate(); }}
          className="bg-white rounded-xl shadow p-4 mb-4 grid sm:grid-cols-2 gap-4"
        >
          <div className="sm:col-span-2">
            <label htmlFor="rev-name" className="block text-xs font-bold text-gray-600 mb-1">اسم الجهة المعتمد</label>
            <input
              id="rev-name"
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              placeholder="مثال: المدير العام للمصرف التجاري السوري…"
              autoComplete="off"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="rev-type" className="block text-xs font-bold text-gray-600 mb-1">نوع الجهة</label>
            <select id="rev-type" value={newType} onChange={(e) => setNewType(e.target.value as PublicEntityEntryDto['entityType'])}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500">
              {ENTITY_TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>
          <div>
            <label htmlFor="rev-citation" className="block text-xs font-bold text-gray-600 mb-1">صيغة ممثلها القانوني</label>
            <select id="rev-citation" value={newCitation} onChange={(e) => setNewCitation(e.target.value as typeof newCitation)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500">
              {CITATION_FORMULA_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>
          <div>
            <label htmlFor="rev-gov" className="block text-xs font-bold text-gray-600 mb-1">المحافظة</label>
            <select id="rev-gov" value={newGov} onChange={(e) => setNewGov(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500">
              <option value="">اختر المحافظة…</option>
              {GOVERNORATES.map((g) => <option key={g} value={g}>{g}</option>)}
            </select>
          </div>
          <div>
            <label htmlFor="rev-branch" className="block text-xs font-bold text-gray-600 mb-1">الفرع</label>
            <input id="rev-branch" value={newBranch} onChange={(e) => setNewBranch(e.target.value)} autoComplete="off"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
          </div>
          {createError && <p role="alert" className="text-red-600 text-sm sm:col-span-2">{createError}</p>}
          <div className="sm:col-span-2 flex gap-2">
            <button type="submit" disabled={creating}
              className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11">
              {creating ? 'جارِ الحفظ…' : 'إضافة إلى السجل'}
            </button>
            <button type="button" onClick={() => setShowCreate(false)}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11">
              إلغاء
            </button>
          </div>
        </form>
      )}

      {/* قائمة المراجعة */}
      <ul className="space-y-3">
        {loading && <li className="bg-white rounded-xl shadow p-5 text-gray-500 text-sm">جارِ التحميل…</li>}
        {!loading && items !== null && items.length === 0 && (
          <li className="bg-white rounded-xl shadow p-10 text-center text-gray-400 text-sm">
            لا توجد جهات بانتظار المراجعة
          </li>
        )}
        {(items ?? []).map((entry) => (
          <li key={entry.id} className="bg-white rounded-xl shadow p-5">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0">
                <h3 className="font-bold text-gray-800 break-words">{entry.canonicalName}</h3>
                <p className="text-sm text-gray-600 mt-1">
                  {entityTypeLabel(entry.entityType)} · {formatEntityCoverage(entry)} / {entry.branchName}
                </p>
                <p className="text-xs text-gray-400 mt-0.5">
                  أدخلها: {entry.createdByName || 'محامٍ'}
                </p>
              </div>
              <span className="inline-block rounded-full bg-amber-100 text-amber-800 px-2 py-0.5 text-xs whitespace-nowrap">
                بانتظار المراجعة
              </span>
            </div>

            <div className="mt-4 flex flex-wrap gap-2">
              <button
                onClick={() => openEdit(entry)}
                disabled={busyId === entry.id}
                className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                تعديل التسمية/البيانات…
              </button>
              <button
                onClick={() => void approve(entry)}
                disabled={busyId === entry.id}
                className="border border-emerald-200 text-emerald-800 hover:bg-emerald-50 disabled:opacity-50 rounded-lg px-4 py-2 text-sm min-h-11"
              >
                اعتماد كما هي
              </button>
            </div>
          </li>
        ))}
      </ul>

      {editing && (
        <div
          className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
          dir="rtl"
          role="dialog"
          aria-modal="true"
          aria-label={`تعديل جهة: ${editing.canonicalName}`}
          style={{ overscrollBehavior: 'contain' }}
        >
          <div className="bg-white rounded-xl shadow-xl w-full max-w-md max-h-[85vh] overflow-y-auto overscroll-contain p-5">
            <div className="flex items-center justify-between mb-3">
              <h3 className="text-lg font-bold text-gray-800">تعديل جهة قيد المراجعة</h3>
              <button onClick={() => setEditing(null)} className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11" aria-label="إغلاق">×</button>
            </div>

            <p className="text-xs font-medium text-red-700 bg-red-50 border border-red-100 rounded-lg p-3 leading-relaxed mb-4">
              تعديل الاسم يعيد تسمية الجهة في كل الملفات المرتبطة فورًا، ويُرسل تنبيهًا للمحامي
              المُدخِل بالاسم القديم والجديد.
            </p>

            <div className="space-y-4">
              <div>
                <label htmlFor="rv-ed-name" className="block text-xs font-bold text-gray-600 mb-1">اسم الجهة المعتمد</label>
                <input id="rv-ed-name" value={editName} onChange={(e) => setEditName(e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
              </div>
              <div className="grid sm:grid-cols-2 gap-3">
                <div>
                  <label htmlFor="rv-ed-type" className="block text-xs font-bold text-gray-600 mb-1">نوع الجهة</label>
                  <select id="rv-ed-type" value={editType} onChange={(e) => setEditType(e.target.value as PublicEntityEntryDto['entityType'])}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500">
                    {ENTITY_TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                  </select>
                </div>
                <div>
                  <label htmlFor="rv-ed-gov" className="block text-xs font-bold text-gray-600 mb-1">المحافظة</label>
                  <select id="rv-ed-gov" value={editGovernorate} onChange={(e) => setEditGovernorate(e.target.value)}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500">
                    <option value="">اختر…</option>
                    {!GOVERNORATES.includes(editGovernorate) && editGovernorate !== '' && (
                      <option value={editGovernorate}>{editGovernorate}</option>
                    )}
                    {GOVERNORATES.map((g) => <option key={g} value={g}>{g}</option>)}
                  </select>
                </div>
              </div>
              <div>
                <label htmlFor="rv-ed-branch" className="block text-xs font-bold text-gray-600 mb-1">الفرع</label>
                <input id="rv-ed-branch" value={editBranch} onChange={(e) => setEditBranch(e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
              </div>
            </div>

            {editError && <p role="alert" className="text-red-600 text-sm mt-3">{editError}</p>}

            <div className="mt-5 flex flex-wrap gap-2 justify-end">
              <button onClick={saveEdit} disabled={savingEdit}
                className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11">
                {savingEdit ? 'جارِ الحفظ…' : 'حفظ التعديل وإقفال المراجعة'}
              </button>
              <button onClick={() => setEditing(null)}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11">
                إلغاء
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
