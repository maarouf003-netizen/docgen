import { useRef, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { useCancellableRequest } from '../../hooks/useCancellableRequest';
import type { BranchDto, CircuitStatsDto, HeadSuccessionDto, SectionDto } from '../../types';
import { validateBranchFields } from './branchesManagementModel';

export type SubTab = 'sections' | 'transfer' | 'succession';

/**
 * حالة وقيادة شاشة «إدارة الفروع»: قائمة الفروع، إنشاء/تعديل/حذف فرع، وإدارة
 * الشعب ونقل الدوائر وسجل التعاقب للفرع المختار. الواجهة في مكوّنات
 * `BranchesManagement` الأربعة، والمنطق هنا.
 */
export function useBranchesManagement() {
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

  // تسلسل طلبات البيانات التابعة للفرع: تبديل «الفرع الأب» بسرعة قد يُرجع استجابة
  // قديمة بعد الأحدث، فتطمس شعب/دوائر/تعاقب فرع آخر. كل تحميل يحمل رقمًا ولا يُطبَّق
  // إلا إن بقي هو الأحدث (ويُبطل الترقيم عند إلغاء الاختيار).
  const sectionsSeq = useRef(0);
  const circuitsSeq = useRef(0);
  const successionSeq = useRef(0);

  const loadSections = async (branchId: number) => {
    const seq = ++sectionsSeq.current;
    setSectionsLoading(true);
    setSectionsError('');
    try {
      const r = await api.get<SectionDto[]>('/sections', { params: { branchId } });
      if (seq !== sectionsSeq.current) return;
      setSections(r.data ?? []);
    } catch (err) {
      if (seq !== sectionsSeq.current) return;
      setSectionsError(getApiErrorMessage(err));
      setSections([]);
    } finally {
      if (seq === sectionsSeq.current) setSectionsLoading(false);
    }
  };

  const loadCircuits = async (branchId: number) => {
    const seq = ++circuitsSeq.current;
    setCircuitsLoading(true);
    setCircuitsError('');
    try {
      const r = await api.get<CircuitStatsDto[]>('/execution-circuits/stats', { params: { branchId } });
      if (seq !== circuitsSeq.current) return;
      setCircuits(r.data ?? []);
    } catch (err) {
      if (seq !== circuitsSeq.current) return;
      setCircuitsError(getApiErrorMessage(err));
      setCircuits([]);
    } finally {
      if (seq === circuitsSeq.current) setCircuitsLoading(false);
    }
  };

  const loadSuccession = async (branchId: number) => {
    const seq = ++successionSeq.current;
    setSuccessionLoading(true);
    setSuccessionError('');
    try {
      const r = await api.get<HeadSuccessionDto[]>('/head-succession', { params: { branchId } });
      if (seq !== successionSeq.current) return;
      setSuccession(r.data ?? []);
    } catch (err) {
      if (seq !== successionSeq.current) return;
      setSuccessionError(getApiErrorMessage(err));
      setSuccession([]);
    } finally {
      if (seq === successionSeq.current) setSuccessionLoading(false);
    }
  };

  const pickSubBranch = (id: number | '') => {
    setSubBranchId(id);
    setTransferMsg('');
    if (id === '') {
      // إلغاء الاختيار يُبطل أي طلب جارٍ كي لا يعيد تعبئة قوائم فرع سابق.
      sectionsSeq.current += 1;
      circuitsSeq.current += 1;
      successionSeq.current += 1;
      setSections([]);
      setCircuits([]);
      setSuccession([]);
      setSectionsLoading(false);
      setCircuitsLoading(false);
      setSuccessionLoading(false);
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
      await api.put(`/sections/${renaming.id}/rename`, { name: nm, version: renaming.version });
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
      await api.put(`/sections/${s.id}/active`, { isActive: active, version: s.version });
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

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const problem = validateBranchFields(name, code, governorate);
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
    const problem = validateBranchFields(editName, editCode, editGovernorate);
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
        version: editing.version,
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

  return {
    branches,
    loading,
    fetchError,
    load,
    showForm,
    setShowForm,
    name,
    setName,
    code,
    setCode,
    address,
    setAddress,
    phone,
    setPhone,
    governorate,
    setGovernorate,
    saving,
    formError,
    resetForm,
    submit,
    editing,
    editName,
    setEditName,
    editCode,
    setEditCode,
    editAddress,
    setEditAddress,
    editPhone,
    setEditPhone,
    editGovernorate,
    setEditGovernorate,
    editActive,
    setEditActive,
    editSaving,
    editError,
    confirmingDelete,
    setConfirmingDelete,
    deleting,
    openEdit,
    closeEdit,
    saveEdit,
    deleteBranch,
    subBranchId,
    subTab,
    setSubTab,
    pickSubBranch,
    sections,
    sectionsLoading,
    sectionsError,
    newSectionName,
    setNewSectionName,
    sectionBusy,
    renaming,
    setRenaming,
    renameName,
    setRenameName,
    createSection,
    saveRename,
    setSectionActive,
    deleteSection,
    circuits,
    circuitsLoading,
    circuitsError,
    transferCircuitId,
    setTransferCircuitId,
    transferTarget,
    setTransferTarget,
    transferBusy,
    transferMsg,
    submitTransfer,
    succession,
    successionLoading,
    successionError,
  };
}

export type BranchesManagementModel = ReturnType<typeof useBranchesManagement>;
