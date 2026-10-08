import { useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useIsMobile } from '../hooks/useMediaQuery';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { GOVERNORATES } from '../utils/governorate';
import type { BranchDto, CircuitStatsDto, HeadSuccessionDto, SectionDto } from '../types';

type SubTab = 'sections' | 'transfer' | 'succession';

const SUCCESSION_EVENT_LABELS: Record<string, string> = {
  appointed: 'تعيين',
  deactivated: 'تعطيل',
  succeeded: 'إحلال خلف',
  'circuit-transferred': 'نقل دائرة',
  renamed: 'إعادة تسمية',
};

export default function BranchesManagement() {
  const isMobile = useIsMobile();

  const branchesQuery = useCancellableRequest<BranchDto[]>(
    (signal) => api.get('/branches', { signal }).then((r) => r.data),
    [],
  );
  const branches = branchesQuery.data ?? [];
  const loading = branchesQuery.isLoading;
  const fetchError = branchesQuery.error;
  const load = branchesQuery.refetch;

  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [address, setAddress] = useState('');
  const [phone, setPhone] = useState('');
  const [governorate, setGovernorate] = useState('');
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');

  const [editing, setEditing] = useState<BranchDto | null>(null);
  const [editName, setEditName] = useState('');
  const [editCode, setEditCode] = useState('');
  const [editAddress, setEditAddress] = useState('');
  const [editPhone, setEditPhone] = useState('');
  const [editGovernorate, setEditGovernorate] = useState('');
  const [editActive, setEditActive] = useState(true);
  const [editSaving, setEditSaving] = useState(false);
  const [editError, setEditError] = useState('');
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);

  // ── الشعب والنقل والتعاقب (§8 — مدير/مشرف) ──────────────────────────
  const [subBranchId, setSubBranchId] = useState<number | ''>('');
  const [subTab, setSubTab] = useState<SubTab>('sections');
  const [sections, setSections] = useState<SectionDto[]>([]);
  const [sectionsLoading, setSectionsLoading] = useState(false);
  const [sectionsError, setSectionsError] = useState('');
  const [newSectionName, setNewSectionName] = useState('');
  const [sectionBusy, setSectionBusy] = useState(false);
  const [renaming, setRenaming] = useState<SectionDto | null>(null);
  const [renameName, setRenameName] = useState('');
  const [circuits, setCircuits] = useState<CircuitStatsDto[]>([]);
  const [circuitsLoading, setCircuitsLoading] = useState(false);
  const [circuitsError, setCircuitsError] = useState('');
  const [transferCircuitId, setTransferCircuitId] = useState<number | ''>('');
  const [transferTarget, setTransferTarget] = useState<number | ''>('');
  const [transferBusy, setTransferBusy] = useState(false);
  const [transferMsg, setTransferMsg] = useState('');
  const [succession, setSuccession] = useState<HeadSuccessionDto[]>([]);
  const [successionLoading, setSuccessionLoading] = useState(false);
  const [successionError, setSuccessionError] = useState('');

  const loadSections = async (branchId: number) => {
    setSectionsLoading(true);
    setSectionsError('');
    try {
      const r = await api.get<SectionDto[]>('/sections', { params: { branchId } });
      setSections(r.data ?? []);
    } catch (err) {
      setSectionsError(getApiErrorMessage(err));
      setSections([]);
    } finally {
      setSectionsLoading(false);
    }
  };

  const loadCircuits = async (branchId: number) => {
    setCircuitsLoading(true);
    setCircuitsError('');
    try {
      const r = await api.get<CircuitStatsDto[]>('/execution-circuits/stats', { params: { branchId } });
      setCircuits(r.data ?? []);
    } catch (err) {
      setCircuitsError(getApiErrorMessage(err));
      setCircuits([]);
    } finally {
      setCircuitsLoading(false);
    }
  };

  const loadSuccession = async (branchId: number) => {
    setSuccessionLoading(true);
    setSuccessionError('');
    try {
      const r = await api.get<HeadSuccessionDto[]>('/head-succession', { params: { branchId } });
      setSuccession(r.data ?? []);
    } catch (err) {
      setSuccessionError(getApiErrorMessage(err));
      setSuccession([]);
    } finally {
      setSuccessionLoading(false);
    }
  };

  const pickSubBranch = (id: number | '') => {
    setSubBranchId(id);
    setTransferMsg('');
    if (id === '') {
      setSections([]);
      setCircuits([]);
      setSuccession([]);
      return;
    }
    void loadSections(id);
    void loadCircuits(id);
    void loadSuccession(id);
  };

  const createSection = async () => {
    if (subBranchId === '') {
      setSectionsError('اختر الفرع الأب أولًا');
      return;
    }
    const nm = newSectionName.trim();
    if (!nm) {
      setSectionsError('اسم الشعبة مطلوب');
      return;
    }
    setSectionBusy(true);
    setSectionsError('');
    try {
      await api.post('/sections', { branchId: subBranchId, name: nm });
      setNewSectionName('');
      await loadSections(subBranchId);
    } catch (err) {
      setSectionsError(getApiErrorMessage(err));
    } finally {
      setSectionBusy(false);
    }
  };

  const saveRename = async () => {
    if (!renaming) return;
    const nm = renameName.trim();
    if (!nm) {
      setSectionsError('اسم الشعبة مطلوب');
      return;
    }
    setSectionBusy(true);
    try {
      await api.put(`/sections/${renaming.id}/rename`, { name: nm });
      setRenaming(null);
      if (subBranchId !== '') await loadSections(subBranchId);
    } catch (err) {
      setSectionsError(getApiErrorMessage(err));
    } finally {
      setSectionBusy(false);
    }
  };

  const setSectionActive = async (s: SectionDto, active: boolean) => {
    setSectionBusy(true);
    try {
      await api.put(`/sections/${s.id}/active`, { isActive: active });
      if (subBranchId !== '') await loadSections(subBranchId);
    } catch (err) {
      setSectionsError(getApiErrorMessage(err));
    } finally {
      setSectionBusy(false);
    }
  };

  const deleteSection = async (s: SectionDto) => {
    if (s.circuitCount > 0) {
      setSectionsError('لا يمكن حذف الشعبة — انقل دوائرها أولًا');
      return;
    }
    if (!window.confirm(`حذف شعبة «${s.name}» نهائيًا؟`)) return;
    setSectionBusy(true);
    try {
      await api.delete(`/sections/${s.id}`);
      if (subBranchId !== '') {
        await loadSections(subBranchId);
        await loadCircuits(subBranchId);
      }
    } catch (err) {
      setSectionsError(getApiErrorMessage(err));
    } finally {
      setSectionBusy(false);
    }
  };

  const submitTransfer = async () => {
    if (transferCircuitId === '') {
      setTransferMsg('اختر الدائرة المراد نقلها');
      return;
    }
    setTransferBusy(true);
    setTransferMsg('');
    try {
      const circuit = circuits.find((c) => c.circuitId === transferCircuitId);
      await api.post(`/execution-circuits/${transferCircuitId}/transfer`, {
        targetSectionId: transferTarget === '' ? null : transferTarget,
        version: circuit?.version ?? null,
      });
      setTransferMsg('تم نقل الدائرة — المفتوح والمغلق يتبع المالك الجديد');
      setTransferCircuitId('');
      setTransferTarget('');
      if (subBranchId !== '') {
        await loadCircuits(subBranchId);
        await loadSections(subBranchId);
      }
    } catch (err) {
      setTransferMsg(getApiErrorMessage(err));
    } finally {
      setTransferBusy(false);
    }
  };



  const resetForm = () => {
    setName('');
    setCode('');
    setAddress('');
    setPhone('');
    setGovernorate('');
    setFormError('');
    setShowForm(false);
  };

  const validate = (nameValue: string, codeValue: string, governorateValue: string): string => {
    if (!nameValue.trim()) return 'اسم الفرع مطلوب';
    if (!codeValue.trim()) return 'كود الفرع مطلوب';
    if (!governorateValue) return 'المحافظة مطلوبة — اختر محافظة الفرع من القائمة';
    return '';
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const problem = validate(name, code, governorate);
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
        governorate: governorate.trim(),
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
    setEditGovernorate(b.governorate ?? '');
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
    const problem = validate(editName, editCode, editGovernorate);
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
        governorate: editGovernorate.trim(),
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
          noValidate
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
          <div>
            <label htmlFor="branch-governorate" className="block text-xs font-medium text-gray-600 mb-1">المحافظة <span aria-hidden="true" className="text-red-500">*</span></label>
            <select
              id="branch-governorate"
              name="governorate"
              required
              value={governorate}
              onChange={(e) => setGovernorate(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              <option value="">اختر المحافظة…</option>
              {GOVERNORATES.map((g) => <option key={g} value={g}>{g}</option>)}
            </select>
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

      {fetchError && <div className="text-red-600 mb-4">{fetchError}</div>}

      <div className="bg-white rounded-xl shadow overflow-hidden">
        {!isMobile && (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-gray-600">
              <tr className="text-right">
                <th className="px-4 py-3">الاسم</th>
                <th className="px-4 py-3">الكود</th>
                <th className="px-4 py-3">المحافظة</th>
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
                  <td className="px-4 py-3 text-gray-500">{b.governorate || '—'}</td>
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
                  <td colSpan={7} className="px-4 py-8 text-center text-gray-400">لا توجد فروع</td>
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
                  {b.governorate ? `${b.governorate} · ` : ''}{b.address || '—'} · {usageLine(b)}
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

      {/* ── الشعب ونقل الدوائر وسجل التعاقب (§8 — مدير/مشرف) ── */}
      <section aria-label="الشعب ونقل الدوائر وسجل التعاقب" className="bg-white rounded-xl shadow p-4 mt-4">
        <h3 className="text-lg font-bold text-gray-800 mb-3">الشعب ونقل الدوائر وسجل التعاقب</h3>
        <div className="mb-3">
          <label htmlFor="sub-branch" className="block text-xs font-medium text-gray-600 mb-1">الفرع الأب</label>
          <select
            id="sub-branch"
            value={subBranchId}
            onChange={(e) => pickSubBranch(e.target.value ? Number(e.target.value) : '')}
            className="w-full sm:w-72 min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            <option value="">اختر الفرع...</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>{b.name}</option>
            ))}
          </select>
        </div>

        {subBranchId !== '' && (
          <>
            <div className="flex flex-wrap gap-2 mb-4" role="tablist" aria-label="أقسام إدارة الشعب">
              {(['sections', 'transfer', 'succession'] as SubTab[]).map((t) => (
                <button
                  key={t}
                  type="button"
                  role="tab"
                  aria-selected={subTab === t}
                  onClick={() => setSubTab(t)}
                  className={`min-h-11 rounded-lg px-4 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 ${
                    subTab === t ? 'bg-emerald-800 text-white' : 'border border-gray-300 text-gray-700 hover:bg-gray-50'
                  }`}
                >
                  {t === 'sections' ? 'الشعب' : t === 'transfer' ? 'نقل دائرة' : 'سجل التعاقب'}
                </button>
              ))}
            </div>

            {subTab === 'sections' && (
              <div role="tabpanel">
                <div className="flex flex-col sm:flex-row gap-2 sm:items-end mb-3">
                  <div className="flex-1">
                    <label htmlFor="new-section-name" className="block text-xs font-medium text-gray-600 mb-1">اسم الشعبة الجديدة</label>
                    <input
                      id="new-section-name"
                      value={newSectionName}
                      onChange={(e) => setNewSectionName(e.target.value)}
                      placeholder="مثال: شعبة مصياف…"
                      autoComplete="off"
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                    />
                  </div>
                  <button
                    type="button"
                    onClick={createSection}
                    disabled={sectionBusy}
                    className="min-h-11 bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm"
                  >
                    {sectionBusy ? 'جارِ الحفظ...' : '+ إضافة شعبة'}
                  </button>
                </div>
                {sectionsError && <p role="alert" className="text-red-600 text-sm mb-3">{sectionsError}</p>}
                {sectionsLoading ? (
                  <p className="text-gray-500 py-4 text-center">جارِ تحميل الشعب...</p>
                ) : sections.length === 0 ? (
                  <p className="text-gray-500 py-4 text-center">لا توجد شعب — أضف أول شعبة لهذا الفرع</p>
                ) : (
                  <ul className="divide-y divide-gray-100">
                    {sections.map((s) => (
                      <li key={s.id} className="py-3 flex flex-wrap items-center justify-between gap-2">
                        <div className="min-w-0">
                          <span className="font-bold text-gray-800 break-words">{s.name}</span>
                          <span className="text-xs text-gray-500 ms-2 tabular-nums">
                            {s.circuitCount} دائرة{s.headName ? ` · الرئيس: ${s.headName}` : ' · بلا رئيس'}
                          </span>
                          {!s.isActive && (
                            <span className="ms-2 rounded-full bg-gray-100 text-gray-600 px-2 py-0.5 text-[11px]">معطلة</span>
                          )}
                        </div>
                        <div className="flex flex-wrap gap-2">
                          <button
                            type="button"
                            onClick={() => { setRenaming(s); setRenameName(s.name); }}
                            aria-label={`إعادة تسمية ${s.name}`}
                            className="border border-gray-300 rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                          >
                            تسمية
                          </button>
                          <button
                            type="button"
                            onClick={() => setSectionActive(s, !s.isActive)}
                            disabled={sectionBusy}
                            aria-label={`${s.isActive ? 'تعطيل' : 'تفعيل'} ${s.name}`}
                            className="border border-gray-300 rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                          >
                            {s.isActive ? 'تعطيل' : 'تفعيل'}
                          </button>
                          <button
                            type="button"
                            onClick={() => deleteSection(s)}
                            disabled={sectionBusy}
                            aria-label={`حذف ${s.name}`}
                            className="border border-red-200 text-red-700 rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-red-500"
                          >
                            حذف
                          </button>
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
                {renaming && (
                  <div className="mt-3 border border-gray-200 rounded-lg p-3 flex flex-col sm:flex-row gap-2 sm:items-end">
                    <div className="flex-1">
                      <label htmlFor="rename-section-name" className="block text-xs font-medium text-gray-600 mb-1">
                        الاسم الجديد لشعبة «{renaming.name}»
                      </label>
                      <input
                        id="rename-section-name"
                        value={renameName}
                        onChange={(e) => setRenameName(e.target.value)}
                        autoComplete="off"
                        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                      />
                    </div>
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={saveRename}
                        disabled={sectionBusy}
                        className="min-h-11 bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm"
                      >
                        حفظ
                      </button>
                      <button
                        type="button"
                        onClick={() => setRenaming(null)}
                        className="min-h-11 border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
                      >
                        إلغاء
                      </button>
                    </div>
                  </div>
                )}
              </div>
            )}

            {subTab === 'transfer' && (
              <div role="tabpanel">
                <p className="text-xs text-gray-500 mb-3">
                  النقل داخل الفرع نفسه — المفتوح ينتقل والمغلق تبقى رؤيته للمالك الجديد، والإحالات المفتوحة تُعاد توجيهها تلقائيًا.
                </p>
                {circuitsError && <p role="alert" className="text-red-600 text-sm mb-3">{circuitsError}</p>}
                <div className="grid sm:grid-cols-2 gap-3 mb-3">
                  <div>
                    <label htmlFor="transfer-circuit" className="block text-xs font-medium text-gray-600 mb-1">الدائرة</label>
                    <select
                      id="transfer-circuit"
                      value={transferCircuitId}
                      onChange={(e) => setTransferCircuitId(e.target.value ? Number(e.target.value) : '')}
                      disabled={circuitsLoading}
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                    >
                      <option value="">{circuitsLoading ? 'جارِ التحميل...' : 'اختر الدائرة...'}</option>
                      {circuits.map((c) => (
                        <option key={c.circuitId} value={c.circuitId}>
                          {c.circuitName} — {c.sectionName ?? 'القسم'}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label htmlFor="transfer-target" className="block text-xs font-medium text-gray-600 mb-1">المالك الجديد</label>
                    <select
                      id="transfer-target"
                      value={transferTarget}
                      onChange={(e) => setTransferTarget(e.target.value ? Number(e.target.value) : '')}
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                    >
                      <option value="">قسم الفرع</option>
                      {sections.map((s) => (
                        <option key={s.id} value={s.id}>شعبة {s.name}</option>
                      ))}
                    </select>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={submitTransfer}
                  disabled={transferBusy || transferCircuitId === ''}
                  className="min-h-11 bg-amber-600 hover:bg-amber-500 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm"
                >
                  {transferBusy ? 'جارِ النقل...' : 'نقل الدائرة'}
                </button>
                {transferMsg && <p role="status" className="text-sm text-gray-700 mt-3">{transferMsg}</p>}
              </div>
            )}

            {subTab === 'succession' && (
              <div role="tabpanel">
                {successionError && <p role="alert" className="text-red-600 text-sm mb-3">{successionError}</p>}
                {successionLoading ? (
                  <p className="text-gray-500 py-4 text-center">جارِ تحميل السجل...</p>
                ) : succession.length === 0 ? (
                  <p className="text-gray-500 py-4 text-center">لا توجد أحداث تعاقب لهذا الفرع</p>
                ) : (
                  <ul className="divide-y divide-gray-100">
                    {succession.map((h) => (
                      <li key={h.id} className="py-2 text-sm">
                        <span className="font-bold text-gray-800 break-words">{h.userName ?? '—'}</span>
                        <span className="text-gray-500"> · {h.role} · {SUCCESSION_EVENT_LABELS[h.event] ?? h.event}</span>
                        {h.sectionName ? <span className="text-gray-500"> · شعبة {h.sectionName}</span> : null}
                        <span className="block text-xs text-gray-400 mt-0.5 break-words">
                          {h.actorName ?? ''} · {h.at}{h.reason ? ` · ${h.reason}` : ''}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            )}
          </>
        )}
      </section>

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
              <div className="sm:col-span-2">
                <label htmlFor="edit-branch-governorate" className="block text-xs font-medium text-gray-600 mb-1">المحافظة <span aria-hidden="true" className="text-red-500">*</span></label>
                <select
                  id="edit-branch-governorate"
                  name="governorate"
                  required
                  value={editGovernorate}
                  onChange={(e) => setEditGovernorate(e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                >
                  <option value="">اختر المحافظة…</option>
                  {GOVERNORATES.map((g) => <option key={g} value={g}>{g}</option>)}
                </select>
                <p className="text-xs text-gray-400 mt-1">إجبارية — تحدد نطاق رئيس القسم في سجل الجهات العامة.</p>
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
