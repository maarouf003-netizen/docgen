import ExecutionActionsModal from '../../components/ExecutionActionsModal';
import ExecutedStatusModal from '../../components/ExecutedStatusModal';
import StatusChangeModal from '../../components/StatusChangeModal';
import TransferDocumentModal from '../../components/TransferDocumentModal';
import FileAlertModal from '../../components/FileAlertModal';
import BaseNumbersModal from '../../components/BaseNumbersModal';
import DelegationFormModal from '../../components/delegation/DelegationFormModal';
import RegisterDelegationModal from '../../components/delegation/RegisterDelegationModal';
import CompleteDelegationModal from '../../components/delegation/CompleteDelegationModal';
import AppealFormModal from '../../components/appeal/AppealFormModal';
import AppealInfoModal from '../../components/appeal/AppealInfoModal';
import DocumentGenerationModal from '../../components/view/DocumentGenerationModal';
import { OccurrencesModal } from '../../components/view/OccurrencesModal';
import { PartyDetailsModal } from '../../components/view/PartyDetailsModal';
import { InterventionDetailsModal } from '../../components/view/InterventionDetailsModal';
import { TransferHistoryModal } from '../../components/view/TransferHistoryModal';
import DocumentChangesModal from '../../components/view/DocumentChangesModal';
import type { DocumentViewModalState } from './useDocumentViewModals';
import type { AssetDto, DelegationDto, DocumentResponse } from '../../types';

interface DocumentViewModalsProps {
  modals: DocumentViewModalState;
  doc: DocumentResponse;
  id: string | undefined;
  isExecuted: boolean;
  debtorFullName: string;
  load: () => void;
  loadDelegations: () => void;
  loadAppeals: () => void;
  formAvailableAssets: AssetDto[];
  formNoAvailableAssets: boolean;
  delegationOfThisFile: DelegationDto | undefined;
  onConfirmDelete: () => void;
}

/**
 * نوافذ صفحة عرض المستند (إجراءات/حالة/نقل/تنبيه/أرقام أساس/تاريخ/وقوعات/
 * استئناف/إنابة/حذف إنابة) بشروط فتحها نفسها وترتيبها السابق.
 */
export function DocumentViewModals({
  modals,
  doc,
  id,
  isExecuted,
  debtorFullName,
  load,
  loadDelegations,
  loadAppeals,
  formAvailableAssets,
  formNoAvailableAssets,
  delegationOfThisFile,
  onConfirmDelete,
}: DocumentViewModalsProps) {
  const documentTitle = debtorFullName || doc.documentType || `مستند #${doc.id}`;

  return (
    <>
      {modals.actionsOpen && id !== undefined && (
        <ExecutionActionsModal
          documentId={Number(id)}
          onClose={() => modals.setActionsOpen(false)}
          onChanged={load}
        />
      )}

      {modals.changesOpen && id !== undefined && (
        <DocumentChangesModal
          documentId={Number(id)}
          onClose={() => modals.setChangesOpen(false)}
        />
      )}

      {modals.generationOpen && id !== undefined && (
        <DocumentGenerationModal doc={doc} id={id} onClose={() => modals.setGenerationOpen(false)} />
      )}

      {modals.statusOpen && (isExecuted ? (
        <ExecutedStatusModal doc={doc} onClose={() => modals.setStatusOpen(false)} onChanged={load} />
      ) : (
        <StatusChangeModal doc={doc} onClose={() => modals.setStatusOpen(false)} onChanged={load} />
      ))}

      {modals.transferOpen && id !== undefined && (
        <TransferDocumentModal
          documentId={Number(id)}
          currentOwnerId={doc.createdById}
          onClose={() => modals.setTransferOpen(false)}
          onTransferred={load}
        />
      )}

      {modals.alertOpen && id !== undefined && (
        <FileAlertModal
          documentId={Number(id)}
          documentTitle={documentTitle}
          recipientName={doc.lawyer}
          onClose={() => modals.setAlertOpen(false)}
        />
      )}

      {modals.historyOpen && id !== undefined && (
        <BaseNumbersModal
          documentId={Number(id)}
          documentTitle={documentTitle}
          fileType={doc.fileType}
          onClose={() => modals.setHistoryOpen(false)}
        />
      )}

      {modals.assignmentsOpen && (
        <TransferHistoryModal
          assignments={doc.assignments ?? []}
          onClose={() => modals.setAssignmentsOpen(false)}
        />
      )}

      {modals.occurrencesOpen && (
        <OccurrencesModal
          documentTitle={documentTitle}
          occurrences={doc.occurrences ?? []}
          interventions={doc.interventions ?? []}
          onOpenIntervention={(intervention) => {
            modals.setOccurrencesOpen(false);
            modals.setInterventionModal(intervention);
          }}
          onClose={() => modals.setOccurrencesOpen(false)}
        />
      )}

      {modals.partyModal && <PartyDetailsModal modal={modals.partyModal} onClose={() => modals.setPartyModal(null)} />}

      {modals.interventionModal && (
        <InterventionDetailsModal
          intervention={modals.interventionModal}
          onClose={() => modals.setInterventionModal(null)}
        />
      )}

      {modals.appealFormVariant && (
        <AppealFormModal
          doc={doc}
          variant={modals.appealFormVariant}
          onClose={() => modals.setAppealFormVariant(null)}
          onSaved={() => {
            modals.setAppealFormVariant(null);
            loadAppeals();
          }}
        />
      )}

      <AppealInfoModal appeal={modals.infoAppeal} onClose={() => modals.setInfoAppeal(null)} />

      {modals.delegationFormOpen && id !== undefined && (
        <DelegationFormModal
          documentId={Number(id)}
          documentTitle={documentTitle}
          assets={formAvailableAssets}
          noAvailableAssets={formNoAvailableAssets}
          initial={modals.editingDelegation}
          onClose={() => {
            modals.setDelegationFormOpen(false);
            modals.setEditingDelegation(null);
          }}
          onSaved={() => {
            modals.setDelegationFormOpen(false);
            modals.setEditingDelegation(null);
            loadDelegations();
          }}
        />
      )}

      {modals.registerOpen && delegationOfThisFile && (
        <RegisterDelegationModal
          delegation={delegationOfThisFile}
          onClose={() => modals.setRegisterOpen(false)}
          onRegistered={() => {
            modals.setRegisterOpen(false);
            loadDelegations();
            load();
          }}
        />
      )}

      {modals.completeOpen && delegationOfThisFile && (
        <CompleteDelegationModal
          delegation={delegationOfThisFile}
          onClose={() => modals.setCompleteOpen(false)}
          onCompleted={() => {
            modals.setCompleteOpen(false);
            loadDelegations();
            load();
          }}
        />
      )}

      {modals.deleteTarget && (
        <div
          className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
          dir="rtl"
          role="dialog"
          aria-modal="true"
          aria-label="تأكيد حذف الإنابة"
        >
          <div className="bg-white rounded-xl shadow-xl w-full max-w-sm p-5">
            <h3 className="text-lg font-bold text-gray-800 mb-2">حذف الإنابة</h3>
            <p className="text-sm text-red-700 mb-4">
              هل أنت متأكد من حذف الإنابة إلى دائرة {modals.deleteTarget.delegatedCourt || 'غير محددة'}؟
            </p>
            <div className="flex justify-end gap-2">
              <button
                onClick={() => modals.setDeleteTarget(null)}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
              >
                إلغاء
              </button>
              <button
                onClick={onConfirmDelete}
                disabled={modals.deleting}
                className="bg-red-700 hover:bg-red-800 text-white rounded-lg px-4 py-2 text-sm min-h-11 disabled:opacity-50"
              >
                {modals.deleting ? 'جارِ الحذف...' : 'تأكيد الحذف'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
