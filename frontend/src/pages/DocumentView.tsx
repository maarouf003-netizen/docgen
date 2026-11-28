import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { normalizeDocumentResponse } from '../utils/apiNormalization';
import {
  getDocumentBadge,
  canDelegateSource,
  isCirculatingTarget,
  EXEC_STATUS_DELEGATION_EXECUTED,
} from '../utils/documentStatus';
import { isExecutedLike } from '../utils/documentDisplay';
import {
  DELEGATION_STATUS_ASSIGNED,
  DELEGATION_STATUS_REGISTERED,
  withCourtPrefix,
} from '../utils/delegationStatus';
import { availableDelegationAssets } from '../utils/delegationAssets';
import { saveLastViewedDocumentId } from '../utils/listSession';
import { useIsMobile } from '../hooks/useMediaQuery';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { rovingTabIndex, useTabListKeyboard } from '../hooks/useTabListKeyboard';
import { DelegationsCard } from '../components/delegation/DelegationsCard';
import { SourceFileInfoCard } from '../components/delegation/SourceFileInfoCard';
import { DelegationStatusCard } from '../components/delegation/DelegationStatusCard';
import DocumentReviewLettersCard from '../components/review/DocumentReviewLettersCard';
import { ExecutoryDocumentCard } from '../components/view/ExecutoryDocumentCard';
import { FileDataCard } from '../components/view/FileDataCard';
import { OccurrencesCard } from '../components/view/OccurrencesCard';
import { PartiesCard } from '../components/view/PartiesCard';
import { AssetsSection } from '../components/view/AssetsSection';
import { fullName, identityFileParts } from '../components/view/viewFormat';
import { StatusCard } from '../components/view/StatusCard';
import { DocumentViewHeader } from './documentView/DocumentViewHeader';
import { DocumentViewModals } from './documentView/DocumentViewModals';
import { useDocumentViewModals } from './documentView/useDocumentViewModals';
import type { AppealDto, DelegationDto, DocumentResponse } from '../types';

/**
 * صفحة عرض المستند — قشرة تركيبية بعد `P6-02b`: جلب الملف/الإنابات/الاستئنافات،
 * اشتقاق الصلاحيات، تركيب اللوحات (مكتبي/جوال)، والترويسة والنوافذ كوحدتين.
 * السلوك منقول بلا تغيير.
 */
type DocumentTabId = 'info' | 'security' | 'delegations' | 'status';

/** ترتيب تبويبات الجوال — مصدر واحد للعرض والتنقّل (لا ينحرف الترتيب). */
const DOCUMENT_TAB_IDS: readonly DocumentTabId[] = ['info', 'security', 'delegations', 'status'];

export default function DocumentView() {
  const { id } = useParams();
  const { user, hasFullAccess } = useAuth();
  const modals = useDocumentViewModals(id);
  // مسار الجوال: تبويبات أقسام الملف (نمط تفصيلي للشاشات الصغيرة) بدل الأعمدة المتوازية.
  const isMobile = useIsMobile();
  const [activeTab, setActiveTab] = useState<'info' | 'security' | 'delegations' | 'status'>('info');

  // تنقّل التبويبات بلوحة المفاتيح (WAI-ARIA، RTL: السهم الأيسر «التالي») — قبل أي return
  // مبكر ليبقى ترتيب الخطافات ثابتًا.
  const onTabKeyDown = useTabListKeyboard({
    ids: DOCUMENT_TAB_IDS,
    activeId: activeTab,
    onSelect: setActiveTab,
    tabId: (tab) => `document-tab-${tab}`,
  });

  // حد الثقة: تُطبَّع استجابة الملف قبل انتشارها للبطاقات.
  // resetOnDepsChange: استعلامات هوية الملف المفرد — تُصفَّر بيانات السابق
  // عند تبديل الملف فلا يُعرض ماله (ولا غيره) تحت عنوان الجديد أثناء التحميل.
  const docQuery = useCancellableRequest<DocumentResponse | null>(
    (signal) =>
      api
        .get<DocumentResponse>(`/documents/${id}`, { signal })
        .then((r) => normalizeDocumentResponse(r.data)),
    [id],
    { enabled: Boolean(id), resetOnDepsChange: true },
  );
  const delegationsQuery = useCancellableRequest<DelegationDto[]>(
    (signal) =>
      api
        .get<DelegationDto[]>(`/documents/${id}/delegations`, { signal })
        .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [id],
    { enabled: Boolean(id), resetOnDepsChange: true },
  );
  // استئنافات الملف: تُغذّي جزء «الاستئنافات» في بطاقة وقوعات الملف.
  const appealsQuery = useCancellableRequest<AppealDto[]>(
    (signal) =>
      api
        .get<AppealDto[]>(`/documents/${id}/appeals`, { signal })
        .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [id],
    { enabled: Boolean(id), resetOnDepsChange: true },
  );

  const doc = docQuery.data ?? null;
  const delegations = delegationsQuery.data ?? [];
  const appeals = appealsQuery.data ?? [];
  // E1: بوابة التحميل الأولى — أول تحميل (بلا بيانات) لا إعادة جلب خلفية ببيانات حاضرة
  // (isLoading يشتعل عند كل attempt) — حتى لا تُفتح نافذة التسطير والمحجوب مجهول.
  const delegationsLoading = delegationsQuery.isLoading && delegations.length === 0;
  const fetchError = docQuery.error;
  const load = docQuery.refetch;
  const loadDelegations = delegationsQuery.refetch;
  const loadAppeals = appealsQuery.refetch;

  // يُسجَّل الملف كآخر ما فُتح في الجلسة ليُميَّز في القائمة عند العودة — فقط بعد تحميل مضمون
  // للملف فعليًا (doc)، فملف غير مصرّح به (403) لا يُسجَّل ولا يتولّد شريط «مستند N» برابط ميت.
  useEffect(() => {
    if (id && doc) saveLastViewedDocumentId(Number(id), user?.id ?? null);
  }, [id, doc, user?.id]);

  if (fetchError) return <div role="alert" className="text-red-600">{fetchError}</div>;
  if (!doc) return <div className="text-gray-500">جارِ التحميل...</div>;

  const canEdit = user?.role === 'lawyer';
  const canTransfer = user?.role === 'head' || user?.role === 'subhead';
  const canDirectAlert = user?.role === 'head' || user?.role === 'subhead';
  const isExecuted = isExecutedLike(doc.generalEntitySide);
  const isLawyer = user?.role === 'lawyer';
  // «منفذ إنابة» (الملف المناب عند إتمام الإنابة): حالة نهائية تُعامل منفذًا — لا توليد
  // مستندات ولا تغيير حالة بعدها (الخلفية تراقب أيضًا عبر آلة الحالات).
  const isDelegationExecuted = doc.execStatus === EXEC_STATUS_DELEGATION_EXECUTED;
  // «الفرع» يظهر للمدير والمشرف فقط؛ و«المحامي المختص» يظهر للمدير والمشرف ورئيس القسم/الشعبة
  // (لا يظهر للمحامي المختص نفسه الذي يرى ملفه من صفحة أخرى).
  const showBranch = user?.role === 'admin' || user?.role === 'manager';
  const showLawyer = user?.role === 'admin' || user?.role === 'manager' || user?.role === 'head' || user?.role === 'subhead';

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
  // حقول الهوية: على الجوال تُجزَّأ إلى حقول خضراء متلاصقة، وعلى الكومبيوتر تبقى
  // التسميات المنفصلة كما كانت.
  const fileIdentity = identityFileParts(doc);
  const fileType = (doc.fileType ?? '').trim();
  const courtDisplay = withCourtPrefix(doc.court);
  // الإنابة: يعرض الملف المناب بطاقة «معلومات الملف المنيب» (إنابته الخاصة)، والملف المنيب
  // بطاقة «تشعبات الملف» (إناباته الصادرة) — وكلتاهما من نقطة الإنابات نفسها.
  const delegationOfThisFile = delegations.find((d) => d.targetDocumentId === doc.id);
  // تسطير الإنابة من محامي الملف المالك على ملف «طالبة تنفيذ» متداول غير منفذ/مشطوب
  // (نفس شروط الخلفية: ValidateSourceForDelegation).
  const isOwner = doc.createdById != null && doc.createdById === user?.id;
  // تسطير استئناف: محامي الملف المالك على أي ملف مقيد (غير تحت الرفع) — حتى المنفذ/المشطوب.
  const canCreateAppeal = canEdit && isOwner && !doc.isDraft;
  const canCreateDelegation =
    canEdit &&
    isOwner &&
    // مطابقة تامة للخلفية (ValidateSourceForDelegation): ليس تحت رفع، صفة «طالبة تنفيذ»،
    // غير منفذ/مشطوب — و«منفذ جبريا (منفذ جزئيا)» يبقى قابلًا للتسطير (قرار 9).
    canDelegateSource(doc);
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
    delegationOfThisFile.status === DELEGATION_STATUS_REGISTERED &&
    // N11: زر «إتمام الإنابة» يخص الملف المناب المتداول فقط — وإلا وعد ثم فشل خلفيةً (E3).
    isCirculatingTarget(doc);

  const openCreateDelegation = () => {
    modals.setEditingDelegation(null);
    modals.setDelegationFormOpen(true);
  };

  const openEditDelegation = (d: DelegationDto) => {
    modals.setEditingDelegation(d);
    modals.setDelegationFormOpen(true);
  };

  // أموال نافذة التسطير: بلا «كفالة رواتب» أبدًا (لا إنابة عليها) وبلا المحجوب بإنابة سارية —
  // إخفاء تام؛ وعند التعديل تُستثنى الإنابة ذاتها (تعديل المعلّقة على أموالها مسموح).
  const sourceAssets = doc.assets ?? [];
  const formAvailableAssets = availableDelegationAssets(
    delegations,
    sourceAssets,
    modals.editingDelegation?.id ?? null,
  );
  const formNoAvailableAssets = sourceAssets.length > 0 && formAvailableAssets.length === 0;

  // أعمدة (Facets) صفحة التفاصيل: العمود الأول «المعلومات»، الثاني «السند والأموال»،
  // والثالث «الإنابات والوقوعات» — وتتحول على الجوال إلى تبويبات يختارها المستخدم.
  const infoPanel = (
    <>
      <PartiesCard doc={doc} onOpen={modals.setPartyModal} onOpenIntervention={modals.setInterventionModal} />
      <FileDataCard
        doc={doc}
        isLawyer={isLawyer}
        showBranch={showBranch}
        showLawyer={showLawyer}
        canViewChanges={hasFullAccess || showLawyer || isOwner}
        onOpenBaseNumbers={() => modals.setHistoryOpen(true)}
        onOpenAssignments={() => modals.setAssignmentsOpen(true)}
        onOpenChanges={() => modals.setChangesOpen(true)}
      />
    </>
  );
  const securityPanel = (
    <>
      <ExecutoryDocumentCard doc={doc} isMirror={doc.sourceDelegationId != null} />
      {delegationOfThisFile && (
        <DelegationStatusCard
          doc={doc}
          delegationId={delegationOfThisFile.id}
          delegation={delegationOfThisFile}
        />
      )}
      {!isExecuted && !delegationOfThisFile && <AssetsSection doc={doc} delegations={delegations} />}
      {id !== undefined && (
        <DocumentReviewLettersCard
          documentId={Number(id)}
          documentTitle={debtorFullName || doc.documentType || undefined}
          canCreate={canEdit && isOwner}
        />
      )}
    </>
  );
  const delegationsPanel = (
    <>
      {delegationOfThisFile ? (
        <SourceFileInfoCard
          delegation={delegationOfThisFile}
          canRegister={canRegisterDelegation}
          onRegister={() => modals.setRegisterOpen(true)}
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
            onDelete={modals.setDeleteTarget}
            sourceAssets={sourceAssets}
            delegationsLoading={delegationsLoading}
          />
        )
      )}
      <OccurrencesCard
        doc={doc}
        appeals={appeals}
        onOpen={() => modals.setOccurrencesOpen(true)}
        onOpenAppeal={modals.setInfoAppeal}
        onOpenIntervention={modals.setInterventionModal}
      />
    </>
  );
  const statusPanel = (
    <StatusCard
      doc={doc}
      canChangeStatus={canEdit && !isDelegationExecuted}
      onOpenStatus={() => modals.setStatusOpen(true)}
      canCompleteDelegation={canCompleteDelegation}
      onCompleteDelegation={() => modals.setCompleteOpen(true)}
      delegationSourceLabel={delegationOfThisFile?.sourceDocumentLabel ?? null}
    />
  );

  const tabs = [
    { id: 'info', label: 'المعلومات', panel: infoPanel },
    { id: 'security', label: 'السند والأموال', panel: securityPanel },
    { id: 'delegations', label: 'الإنابات والوقوعات', panel: delegationsPanel },
    { id: 'status', label: 'الحالة', panel: statusPanel },
  ] as const;

  const confirmDeleteDelegation = async () => {
    if (!modals.deleteTarget) return;
    modals.setDeleting(true);
    try {
      await api.delete(`/delegations/${modals.deleteTarget.id}`);
      modals.setDeleteTarget(null);
      loadDelegations();
    } catch (err) {
      modals.setDeleteTarget(null);
      window.alert(getApiErrorMessage(err));
    } finally {
      modals.setDeleting(false);
    }
  };

  return (
    <div className="max-w-6xl mx-auto">
      <DocumentViewHeader
        doc={doc}
        id={id}
        isMobile={isMobile}
        isExecuted={isExecuted}
        isDelegationExecuted={isDelegationExecuted}
        debtorFullName={debtorFullName}
        statusBadge={statusBadge}
        fileIdentity={fileIdentity}
        fileType={fileType}
        courtDisplay={courtDisplay}
        showBranch={showBranch}
        canEdit={canEdit}
        canCreateAppeal={canCreateAppeal}
        canTransfer={canTransfer}
        canDirectAlert={canDirectAlert}
        onOpenActions={() => modals.setActionsOpen(true)}
        onOpenGeneration={() => modals.setGenerationOpen(true)}
        onOpenAlert={() => modals.setAlertOpen(true)}
        onOpenTransfer={() => modals.setTransferOpen(true)}
        onStartAppeal={modals.setAppealFormVariant}
      />

      {isMobile ? (
        <div>
          <div
            role="tablist"
            aria-label="أقسام الملف"
            className="flex gap-2 overflow-x-auto pb-1 mb-4 -mx-1 px-1"
            onKeyDown={onTabKeyDown}
          >
            {tabs.map((t) => (
              <button
                key={t.id}
                type="button"
                role="tab"
                id={`document-tab-${t.id}`}
                aria-selected={activeTab === t.id}
                // اللوحة غير النشطة غير مُصيَّرة (لوحة واحدة فقط)، فلا `aria-controls` إلى معرّف غائب.
                aria-controls={activeTab === t.id ? `document-panel-${t.id}` : undefined}
                tabIndex={rovingTabIndex(activeTab === t.id)}
                onClick={() => setActiveTab(t.id)}
                className={`shrink-0 min-h-11 px-4 rounded-lg text-sm font-medium transition-colors ${
                  activeTab === t.id
                    ? 'bg-emerald-900 text-white'
                    : 'bg-white border border-gray-300 text-gray-800 hover:bg-gray-50'
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

      <DocumentViewModals
        modals={modals}
        doc={doc}
        id={id}
        isExecuted={isExecuted}
        debtorFullName={debtorFullName}
        load={load}
        loadDelegations={loadDelegations}
        loadAppeals={loadAppeals}
        formAvailableAssets={formAvailableAssets}
        formNoAvailableAssets={formNoAvailableAssets}
        delegationOfThisFile={delegationOfThisFile}
        onConfirmDelete={confirmDeleteDelegation}
      />
    </div>
  );
}
