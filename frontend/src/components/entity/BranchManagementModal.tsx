import { useCallback, useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { isEntryPendingReview, formatEntityCoverage } from '../../utils/entityRegistry';
import {
  CITATION_FORMULA_OPTIONS,
  ENTITY_TYPE_OPTIONS,
} from '../../utils/entityRegistry';
import type {
  AbolishBranchRequest,
  AbolishBranchResponse,
  BranchActionKind,
  BranchActionPreviewResponse,
  BranchPreviewEntryDto,
  CitationFormula,
  MergeBranchesRequest,
  MergeBranchesResponse,
  ParentEditSuggestionDto,
  ParentEditSuggestionListResponse,
  PreviewBranchActionRequest,
  PublicEntityEntryDto,
  PublicEntityType,
  RenameBranchRequest,
  RenameBranchResponse,
  SuggestParentEditRequest,
  UnifyBranchesRequest,
  UnifyBranchesResponse,
} from '../../types';

interface BranchManagementModalProps {
  groupId: number;
  groupName: string;
  onClose: () => void;
  onCommitted: (summary: string) => void;
}

const fCount = new Intl.NumberFormat('ar-EG');

/**
 * إدارة فروع جهة عامة لرئيس القسم (محافظته فقط):
 * تعديل تسمية / دمج / إلغاء / توحيد تسميات — كلها تمر بمعاينة موحّدة قبل الاعتماد،
 * مع بطاقة «الجهة الأم» للقراءة فقط واقتراح تعديلها (سحب ذاتي ما دام معلّقًا).
 */
export function BranchManagementModal({ groupId, groupName, onClose, onCommitted }: BranchManagementModalProps) {
  const [entries, setEntries] = useState<PublicEntityEntryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [pendingSuggestion, setPendingSuggestion] = useState<ParentEditSuggestionDto | null>(null);

  const [editingId, setEditingId] = useState<number | null>(null);
  const [editBranch, setEditBranch] = useState('');
  const [editCoverage, setEditCoverage] = useState('');
  const [editShowCoverage, setEditShowCoverage] = useState(false);

  const [mergeSource, setMergeSource] = useState<number | ''>('');
  const [mergeTarget, setMergeTarget] = useState<number | ''>('');

  const [abolishId, setAbolishId] = useState<number | null>(null);
  const [abolishTarget, setAbolishTarget] = useState<number | ''>('');

  const [unifyTarget, setUnifyTarget] = useState<number | ''>('');
  const [unifyAbsorbed, setUnifyAbsorbed] = useState<number[]>([]);
  const [unifyCorrected, setUnifyCorrected] = useState('');
  const [unifyConfirmText, setUnifyConfirmText] = useState('');

  const [proposeEntry, setProposeEntry] = useState<PublicEntityEntryDto | null>(null);
  const [pCanonical, setPCanonical] = useState('');
  const [pType, setPType] = useState<PublicEntityType | ''>('');
  const [pCitation, setPCitation] = useState<CitationFormula | ''>('');
  const [pReason, setPReason] = useState('');

  const [preview, setPreview] = useState<BranchActionPreviewResponse | null>(null);
  const [previewFor, setPreviewFor] = useState<BranchActionKind | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [busy, setBusy] = useState(false);

  const reload = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const entriesRes = await api.get<PublicEntityEntryDto[]>(`/entity-registry/groups/${groupId}/entries`);
      setEntries(entriesRes.data ?? []);
    } catch (err) {
      setError(getApiErrorMessage(err));
    }
    try {
      const suggRes = await api.get<ParentEditSuggestionListResponse>('/entity-registry/parent-edit-suggestions', {
        params: { groupId, status: 'pending', perPage: 10 },
      });
      setPendingSuggestion(suggRes.data?.items?.[0] ?? null);
    } catch {
      setPendingSuggestion(null);
    } finally {
      setLoading(false);
    }
  }, [groupId]);

  useEffect(() => {
    reload();
  }, [reload]);

  const parent = entries.find((e) => e.isParentEntity);
  const activeEntries = entries.filter((e) => !e.isParentEntity);

  const clearPreview = () => {
    setPreview(null);
    setPreviewFor(null);
  };

  const startEdit = (e: PublicEntityEntryDto) => {
    setEditingId(e.id);
    setEditBranch(e.branchName);
    setEditShowCoverage(!!e.coverageLabel);
    setEditCoverage(e.coverageLabel ?? '');
    setError('');
    setSuccess('');
    clearPreview();
  };

  const runPreview = async (kind: BranchActionKind, payload: PreviewBranchActionRequest) => {
    setPreviewing(true);
    setError('');
    try {
      const res = await api.post<BranchActionPreviewResponse>(
        `/entity-registry/groups/${groupId}/branches/preview`,
        payload,
      );
      setPreview(res.data ?? null);
      setPreviewFor(kind);
    } catch (err) {
      clearPreview();
      setError(getApiErrorMessage(err));
    } finally {
      setPreviewing(false);
    }
  };

  const renamePreview = () => {
    if (editingId === null) return;
    if (!editBranch.trim()) {
      setError('اسم الفرع مطلوب');
      return;
    }
    void runPreview('rename', {
      action: 'rename',
      entryId: editingId,
      newBranchName: editBranch.trim(),
    });
  };

  const renameCommit = async () => {
    if (editingId === null || !editBranch.trim()) return;
    setBusy(true);
    setError('');
    try {
      const payload: RenameBranchRequest = {
        newBranchName: editBranch.trim(),
        coverageLabel: editShowCoverage && editCoverage.trim() ? editCoverage.trim() : null,
      };
      const res = await api.post<RenameBranchResponse>(
        `/entity-registry/groups/${groupId}/branches/${editingId}/rename-branch`,
        payload,
      );
      setSuccess(`تمت إعادة تسمية الفرع إلى «${res.data?.newBranchName ?? editBranch.trim()}» وسُوّغت ملفات مرتبطة`);
      setEditingId(null);
      clearPreview();
      await reload();
      onCommitted(`تم تعديل فرع في «${groupName}»`);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const mergeCommit = async () => {
    if (mergeSource === '' || mergeTarget === '' || mergeSource === mergeTarget) {
      setError('اختر فرعًا مصدرًا وفرعًا هدفًا مختلفين');
      return;
    }
    setBusy(true);
    setError('');
    try {
      const payload: MergeBranchesRequest = { sourceEntryId: mergeSource, targetEntryId: mergeTarget };
      await api.post<MergeBranchesResponse>(`/entity-registry/groups/${groupId}/branches/merge`, payload);
      setSuccess('تم دمج الفرعين — رُبطت ملفات المصدر بالفرع الهدف وعُطّل المصدر');
      setMergeSource('');
      setMergeTarget('');
      clearPreview();
      await reload();
      onCommitted(`تم دمج فرعين في «${groupName}»`);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const abolishCommit = async () => {
    if (abolishId === null) return;
    setBusy(true);
    setError('');
    try {
      const payload: AbolishBranchRequest = {
        targetEntryId: abolishTarget === '' || abolishTarget === abolishId ? null : abolishTarget,
      };
      const res = await api.post<AbolishBranchResponse>(
        `/entity-registry/groups/${groupId}/branches/${abolishId}/abolish`,
        payload,
      );
      setSuccess(
        res.data?.targetEntryId != null
          ? 'تم إلغاء الفرع ونقل ملفاته إلى الفرع الهدف (دمج ضمني)'
          : 'تم إلغاء الفرع — لن يظهر للربط',
      );
      setAbolishId(null);
      setAbolishTarget('');
      clearPreview();
      await reload();
      onCommitted(`تم إلغاء فرع في «${groupName}»`);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const unifySurvivorName = () => {
    const corrected = unifyCorrected.trim();
    if (corrected) return corrected;
    const target = entries.find((e) => e.id === unifyTarget);
    return target?.branchName ?? '';
  };

  const unifyCommit = async () => {
    if (unifyTarget === '' || unifyAbsorbed.length === 0) {
      setError('اختر الفرع الناجي وفرعًا واحدًا على الأقل للامتصاص');
      return;
    }
    const survivor = unifySurvivorName();
    if (unifyConfirmText.trim() !== survivor) {
      setError(`اكتب اسم الفرع الناجي «${survivor}» بالضبط لتأكيد التوحيد`);
      return;
    }
    setBusy(true);
    setError('');
    try {
      const payload: UnifyBranchesRequest = {
        targetEntryId: unifyTarget,
        absorbedEntryIds: unifyAbsorbed,
        correctedName: unifyCorrected.trim() ? unifyCorrected.trim() : null,
      };
      const res = await api.post<UnifyBranchesResponse>(
        `/entity-registry/groups/${groupId}/branches/unify`,
        payload,
      );
      setSuccess(`تم توحيد تسميات ${fCount.format(res.data?.entriesUnified ?? 0)} فروع في «${survivor}»`);
      setUnifyTarget('');
      setUnifyAbsorbed([]);
      setUnifyCorrected('');
      setUnifyConfirmText('');
      clearPreview();
      await reload();
      onCommitted(`تم توحيد تسميات فروع في «${groupName}»`);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const suggestParentEdit = async () => {
    if (!proposeEntry) return;
    if (!pReason.trim()) {
      setError('سبب الاقتراح مطلوب');
      return;
    }
    const payload: SuggestParentEditRequest = {
      proposedCanonicalName: pCanonical.trim() || null,
      proposedEntityType: pType || null,
      proposedCitationFormula: pCitation || null,
      reason: pReason.trim(),
    };
    setBusy(true);
    setError('');
    try {
      await api.post(`/entity-registry/entries/${proposeEntry.id}/suggest-parent-edit`, payload);
      setSuccess('أُرسل اقتراح تعديل الجهة الأم — بانتظار مراجعة الإدارة');
      setProposeEntry(null);
      setPCanonical('');
      setPType('');
      setPCitation('');
      setPReason('');
      await reload();
      onCommitted(`أُرسل اقتراح تعديل الجهة الأم في «${groupName}»`);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const withdrawSuggestion = async () => {
    if (!pendingSuggestion) return;
    setBusy(true);
    setError('');
    try {
      await api.post(`/entity-registry/parent-edit-suggestions/${pendingSuggestion.id}/withdraw`);
      setSuccess('سُحب اقتراح تعديل الجهة الأم');
      await reload();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const previewOk = preview !== null && previewFor !== null && (preview.errors ?? []).length === 0;
  const canCommit = !busy && !previewing && previewOk && previewFor !== null && preview !== null;

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={`إدارة فروع: ${groupName}`}
      style={{ overscrollBehavior: 'contain' }}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-3xl max-h-[85vh] flex flex-col overflow-hidden">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <div className="min-w-0">
            <h3 className="text-lg font-bold text-gray-800">إدارة فروع الجهة</h3>
            <p className="text-xs text-gray-500 mt-0.5 break-words">
              «{groupName}» — محافظتك فقط · تعديل / دمج / إلغاء / توحيد
            </p>
          </div>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 shrink-0 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500 rounded-lg"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="overflow-y-auto p-5 grow overscroll-contain">
          {loading && <p className="text-sm text-gray-500">جارِ تحميل الفروع…</p>}
          {error && <p role="alert" className="text-red-600 text-sm mb-3">{error}</p>}
          {success && <p role="status" className="text-emerald-700 bg-emerald-50 border border-emerald-100 rounded-lg p-2 text-sm mb-3">{success}</p>}

          {!loading && entries.length === 0 && (
            <p className="text-sm text-gray-400 text-center py-8">لا توجد فروع نشطة في محافظتك لهذه الجهة</p>
          )}

          {!loading && entries.length > 0 && (
            <>
              {/* بطاقة الجهة الأم — قراءة فقط + اقتراح تعديل */}
              {parent && (
                <div className="bg-violet-50 border border-violet-200 rounded-xl p-4 mb-5">
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-bold text-gray-800 break-words">{parent.canonicalName}</span>
                        <span className="inline-block bg-violet-700 text-white rounded-full px-2 py-0.5 text-xs">الجهة الأم</span>
                        <span className="inline-block bg-violet-100 text-violet-700 rounded-full px-2 py-0.5 text-xs">قراءة فقط</span>
                      </div>
                      <p className="text-xs text-gray-500 mt-1 break-words">
                        {formatEntityCoverage(parent)}
                        {parent.coverageLabel && <span className="mr-2 text-violet-700">{parent.governorate}</span>}
                      </p>
                      {pendingSuggestion && (
                        <p className="mt-2 flex flex-wrap items-center gap-2 text-xs">
                          <span className="inline-block bg-amber-100 text-amber-800 rounded-full px-2 py-0.5">
                            اقتراحك «{parent.canonicalName}» قيد المراجعة
                          </span>
                        </p>
                      )}
                    </div>
                    <div className="flex flex-wrap gap-2 shrink-0">
                      {pendingSuggestion ? (
                        <>
                          {isEntryPendingReview(parent) && (
                            <span className="inline-block bg-amber-100 text-amber-800 rounded-full px-2 py-0.5 text-xs self-center">بانتظار المراجعة</span>
                          )}
                          <button
                            onClick={withdrawSuggestion}
                            disabled={busy}
                            className="border border-red-200 text-red-700 hover:bg-red-50 disabled:opacity-50 rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
                          >
                            سحب الاقتراح
                          </button>
                        </>
                      ) : (
                        <button
                          onClick={() => {
                            setProposeEntry(parent);
                            setError('');
                            setSuccess('');
                          }}
                          className="bg-violet-700 hover:bg-violet-600 text-white rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-violet-500"
                        >
                          اقتراح تعديل الجهة الأم
                        </button>
                      )}
                    </div>
                  </div>

                  {/* نموذج اقتراح تعديل الجهة الأم */}
                  {proposeEntry && !pendingSuggestion && (
                    <div className="mt-4 bg-white border border-violet-200 rounded-xl p-4">
                      <h4 className="font-bold text-sm text-gray-800 mb-3">اقتراح تعديل بيانات الجهة الأم</h4>
                      <p className="text-xs text-gray-500 mb-3">
                        يُسجَّل اقتراحك للإدارة لمراجعته — لا يُعدَّل اسم الجهة فورًا. بادر بتعيين ما تراه مطلوبًا فقط.
                      </p>
                      <div className="grid sm:grid-cols-2 gap-3">
                        <div className="sm:col-span-2">
                          <label htmlFor="bmp-name" className="block text-xs font-medium text-gray-600 mb-1">الاسم المعتمد المقترح</label>
                          <input
                            id="bmp-name"
                            value={pCanonical}
                            onChange={(e) => setPCanonical(e.target.value)}
                            placeholder="مثال: المديرية العامة للمصارف…"
                            autoComplete="off"
                            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-violet-500"
                          />
                        </div>
                        <div>
                          <label htmlFor="bmp-type" className="block text-xs font-medium text-gray-600 mb-1">نوع الجهة المقترح</label>
                          <select
                            id="bmp-type"
                            value={pType}
                            onChange={(e) => setPType(e.target.value as PublicEntityType | '')}
                            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-violet-500"
                          >
                            <option value="">بدون تغيير…</option>
                            {ENTITY_TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                          </select>
                        </div>
                        <div>
                          <label htmlFor="bmp-citation" className="block text-xs font-medium text-gray-600 mb-1">صيغة ممثلها المقترحة</label>
                          <select
                            id="bmp-citation"
                            value={pCitation}
                            onChange={(e) => setPCitation(e.target.value as CitationFormula | '')}
                            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-violet-500"
                          >
                            <option value="">بدون تغيير…</option>
                            {CITATION_FORMULA_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                          </select>
                        </div>
                        <div className="sm:col-span-2">
                          <label htmlFor="bmp-reason" className="block text-xs font-medium text-gray-600 mb-1">
                            سبب الاقتراح <span className="text-red-600">*</span>
                          </label>
                          <textarea
                            id="bmp-reason"
                            value={pReason}
                            onChange={(e) => setPReason(e.target.value)}
                            rows={2}
                            placeholder="مثال: تغيّر التسمية بموجب النظام الداخلي الجديد…"
                            className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-violet-500"
                          />
                        </div>
                      </div>
                      <div className="flex gap-2 mt-3">
                        <button
                          onClick={suggestParentEdit}
                          disabled={busy}
                          className="bg-violet-700 hover:bg-violet-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-violet-500"
                        >
                          {busy ? 'جارِ الإرسال…' : 'إرسال الاقتراح للإدارة'}
                        </button>
                        <button
                          onClick={() => setProposeEntry(null)}
                          className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
                        >
                          تراجع
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              )}

              {/* قائمة الفروع */}
              <div className="divide-y divide-gray-100 border border-gray-100 rounded-xl overflow-hidden mb-5">
                {activeEntries.map((e) => (
                  <div key={e.id} className="p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                    <div className="min-w-0">
                      <div className="font-medium text-gray-800 break-words">{e.branchName}</div>
                      <div className="text-xs text-gray-500 mt-0.5">
                        {formatEntityCoverage(e)}
                        {e.coverageLabel && <span className="mr-2">{e.governorate}</span>}
                      </div>
                      {isEntryPendingReview(e) && <span className="inline-block mt-1 bg-amber-100 text-amber-800 rounded-full px-2 py-0.5 text-xs">بانتظار المراجعة</span>}
                    </div>
                    <div className="flex flex-wrap gap-2 shrink-0">
                      <button
                        onClick={() => startEdit(e)}
                        className="border border-sky-200 text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-sky-500"
                      >
                        تعديل التسمية
                      </button>
                      <button
                        onClick={() => {
                          setAbolishId(e.id);
                          setAbolishTarget('');
                          setError('');
                          setSuccess('');
                          clearPreview();
                        }}
                        className="border border-red-200 text-red-700 hover:bg-red-50 rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
                      >
                        إلغاء
                      </button>
                    </div>
                  </div>
                ))}
              </div>

              {/* تعديل تسمية فرع */}
              {editingId !== null && (
                <div className="bg-gray-50 border border-gray-200 rounded-xl p-4 mb-4">
                  <h4 className="font-bold text-sm text-gray-800 mb-1">تعديل تسمية الفرع</h4>
                  <p className="text-xs text-gray-500 mb-3">تُحدَّث التسمية في كل الملفات المرتبطة (النشطة والمشطوبة والتريث والاستئنافات) فورًا.</p>
                  <label htmlFor="bm-branch" className="block text-xs font-medium text-gray-600 mb-1">اسم الفرع الجديد</label>
                  <input
                    id="bm-branch"
                    value={editBranch}
                    onChange={(e) => { setEditBranch(e.target.value); clearPreview(); }}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  />
                  <label className="inline-flex items-center gap-2 text-sm cursor-pointer mt-3 min-h-11">
                    <input
                      type="checkbox"
                      checked={editShowCoverage}
                      onChange={(ev) => { setEditShowCoverage(ev.target.checked); if (!ev.target.checked) setEditCoverage(''); clearPreview(); }}
                      className="h-4 w-4"
                    />
                    تغطية أكثر من محافظة
                  </label>
                  {editShowCoverage && (
                    <input
                      value={editCoverage}
                      onChange={(e) => { setEditCoverage(e.target.value); clearPreview(); }}
                      placeholder="مثال: دمشق وريفها"
                      maxLength={150}
                      className="mt-2 w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    />
                  )}
                  <div className="flex flex-wrap gap-2 mt-3">
                    <button
                      onClick={renamePreview}
                      disabled={busy || previewing}
                      className="border border-emerald-300 text-emerald-800 hover:bg-emerald-50 disabled:opacity-50 rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
                    >
                      {previewing && previewFor === 'rename' ? 'جارِ المعاينة…' : 'معاينة'}
                    </button>
                    {canCommit && previewFor === 'rename' && (
                      <button
                        onClick={renameCommit}
                        disabled={busy}
                        className="bg-emerald-700 hover:bg-emerald-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
                      >
                        {busy ? 'جارِ الحفظ…' : 'حفظ التعديل وإقفال المراجعة'}
                      </button>
                    )}
                    <button
                      onClick={() => { setEditingId(null); clearPreview(); }}
                      className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
                    >
                      إلغاء
                    </button>
                  </div>
                  {preview && previewFor === 'rename' && <BranchPreviewPanel preview={preview} />}
                </div>
              )}

              {/* دمج فرعين */}
              <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 mb-4">
                <h4 className="font-bold text-sm text-gray-800 mb-2">دمج فرعين</h4>
                <p className="text-xs text-gray-500 mb-3">انقل روابط فرع مصدر إلى فرع هدف ضمن نفس الجهة — يُلغى المصدر.</p>
                <div className="grid sm:grid-cols-2 gap-3">
                  <div>
                    <label htmlFor="bm-src" className="block text-xs font-medium text-gray-600 mb-1">الفرع المصدر (سيُلغى)</label>
                    <select
                      id="bm-src"
                      value={mergeSource}
                      onChange={(e) => { setMergeSource(e.target.value ? Number(e.target.value) : ''); clearPreview(); }}
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-amber-500"
                    >
                      <option value="">اختر المصدر…</option>
                      {activeEntries.map((e) => <option key={e.id} value={e.id}>{e.branchName} — {e.governorate}</option>)}
                    </select>
                  </div>
                  <div>
                    <label htmlFor="bm-tgt" className="block text-xs font-medium text-gray-600 mb-1">الفرع الهدف (يبقى)</label>
                    <select
                      id="bm-tgt"
                      value={mergeTarget}
                      onChange={(e) => { setMergeTarget(e.target.value ? Number(e.target.value) : ''); clearPreview(); }}
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-amber-500"
                    >
                      <option value="">اختر الهدف…</option>
                      {activeEntries.filter((e) => e.id !== mergeSource).map((e) => <option key={e.id} value={e.id}>{e.branchName} — {e.governorate}</option>)}
                    </select>
                  </div>
                </div>
                <div className="flex flex-wrap gap-2 mt-3">
                  <button
                    onClick={() => {
                      if (mergeSource === '' || mergeTarget === '' || mergeSource === mergeTarget) {
                        setError('اختر فرعًا مصدرًا وفرعًا هدفًا مختلفين');
                        return;
                      }
                      void runPreview('merge', { action: 'merge', entryId: mergeSource, targetId: mergeTarget });
                    }}
                    disabled={busy || previewing || mergeSource === '' || mergeTarget === ''}
                    className="border border-amber-300 text-amber-800 hover:bg-amber-50 disabled:opacity-50 rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-amber-500"
                  >
                    {previewing && previewFor === 'merge' ? 'جارِ المعاينة…' : 'معاينة الدمج'}
                  </button>
                  {canCommit && previewFor === 'merge' && (
                    <button
                      onClick={mergeCommit}
                      disabled={busy}
                      className="bg-amber-700 hover:bg-amber-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-amber-500"
                    >
                      {busy ? 'جارِ الدمج…' : 'تنفيذ الدمج'}
                    </button>
                  )}
                </div>
                {preview && previewFor === 'merge' && <BranchPreviewPanel preview={preview} />}
              </div>

              {/* إلغاء فرع */}
              {abolishId !== null && (
                <div className="bg-red-50 border border-red-200 rounded-xl p-4 mb-4">
                  <h4 className="font-bold text-sm text-red-900 mb-2">إلغاء فرع</h4>
                  <p className="text-sm text-red-800 mb-3">
                    فرع به ملفات مرتبطة يتطلب اختيار فرع هدف يُنقل إليه (دمج ضمني) — الإلغاء المباشر مسموح لفرع بلا روابط فقط.
                  </p>
                  <label htmlFor="bm-abolish-target" className="block text-xs font-medium text-red-800 mb-1">
                    فرع الهدف (اختياري — يُنقل إليه الملفات)
                  </label>
                  <select
                    id="bm-abolish-target"
                    value={abolishTarget}
                    onChange={(e) => { setAbolishTarget(e.target.value ? Number(e.target.value) : ''); clearPreview(); }}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-red-500"
                  >
                    <option value="">بلا هدف (إلغاء مباشر)…</option>
                    {activeEntries.filter((e) => e.id !== abolishId).map((e) => <option key={e.id} value={e.id}>{e.branchName} — {e.governorate}</option>)}
                  </select>
                  <div className="flex flex-wrap gap-2 mt-3">
                    <button
                      onClick={() => {
                        void runPreview('abolish', { action: 'abolish', entryId: abolishId, targetId: abolishTarget === '' ? null : abolishTarget });
                      }}
                      disabled={busy || previewing}
                      className="border border-red-300 text-red-800 hover:bg-red-50 disabled:opacity-50 rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
                    >
                      {previewing && previewFor === 'abolish' ? 'جارِ المعاينة…' : 'معاينة الإلغاء'}
                    </button>
                    {canCommit && previewFor === 'abolish' && (
                      <button
                        onClick={abolishCommit}
                        disabled={busy}
                        className="bg-red-700 hover:bg-red-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
                      >
                        {busy ? 'جارِ الإلغاء…' : 'تأكيد الإلغاء'}
                      </button>
                    )}
                    <button
                      onClick={() => { setAbolishId(null); setAbolishTarget(''); clearPreview(); }}
                      className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
                    >
                      تراجع
                    </button>
                  </div>
                  {preview && previewFor === 'abolish' && <BranchPreviewPanel preview={preview} />}
                </div>
              )}

              {/* توحيد تسميات فروع */}
              {activeEntries.length >= 2 && (
                <div className="bg-sky-50 border border-sky-200 rounded-xl p-4">
                  <h4 className="font-bold text-sm text-gray-800 mb-2">توحيد تسميات فروع</h4>
                  <p className="text-xs text-gray-500 mb-3">
                    اجمع عدة فروع في فرع ناجٍ واحد (اختياريًا مع تصحيح كتابة اسمه) في معاملة واحدة.
                  </p>
                  <div className="grid sm:grid-cols-2 gap-3">
                    <div>
                      <label htmlFor="bm-unify-target" className="block text-xs font-medium text-gray-600 mb-1">الفرع الناجي (يبقى)</label>
                      <select
                        id="bm-unify-target"
                        value={unifyTarget}
                        onChange={(e) => { setUnifyTarget(e.target.value ? Number(e.target.value) : ''); clearPreview(); }}
                        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-sky-500"
                      >
                        <option value="">اختر الناجي…</option>
                        {activeEntries.map((e) => <option key={e.id} value={e.id}>{e.branchName} — {e.governorate}</option>)}
                      </select>
                    </div>
                    <div>
                      <label htmlFor="bm-unify-absorbed" className="block text-xs font-medium text-gray-600 mb-1">الفروع الممتصة (تُلغى)</label>
                      <select
                        id="bm-unify-absorbed"
                        multiple
                        value={unifyAbsorbed.map(String)}
                        onChange={(e) => {
                          const picked = Array.from(e.target.selectedOptions).map((o) => Number(o.value));
                          setUnifyAbsorbed(picked);
                          clearPreview();
                        }}
                        className="w-full min-h-32 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-sky-500"
                      >
                        {activeEntries.filter((e) => e.id !== unifyTarget).map((e) => <option key={e.id} value={e.id}>{e.branchName} — {e.governorate}</option>)}
                      </select>
                      <p className="text-[11px] text-gray-400 mt-1">اضغط مع Ctrl لتحديد أكثر من فرع</p>
                    </div>
                  </div>
                  <div className="mt-3">
                    <label htmlFor="bm-unify-corrected" className="block text-xs font-medium text-gray-600 mb-1">تصحيح كتابة اسم الناجي (اختياري)</label>
                    <input
                      id="bm-unify-corrected"
                      value={unifyCorrected}
                      onChange={(e) => { setUnifyCorrected(e.target.value); setUnifyConfirmText(''); clearPreview(); }}
                      placeholder={unifyTarget !== '' ? `اسم الناجي الحالي: ${entries.find((e) => e.id === unifyTarget)?.branchName ?? ''}` : 'اكتب الكتابة الصحيحة…'}
                      autoComplete="off"
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-500"
                    />
                  </div>
                  {preview && previewFor === 'unify' && (
                    <div className="mt-3">
                      <label htmlFor="bm-unify-confirm" className="block text-xs font-medium text-gray-600 mb-1">
                        اكتب اسم الناجي بالضبط لتأكيد التوحيد: «{unifySurvivorName()}»
                      </label>
                      <input
                        id="bm-unify-confirm"
                        value={unifyConfirmText}
                        onChange={(e) => setUnifyConfirmText(e.target.value)}
                        autoComplete="off"
                        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-500"
                      />
                    </div>
                  )}
                  <div className="flex flex-wrap gap-2 mt-3">
                    <button
                      onClick={() => {
                        if (unifyTarget === '' || unifyAbsorbed.length === 0) {
                          setError('اختر الفرع الناجي وفرعًا واحدًا على الأقل للامتصاص');
                          return;
                        }
                        void runPreview('unify', {
                          action: 'unify',
                          entryId: unifyTarget,
                          absorbedIds: unifyAbsorbed,
                          correctedName: unifyCorrected.trim() || null,
                        });
                      }}
                      disabled={busy || previewing || unifyTarget === '' || unifyAbsorbed.length === 0}
                      className="border border-sky-300 text-sky-800 hover:bg-sky-50 disabled:opacity-50 rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-sky-500"
                    >
                      {previewing && previewFor === 'unify' ? 'جارِ المعاينة…' : 'معاينة التوحيد'}
                    </button>
                    {canCommit && previewFor === 'unify' && unifyConfirmText.trim() === unifySurvivorName() && (
                      <button
                        onClick={unifyCommit}
                        disabled={busy}
                        className="bg-sky-700 hover:bg-sky-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-sky-500"
                      >
                        {busy ? 'جارِ التوحيد…' : 'تنفيذ التوحيد'}
                      </button>
                    )}
                  </div>
                  {preview && previewFor === 'unify' && <BranchPreviewPanel preview={preview} />}
                </div>
              )}
            </>
          )}
        </div>

        <div className="px-5 py-4 border-t border-gray-100 flex justify-end">
          <button
            onClick={onClose}
            className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            إغلاق
          </button>
        </div>
      </div>
    </div>
  );
}

/** لوحة نتائج المعاينة الموحّدة — تُعرض بعد طلب المعاينة وقبل زر التنفيذ. */
function BranchPreviewPanel({ preview }: { preview: BranchActionPreviewResponse }) {
  const hasErrors = (preview.errors ?? []).length > 0;
  return (
    <div className="mt-3 bg-white border border-gray-200 rounded-lg p-3 text-sm" role="status">
      {hasErrors && (
        <p role="alert" className="text-red-700 bg-red-50 border border-red-100 rounded-lg p-2 mb-2">
          {(preview.errors ?? []).join(' — ')}
        </p>
      )}
      {preview.summary && (
        <p className="text-gray-700 break-words">{preview.summary}</p>
      )}
      {(preview.warnings ?? []).length > 0 && (
        <ul className="mt-2 space-y-1">
          {(preview.warnings ?? []).map((w) => (
            <li key={w} className="text-amber-700 text-xs bg-amber-50 rounded-lg px-2 py-1 break-words">
              {w}
            </li>
          ))}
        </ul>
      )}
      {(preview.entries ?? []).length > 0 && (
        <ul className="mt-2 space-y-1">
          {(preview.entries ?? []).map((e: BranchPreviewEntryDto) => (
            <li key={e.entryId} className="flex flex-wrap items-center justify-between gap-1 text-xs text-gray-600">
              <span className="min-w-0 break-words">{e.branchName} — {e.governorate}</span>
              <span className="tabular-nums whitespace-nowrap">{fCount.format(e.documentCount)} ملف</span>
            </li>
          ))}
        </ul>
      )}
      <p className="mt-2 font-medium text-gray-800 tabular-nums">
        إجمالي الملفات المتأثرة: {fCount.format(preview.totalAffectedDocuments)}
      </p>
    </div>
  );
}