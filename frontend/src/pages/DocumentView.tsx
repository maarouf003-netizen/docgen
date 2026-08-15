import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { getDocumentBadge } from '../utils/documentStatus';
import { isExecutedLike } from '../utils/documentDisplay';
import { saveLastViewedDocumentId } from '../utils/listSession';
import ExecutionActionsModal from '../components/ExecutionActionsModal';
import ExecutedStatusModal from '../components/ExecutedStatusModal';
import StatusChangeModal from '../components/StatusChangeModal';
import TransferDocumentModal from '../components/TransferDocumentModal';
import FileAlertModal from '../components/FileAlertModal';
import BaseNumbersModal from '../components/BaseNumbersModal';
import type { DocumentResponse } from '../types';
import { DocumentGenerationSection } from '../components/view/DocumentGenerationSection';
import { ExecutoryDocumentCard } from '../components/view/ExecutoryDocumentCard';
import { FileDataCard } from '../components/view/FileDataCard';
import { OccurrencesCard } from '../components/view/OccurrencesCard';
import { OccurrencesModal } from '../components/view/OccurrencesModal';
import { PartiesCard } from '../components/view/PartiesCard';
import { PartyDetailsModal } from '../components/view/PartyDetailsModal';
import { RealEstatesSection } from '../components/view/RealEstatesSection';
import { TransferHistoryModal } from '../components/view/TransferHistoryModal';
import {
  buildStatusSummary,
  executedTitle,
  fullName,
} from '../components/view/viewFormat';
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
  const [partyModal, setPartyModal] = useState<PartyModal | null>(null);

  const load = () => {
    api
      .get<DocumentResponse>(`/documents/${id}`)
      .then((r) => {
        setDoc(r.data);
        setError('');
      })
      .catch((err) => setError(getApiErrorMessage(err)));
  };

  useEffect(() => {
    load();
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
          {canEdit && (
            <button
              onClick={() => setStatusOpen(true)}
              className="bg-blue-700 hover:bg-blue-600 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              تغيير الحالة
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
        {isExecuted ? (
          <OccurrencesCard doc={doc} onOpen={() => setOccurrencesOpen(true)} />
        ) : (
          <>
            <RealEstatesSection doc={doc} />
            {/* «وقوعات الملف» لنظام «طالبة تنفيذ»: تسجّل إجراءات تغيير الحالة (تريث/منفذ/تراجع). */}
            <OccurrencesCard doc={doc} onOpen={() => setOccurrencesOpen(true)} />
          </>
        )}
      </div>

      {isExecuted ? null : (
        <div className="bg-white rounded-xl shadow p-5 mt-6">
          <h3 className="font-bold text-gray-800 mb-3">الحالة</h3>
          <p className="text-gray-800">{buildStatusSummary(doc)}</p>
          <p className="text-xs text-gray-500 mt-2">لتغيير الحالة اضغط زر «تغيير الحالة»</p>
        </div>
      )}

      {!isExecuted && <DocumentGenerationSection doc={doc} id={id} />}

      {actionsOpen && id !== undefined && (
        <ExecutionActionsModal
          documentId={Number(id)}
          onClose={() => setActionsOpen(false)}
          onChanged={load}
        />
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
    </div>
  );
}
