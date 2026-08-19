import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { getDocumentBadge, EXEC_STATUS_FORCIBLY, EXEC_STATUS_SETTLED, EXEC_STATUS_STRUCK_OFF, EXEC_STATUS_DELEGATION_EXECUTED } from '../utils/documentStatus';
import { isExecutedLike } from '../utils/documentDisplay';
import { DELEGATION_STATUS_ASSIGNED, DELEGATION_STATUS_REGISTERED } from '../utils/delegationStatus';
import { saveLastViewedDocumentId } from '../utils/listSession';
import ExecutionActionsModal from '../components/ExecutionActionsModal';
import ExecutedStatusModal from '../components/ExecutedStatusModal';
import StatusChangeModal from '../components/StatusChangeModal';
import TransferDocumentModal from '../components/TransferDocumentModal';
import FileAlertModal from '../components/FileAlertModal';
import BaseNumbersModal from '../components/BaseNumbersModal';
import DelegationFormModal from '../components/delegation/DelegationFormModal';
import RegisterDelegationModal from '../components/delegation/RegisterDelegationModal';
import CompleteDelegationModal from '../components/delegation/CompleteDelegationModal';
import { DelegationsCard } from '../components/delegation/DelegationsCard';
import { SourceFileInfoCard } from '../components/delegation/SourceFileInfoCard';
import type { DelegationDto, DocumentResponse } from '../types';
import DocumentGenerationModal from '../components/view/DocumentGenerationModal';
import { ExecutoryDocumentCard } from '../components/view/ExecutoryDocumentCard';
import { FileDataCard } from '../components/view/FileDataCard';
import { OccurrencesCard } from '../components/view/OccurrencesCard';
import { OccurrencesModal } from '../components/view/OccurrencesModal';
import { PartiesCard } from '../components/view/PartiesCard';
import { PartyDetailsModal } from '../components/view/PartyDetailsModal';
import { AssetsSection } from '../components/view/AssetsSection';
import { TransferHistoryModal } from '../components/view/TransferHistoryModal';
import { executedTitle, fullName } from '../components/view/viewFormat';
import { StatusCard } from '../components/view/StatusCard';
import type { PartyModal } from '../components/view/viewTypes';

export default function DocumentView() {
  const { id } = useParams();
  const { user } = useAuth();
  const [doc, setDoc] = useState<DocumentResponse | null>(null);
  const [error, setError] = useState('');
  const [actionsOpen, setActionsOpen] = useState(false);
  const [statusOpen, setStatusOpen] = useState(false);
  const [transferOpen, setTransferOpen] = useState(false);
  const [alertOpen, setAlertOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [occurrencesOpen, setOccurrencesOpen] = useState(false);
  const [assignmentsOpen, setAssignmentsOpen] = useState(false);
  const [generationOpen, setGenerationOpen] = useState(false);
  const [partyModal, setPartyModal] = useState<PartyModal | null>(null);
  const [delegations, setDelegations] = useState<DelegationDto[]>([]);
  const [delegationFormOpen, setDelegationFormOpen] = useState(false);
  const [editingDelegation, setEditingDelegation] = useState<DelegationDto | null>(null);
  const [registerOpen, setRegisterOpen] = useState(false);
  const [completeOpen, setCompleteOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<DelegationDto | null>(null);
  const [deleting, setDeleting] = useState(false);

  const load = () => {
    api
      .get<DocumentResponse>(`/documents/${id}`)
      .then((r) => {
        setDoc(r.data);
        setError('');
      })
      .catch((err) => setError(getApiErrorMessage(err)));
  };

  const loadDelegations = () => {
    api
      .get<DelegationDto[]>(`/documents/${id}/delegations`)
      .then((r) => setDelegations(Array.isArray(r.data) ? r.data : []))
      .catch(() => setDelegations([]));
  };

  useEffect(() => {
    load();
    loadDelegations();
    // يُسجَّل الملف كآخر ما فُتح في الجلسة ليُميَّز في القائمة عند العودة (حتى لو فُتح من غير القائمة).
    if (id) saveLastViewedDocumentId(Number(id));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  if (error) return <div className="text-red-600">{error}</div>;
  if (!doc) return <div className="text-gray-500">جارِ التحميل...</div>;

  const canEdit = user?.role === 'lawyer';
  const canTransfer = user?.role === 'head';
  const canDirectAlert = user?.role === 'head';
  const isExecuted = isExecutedLike(doc.generalEntitySide);
  const isLawyer = user?.role === 'lawyer';
  // «منفذ إنابة» (الملف المناب عند إتمام الإنابة): حالة نهائية تُعامل منفذًا — لا توليد
  // مستندات ولا تغيير حالة بعدها (الخلفية تراقب أيضًا عبر آلة الحالات).
  const isDelegationExecuted = doc.execStatus === EXEC_STATUS_DELEGATION_EXECUTED;
  // «الفرع» يظهر للمدير والمشرف فقط؛ و«المحامي المختص» يظهر للمدير والمشرف ورئيس القسم
  // (لا يظهر للمحامي المختص نفسه الذي يرى ملفه من صفحة أخرى).
  const showBranch = user?.role === 'admin' || user?.role === 'manager';
  const showLawyer = user?.role === 'admin' || user?.role === 'manager' || user?.role === 'head';

  const debtor = {
    name: doc.borrowerName,
    father: doc.borrowerFather,
    family: doc.borrowerFamily,
    mother: doc.borrowerMother,
    birth: doc.borrowerBirth,
    register: doc.borrowerRegister,
    nationalId: doc.borrowerNationalId,
    addressType: doc.borrowerAddressType,
    address: doc.borrowerAddress,
  };
  const debtorFullName = fullName(debtor);
  const statusBadge = getDocumentBadge(doc);
  // الإنابة: يعرض الملف المناب بطاقة «معلومات الملف المنيب» (إنابته الخاصة)، والملف المنيب
  // بطاقة «تشعبات الملف» (إناباته الصادرة) — وكلتاهما من نقطة الإنابات نفسها.
  const delegationOfThisFile = delegations.find((d) => d.targetDocumentId === doc.id);
  // تسطير الإنابة من محامي الملف المالك على ملف «طالبة تنفيذ» متداول غير منفذ/مشطوب
  // (نفس شروط الخلفية: ValidateSourceForDelegation).
  const isOwner = doc.createdById != null && doc.createdById === user?.id;
  const canCreateDelegation =
    canEdit &&
    isOwner &&
    !isExecuted &&
    doc.execStatus !== EXEC_STATUS_FORCIBLY &&
    doc.execStatus !== EXEC_STATUS_SETTLED &&
    doc.execStatus !== EXEC_STATUS_STRUCK_OFF;
  const showDelegationsCard = delegations.length > 0 || canCreateDelegation;
  // متابعة الإنابة من محامي الملف المناب: «تسجيل أصولًا» بعد الاعتماد، ثم «إتمام الإنابة»
  // بعد التسجيل أصولًا (نفس شروط الخلفية: RegisterAsync/CompleteAsync).
  const canRegisterDelegation =
    canEdit &&
    isOwner &&
    delegationOfThisFile != null &&
    delegationOfThisFile.status === DELEGATION_STATUS_ASSIGNED;
  const canCompleteDelegation =
    canEdit &&
    isOwner &&
    delegationOfThisFile != null &&
    delegationOfThisFile.status === DELEGATION_STATUS_REGISTERED;

  const openCreateDelegation = () => {
    setEditingDelegation(null);
    setDelegationFormOpen(true);
  };

  const openEditDelegation = (d: DelegationDto) => {
    setEditingDelegation(d);
    setDelegationFormOpen(true);
  };

  const confirmDeleteDelegation = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await api.delete(`/delegations/${deleteTarget.id}`);
      setDeleteTarget(null);
      loadDelegations();
    } catch (err) {
      setDeleteTarget(null);
      setError(getApiErrorMessage(err));
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-2xl font-bold text-gray-800 flex items-center gap-3">
          <span className={`rounded-full px-3 py-1 text-sm ${statusBadge.cls}`}>
            {statusBadge.text}
          </span>
          <span>{isExecuted ? executedTitle(doc) : debtorFullName || doc.documentType || `مستند #${doc.id}`}</span>
        </h2>
        <div className="flex gap-2 flex-wrap">
          {canEdit && (
            <Link to={`/documents/${id}/edit`} className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm inline-flex items-center min-h-11">
              تعديل
            </Link>
          )}
          {!isExecuted && !isDelegationExecuted && (
            <button
              onClick={() => setGenerationOpen(true)}
              className="bg-gray-800 hover:bg-gray-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              توليد مستندات
            </button>
          )}
          <button
            onClick={() => setActionsOpen(true)}
            className="bg-[#800000] hover:bg-[#9e0e0e] text-white rounded-lg px-4 py-2 text-sm min-h-11"
          >
            الإجراءات والملاحظات
          </button>
          {canDirectAlert && (
            <button
              onClick={() => setAlertOpen(true)}
              className="bg-red-600 hover:bg-red-500 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              توجيه تنبيه
            </button>
          )}
          {canTransfer && (
            <button
              onClick={() => setTransferOpen(true)}
              className="bg-sky-800 hover:bg-sky-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              نقل الملف
            </button>
          )}
          <Link to="/documents" className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 inline-flex items-center min-h-11">
            عودة
          </Link>
        </div>
      </div>

      <div className="grid md:grid-cols-2 gap-6 items-start">
        <PartiesCard doc={doc} onOpen={setPartyModal} />
        <FileDataCard
          doc={doc}
          isLawyer={isLawyer}
          showBranch={showBranch}
          showLawyer={showLawyer}
          onOpenBaseNumbers={() => setHistoryOpen(true)}
          onOpenAssignments={() => setAssignmentsOpen(true)}
        />
        <ExecutoryDocumentCard doc={doc} />
        {delegationOfThisFile ? (
          <SourceFileInfoCard
            delegation={delegationOfThisFile}
            canRegister={canRegisterDelegation}
            canComplete={canCompleteDelegation}
            onRegister={() => setRegisterOpen(true)}
            onComplete={() => setCompleteOpen(true)}
          />
        ) : (
          showDelegationsCard && (
            <DelegationsCard
              delegations={delegations}
              canCreate={canCreateDelegation}
              currentUserId={user?.role === 'lawyer' ? user.id : undefined}
              onCreate={openCreateDelegation}
              onEdit={openEditDelegation}
              onDelete={setDeleteTarget}
            />
          )
        )}
        {isExecuted ? (
          <OccurrencesCard doc={doc} onOpen={() => setOccurrencesOpen(true)} />
        ) : (
          <>
            <AssetsSection doc={doc} />
            {/* «وقوعات الملف» لنظام «طالبة تنفيذ»: تسجّل إجراءات تغيير الحالة (تريث/منفذ/تراجع). */}
            <OccurrencesCard doc={doc} onOpen={() => setOccurrencesOpen(true)} />
          </>
        )}
      </div>

      <div className="mt-6">
        <StatusCard doc={doc} canChangeStatus={canEdit && !isDelegationExecuted} onOpenStatus={() => setStatusOpen(true)} />
      </div>

      {actionsOpen && id !== undefined && (
        <ExecutionActionsModal
          documentId={Number(id)}
          onClose={() => setActionsOpen(false)}
          onChanged={load}
        />
      )}

      {generationOpen && id !== undefined && (
        <DocumentGenerationModal doc={doc} id={id} onClose={() => setGenerationOpen(false)} />
      )}

      {statusOpen && (isExecuted ? (
        <ExecutedStatusModal doc={doc} onClose={() => setStatusOpen(false)} onChanged={load} />
      ) : (
        <StatusChangeModal doc={doc} onClose={() => setStatusOpen(false)} onChanged={load} />
      ))}

      {transferOpen && id !== undefined && (
        <TransferDocumentModal
          documentId={Number(id)}
          currentOwnerId={doc.createdById}
          onClose={() => setTransferOpen(false)}
          onTransferred={load}
        />
      )}

      {alertOpen && id !== undefined && (
        <FileAlertModal
          documentId={Number(id)}
          documentTitle={debtorFullName || doc.documentType || `مستند #${doc.id}`}
          recipientName={doc.lawyer}
          onClose={() => setAlertOpen(false)}
        />
      )}

      {historyOpen && id !== undefined && (
        <BaseNumbersModal
          documentId={Number(id)}
          documentTitle={debtorFullName || doc.documentType || `مستند #${doc.id}`}
          fileType={doc.fileType}
          onClose={() => setHistoryOpen(false)}
        />
      )}

      {assignmentsOpen && (
        <TransferHistoryModal
          assignments={doc.assignments ?? []}
          onClose={() => setAssignmentsOpen(false)}
        />
      )}

      {occurrencesOpen && (
        <OccurrencesModal
          documentTitle={debtorFullName || doc.documentType || `مستند #${doc.id}`}
          occurrences={doc.occurrences ?? []}
          onClose={() => setOccurrencesOpen(false)}
        />
      )}

      {partyModal && <PartyDetailsModal modal={partyModal} onClose={() => setPartyModal(null)} />}

      {delegationFormOpen && id !== undefined && (
        <DelegationFormModal
          documentId={Number(id)}
          documentTitle={debtorFullName || doc.documentType || `مستند #${doc.id}`}
          assets={doc.assets ?? []}
          initial={editingDelegation}
          onClose={() => {
            setDelegationFormOpen(false);
            setEditingDelegation(null);
          }}
          onSaved={() => {
            setDelegationFormOpen(false);
            setEditingDelegation(null);
            loadDelegations();
          }}
        />
      )}

      {registerOpen && delegationOfThisFile && (
        <RegisterDelegationModal
          delegation={delegationOfThisFile}
          onClose={() => setRegisterOpen(false)}
          onRegistered={() => {
            setRegisterOpen(false);
            loadDelegations();
            load();
          }}
        />
      )}

      {completeOpen && delegationOfThisFile && (
        <CompleteDelegationModal
          delegation={delegationOfThisFile}
          onClose={() => setCompleteOpen(false)}
          onCompleted={() => {
            setCompleteOpen(false);
            loadDelegations();
            load();
          }}
        />
      )}

      {deleteTarget && (
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
              هل أنت متأكد من حذف الإنابة إلى دائرة {deleteTarget.delegatedCourt || 'غير محددة'}؟
            </p>
            <div className="flex justify-end gap-2">
              <button
                onClick={() => setDeleteTarget(null)}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
              >
                إلغاء
              </button>
              <button
                onClick={confirmDeleteDelegation}
                disabled={deleting}
                className="bg-red-700 hover:bg-red-800 text-white rounded-lg px-4 py-2 text-sm min-h-11 disabled:opacity-50"
              >
                {deleting ? 'جارِ الحذف...' : 'تأكيد الحذف'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
