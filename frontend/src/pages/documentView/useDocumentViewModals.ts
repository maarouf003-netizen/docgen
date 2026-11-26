import { useEffect, useState } from 'react';
import type { AppealDirection, AppealDto, DelegationDto, DocumentInterventionDto } from '../../types';
import type { PartyModal } from '../../components/view/viewTypes';

/**
 * حالة نوافذ صفحة عرض المستند (فتح/إغلاق + النوافذ الفرعية للاستئناف والإنابة).
 * مفصولة عن الصفحة لتقليل حجمها، والسلوك منقول حرفيًا: لا نافذة تُفتح تلقائيًا
 * ولا تُترك مفتوحة عند تبديل الملف (`id`).
 */
export function useDocumentViewModals(id: string | undefined) {
  const [actionsOpen, setActionsOpen] = useState(false);
  const [statusOpen, setStatusOpen] = useState(false);
  const [transferOpen, setTransferOpen] = useState(false);
  const [alertOpen, setAlertOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [changesOpen, setChangesOpen] = useState(false);
  const [occurrencesOpen, setOccurrencesOpen] = useState(false);
  const [assignmentsOpen, setAssignmentsOpen] = useState(false);
  const [generationOpen, setGenerationOpen] = useState(false);
  const [partyModal, setPartyModal] = useState<PartyModal | null>(null);
  const [interventionModal, setInterventionModal] = useState<DocumentInterventionDto | null>(null);
  const [delegationFormOpen, setDelegationFormOpen] = useState(false);
  const [editingDelegation, setEditingDelegation] = useState<DelegationDto | null>(null);
  const [registerOpen, setRegisterOpen] = useState(false);
  const [completeOpen, setCompleteOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<DelegationDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  // الاستئنافات: اتجاه نموذج التسطير المفتوح، ونافذة تفاصيل الاستئناف.
  const [appealFormVariant, setAppealFormVariant] = useState<AppealDirection | null>(null);
  const [infoAppeal, setInfoAppeal] = useState<AppealDto | null>(null);

  // الانتقال بين ملفين يعيد استخدام نفس الصفحة: تُغلق واجهات الاستئناف المعلّقة
  // حتى لا تبقى مفتوحة على محتوى ملف سابق.
  useEffect(() => {
    setAppealFormVariant(null);
    setInfoAppeal(null);
    // oxlint: التصفير عند تبديل الملف فقط (id)، لا عند تغيّر قوائم الاستئناف.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  return {
    actionsOpen,
    setActionsOpen,
    statusOpen,
    setStatusOpen,
    transferOpen,
    setTransferOpen,
    alertOpen,
    setAlertOpen,
    historyOpen,
    setHistoryOpen,
    changesOpen,
    setChangesOpen,
    occurrencesOpen,
    setOccurrencesOpen,
    assignmentsOpen,
    setAssignmentsOpen,
    generationOpen,
    setGenerationOpen,
    partyModal,
    setPartyModal,
    interventionModal,
    setInterventionModal,
    delegationFormOpen,
    setDelegationFormOpen,
    editingDelegation,
    setEditingDelegation,
    registerOpen,
    setRegisterOpen,
    completeOpen,
    setCompleteOpen,
    deleteTarget,
    setDeleteTarget,
    deleting,
    setDeleting,
    appealFormVariant,
    setAppealFormVariant,
    infoAppeal,
    setInfoAppeal,
  };
}

export type DocumentViewModalState = ReturnType<typeof useDocumentViewModals>;
