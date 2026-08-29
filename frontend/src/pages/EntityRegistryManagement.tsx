import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { normalizeArabicDigits } from '../utils/arabicDigits';
import { useAuth } from '../auth/useAuth';
import { useIsMobile } from '../hooks/useMediaQuery';
import { GOVERNORATES } from '../utils/governorate';
import {
  CITATION_FORMULA_OPTIONS,
  ENTITY_TYPE_OPTIONS,
  citationFormulaLabel,
  entityTypeLabel,
  formatEntityCoverage,
  isEntryPendingReview,
  publicEntityStatusLabel,
} from '../utils/entityRegistry';
import type {
  PublicEntityEntryDto,
  PublicEntityListResponse,
  PublicEntityGroupListResponse,
  PublicEntityType,
  PublicEntityStatus,
} from '../types';
import { ImportModal } from '../components/entity/ImportModal';
import { MergeModal } from '../components/entity/MergeModal';
import { UnifyNamesModal } from '../components/entity/UnifyNamesModal';
import { BranchManagementModal } from '../components/entity/BranchManagementModal';

const PAGE_SIZE = 20;

export default function EntityRegistryManagement() {
  const { user } = useAuth();
  const isMobile = useIsMobile();
  const hasFullAccess = user?.role === 'manager' || user?.role === 'admin';
  const isHead = user?.role === 'head';
  const canViewGroups = hasFullAccess || isHead;

  // ── وضع العرض: قيود (مسطّح) أو مجموعات (هويات أم) ──
  const [viewMode, setViewMode] = useState<'entries' | 'groups'>('entries');

  // ── قائمة السجل والفلاتر ──
  const [query, setQuery] = useState('');
  const [governorateFilter, setGovernorateFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [list, setList] = useState<PublicEntityListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [listError, setListError] = useState('');
  const [reloadTick, setReloadTick] = useState(0);

  const reload = useCallback(() => setReloadTick((t) => t + 1), []);

  useEffect(() => {
    let active = true;
    setLoading(true);
    api
      .get<PublicEntityListResponse>('/entity-registry', {
        params: {
          q: query.trim() || undefined,
          governorate: governorateFilter || undefined,
          status: statusFilter || undefined,
          page,
          perPage: PAGE_SIZE,
        },
      })
      .then((res) => {
        if (active) setList(res.data);
      })
      .catch((err) => {
        if (active) setListError(getApiErrorMessage(err));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [query, governorateFilter, statusFilter, page, reloadTick]);

  const entries = list?.items ?? [];
  const totalCount = list?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  // ── نموذج الإضافة ──
  const [showForm, setShowForm] = useState(false);
  const [formName, setFormName] = useState('');
  const [formType, setFormType] = useState<PublicEntityType>('ministry');
  const [formGovernorate, setFormGovernorate] = useState('');
  const [formBranch, setFormBranch] = useState('الجهة الأم');
  const [formCitation, setFormCitation] = useState<'add-to-job' | 'add-to-position'>('add-to-job');
  const [formAliases, setFormAliases] = useState('');
  const [formShowCoverage, setFormShowCoverage] = useState(false);
  const [formCoverageLabel, setFormCoverageLabel] = useState('');
  const [formSaving, setFormSaving] = useState(false);
  const [formError, setFormError] = useState('');

  const resetForm = () => {
    setFormName('');
    setFormType('ministry');
    setFormGovernorate('');
    setFormBranch('الجهة الأم');
    setFormCitation('add-to-job');
    setFormAliases('');
    setFormShowCoverage(false);
    setFormCoverageLabel('');
    setFormError('');
    setShowForm(false);
  };

  const submitForm = async (e: FormEvent) => {
    e.preventDefault();
    if (!formName.trim()) {
      setFormError('اسم الجهة مطلوب');
      return;
    }
    if (!formGovernorate) {
      setFormError('المحافظة مطلوبة');
      return;
    }

    setFormSaving(true);
    setFormError('');
    try {
      await api.post('/entity-registry', {
        canonicalName: formName.trim(),
        entityType: formType,
        governorate: formGovernorate,
        branchName: formBranch.trim(),
        citationFormula: formCitation,
        aliases: formAliases.split('\n').map((a) => a.trim()).filter(Boolean),
        coverageLabel: formShowCoverage && formCoverageLabel.trim() ? formCoverageLabel.trim() : null,
      });
      resetForm();
      reload();
    } catch (err) {
      setFormError(getApiErrorMessage(err));
    } finally {
      setFormSaving(false);
    }
  };

  // ── نافذة التعديل ──
  const [editing, setEditing] = useState<PublicEntityEntryDto | null>(null);
  const [editName, setEditName] = useState('');
  const [editType, setEditType] = useState<PublicEntityType>('ministry');
  const [editGovernorate, setEditGovernorate] = useState('');
  const [editBranch, setEditBranch] = useState('');
  const [editCitation, setEditCitation] = useState<'add-to-job' | 'add-to-position'>('add-to-job');
  const [editStatus, setEditStatus] = useState<PublicEntityStatus>('final');
  const [editActive, setEditActive] = useState(true);
  const [editShowCoverage, setEditShowCoverage] = useState(false);
  const [editCoverageLabel, setEditCoverageLabel] = useState('');
  const [newAlias, setNewAlias] = useState('');
  const [editSaving, setEditSaving] = useState(false);
  const [aliasSaving, setAliasSaving] = useState(false);
  const [editError, setEditError] = useState('');
  const [editDecreeKind, setEditDecreeKind] = useState('');
  const [editDecreeNumber, setEditDecreeNumber] = useState('');
  const [editDecreeDate, setEditDecreeDate] = useState('');

  const openEdit = (entry: PublicEntityEntryDto) => {
    setEditing(entry);
    setEditName(entry.canonicalName);
    setEditType(entry.entityType);
    setEditGovernorate(entry.governorate);
    setEditBranch(entry.branchName);
    setEditCitation(entry.citationFormula === 'add-to-position' ? 'add-to-position' : 'add-to-job');
    setEditStatus(entry.status === 'pending' ? 'pending' : 'final');
    setEditActive(entry.isActive);
    setEditShowCoverage(!!entry.coverageLabel);
    setEditCoverageLabel(entry.coverageLabel ?? '');
    setNewAlias('');
    setEditError('');
    setEditDecreeKind('');
    setEditDecreeNumber('');
    setEditDecreeDate('');
  };

  const closeEdit = () => {
    setEditing(null);
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

    setEditSaving(true);
    setEditError('');
    try {
      await api.put(`/entity-registry/${editing.id}`, {
        canonicalName: editName.trim(),
        entityType: editType,
        governorate: editGovernorate.trim(),
        branchName: editBranch.trim(),
        citationFormula: editCitation,
        status: editStatus,
        isActive: editActive,
        coverageLabel: editShowCoverage && editCoverageLabel.trim() ? editCoverageLabel.trim() : null,
        decreeKind: editDecreeKind.trim() || null,
        decreeNumber: editDecreeNumber.trim() || null,
        decreeDate: normalizeArabicDigits(editDecreeDate).trim() || null,
      });
      closeEdit();
      reload();
    } catch (err) {
      setEditError(getApiErrorMessage(err));
    } finally {
      setEditSaving(false);
    }
  };

  const addAlias = async () => {
    if (!editing || !newAlias.trim()) return;
    setAliasSaving(true);
    setEditError('');
    try {
      await api.post(`/entity-registry/${editing.id}/aliases`, { aliasText: newAlias.trim() });
      setEditing((prev) =>
        prev ? { ...prev, aliases: [...prev.aliases, newAlias.trim()] } : prev,
      );
      setNewAlias('');
    } catch (err) {
      setEditError(getApiErrorMessage(err));
    } finally {
      setAliasSaving(false);
    }
  };

  // ── الاستيراد التاريخي ──
  const [showImport, setShowImport] = useState(false);
  const [importSummary, setImportSummary] = useState('');

  // ── الدمج N←1 ──
  const [showMerge, setShowMerge] = useState(false);
  const [mergeSummary, setMergeSummary] = useState('');

  // ── قائمة المجموعات (للعرض المستقل وتوحيد التسمية) ──
  const [groupsQuery, setGroupsQuery] = useState('');
  const [groupsGov, setGroupsGov] = useState('');
  const [groupsPage, setGroupsPage] = useState(1);
  const [groupsList, setGroupsList] = useState<PublicEntityGroupListResponse | null>(null);
  const [groupsLoading, setGroupsLoading] = useState(false);
  const [groupsError, setGroupsError] = useState('');
  const [groupsReloadTick, setGroupsReloadTick] = useState(0);
  const reloadGroups = useCallback(() => setGroupsReloadTick((t) => t + 1), []);

  useEffect(() => {
    if (!canViewGroups || viewMode !== 'groups') return;
    let active = true;
    setGroupsLoading(true);
    setGroupsError('');
    api
      .get<PublicEntityGroupListResponse>('/entity-registry/groups', {
        params: {
          q: groupsQuery.trim() || undefined,
          governorate: groupsGov || undefined,
          page: groupsPage,
          perPage: PAGE_SIZE,
        },
      })
      .then((res) => {
        if (active) setGroupsList(res.data);
      })
      .catch((err) => {
        if (active) setGroupsError(getApiErrorMessage(err));
      })
      .finally(() => {
        if (active) setGroupsLoading(false);
      });
    return () => { active = false; };
  }, [canViewGroups, viewMode, groupsQuery, groupsGov, groupsPage, groupsReloadTick]);

  const groups = groupsList?.items ?? [];
  const groupsTotalCount = groupsList?.totalCount ?? 0;
  const groupsTotalPages = Math.max(1, Math.ceil(groupsTotalCount / PAGE_SIZE));

  // ── توحيد التسمية N←1 (بديل دمج الملفات) ──
  const [showUnify, setShowUnify] = useState(false);
  const [unifyInitialGroupId, setUnifyInitialGroupId] = useState<number | undefined>(undefined);
  const [unifySummary, setUnifySummary] = useState('');

  const openUnify = useCallback((groupId?: number) => {
    setUnifyInitialGroupId(groupId);
    setShowUnify(true);
  }, []);

  // ── إدارة فروع الجهة لرئيس القسم (محافظته فقط) ──
  const [showBranchManage, setShowBranchManage] = useState(false);
  const [branchManageGroupId, setBranchManageGroupId] = useState<number | null>(null);
  const [branchManageGroupName, setBranchManageGroupName] = useState('');

  const openBranchManage = useCallback((groupId: number, groupName: string) => {
    setBranchManageGroupId(groupId);
    setBranchManageGroupName(groupName);
    setShowBranchManage(true);
  }, []);

  const statusBadge = (e: { status: PublicEntityStatus; needsReview?: boolean | null }) => (
    <span
      className={`inline-block rounded-full px-2 py-0.5 text-xs whitespace-nowrap ${
        isEntryPendingReview(e) ? 'bg-amber-100 text-amber-800' : 'bg-emerald-100 text-emerald-800'
      }`}
    >
      {isEntryPendingReview(e) ? 'بانتظار المراجعة' : publicEntityStatusLabel(e.status)}
    </span>
  );

  return (
    <div className="max-w-6xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-6">سجل الجهات العامة</h2>

      {canViewGroups && (
        <div className="mb-4 flex items-center gap-2 bg-white rounded-xl shadow p-1 w-fit">
          <button
            onClick={() => setViewMode('entries')}
            aria-pressed={viewMode === 'entries'}
            className={`rounded-lg px-4 py-2 text-sm min-h-11 font-medium transition-colors focus-visible:ring-2 focus-visible:ring-emerald-500 ${viewMode === 'entries' ? 'bg-emerald-700 text-white shadow' : 'text-gray-600 hover:bg-gray-50'}`}
          >
            عرض القيود
          </button>
          <button
            onClick={() => { setViewMode('groups'); setGroupsPage(1); }}
            aria-pressed={viewMode === 'groups'}
            className={`rounded-lg px-4 py-2 text-sm min-h-11 font-medium transition-colors focus-visible:ring-2 focus-visible:ring-emerald-500 ${viewMode === 'groups' ? 'bg-emerald-700 text-white shadow' : 'text-gray-600 hover:bg-gray-50'}`}
          >
            عرض المجموعات
          </button>
        </div>
      )}

      <div className="bg-white rounded-xl shadow p-4 mb-4 flex flex-wrap items-center gap-3">
        {viewMode === 'entries' ? (
          <>
            <div className="grow min-w-[200px]">
              <label htmlFor="reg-search" className="sr-only">بحث بالاسم</label>
              <input
                id="reg-search"
                value={query}
                onChange={(e) => { setQuery(e.target.value); setPage(1); }}
                placeholder="بحث باسم الجهة…"
                autoComplete="off"
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
            </div>
            <div>
              <label htmlFor="reg-gov" className="sr-only">فلتر المحافظة</label>
              <select
                id="reg-gov"
                value={governorateFilter}
                onChange={(e) => { setGovernorateFilter(e.target.value); setPage(1); }}
                className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
              >
                <option value="">كل المحافظات</option>
                {GOVERNORATES.map((g) => <option key={g} value={g}>{g}</option>)}
              </select>
            </div>
            <div>
              <label htmlFor="reg-status" className="sr-only">فلتر الحالة</label>
              <select
                id="reg-status"
                value={statusFilter}
                onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
                className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
              >
                <option value="">كل الحالات</option>
                <option value="final">نهائي</option>
                <option value="pending">بانتظار المراجعة</option>
              </select>
            </div>
            {hasFullAccess && (
              <button
                onClick={() => setShowImport(true)}
                className="border border-sky-200 text-sky-800 hover:bg-sky-50 rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-sky-500"
              >
                استيراد النصوص التاريخية…
              </button>
            )}
            {hasFullAccess && (
              <button
                onClick={() => setShowMerge(true)}
                className="border border-amber-200 text-amber-800 hover:bg-amber-50 rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-amber-500"
              >
                دمج جهات…
              </button>
            )}
            {hasFullAccess && (
              <button
                onClick={() => openUnify()}
                className="border border-emerald-200 text-emerald-800 hover:bg-emerald-50 rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
              >
                توحيد تسمية…
              </button>
            )}
            <button
              onClick={() => setShowForm((v) => !v)}
              aria-expanded={showForm}
              className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
            >
              {showForm ? 'إلغاء' : '+ إضافة جهة'}
            </button>
          </>
        ) : (
          <>
            <div className="grow min-w-[200px]">
              <label htmlFor="groups-search" className="sr-only">بحث باسم المجموعة</label>
              <input
                id="groups-search"
                value={groupsQuery}
                onChange={(e) => { setGroupsQuery(e.target.value); setGroupsPage(1); }}
                placeholder="بحث باسم المجموعة…"
                autoComplete="off"
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
            </div>
            {hasFullAccess && (
              <div>
                <label htmlFor="groups-gov" className="sr-only">فلتر المحافظة للمجموعات</label>
                <select
                  id="groups-gov"
                  value={groupsGov}
                  onChange={(e) => { setGroupsGov(e.target.value); setGroupsPage(1); }}
                  className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                >
                  <option value="">كل المحافظات</option>
                  {GOVERNORATES.map((g) => <option key={g} value={g}>{g}</option>)}
                </select>
              </div>
            )}
            {hasFullAccess && (
              <button
                onClick={() => openUnify()}
                className="border border-emerald-200 text-emerald-800 hover:bg-emerald-50 rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
              >
                توحيد تسمية…
              </button>
            )}
          </>
        )}
      </div>

      {importSummary && (
        <p role="status" className="mb-4 bg-emerald-50 border border-emerald-100 text-emerald-800 rounded-lg p-3 text-sm">
          {importSummary}
        </p>
      )}

      {mergeSummary && (
        <p role="status" className="mb-4 bg-amber-50 border border-amber-100 text-amber-800 rounded-lg p-3 text-sm">
          {mergeSummary}
        </p>
      )}

      {unifySummary && (
        <p role="status" className="mb-4 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-lg p-3 text-sm">
          {unifySummary}
        </p>
      )}

      {showForm && (
        <form onSubmit={submitForm} className="bg-white rounded-xl shadow p-4 mb-4 grid sm:grid-cols-2 gap-4">
          <div className="sm:col-span-2">
            <label htmlFor="fe-name" className="block text-xs font-medium text-gray-600 mb-1">اسم الجهة المعتمد</label>
            <input
              id="fe-name"
              value={formName}
              onChange={(e) => setFormName(e.target.value)}
              placeholder="مثال: المدير العام للمصرف التجاري السوري…"
              autoComplete="off"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="fe-type" className="block text-xs font-medium text-gray-600 mb-1">نوع الجهة</label>
            <select
              id="fe-type"
              value={formType}
              onChange={(e) => setFormType(e.target.value as PublicEntityType)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {ENTITY_TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>
          <div>
            <label htmlFor="fe-citation" className="block text-xs font-medium text-gray-600 mb-1">صيغة ممثلها القانوني</label>
            <select
              id="fe-citation"
              value={formCitation}
              onChange={(e) => setFormCitation(e.target.value as typeof formCitation)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {CITATION_FORMULA_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>
          <div>
            <label htmlFor="fe-gov" className="block text-xs font-medium text-gray-600 mb-1">المحافظة</label>
            <select
              id="fe-gov"
              value={formGovernorate}
              onChange={(e) => setFormGovernorate(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              <option value="">اختر المحافظة…</option>
              {GOVERNORATES.map((g) => <option key={g} value={g}>{g}</option>)}
            </select>
          </div>
          <div>
            <label htmlFor="fe-branch" className="block text-xs font-medium text-gray-600 mb-1">الفرع</label>
            <input
              id="fe-branch"
              value={formBranch}
              onChange={(e) => setFormBranch(e.target.value)}
              placeholder="مثال: فرع حمص…"
              autoComplete="off"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div className="sm:col-span-2">
            <label htmlFor="fe-aliases" className="block text-xs font-medium text-gray-600 mb-1">أسماء بديلة (كل اسم في سطر)</label>
            <textarea
              id="fe-aliases"
              value={formAliases}
              onChange={(e) => setFormAliases(e.target.value)}
              rows={2}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <label className="inline-flex items-center gap-2 text-sm cursor-pointer min-h-11 sm:col-span-2">
            <input
              type="checkbox"
              checked={formShowCoverage}
              onChange={(e) => { setFormShowCoverage(e.target.checked); if (!e.target.checked) setFormCoverageLabel(''); }}
              className="h-4 w-4"
            />
            تغطية الجهة تشمل أكثر من محافظة
          </label>
          {formShowCoverage && (
            <div className="sm:col-span-2">
              <label htmlFor="fe-coverage" className="block text-xs font-medium text-gray-600 mb-1">تسمية التغطية (حد أقصى 150 حرفًا)</label>
              <input
                id="fe-coverage"
                value={formCoverageLabel}
                onChange={(e) => setFormCoverageLabel(e.target.value)}
                placeholder="مثال: دمشق وريف دمشق والقنيطرة"
                maxLength={150}
                autoComplete="off"
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
            </div>
          )}
          {formError && <p role="alert" className="text-red-600 text-sm sm:col-span-2">{formError}</p>}
          <div className="sm:col-span-2">
            <button
              type="submit"
              disabled={formSaving}
              className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              {formSaving ? 'جارِ الحفظ…' : 'إنشاء القيد'}
            </button>
          </div>
        </form>
      )}

      {viewMode === 'entries' ? (
        <>
          {listError && <div role="alert" className="text-red-600 mb-4">{listError}</div>}

          <div className="bg-white rounded-xl shadow overflow-hidden">
            {!isMobile && (
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-gray-600">
                  <tr className="text-right">
                    <th className="px-4 py-3">الجهة</th>
                    <th className="px-4 py-3">النوع</th>
                    <th className="px-4 py-3">المحافظة / الفرع</th>
                    <th className="px-4 py-3">الصيغة</th>
                    <th className="px-4 py-3">الحالة</th>
                    <th className="px-4 py-3">إجراء</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {entries.map((entry) => (
                    <tr key={entry.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3">
                        <div className="font-medium text-gray-800">{entry.canonicalName}</div>
                        {entry.aliases.length > 0 && (
                          <div className="text-xs text-gray-400 truncate max-w-xs" title={entry.aliases.join('، ')}>
                            أسماء أخرى: {entry.aliases.length}
                          </div>
                        )}
                      </td>
                      <td className="px-4 py-3 text-gray-600">{entityTypeLabel(entry.entityType)}</td>
                      <td className="px-4 py-3 text-gray-600">{formatEntityCoverage(entry)} / {entry.branchName}</td>
                      <td className="px-4 py-3 text-gray-600 whitespace-nowrap">{citationFormulaLabel(entry.citationFormula)}</td>
                      <td className="px-4 py-3">{statusBadge(entry)}</td>
                      <td className="px-4 py-3">
                        <button
                          onClick={() => openEdit(entry)}
                          className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-sky-500"
                        >
                          تعديل
                        </button>
                      </td>
                    </tr>
                  ))}
                  {!loading && entries.length === 0 && (
                    <tr><td colSpan={6} className="px-4 py-8 text-center text-gray-400">لا توجد جهات مطابقة</td></tr>
                  )}
                </tbody>
              </table>
            )}

            {isMobile && (
              <div className="divide-y divide-gray-100">
                {!loading && entries.length === 0 && (
                  <div className="px-4 py-8 text-center text-gray-400">لا توجد جهات مطابقة</div>
                )}
                {entries.map((entry) => (
                  <article key={entry.id} className="p-4">
                    <div className="flex items-start justify-between gap-3">
                      <h3 className="font-bold text-gray-800 leading-snug break-words">{entry.canonicalName}</h3>
                      {statusBadge(entry)}
                    </div>
                    <p className="text-sm text-gray-600 mt-1">
                      {entityTypeLabel(entry.entityType)} · {formatEntityCoverage(entry)} / {entry.branchName}
                    </p>
                    <p className="text-xs text-gray-400 mt-0.5">{citationFormulaLabel(entry.citationFormula)}</p>
                    <button
                      onClick={() => openEdit(entry)}
                      className="mt-3 text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-sky-500"
                    >
                      تعديل
                    </button>
                  </article>
                ))}
              </div>
            )}

            {totalCount > PAGE_SIZE && (
              <nav aria-label="تصفح السجل" className="flex items-center justify-between gap-2 px-4 py-3 border-t border-gray-100 text-sm">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page <= 1 || loading}
                  className="border border-gray-300 rounded-lg px-3 py-2 min-h-11 disabled:opacity-40 hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  السابق
                </button>
                <span className="text-gray-500 tabular-nums">{page} من {totalPages} — {totalCount} جهة</span>
                <button
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={page >= totalPages || loading}
                  className="border border-gray-300 rounded-lg px-3 py-2 min-h-11 disabled:opacity-40 hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  التالي
                </button>
              </nav>
            )}
          </div>
        </>
      ) : (
        <>
          {groupsError && <div role="alert" className="text-red-600 mb-4">{groupsError}</div>}

          <div className="bg-white rounded-xl shadow overflow-hidden">
            {groupsLoading && <p className="text-sm text-gray-500 p-6 text-center">جارِ تحميل المجموعات…</p>}

            {!groupsLoading && !isMobile && (
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-gray-600">
                  <tr className="text-right">
                    <th className="px-4 py-3">المجموعة (الهوية الأم)</th>
                    <th className="px-4 py-3">النوع</th>
                    <th className="px-4 py-3">عدد القيود</th>
                    <th className="px-4 py-3">المحافظات</th>
                    <th className="px-4 py-3">إجراء</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {groups.map((g) => (
                    <tr key={g.groupId} className="hover:bg-gray-50">
                      <td className="px-4 py-3">
                        <div className="font-bold text-gray-800">{g.canonicalName}</div>
                      </td>
                      <td className="px-4 py-3 text-gray-600">{entityTypeLabel(g.entityType)}</td>
                      <td className="px-4 py-3 text-gray-600 tabular-nums">{g.entryCount}</td>
                      <td className="px-4 py-3 text-gray-600">
                        {g.governorates.length > 0 ? (
                          <span className="inline-flex flex-wrap gap-1">
                            {g.governorates.map((gov) => (
                              <span key={gov} className="bg-gray-100 text-gray-700 rounded-full px-2 py-0.5 text-xs">{gov}</span>
                            ))}
                          </span>
                        ) : (
                          <span className="text-gray-400">—</span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        {hasFullAccess ? (
                          <button
                            onClick={() => openUnify(g.groupId)}
                            className="border border-emerald-200 text-emerald-700 hover:bg-emerald-50 rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
                          >
                            توحيد تسمية
                          </button>
                        ) : isHead ? (
                          <button
                            onClick={() => openBranchManage(g.groupId, g.canonicalName)}
                            className="border border-sky-200 text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-sky-500"
                          >
                            إدارة الفروع
                          </button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                  {!groupsLoading && groups.length === 0 && (
                    <tr><td colSpan={5} className="px-4 py-8 text-center text-gray-400">لا توجد مجموعات مطابقة</td></tr>
                  )}
                </tbody>
              </table>
            )}

            {!groupsLoading && isMobile && (
              <div className="divide-y divide-gray-100">
                {!groupsLoading && groups.length === 0 && (
                  <div className="px-4 py-8 text-center text-gray-400">لا توجد مجموعات مطابقة</div>
                )}
                {groups.map((g) => (
                  <article key={g.groupId} className="p-4">
                    <h3 className="font-bold text-gray-800 leading-snug break-words text-pretty">{g.canonicalName}</h3>
                    <p className="text-sm text-gray-600 mt-1">
                      {entityTypeLabel(g.entityType)} · {g.entryCount} قيد
                    </p>
                    {g.governorates.length > 0 && (
                      <div className="flex flex-wrap gap-1 mt-2">
                        {g.governorates.map((gov) => (
                          <span key={gov} className="bg-gray-100 text-gray-700 rounded-full px-2 py-0.5 text-xs">{gov}</span>
                        ))}
                      </div>
                    )}
                    {hasFullAccess ? (
                      <button
                        onClick={() => openUnify(g.groupId)}
                        className="mt-3 border border-emerald-200 text-emerald-700 hover:bg-emerald-50 rounded-lg px-3 py-2 text-xs min-h-11 w-full focus-visible:ring-2 focus-visible:ring-emerald-500"
                      >
                        توحيد تسمية هذه المجموعة
                      </button>
                    ) : isHead ? (
                      <button
                        onClick={() => openBranchManage(g.groupId, g.canonicalName)}
                        className="mt-3 border border-sky-200 text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 text-xs min-h-11 w-full focus-visible:ring-2 focus-visible:ring-sky-500"
                      >
                        إدارة فروع هذه المجموعة
                      </button>
                    ) : null}
                  </article>
                ))}
              </div>
            )}

            {groupsTotalCount > PAGE_SIZE && (
              <nav aria-label="تصفح المجموعات" className="flex items-center justify-between gap-2 px-4 py-3 border-t border-gray-100 text-sm">
                <button
                  onClick={() => setGroupsPage((p) => Math.max(1, p - 1))}
                  disabled={groupsPage <= 1 || groupsLoading}
                  className="border border-gray-300 rounded-lg px-3 py-2 min-h-11 disabled:opacity-40 hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  السابق
                </button>
                <span className="text-gray-500 tabular-nums">{groupsPage} من {groupsTotalPages} — {groupsTotalCount} مجموعة</span>
                <button
                  onClick={() => setGroupsPage((p) => Math.min(groupsTotalPages, p + 1))}
                  disabled={groupsPage >= groupsTotalPages || groupsLoading}
                  className="border border-gray-300 rounded-lg px-3 py-2 min-h-11 disabled:opacity-40 hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  التالي
                </button>
              </nav>
            )}
          </div>
        </>
      )}

      {editing && (
        <div
          className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
          dir="rtl"
          role="dialog"
          aria-modal="true"
          aria-label={`تعديل جهة: ${editing.canonicalName}`}
          style={{ overscrollBehavior: 'contain' }}
        >
          <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[85vh] overflow-y-auto overscroll-contain p-5">
            <div className="flex items-center justify-between mb-3">
              <h3 className="text-lg font-bold text-gray-800">تعديل جهة</h3>
              <button
                onClick={closeEdit}
                className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
                aria-label="إغلاق"
              >
                ×
              </button>
            </div>

            <div className="grid sm:grid-cols-2 gap-4">
              <div className="sm:col-span-2">
                <span className="block text-xs font-medium text-red-700 bg-red-50 border border-red-100 rounded-lg p-3 leading-relaxed">
                  تعديل الاسم يعيد تسمية الجهة في كل الملفات المرتبطة بها فورًا ويُدوَّن في سجل التعديلات.
                </span>
              </div>
              <div className="sm:col-span-2">
                <label htmlFor="ed-name" className="block text-xs font-medium text-gray-600 mb-1">الاسم المعتمد</label>
                <input
                  id="ed-name"
                  value={editName}
                  onChange={(e) => setEditName(e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <div>
                <label htmlFor="ed-type" className="block text-xs font-medium text-gray-600 mb-1">نوع الجهة</label>
                <select
                  id="ed-type"
                  value={editType}
                  onChange={(e) => setEditType(e.target.value as PublicEntityType)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                >
                  {ENTITY_TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                </select>
              </div>
              <div>
                <label htmlFor="ed-citation" className="block text-xs font-medium text-gray-600 mb-1">صيغة ممثلها القانوني</label>
                <select
                  id="ed-citation"
                  value={editCitation}
                  onChange={(e) => setEditCitation(e.target.value as typeof editCitation)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                >
                  {CITATION_FORMULA_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                </select>
              </div>
              <div>
                <label htmlFor="ed-gov" className="block text-xs font-medium text-gray-600 mb-1">المحافظة</label>
                <select
                  id="ed-gov"
                  value={editGovernorate}
                  onChange={(e) => setEditGovernorate(e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                >
                  {!GOVERNORATES.includes(editGovernorate) && editGovernorate !== '' && (
                    <option value={editGovernorate}>{editGovernorate}</option>
                  )}
                  {GOVERNORATES.map((g) => <option key={g} value={g}>{g}</option>)}
                </select>
              </div>
              <div>
                <label htmlFor="ed-branch" className="block text-xs font-medium text-gray-600 mb-1">الفرع</label>
                <input
                  id="ed-branch"
                  value={editBranch}
                  onChange={(e) => setEditBranch(e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <div>
                <label htmlFor="ed-status" className="block text-xs font-medium text-gray-600 mb-1">الحالة</label>
                <select
                  id="ed-status"
                  value={editStatus}
                  onChange={(e) => setEditStatus(e.target.value as PublicEntityStatus)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                >
                  <option value="final">نهائي</option>
                  <option value="pending">بانتظار المراجعة</option>
                </select>
              </div>
              <label className="inline-flex items-center gap-2 text-sm cursor-pointer min-h-11">
                <input type="checkbox" checked={editActive} onChange={(e) => setEditActive(e.target.checked)} />
                القيد مفعّل
              </label>
              <label className="inline-flex items-center gap-2 text-sm cursor-pointer min-h-11">
                <input
                  type="checkbox"
                  checked={editShowCoverage}
                  onChange={(e) => { setEditShowCoverage(e.target.checked); if (!e.target.checked) setEditCoverageLabel(''); }}
                />
                تغطية أكثر من محافظة
              </label>
            </div>

            {editShowCoverage && (
              <div className="mt-3">
                <label htmlFor="ed-coverage" className="block text-xs font-medium text-gray-600 mb-1">تسمية التغطية (حد أقصى 150 حرفًا)</label>
                <input
                  id="ed-coverage"
                  value={editCoverageLabel}
                  onChange={(e) => setEditCoverageLabel(e.target.value)}
                  maxLength={150}
                  autoComplete="off"
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
            )}

            {hasFullAccess && (
              <div className="mt-4 border-t border-gray-100 pt-4">
                <h4 className="text-sm font-bold text-gray-700 mb-2">المرسوم (للتعديلات العامة — اختياري)</h4>
                <div className="grid sm:grid-cols-3 gap-3">
                  <div>
                    <label htmlFor="ed-decree-kind" className="block text-xs font-medium text-gray-600 mb-1">نوع المرسوم</label>
                    <input
                      id="ed-decree-kind"
                      value={editDecreeKind}
                      onChange={(e) => setEditDecreeKind(e.target.value)}
                      placeholder="مثال: مرسوم تشريعي…"
                      autoComplete="off"
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    />
                  </div>
                  <div>
                    <label htmlFor="ed-decree-number" className="block text-xs font-medium text-gray-600 mb-1">رقم المرسوم</label>
                    <input
                      id="ed-decree-number"
                      value={editDecreeNumber}
                      onChange={(e) => setEditDecreeNumber(e.target.value)}
                      placeholder="مثال: 123…"
                      autoComplete="off"
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    />
                  </div>
                  <div>
                    <label htmlFor="ed-decree-date" className="block text-xs font-medium text-gray-600 mb-1">تاريخ المرسوم</label>
                    <input
                      id="ed-decree-date"
                      type="text"
                      value={editDecreeDate}
                      onChange={(e) => setEditDecreeDate(e.target.value)}
                      placeholder="مثال: 1/8/2026"
                      autoComplete="off"
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    />
                  </div>
                </div>
              </div>
            )}

            <div className="mt-4">
              <span className="block text-xs font-medium text-gray-600 mb-1">الأسماء البديلة</span>
              {editing.aliases.length > 0 && (
                <ul className="flex flex-wrap gap-1.5 mb-2">
                  {editing.aliases.map((alias) => (
                    <li key={alias} className="bg-gray-100 text-gray-700 rounded-full px-2.5 py-1 text-xs break-all">{alias}</li>
                  ))}
                </ul>
              )}
              <div className="flex gap-2">
                <label htmlFor="ed-alias" className="sr-only">إضافة اسم بديل</label>
                <input
                  id="ed-alias"
                  value={newAlias}
                  onChange={(e) => setNewAlias(e.target.value)}
                  placeholder="مثال: كتابة قديمة للاسم…"
                  autoComplete="off"
                  className="flex-1 min-w-0 min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
                <button
                  onClick={addAlias}
                  disabled={aliasSaving || !newAlias.trim()}
                  className="border border-emerald-200 text-emerald-800 hover:bg-emerald-50 disabled:opacity-40 rounded-lg px-4 py-2 text-sm min-h-11"
                >
                  {aliasSaving ? 'جارِ الإضافة…' : 'إضافة'}
                </button>
              </div>
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
                onClick={closeEdit}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
              >
                إلغاء
              </button>
            </div>
          </div>
        </div>
      )}

      {showImport && (
        <ImportModal
          onClose={() => setShowImport(false)}
          onCommitted={(summary) => {
            setShowImport(false);
            setImportSummary(summary);
            setPage(1);
            reload();
          }}
        />
      )}

      {showMerge && (
        <MergeModal
          onClose={() => setShowMerge(false)}
          onCommitted={(summary) => {
            setShowMerge(false);
            setMergeSummary(summary);
            setPage(1);
            reload();
          }}
        />
      )}

      {showUnify && (
        <UnifyNamesModal
          initialGroupId={unifyInitialGroupId}
          onClose={() => setShowUnify(false)}
          onCommitted={(summary) => {
            setShowUnify(false);
            setUnifySummary(summary);
            setPage(1);
            reload();
            reloadGroups();
          }}
        />
      )}

      {showBranchManage && branchManageGroupId !== null && (
        <BranchManagementModal
          groupId={branchManageGroupId}
          groupName={branchManageGroupName}
          onClose={() => setShowBranchManage(false)}
          onCommitted={(summary) => {
            setShowBranchManage(false);
            setUnifySummary(summary);
            reload();
            reloadGroups();
          }}
        />
      )}
    </div>
  );
}
