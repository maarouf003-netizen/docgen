import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../api/client';
import { normalizeDocumentResponse } from '../utils/apiNormalization';
import { getDocumentBadge } from '../utils/documentStatus';
import { isExecutedLike } from '../utils/documentDisplay';
import { useIsMobile } from '../hooks/useMediaQuery';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { executedTitle, fullName } from '../components/view/viewFormat';
import { SectionCard } from '../components/view/SectionCard';
import { PartiesCard } from '../components/view/PartiesCard';
import { FileDataCard } from '../components/view/FileDataCard';
import { ExecutoryDocumentCard } from '../components/view/ExecutoryDocumentCard';
import { AssetsSection } from '../components/view/AssetsSection';
import { OccurrencesCard } from '../components/view/OccurrencesCard';
import { OccurrencesModal } from '../components/view/OccurrencesModal';
import { StatusCard } from '../components/view/StatusCard';
import { PartyDetailsModal } from '../components/view/PartyDetailsModal';
import { TransferHistoryModal } from '../components/view/TransferHistoryModal';
import type { PartyModal } from '../components/view/viewTypes';
import { DelegationsCard } from '../components/delegation/DelegationsCard';
import { SourceFileInfoCard } from '../components/delegation/SourceFileInfoCard';
import { DelegationStatusCard } from '../components/delegation/DelegationStatusCard';
import AppealInfoModal from '../components/appeal/AppealInfoModal';
import BaseNumbersModal from '../components/BaseNumbersModal';
import DocumentCorrespondenceCard from '../components/correspondence/DocumentCorrespondenceCard';
import { PortalExecutionActionsCard } from '../components/portal/PortalExecutionActionsCard';
import type {
  AppealDto,
  DelegationDto,
  DocumentResponse,
  PortalExecutionActionDto,
} from '../types';

/**
 * تفاصيل قرائية لملف داخل نطاق بوابة مندوب الجهة — مرآة قرائية لصفحة المحامي
 * (DocumentView) وفق خطة PORTAL_FILE_DETAIL_MIRROR_PLAN: البطاقات السبع + «الإجراءات
 * التنفيذية» (action فقط) والنوافذ، بلا أي زر فعل (ق1–ق10). كل نقاط الجلب ضمن
 * /api/portal* والمسموح بها لبند المسرّح، والنوافذ تُفتح من بيانات الاستجابة نفسها.
 */
/**
 * شريط أعطال موحّد لأقسام الملف الفرعية (الإنابات/وقوعات الاستئنافات/الإجراءات): يمنع
 * الفراغ الكاذب عند تعثر الشبكة (يُبنى عليه قرار)، ويُعيد المحاولة عبر «refetch».
 */
function PortalRetryBar({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div
      role="alert"
      className="bg-amber-50 border border-amber-200 rounded-xl px-4 py-3 text-sm text-amber-800 flex items-center justify-between gap-3 flex-wrap"
    >
      <span>{message}</span>
      <button
        type="button"
        onClick={onRetry}
        className="min-h-11 px-4 rounded-lg border border-amber-300 hover:bg-amber-100 text-amber-900 font-medium focus:outline-none focus-visible:ring-2 focus-visible:ring-amber-500"
      >
        إعادة المحاولة
      </button>
    </div>
  );
}

export default function PortalFileDetail() {
  const { id } = useParams();
  const isMobile = useIsMobile();
  const [activeTab, setActiveTab] = useState<'info' | 'security' | 'delegations' | 'status'>('info');
  const [partyModal, setPartyModal] = useState<PartyModal | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [assignmentsOpen, setAssignmentsOpen] = useState(false);
  const [occurrencesOpen, setOccurrencesOpen] = useState(false);
  const [infoAppeal, setInfoAppeal] = useState<AppealDto | null>(null);
  // القسم الذي أُعيدت محاولة تحميله يدويًا: يُبقي شريط إعادة المحاولة مثبتًا أثناء
  // إعادة الجلب (فلا يضيع التركيز)، وينقل التركيز إلى القسم عند نجاحها.
  const [retryingSection, setRetryingSection] = useState<'delegations' | 'appeals' | 'actions' | null>(null);
  const delegationsGroupRef = useRef<HTMLDivElement>(null);
  const appealsGroupRef = useRef<HTMLDivElement>(null);
  const actionsGroupRef = useRef<HTMLDivElement>(null);

  const docQuery = useCancellableRequest<DocumentResponse | null>(
    (signal) =>
      api
        .get<DocumentResponse>(`/portal/files/${id}`, { signal })
        .then((r) => normalizeDocumentResponse(r.data)),
    [id],
    { enabled: Boolean(id) },
  );
  const delegationsQuery = useCancellableRequest<DelegationDto[]>(
    (signal) =>
      api
        .get<DelegationDto[]>(`/portal/files/${id}/delegations`, { signal })
        .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [id],
    { enabled: Boolean(id) },
  );
  // تفاصيل استئنافات الملف كاملة (رأي المحامي مصفَّر خلفيًا — ق10) لنافذة الاستئناف.
  const appealsQuery = useCancellableRequest<AppealDto[]>(
    (signal) =>
      api
        .get<AppealDto[]>(`/portal/files/${id}/appeals/details`, { signal })
        .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [id],
    { enabled: Boolean(id) },
  );
  const actionsQuery = useCancellableRequest<PortalExecutionActionDto[]>(
    (signal) =>
      api
        .get<PortalExecutionActionDto[]>(`/portal/files/${id}/execution-actions`, { signal })
        .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [id],
    { enabled: Boolean(id) },
  );

  const doc = docQuery.data ?? null;
  const delegations = delegationsQuery.data ?? [];
  const appeals = appealsQuery.data ?? [];
  const executionActions = actionsQuery.data ?? [];
  const delegationsLoading = delegationsQuery.isLoading && delegations.length === 0;

  // بعد نجاح إعادة محاولة يدوية: انقل التركيز إلى القسم المعاد تحميله ليُعلن محتواه
  // لقارئ الشاشة بدل ضياعه مع اختفاء الشريط — يعمل فقط عند قدوم النجاح بعد نقرة
  // (retryingSection مضبوط)، فلا يسرق التركيز عند التحميل الأولي أبدًا.
  const delegationsQuiet = !delegationsQuery.error && !delegationsQuery.isLoading;
  const appealsQuiet = !appealsQuery.error && !appealsQuery.isLoading;
  const actionsQuiet = !actionsQuery.error && !actionsQuery.isLoading;
  useEffect(() => {
    if (!retryingSection) return;
    const ready =
      retryingSection === 'delegations'
        ? delegationsQuiet
        : retryingSection === 'appeals'
          ? appealsQuiet
          : actionsQuiet;
    if (!ready) return;
    setRetryingSection(null);
    const el =
      retryingSection === 'delegations'
        ? delegationsGroupRef.current
        : retryingSection === 'appeals'
          ? appealsGroupRef.current
          : actionsGroupRef.current;
    el?.focus();
  }, [retryingSection, delegationsQuiet, appealsQuiet, actionsQuiet]);

  if (docQuery.error) {
    return (
      <div className="max-w-3xl mx-auto" role="alert">
        <p className="text-red-600 mb-4">{docQuery.error}</p>
        <div className="flex gap-3 flex-wrap">
          <button
            type="button"
            onClick={docQuery.refetch}
            className="min-h-11 px-4 rounded-lg border border-red-300 hover:bg-red-50 text-red-800 font-medium focus:outline-none focus-visible:ring-2 focus-visible:ring-red-500"
          >
            إعادة المحاولة
          </button>
          <Link
            to="/portal"
            className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 min-h-11 inline-block"
          >
            رجوع إلى ملفات الجهة
          </Link>
        </div>
      </div>
    );
  }

  if (!doc) {
    return <div className="max-w-3xl mx-auto text-gray-500 text-sm">جارِ التحميل…</div>;
  }

  const isExecuted = isExecutedLike(doc.generalEntitySide);
  const statusBadge = getDocumentBadge(doc);
  const debtorFullName = fullName({
    name: doc.borrowerName,
    father: doc.borrowerFather,
    family: doc.borrowerFamily,
  });
  const title = isExecuted ? executedTitle(doc) : debtorFullName || doc.documentType || `مستند #${doc.id}`;
  const modalTitle = debtorFullName || doc.documentType || `مستند #${doc.id}`;

  // الإنابة: الملف المناب يعرض «معلومات الملف المنيب» (إنابته الخاصة)، والملف المنيب
  // يعرض «تشعبات الملف» (إناباته الصادرة) — من نقطة الإنابات نفسها (مرآة DocumentView).
  const delegationOfThisFile = delegations.find((d) => d.targetDocumentId === doc.id);
  // «تشعبات الملف» تظهر فقط عند وجود إنابات (للمندوب canCreate=false دائمًا — ف10).
  const showDelegationsCard = delegations.length > 0;
  // ف9: OccurrencesCard تُرجع null عند الفراغ التام — نعرض بدلًا بطاقة فراغ صريحة.
  const hasOccurrencesPanel =
    (doc.occurrences?.length ?? 0) > 0 || Boolean(doc.struckOffDate) || appeals.length > 0;

  const infoPanel = (
    <>
      <PartiesCard doc={doc} onOpen={setPartyModal} />
      <FileDataCard
        doc={doc}
        isLawyer={false}
        showBranch={false}
        showLawyer={true}
        canViewChanges={false}
        onOpenBaseNumbers={() => setHistoryOpen(true)}
        onOpenAssignments={() => setAssignmentsOpen(true)}
        onOpenChanges={() => {}}
      />
    </>
  );

  const securityPanel = (
    <>
      <ExecutoryDocumentCard doc={doc} />
      {delegationOfThisFile && (
        <DelegationStatusCard doc={doc} delegationId={delegationOfThisFile.id} />
      )}
      {!isExecuted && !delegationOfThisFile && (
        <AssetsSection doc={doc} delegations={delegations} />
      )}
    </>
  );

  const delegationsPanel = (
    <>
      <div ref={delegationsGroupRef} tabIndex={-1} role="group" aria-label="قسم الإنابات">
        {delegationOfThisFile ? (
          <SourceFileInfoCard delegation={delegationOfThisFile} />
        ) : delegationsQuery.error || (retryingSection === 'delegations' && delegationsQuery.isLoading) ? (
          <PortalRetryBar
            message="تعذر تحميل الإنابات — تفقّد الاتصال وأعد المحاولة."
            onRetry={() => {
              setRetryingSection('delegations');
              delegationsQuery.refetch();
            }}
          />
        ) : (
          showDelegationsCard && (
            <DelegationsCard
              delegations={delegations}
              canCreate={false}
              currentUserId={undefined}
              onCreate={() => {}}
              onEdit={() => {}}
              onDelete={() => {}}
              sourceAssets={doc.assets}
              delegationsLoading={delegationsLoading}
            />
          )
        )}
      </div>
      <div ref={appealsGroupRef} tabIndex={-1} role="group" aria-label="قسم الوقوعات والاستئنافات">
        {appealsQuery.error || (retryingSection === 'appeals' && appealsQuery.isLoading) ? (
          <PortalRetryBar
            message="تعذر تحميل الوقوعات والاستئنافات — تفقّد الاتصال وأعد المحاولة."
            onRetry={() => {
              setRetryingSection('appeals');
              appealsQuery.refetch();
            }}
          />
        ) : hasOccurrencesPanel ? (
          <OccurrencesCard
            doc={doc}
            appeals={appeals}
            onOpen={() => setOccurrencesOpen(true)}
            onOpenAppeal={setInfoAppeal}
          />
        ) : (
          <SectionCard title="وقوعات الملف">
            <p className="text-gray-400 text-sm">لا توجد وقوعات أو استئنافات على هذا الملف</p>
          </SectionCard>
        )}
      </div>
    </>
  );

  const statusPanel = (
    <>
      <StatusCard
        doc={doc}
        canChangeStatus={false}
        onOpenStatus={() => {}}
        canCompleteDelegation={false}
        onCompleteDelegation={() => {}}
        delegationSourceLabel={delegationOfThisFile?.sourceDocumentLabel ?? null}
      />
      <div ref={actionsGroupRef} tabIndex={-1} role="group" aria-label="قسم الإجراءات التنفيذية">
        {actionsQuery.error || (retryingSection === 'actions' && actionsQuery.isLoading) ? (
          <PortalRetryBar
            message="تعذر تحميل الإجراءات التنفيذية — تفقّد الاتصال وأعد المحاولة."
            onRetry={() => {
              setRetryingSection('actions');
              actionsQuery.refetch();
            }}
          />
        ) : (
          <PortalExecutionActionsCard actions={executionActions} loading={actionsQuery.isLoading} />
        )}
      </div>
    </>
  );

  const tabs = [
    { id: 'info', label: 'المعلومات', panel: infoPanel },
    { id: 'security', label: 'السند والأموال', panel: securityPanel },
    { id: 'delegations', label: 'الإنابات والوقوعات', panel: delegationsPanel },
    { id: 'status', label: 'الحالة', panel: statusPanel },
  ] as const;

  return (
    <div className="max-w-6xl mx-auto">
      <Link
        to="/portal"
        className="inline-block text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 min-h-11 mb-4"
      >
        ← رجوع إلى ملفات الجهة
      </Link>

      <div className="sticky top-0 z-30 bg-white/95 backdrop-blur border-b border-gray-200 rounded-b-xl shadow-sm px-4 py-3 mb-5">
        <div className="flex items-center justify-between gap-3 flex-wrap">
          <h2 className="text-xl md:text-2xl font-bold text-gray-800 flex items-center gap-3 min-w-0">
            <span className={`rounded-full px-3 py-1 text-sm ${statusBadge.cls}`}>
              {statusBadge.text}
            </span>
            <span className="min-w-0 truncate">{title}</span>
          </h2>
          <div className="flex gap-2 flex-wrap">
            {/* زر «مراسلات» جانب العنوان: يتمرير إلى بطاقة مراسلات الملف (ما المندوب طرف فيه). */}
            <a
              href="#file-correspondence"
              onClick={(e) => {
                e.preventDefault();
                const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
                document
                  .getElementById('file-correspondence')
                  ?.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });
              }}
              className="bg-sky-800 hover:bg-sky-700 text-white rounded-lg px-4 py-2 text-sm inline-flex items-center min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-700"
            >
              مراسلات
            </a>
          </div>
        </div>

        {/* شريط الهوية: بطاقات الملف الأساسية (رقم/سنة/دائرة) لقراءة فورية أثناء التمرير. */}
        <dl className="mt-3 flex flex-wrap gap-2 text-sm">
          <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-emerald-50 border border-emerald-200 px-3 py-1.5">
            <dt className="text-xs text-emerald-800 font-medium">رقم الملف</dt>
            <dd className="text-gray-800 font-semibold tabular-nums">
              {doc.displayFileNumber ?? doc.fileNumber ?? '—'}
            </dd>
          </div>
          <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-gray-50 border border-gray-200 px-3 py-1.5">
            <dt className="text-xs text-gray-500 font-medium">السنة</dt>
            <dd className="text-gray-800 font-semibold tabular-nums">
              {doc.displayFileYear ?? doc.fileYear ?? '—'}
            </dd>
          </div>
          <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-gray-50 border border-gray-200 px-3 py-1.5">
            <dt className="text-xs text-gray-500 font-medium">الدائرة</dt>
            <dd className="text-gray-800 font-semibold">{doc.court || '—'}</dd>
          </div>
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
        <>
          <div className="grid md:grid-cols-3 gap-5 items-stretch">
            <div className="flex flex-col gap-5 min-w-0">{infoPanel}</div>
            <div className="flex flex-col gap-5 min-w-0">{securityPanel}</div>
            <div className="flex flex-col gap-5 min-w-0">{delegationsPanel}</div>
          </div>
          <div className="mt-6 flex flex-col gap-5">{statusPanel}</div>
        </>
      )}

      {/* مراسلات الملف للمندوب: ما هو طرف فيه فقط (تسطير/رد/مشاهدة عبر مسارات البوابة). */}
      {id !== undefined && (
        <div className="mt-6 flex flex-col gap-5">
          <DocumentCorrespondenceCard
            documentId={Number(id)}
            documentTitle={modalTitle}
            canCreate={true}
            portal={true}
          />
        </div>
      )}

      {partyModal && <PartyDetailsModal modal={partyModal} onClose={() => setPartyModal(null)} />}

      {historyOpen && id !== undefined && (
        <BaseNumbersModal
          documentId={Number(id)}
          documentTitle={modalTitle}
          fileType={doc.fileType}
          fetchUrl={`/portal/files/${id}/base-numbers`}
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
          documentTitle={modalTitle}
          occurrences={doc.occurrences ?? []}
          onClose={() => setOccurrencesOpen(false)}
        />
      )}

      <AppealInfoModal appeal={infoAppeal} hideOpinion onClose={() => setInfoAppeal(null)} />

      <p className="mt-4 text-xs text-gray-400">عرض قرائي عبر بوابة الجهة العامة.</p>
    </div>
  );
}