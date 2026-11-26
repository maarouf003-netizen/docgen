import { useCallback, useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../../../api/client';
import { branchCountFormatter } from './branchManagementFormat';
import type {
  AbolishBranchRequest,
  AbolishBranchResponse,
  BranchActionKind,
  BranchActionPreviewResponse,
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
} from '../../../types';

const fCount = branchCountFormatter;

/**
 * حالة وقيادة نافذة إدارة فروع الجهة: تحميل القيود والاقتراح المعلّق، ومعاينات
 * الأفعال الأربعة (تعديل/دمج/إلغاء/توحيد) ثم تنفيذها، واقتراح تعديل الجهة الأم
 * وسحبه. الواجهة في `BranchManagementModal` ولوحاتها، والقواعد هنا.
 */
export function useBranchManagement({
  groupId,
  groupName,
  onCommitted,
}: {
  groupId: number;
  groupName: string;
  onCommitted: (summary: string) => void;
}) {
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

  const openPropose = (parentEntry: PublicEntityEntryDto) => {
    setProposeEntry(parentEntry);
    setError('');
    setSuccess('');
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

  return {
    entries,
    loading,
    error,
    success,
    pendingSuggestion,
    editingId,
    editBranch,
    editCoverage,
    editShowCoverage,
    mergeSource,
    mergeTarget,
    abolishId,
    abolishTarget,
    unifyTarget,
    unifyAbsorbed,
    unifyCorrected,
    unifyConfirmText,
    proposeEntry,
    pCanonical,
    pType,
    pCitation,
    pReason,
    preview,
    previewFor,
    previewing,
    busy,
    parent,
    activeEntries,
    canCommit,
    setError,
    setSuccess,
    setEditingId,
    setEditBranch,
    setEditCoverage,
    setEditShowCoverage,
    setMergeSource,
    setMergeTarget,
    setAbolishId,
    setAbolishTarget,
    setUnifyTarget,
    setUnifyAbsorbed,
    setUnifyCorrected,
    setUnifyConfirmText,
    setProposeEntry,
    setPCanonical,
    setPType,
    setPCitation,
    setPReason,
    startEdit,
    openPropose,
    clearPreview,
    runPreview,
    renamePreview,
    renameCommit,
    mergeCommit,
    abolishCommit,
    unifyCommit,
    unifySurvivorName,
    suggestParentEdit,
    withdrawSuggestion,
  };
}

export type BranchManagementModel = ReturnType<typeof useBranchManagement>;
