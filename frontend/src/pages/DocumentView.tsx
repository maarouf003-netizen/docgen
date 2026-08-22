import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { normalizeDocumentResponse } from '../utils/apiNormalization';
import { getDocumentBadge, EXEC_STATUS_FORCIBLY, EXEC_STATUS_SETTLED, EXEC_STATUS_STRUCK_OFF, EXEC_STATUS_DELEGATION_EXECUTED } from '../utils/documentStatus';
import { isExecutedLike } from '../utils/documentDisplay';
import { DELEGATION_STATUS_ASSIGNED, DELEGATION_STATUS_REGISTERED } from '../utils/delegationStatus';
import { saveLastViewedDocumentId } from '../utils/listSession';
import { useIsMobile } from '../hooks/useMediaQuery';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
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
  const [actionsOpen, setActionsOpen] = useState(false);
  const [statusOpen, setStatusOpen] = useState(false);
  const [transferOpen, setTransferOpen] = useState(false);
  const [alertOpen, setAlertOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [occurrencesOpen, setOccurrencesOpen] = useState(false);
  const [assignmentsOpen, setAssignmentsOpen] = useState(false);
  const [generationOpen, setGenerationOpen] = useState(false);
  const [partyModal, setPartyModal] = useState<PartyModal | null>(null);
  const [delegationFormOpen, setDelegationFormOpen] = useState(false);
  const [editingDelegation, setEditingDelegation] = useState<DelegationDto | null>(null);
  const [registerOpen, setRegisterOpen] = useState(false);
  const [completeOpen, setCompleteOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<DelegationDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  // مسار الجوال: تبويبات أقسام الملف (نمط تفصيلي للشاشات الصغيرة) بدل الأعمدة المتوازية.
  const isMobile = useIsMobile();
  const [activeTab, setActiveTab] = useState<'info' | 'security' | 'delegations' | 'status'>('info');

  // حد الثقة: تُطبَّع استجابة الملف قبل انتشارها للبطاقات.
  const docQuery = useCancellableRequest<DocumentResponse | null>(
    (signal) =>
      api
        .get<DocumentResponse>(`/documents/${id}`, { signal })
        .then((r) => normalizeDocumentResponse(r.data)),
    [id],
    { enabled: Boolean(id) },
  );
  const delegationsQuery = useCancellableRequest<DelegationDto[]>(
    (signal) =>
      api
        .get<DelegationDto[]>(`/documents/${id}/delegations`, { signal })
        .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [id],
    { enabled: Boolean(id) },
  );

  const doc = docQuery.data ?? null;
  const delegations = delegationsQuery.data ?? [];
  const fetchError = docQuery.error;
  const load = docQuery.refetch;
  const loadDelegations = delegationsQuery.refetch;

  // يُسجَّل الملف كآخر ما فُتح في الجلسة ليُميَّز في القائمة عند العودة (حتى لو فُتح من غير القائمة).
  useEffect(() => {
    if (id) saveLastViewedDocumentId(Number(id));
  }, [id]);

  if (fetchError) return <div role="alert" className="text-red-600">{fetchError}</div>;
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

  // أعمدة (Facets) صفحة التفاصيل: العمود الأول «المعلومات»، الثاني «السند والأموال»،
  // والثالث «الإنابات والوقوعات» — وتتحول على الجوال إلى تبويبات يختارها المستخدم.
  const infoPanel = (
    <>
      <PartiesCard doc={doc} onOpen={setPartyModal} />
      <FileDataCard
        doc={doc}
        isLawyer={isLawyer}
        showBranch={showBranch}
        showLawyer={showLawyer}
        onOpenBaseNumbers={() => setHistoryOpen(true)}
        onOpenAssignments={() => setAssignmentsOpen(true)}
      />
    </>
  );
  const securityPanel = (
    <>
      <ExecutoryDocumentCard doc={doc} />
      {!isExecuted && <AssetsSection doc={doc} />}
    </>
  );
  const delegationsPanel = (
    <>
      {delegationOfThisFile ? (
        <SourceFileInfoCard
          delegation={delegationOfThisFile}
          canRegister={canRegisterDelegation}
          canComplete={canCompleteDelegation}
          onRegister={() => setRegisterOpen(true)}
          onComplete={() => setCompleteOpen(true)}
        />
      ) : delegationsQuery.error ? (
        <div
          role="alert"
          className="bg-amber-50 border border-amber-200 rounded-xl px-4 py-3 text-sm text-amber-800 flex items-center justify-between gap-3 flex-wrap"
        >
          <span>تعذر تحميل الإنابات — تفقّد الاتصال وأعد المحاولة.</span>
          <button
            type="button"
            onClick={loadDelegations}
            className="min-h-11 px-4 rounded-lg border border-amber-300 hover:bg-amber-100 text-amber-900 font-medium"
          >
            إعادة المحاولة
          </button>
        </div>
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
      <OccurrencesCard doc={doc} onOpen={() => setOccurrencesOpen(true)} />
    </>
  );
  const statusPanel = (
    <StatusCard doc={doc} canChangeStatus={canEdit && !isDelegationExecuted} onOpenStatus={() => setStatusOpen(true)} />
  );

  const tabs = [
    { id: 'info', label: 'المعلومات', panel: infoPanel },
    { id: 'security', label: 'السند والأموال', panel: securityPanel },
    { id: 'delegations', label: 'الإنابات والوقوعات', panel: delegationsPanel },
    { id: 'status', label: 'الحالة', panel: statusPanel },
  ] as const;

  const confirmDeleteDelegation = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await api.delete(`/delegations/${deleteTarget.id}`);
      setDeleteTarget(null);
      loadDelegations();
    } catch (err) {
      setDeleteTarget(null);
      window.alert(getApiErrorMessage(err));
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="max-w-6xl mx-auto">
      <div className="sticky top-0 z-30 bg-white/95 backdrop-blur border-b border-gray-200 rounded-b-xl shadow-sm px-4 py-3 mb-5">
        <div className="flex items-center justify-between gap-3 flex-wrap">
          <h2 className="text-xl md:text-2xl font-bold text-gray-800 flex items-center gap-3 min-w-0">
            <span className={`rounded-full px-3 py-1 text-sm ${statusBadge.cls}`}>
              {statusBadge.text}
            </span>
            <span className="min-w-0 truncate">
              {isExecuted ? executedTitle(doc) : debtorFullName || doc.documentType || `مستند #${doc.id}`}
            </span>
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

        {/* شريط الهوية: بطاقات الملف الأساسية (رقم/سنة/دائرة/فرع/محامٍ) لقراءة فورية أثناء التمرير. */}
        <dl className="mt-3 flex flex-wrap gap-2 text-sm">
          <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-emerald-50 border border-emerald-200 px-3 py-1.5">
            <dt className="text-xs text-emerald-800 font-medium">رقم الملف</dt>
            <dd className="text-gray-800 font-semibold tabular-nums">
              {doc.displayFileNumber ?? doc.fileNumber ?? '—'}
            </dd>
          </div>
          <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-gray-50 border border-gray-200 px-3 py-1.5">
            <dt className="text-xs text-gray-500 font-medium">السنة</dt>
            <dd className="text-gray-800 font-semibold tabular-nums">{doc.fileYear || '—'}</dd>
          </div>
          <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-gray-50 border border-gray-200 px-3 py-1.5">
            <dt className="text-xs text-gray-500 font-medium">الدائرة</dt>
            <dd className="text-gray-800 font-semibold">{doc.court || '—'}</dd>
          </div>
          {showBranch && (
            <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-gray-50 border border-gray-200 px-3 py-1.5">
              <dt className="text-xs text-gray-500 font-medium">الفرع</dt>
              <dd className="text-gray-800 font-semibold">{doc.branchName || '—'}</dd>
            </div>
          )}
        </dl>
      </div>

      {isMobile ? (
        <div>
          <div
            role="tablist"
            aria-label="أقسام الملف"
            className="flex gap-2 overflow-x-auto pb-1 mb-4 -mx-1 px-1"
          >
            {tabs.map((t) => (
              <button
                key={t.id}
                type="button"
                role="tab"
                id={`document-tab-${t.id}`}
                aria-selected={activeTab === t.id}
                aria-controls={`document-panel-${t.id}`}
                onClick={() => setActiveTab(t.id)}
                className={`shrink-0 min-h-11 px-4 rounded-lg text-sm font-medium transition-colors ${
                  activeTab === t.id
                    ? 'bg-emerald-800 text-white'
                    : 'bg-white border border-gray-300 text-gray-700 hover:bg-gray-50'
                }`}
              >
                {t.label}
              </button>
            ))}
          </div>
          <div
            role="tabpanel"
            id={`document-panel-${activeTab}`}
            aria-labelledby={`document-tab-${activeTab}`}
          >
            {tabs.find((t) => t.id === activeTab)?.panel}
          </div>
        </div>
      ) : (
        <div className="grid md:grid-cols-3 gap-5 items-stretch">
          <div className="flex flex-col gap-5 min-w-0">{infoPanel}</div>
          <div className="flex flex-col gap-5 min-w-0">{securityPanel}</div>
          <div className="flex flex-col gap-5 min-w-0">{delegationsPanel}</div>
        </div>
      )}

      {!isMobile && <div className="mt-6">{statusPanel}</div>}

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
