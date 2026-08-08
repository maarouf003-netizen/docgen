import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { getDocumentStatus, getDocumentBadge } from '../utils/documentStatus';
import ExecutionActionsModal from '../components/ExecutionActionsModal';
import TransferDocumentModal from '../components/TransferDocumentModal';
import FileAlertModal from '../components/FileAlertModal';
import BaseNumbersModal from '../components/BaseNumbersModal';
import type {
  DocumentResponse,
  ExecutedHeirDto,
  HeirDto,
  RealEstateDto,
} from '../types';

function Row({
  label,
  value,
  showEmpty = false,
}: {
  label: string;
  value?: string | number | null;
  showEmpty?: boolean;
}) {
  const empty = value === undefined || value === null || value === '';
  if (!showEmpty && empty) return null;
  return (
    <div className="py-2 border-b border-gray-100 last:border-0">
      <span className="text-gray-500 text-xs block">{label}</span>
      <span className="text-gray-800">{empty ? '—' : value}</span>
    </div>
  );
}

function formatAmount(numeric: number, currency?: string): string {
  return numeric > 0 ? `${numeric} ${currency ?? ''}`.trim() : '';
}

function formatFileNumber(doc: DocumentResponse): string {
  const number = doc.displayFileNumber ?? doc.fileNumber ?? '';
  const parts = [number];
  if (doc.fileType) parts.push(doc.fileType);
  if (doc.fileYear) parts.push(`لعام ${doc.fileYear}`);
  return parts.filter(Boolean).join(' ');
}

function formatApplicant(doc: DocumentResponse): string {
  if (!doc.applicant) return '';
  return doc.branchName ? `${doc.applicant} — ${doc.branchName}` : doc.applicant;
}

type BasicDoc = { code: string; label: string };

const BASIC_DOCS: BasicDoc[] = [
  { code: '001', label: 'استدعاء تنفيذي' },
  { code: '002', label: 'محضر تنفيذي' },
  { code: '004', label: 'حجز منظومة' },
  { code: 'PS', label: 'حجز عقاري' },
];

function buildStatusSummary(doc: DocumentResponse): string {
  if (doc.execStatus === 'منفذ بالتسوية') {
    const parts = ['منفذ بموجب كتاب براءة الذمة'];
    if (doc.baraetNumber) parts.push(`رقم ${doc.baraetNumber}`);
    if (doc.baraetDate) parts.push(`تاريخ ${doc.baraetDate}`);
    const reg: string[] = [];
    if (doc.baraetRegNumber) reg.push(`برقم ${doc.baraetRegNumber}`);
    if (doc.baraetRegDate) reg.push(`تاريخ ${doc.baraetRegDate}`);
    if (reg.length) parts.push(`والمسجل ${reg.join(' ')}`);
    return parts.join(' ');
  }
  if (doc.execStatus === 'تريث') {
    const parts = ['تريث بموجب كتاب التريث'];
    if (doc.tarithNumber) parts.push(`رقم ${doc.tarithNumber}`);
    if (doc.tarithDate) parts.push(`تاريخ ${doc.tarithDate}`);
    const reg: string[] = [];
    if (doc.tarithRegNumber) reg.push(`برقم ${doc.tarithRegNumber}`);
    if (doc.tarithRegDate) reg.push(`تاريخ ${doc.tarithRegDate}`);
    if (reg.length) parts.push(`والمسجل ${reg.join(' ')}`);
    return parts.join(' ');
  }
  if (doc.execStatus === 'منفذ جبريا') {
    const parts = ['منفذ جبريا'];
    if (doc.execSubStatus) parts.push(`(${doc.execSubStatus})`);
    if (doc.collectedAmount != null && doc.collectedAmount > 0) {
      parts.push(`المبلغ المحصل: ${doc.collectedAmount}`);
    }
    return parts.join(' ');
  }
  return getDocumentStatus(doc);
}

function ContractSection({ doc }: { doc: DocumentResponse }) {
  const isOrdinary = doc.contractTypeSelector === 'عادي';
  const hasOrdinaryAmount = doc.inclusionAmountNumeric > 0 || Boolean(doc.inclusionAmountWords);
  const hasSecondAmount = doc.amount2Numeric > 0;

  return (
    <div className="bg-white rounded-xl shadow p-5">
      <h3 className="font-bold text-gray-800 mb-3">بيانات السند التنفيذي</h3>
      <Row label="نوع السند" value={doc.contractTypeSelector} showEmpty />
      {isOrdinary ? (
        <>
          <Row label="رقم القرار" value={doc.contractNumber} showEmpty />
          <Row label="تاريخ القرار" value={doc.contractDate} showEmpty />
          <Row label="المحكمة مصدرة القرار" value={doc.contractType} showEmpty />
          <Row label="خلاصة الحكم" value={doc.inclusionText} showEmpty />
          {hasOrdinaryAmount && (
            <>
              <Row label="المبلغ المطالب به" value={formatAmount(doc.inclusionAmountNumeric, doc.inclusionCurrency)} />
              <Row label="المبلغ كتابة" value={doc.inclusionAmountWords} />
            </>
          )}
        </>
      ) : (
        <>
          <Row label="نوع العقد" value={doc.contractType} showEmpty />
          <Row label="رقم العقد" value={doc.contractNumber} showEmpty />
          <Row label="تاريخ العقد" value={doc.contractDate} showEmpty />
          <Row label="المبلغ المطالب به" value={formatAmount(doc.amountNumeric, doc.currency)} showEmpty />
          <Row label="المبلغ كتابة" value={doc.amountWords} />
          {hasSecondAmount && (
            <>
              <Row label="المبلغ الثاني" value={formatAmount(doc.amount2Numeric, doc.currency2)} />
              <Row label="المبلغ الثاني كتابة" value={doc.amount2Words} />
            </>
          )}
        </>
      )}
    </div>
  );
}

type PersonFields = {
  name?: string;
  father?: string;
  family?: string;
  mother?: string;
  birth?: string;
  register?: string;
  nationalId?: string;
  addressType?: string;
  address?: string;
};

function fullName(person: PersonFields): string {
  return [person.name, person.father, person.family].filter(Boolean).join(' ');
}

function PersonDetails({ person, showEmpty = false }: { person: PersonFields; showEmpty?: boolean }) {
  return (
    <>
      <Row label="الاسم الثلاثي" value={fullName(person)} showEmpty={showEmpty} />
      <Row label="اسم الأم" value={person.mother} showEmpty={showEmpty} />
      <Row label="مكان وتاريخ الولادة" value={person.birth} showEmpty={showEmpty} />
      <Row label="مكان ورقم القيد" value={person.register} showEmpty={showEmpty} />
      <Row label="الرقم الوطني" value={person.nationalId} showEmpty={showEmpty} />
      {person.addressType === 'يمثله' ? (
        <Row label="وكيله" value={person.address} showEmpty={showEmpty} />
      ) : (
        <>
          <Row label="نوع العنوان" value={person.addressType} showEmpty={showEmpty} />
          <Row label="العنوان" value={person.address} showEmpty={showEmpty} />
        </>
      )}
    </>
  );
}

function HeirsDisplay({ heirs, deceasedName }: { heirs: HeirDto[] | undefined; deceasedName: string }) {
  const visible = (heirs ?? []).filter((h) => (h.name ?? '').trim());
  if (visible.length === 0) return null;
  return (
    <div className="mt-2 pt-2 border-t border-dashed border-gray-200">
      <span className="text-gray-500 text-xs block mb-1">
        ورثة المتوفى ({deceasedName})
      </span>
      {visible.map((h, i) => (
        <span key={i} className="text-gray-800 block text-sm">
          {h.name}
          {(h.address ?? '').trim()
            ? h.addressType === 'وكيل'
              ? ` — يمثله: ${h.address}`
              : ` — ${h.addressType ?? 'عنوان'}: ${h.address}`
            : ''}
        </span>
      ))}
    </div>
  );
}

function ExecutedHeirsDisplay({ heirs, deceasedName }: { heirs: ExecutedHeirDto[] | undefined; deceasedName: string }) {
  const visible = (heirs ?? []).filter((h) => (h.heirName ?? '').trim());
  if (visible.length === 0) return null;
  return (
    <div className="mt-2 pt-2 border-t border-dashed border-gray-200">
      <span className="text-gray-500 text-xs block mb-1">
        ورثة المتوفى ({deceasedName})
      </span>
      {visible.map((h, i) => (
        <span key={i} className="text-gray-800 block text-sm">
          {[h.heirName, h.heirFather, h.heirFamily].filter(Boolean).join(' ')}
          {(h.heirAddress ?? '').trim()
            ? h.addressType === 'وكيل'
              ? ` — يمثله: ${h.heirAddress}`
              : ` — ${h.addressType ?? 'عنوان'}: ${h.heirAddress}`
            : ''}
        </span>
      ))}
    </div>
  );
}

function ExecutedApplicantsSection({ doc }: { doc: DocumentResponse }) {
  return (
    <div className="bg-white rounded-xl shadow p-5">
      <h3 className="font-bold text-gray-800 mb-3">طالب التنفيذ</h3>
      {doc.executionApplicants.length === 0 && <p className="text-gray-400 text-sm">لا يوجد طالب تنفيذ</p>}
      {doc.executionApplicants.map((a, i) => (
        <div key={a.id ?? i} className="mb-4 pb-4 border-b border-gray-100 last:border-0">
          <div className="font-bold text-emerald-800 mb-2">طالب التنفيذ {i + 1}</div>
          <Row label="الاسم الثلاثي" value={fullName(a)} showEmpty />
          <Row label="الوكيل القانوني" value={a.legalRepresentative} showEmpty />
          <Row label="نوع التمثيل" value={a.representationType} showEmpty />
          {a.representationType === 'إضافة لتركة' && (
            <>
              <Row
                label="المورث المتوفى"
                value={fullName({ name: a.deceasedName, father: a.deceasedFather, family: a.deceasedFamily })}
                showEmpty
              />
              <ExecutedHeirsDisplay
                heirs={a.heirs}
                deceasedName={fullName({ name: a.deceasedName, father: a.deceasedFather, family: a.deceasedFamily }) || fullName(a)}
              />
            </>
          )}
        </div>
      ))}
    </div>
  );
}

function ExecutedEntitiesSection({ doc }: { doc: DocumentResponse }) {
  return (
    <div className="bg-white rounded-xl shadow p-5">
      <h3 className="font-bold text-gray-800 mb-3">الجهات العامة المنفذ عليها</h3>
      {doc.executedPublicEntities.length === 0 && <p className="text-gray-400 text-sm">لا توجد جهات عامة</p>}
      {doc.executedPublicEntities.map((e, i) => (
        <div key={e.id ?? i} className="flex flex-wrap items-center gap-x-5 gap-y-1 py-2 border-b border-gray-100 last:border-0 text-sm">
          <span className="font-bold text-gray-700">جهة عامة {i + 1}</span>
          <span className="text-gray-800">{e.entityName || '—'}</span>
          <span className="text-gray-500">
            الفرع: <span className="text-gray-800">{e.entityBranch || '—'}</span>
          </span>
        </div>
      ))}
    </div>
  );
}

function ExecutedNaturalPersonsSection({ doc }: { doc: DocumentResponse }) {
  return (
    <div className="bg-white rounded-xl shadow p-5">
      <h3 className="font-bold text-gray-800 mb-3">الأشخاص الطبيعيون المنفذ عليهم</h3>
      {doc.executedNaturalPersons.length === 0 && <p className="text-gray-400 text-sm">لا يوجد أشخاص طبيعيون</p>}
      {doc.executedNaturalPersons.map((p, i) => (
        <div key={p.id ?? i} className="mb-4 pb-4 border-b border-gray-100 last:border-0">
          <div className="font-bold text-emerald-800 mb-2">شخص طبيعي {i + 1}</div>
          <Row label="الاسم الثلاثي" value={fullName(p)} showEmpty />
          <Row label="نوع العنوان" value={p.addressType} showEmpty />
          <Row
            label={p.addressType === 'وكيل' ? 'الوكيل' : 'العنوان'}
            value={p.addressOrRepresentative}
            showEmpty
          />
          <Row label="نوع التمثيل" value={p.representationType} showEmpty />
          {p.representationType === 'إضافة لتركة' && (
            <>
              <Row
                label="المورث المتوفى"
                value={fullName({ name: p.deceasedName, father: p.deceasedFather, family: p.deceasedFamily })}
                showEmpty
              />
              <ExecutedHeirsDisplay
                heirs={p.heirs}
                deceasedName={fullName({ name: p.deceasedName, father: p.deceasedFather, family: p.deceasedFamily }) || fullName(p)}
              />
            </>
          )}
        </div>
      ))}
    </div>
  );
}

/** عنوان ملف وضع «منفذ عليه»: أول منفذ عليه (طبيعي/جهة)، ثم طالب التنفيذ، ثم الصفة. */
function executedTitle(doc: DocumentResponse): string {
  const person = doc.executedNaturalPersons[0];
  const personName = person ? fullName(person) : '';
  const entity = doc.executedPublicEntities[0]?.entityName ?? '';
  const applicant = doc.applicant ?? '';
  return personName || entity || applicant || doc.generalEntitySideLabel || `مستند #${doc.id}`;
}

function formatDate(value?: string): string {
  if (!value) return '';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString('ar-SY');
}

function PartiesSection({ doc }: { doc: DocumentResponse }) {
  const isOrdinary = doc.contractTypeSelector === 'عادي';
  const title = isOrdinary ? 'المنفذ عليهم الآخرون' : 'الكفلاء';
  const itemLabel = isOrdinary ? 'منفذ عليه' : 'كفيل';
  const emptyText = isOrdinary ? 'لا يوجد منفذ عليهم آخرون' : 'لا يوجد كفلاء';

  return (
    <div className="bg-white rounded-xl shadow p-5">
      <h3 className="font-bold text-gray-800 mb-3">{title}</h3>
      {doc.guarantors.length === 0 && <p className="text-gray-400 text-sm">{emptyText}</p>}
      {doc.guarantors.map((g, i) => (
        <div key={g.id ?? g.guarantorNumber} className="mb-4 pb-4 border-b border-gray-100 last:border-0">
          <div className="font-bold text-emerald-800 mb-2">
            {itemLabel} {isOrdinary ? i + 2 : (g.guarantorNumber ?? i + 1)}
          </div>
          <PersonDetails person={g} />
          <HeirsDisplay heirs={g.heirs} deceasedName={fullName(g)} />
        </div>
      ))}
    </div>
  );
}

function RealEstatesSection({ doc }: { doc: DocumentResponse }) {
  return (
    <div className="bg-white rounded-xl shadow p-5">
      <h3 className="font-bold text-gray-800 mb-3">العقارات</h3>
      {doc.realEstates.length === 0 && <p className="text-gray-400 text-sm">لا توجد عقارات</p>}
      {doc.realEstates.map((r, i) => (
        <div
          key={r.id ?? i}
          className="flex flex-wrap items-center gap-x-5 gap-y-1 py-2 border-b border-gray-100 last:border-0 text-sm"
        >
          <span className="font-bold text-gray-700">عقار {r.property ?? i + 1}</span>
          <span className="inline-flex items-center gap-1">
            <span className="text-gray-500">رقم العقار</span>
            <span className="text-gray-800">{r.propertyNumber || '—'}</span>
          </span>
          <span className="inline-flex items-center gap-1">
            <span className="text-gray-500">المنطقة العقارية</span>
            <span className="text-gray-800">{r.propertyDistrict || '—'}</span>
          </span>
          <span className="inline-flex items-center gap-1">
            <span className="text-gray-500">المصالح العقارية المختصة</span>
            <span className="text-gray-800">{r.landRegistry || '—'}</span>
          </span>
          <span className="inline-flex items-center gap-1">
            <span className="text-gray-500">مالك العقار</span>
            <span className="text-gray-800">{(r.owners ?? []).join(' و ') || '—'}</span>
          </span>
        </div>
      ))}
    </div>
  );
}

function EstateSelection({
  estates,
  selected,
  onToggle,
}: {
  estates: RealEstateDto[];
  selected: number[];
  onToggle: (id: number) => void;
}) {
  if (estates.length === 0) {
    return <p className="text-gray-400 text-sm">لا توجد ضمانات عقارية — أضف عقاراً أولاً ثم احفظ</p>;
  }

  return (
    <div className="flex flex-wrap gap-x-5 gap-y-2">
      {estates.map((r, i) => (
        <label key={r.id ?? i} className="inline-flex items-center gap-2 text-sm cursor-pointer min-h-11">
          <input
            type="checkbox"
            checked={r.id !== undefined && selected.includes(r.id)}
            onChange={() => r.id !== undefined && onToggle(r.id)}
          />
          {r.property} — {(r.owners ?? []).join(' و ')}
        </label>
      ))}
    </div>
  );
}

export default function DocumentView() {
  const { id } = useParams();
  const { user } = useAuth();
  const [doc, setDoc] = useState<DocumentResponse | null>(null);
  const [error, setError] = useState('');
  const [generating, setGenerating] = useState('');
  const [downloadError, setDownloadError] = useState('');
  const [downloadSuccess, setDownloadSuccess] = useState('');
  const [noticeSel, setNoticeSel] = useState<number[]>([]);
  const [estateSel, setEstateSel] = useState<number[]>([]);
  const [actionsOpen, setActionsOpen] = useState(false);
  const [transferOpen, setTransferOpen] = useState(false);
  const [alertOpen, setAlertOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [execStatus, setExecStatus] = useState('');
  const [execStatusBusy, setExecStatusBusy] = useState(false);
  const [execStatusError, setExecStatusError] = useState('');
  const [execStatusMsg, setExecStatusMsg] = useState('');

  const load = () => {
    api
      .get<DocumentResponse>(`/documents/${id}`)
      .then((r) => {
        setDoc(r.data);
        setExecStatus(r.data.executedStatus ?? '');
        setExecStatusError('');
      })
      .catch((err) => setError(getApiErrorMessage(err)));
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  if (error) return <div className="text-red-600">{error}</div>;
  if (!doc) return <div className="text-gray-500">جارِ التحميل...</div>;

  const canEdit = user?.role === 'lawyer';
  const canTransfer = user?.role === 'head';
  const canDirectAlert = user?.role === 'head';
  const isExecuted = doc.generalEntitySide === 'executed';

  const saveExecutedStatus = async () => {
    if (!id) return;
    setExecStatusBusy(true);
    setExecStatusError('');
    setExecStatusMsg('');
    try {
      await api.post(`/documents/${id}/executed-status`, { status: execStatus });
      setExecStatusMsg('تم تحديث حالة الملف');
      load();
    } catch (err) {
      setExecStatusError(getApiErrorMessage(err));
    } finally {
      setExecStatusBusy(false);
    }
  };

  const restoreStruckOff = async () => {
    if (!id) return;
    setExecStatusBusy(true);
    setExecStatusError('');
    setExecStatusMsg('');
    try {
      await api.post(`/documents/${id}/restore-struck-off`);
      setExecStatusMsg('أعيد الملف المشطوب إلى المتداول');
      load();
    } catch (err) {
      setExecStatusError(getApiErrorMessage(err));
    } finally {
      setExecStatusBusy(false);
    }
  };

  const toggleNotice = (number: number) => {
    setNoticeSel((prev) =>
      prev.includes(number) ? prev.filter((x) => x !== number) : [...prev, number],
    );
  };

  const toggleEstate = (estateId: number) => {
    setEstateSel((prev) =>
      prev.includes(estateId) ? prev.filter((x) => x !== estateId) : [...prev, estateId],
    );
  };

  const downloadOne = async (code: string, recipient: number, estateIds: number[], heirId?: number) => {
    const res = await api.get(`/documents/${id}/generate`, {
      params: {
        template: code,
        recipient,
        heirId: heirId || undefined,
        estateIds: estateIds.length > 0 ? estateIds : undefined,
      },
      responseType: 'blob',
    });
    const disposition = (res.headers['content-disposition'] as string | undefined) ?? '';
    const match = disposition.match(/filename="?([^";]+)"?/i);
    const filename = match?.[1] ?? `مستند_${code}.docx`;

    const url = URL.createObjectURL(res.data as Blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
  };

  const runGeneration = async (
    code: string,
    task: () => Promise<void>,
    successMessage: string,
    failMessage: string,
  ) => {
    setGenerating(code);
    setDownloadError('');
    setDownloadSuccess('');
    try {
      await task();
      setDownloadSuccess(successMessage);
    } catch {
      setDownloadError(failMessage);
    } finally {
      setGenerating('');
    }
  };

  const generateBasic = async (code: string, label: string) => {
    await runGeneration(
      code,
      () => downloadOne(code, 0, []),
      `✅ تم إنشاء ${label} بنجاح`,
      `فشل توليد ${label} — تحقق من اكتمال البيانات`,
    );
  };

  const generateSeizure = async () => {
    if (estateSel.length === 0) {
      setDownloadError('حجز عقاري: اختر عقاراً واحداً على الأقل');
      return;
    }
    await runGeneration(
      'PS',
      async () => {
        for (const estateId of estateSel) {
          await downloadOne('PS', 0, [estateId]);
        }
      },
      `✅ تم إنشاء ${estateSel.length} مستند حجز عقاري`,
      'فشل توليد حجز عقاري — تحقق من اكتمال البيانات',
    );
  };

  const generateNotice = async (code: '003' | '007') => {
    if (noticeSel.length === 0) {
      setDownloadError('اختر شخصاً واحداً على الأقل من قائمة المنفَّذ عليهم');
      return;
    }
    const label = code === '007' ? 'إخطار تنفيذي بالصحف' : 'إخطار تنفيذي';
    await runGeneration(
      code,
      async () => {
        for (const person of noticePersons) {
          if (!noticeSel.includes(person.number)) continue;
          await downloadOne(code, person.heirId != null ? 0 : person.number, [], person.heirId);
        }
      },
      code === '007'
        ? `✅ تم إنشاء ${noticeSel.length} إخطار تنفيذي بالصحف بنجاح`
        : `✅ تم إنشاء ${noticeSel.length} إخطار بنجاح`,
      `فشل توليد ${label} — تحقق من اكتمال البيانات`,
    );
  };

  const generateEstateNotice = async (code: '005' | '006') => {
    if (estateSel.length === 0) {
      setDownloadError('اختر عقاراً واحداً على الأقل من قائمة العقارات');
      return;
    }
    if (multiOwner) {
      setDownloadError('يجب أن تكون العقارات لنفس المالك');
      return;
    }
    const label = code === '005' ? 'إخطار بيع أموال غير منقولة' : 'إخطار بيع أموال غير منقولة بالصحف';
    const ownerHeirs = findEstateHeirs(selectedEstates[0]);
    const heirList = ownerHeirs ?? [];
    const hasIdHeirs = heirList.filter((h) => h.id != null);
    const perHeir = heirList.length > 0 && hasIdHeirs.length > 0;
    await runGeneration(
      code,
      () =>
        perHeir
          ? (async () => {
              for (const heir of hasIdHeirs) {
                await downloadOne(code, 0, estateSel, heir.id);
              }
            })()
          : downloadOne(code, 0, estateSel),
      perHeir
        ? code === '005'
          ? `✅ تم إنشاء ${hasIdHeirs.length} إخطار بيع أموال غير منقولة بنجاح`
          : `✅ تم إنشاء ${hasIdHeirs.length} إخطار بيع أموال غير منقولة بالصحف بنجاح`
        : code === '005'
          ? '✅ تم إنشاء إخطار بيع أموال غير منقولة بنجاح'
          : 'تم إنشاء إخطار بيع أموال غير منقولة بالصحف بنجاح',
      `فشل توليد ${label} — تحقق من اكتمال البيانات`,
    );
  };

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

  const isOrdinary = doc.contractTypeSelector === 'عادي';

  const findEstateHeirs = (estate?: RealEstateDto): HeirDto[] | null => {
    const owners = (estate?.owners ?? []).filter((o) => (o ?? '').trim());
    if (owners.length === 0) return null;

    const result: HeirDto[] = [];
    const seen = new Set<number | string>();
    const add = (heir: HeirDto) => {
      const key = heir.id ?? (heir.name ?? '').trim();
      if (!key || seen.has(key)) return;
      seen.add(key);
      result.push(heir);
    };

    for (const owner of owners) {
      if (doc.borrowerHeirs?.length && owner === fullName(debtor)) {
        doc.borrowerHeirs.forEach(add);
        continue;
      }
      const guarantor = doc.guarantors.find((x) => fullName(x) === owner);
      if (guarantor?.heirs?.length) {
        guarantor.heirs.forEach(add);
        continue;
      }
      [
        ...(doc.borrowerHeirs ?? []).filter((h) => (h.name ?? '').trim() === owner),
        ...doc.guarantors.flatMap((x) =>
          (x.heirs ?? []).filter((h) => (h.name ?? '').trim() === owner),
        ),
      ].forEach(add);
    }

    return result.length > 0 ? result : null;
  };

  const noticePersons: { number: number; heirId?: number; label: string }[] = [];
  let heirKey = 100;
  const pushHeirs = (heirs: HeirDto[] | undefined, deceasedFull: string) => {
    (heirs ?? []).forEach((h) => {
      const name = (h.name ?? '').trim();
      if (!name) return;
      noticePersons.push({
        number: heirKey++,
        heirId: h.id,
        label: `الوريث :  ${name} — إضافة لتركة ${deceasedFull}`,
      });
    });
  };
  if (doc.borrowerName) {
    if (doc.borrowerHeirs && doc.borrowerHeirs.length > 0) {
      pushHeirs(doc.borrowerHeirs, fullName(debtor));
    } else {
      noticePersons.push({
        number: 0,
        label: `المقترض :  ${[doc.borrowerName, doc.borrowerFamily].filter(Boolean).join(' ')}`,
      });
    }
  }
  doc.guarantors.forEach((g) => {
    const role = isOrdinary ? 'منفذ عليه' : 'كفيل';
    const family = g.family ? `  ${g.family}` : '';
    const gFull = fullName(g);
    if (g.heirs && g.heirs.length > 0) {
      pushHeirs(g.heirs, gFull || `${role} ${g.guarantorNumber}`);
    } else {
      noticePersons.push({
        number: g.guarantorNumber,
        label: `${role} ${g.guarantorNumber} :  ${g.name ?? ''}${family}`,
      });
    }
  });
  const selectedEstates = doc.realEstates.filter(
    (r) => r.id !== undefined && estateSel.includes(r.id),
  );
  const estateOwnersKey = (r: RealEstateDto) => [...(r.owners ?? [])].sort().join('|');
  const multiOwner = new Set(selectedEstates.map(estateOwnersKey)).size > 1;

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
          <button
            onClick={() => setActionsOpen(true)}
            className="bg-blue-700 hover:bg-blue-600 text-white rounded-lg px-4 py-2 text-sm min-h-11"
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

      <div className="grid md:grid-cols-2 gap-6">
        {isExecuted ? (
          <>
            <ExecutedApplicantsSection doc={doc} />
            <ExecutedEntitiesSection doc={doc} />
            <ExecutedNaturalPersonsSection doc={doc} />
          </>
        ) : (
          <>
            <div className="bg-white rounded-xl shadow p-5">
              <h3 className="font-bold text-gray-800 mb-3">بيانات المنفذ عليه</h3>
              <PersonDetails person={debtor} showEmpty />
              <HeirsDisplay heirs={doc.borrowerHeirs} deceasedName={fullName(debtor)} />
            </div>
            <ContractSection doc={doc} />
            <PartiesSection doc={doc} />
            <RealEstatesSection doc={doc} />
          </>
        )}

        <div className="bg-white rounded-xl shadow p-5">
          <h3 className="font-bold text-gray-800 mb-3">بيانات الملف</h3>
          <Row label="دائرة التنفيذ" value={doc.court} />
          <Row label="طالب التنفيذ" value={formatApplicant(doc)} />
          <Row label="المحامي" value={doc.lawyer} />
          <div className="py-2 border-b border-gray-100 last:border-0">
            <span className="text-gray-500 text-xs block">رقم الملف</span>
            {formatFileNumber(doc) ? (
              <button
                type="button"
                onClick={() => setHistoryOpen(true)}
                aria-label="عرض أرقام الأساس للسنوات السابقة"
                className="text-emerald-800 font-medium hover:underline inline-flex items-center gap-1 min-h-11"
              >
                {formatFileNumber(doc)}
              </button>
            ) : (
              <span className="text-gray-800">—</span>
            )}
          </div>
          <Row label="رقم كتاب الجهة العامة" value={doc.fileIncoming} />
          <Row label="تاريخ كتاب الجهة العامة" value={doc.fileIncomingDate} />
          <Row label="رقم تحت رفع" value={doc.underFilingNumber} />
          <Row label="تاريخ قيد الملف" value={doc.fileRegistrationDate} />
          <Row label="تاريخ ورود الملف" value={formatDate(doc.fileReceiptDate)} />
          <Row label="تاريخ الحجز" value={doc.seizureDate} />
          {isExecuted && (
            <>
              <Row
                label="حالة الملف"
                value={doc.executedStatus || 'متداول'}
                showEmpty
              />
              <Row label="المبلغ المطلوب دفعه من الجهة العامة" value={formatAmount(doc.executedRequiredAmount ?? 0)} showEmpty />
              <Row label="المبلغ الذي دفعته الجهة العامة" value={formatAmount(doc.executedPaidAmount ?? 0)} showEmpty />
              <Row label="كيفية تنفيذ الملف" value={doc.executedDescription} />
              {doc.struckOffDate && (
                <Row label="تاريخ الشطب" value={formatDate(doc.struckOffDate)} showEmpty />
              )}
            </>
          )}
          <Row label="منشئ المستند" value={doc.createdByName} />
        </div>
      </div>

      {isExecuted ? (
        <div className="bg-white rounded-xl shadow p-5 mt-6">
          <h3 className="font-bold text-gray-800 mb-3">حالة الملف</h3>
          {execStatusMsg && (
            <div className="bg-emerald-50 border border-emerald-200 text-emerald-700 rounded-lg px-4 py-2 mb-3 text-sm">
              {execStatusMsg}
            </div>
          )}
          {execStatusError && <p className="text-red-600 text-sm mb-3">{execStatusError}</p>}
          <div className="flex items-center gap-3 flex-wrap">
            <span className={`rounded-full px-3 py-1 text-sm ${statusBadge.cls}`}>
              {statusBadge.text}
            </span>
            {doc.struckOffDate && (
              <span className="text-xs text-gray-500">تاريخ الشطب: {formatDate(doc.struckOffDate)}</span>
            )}
          </div>
          {doc.executedStatus === 'مشطوب' ? (
            canEdit && (
              <button
                type="button"
                onClick={restoreStruckOff}
                disabled={execStatusBusy}
                className="mt-4 bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {execStatusBusy ? 'جارِ الإعادة...' : 'إعادة الملف إلى المتداول'}
              </button>
            )
          ) : canEdit ? (
            <div className="mt-4 flex flex-wrap items-end gap-3">
              <div>
                <label htmlFor="executedStatus" className="block text-xs font-bold text-gray-600 mb-1">
                  الحالة
                </label>
                <select
                  id="executedStatus"
                  value={execStatus}
                  onChange={(e) => setExecStatus(e.target.value)}
                  className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none"
                >
                  <option value="">متداول</option>
                  <option value="منفذ">منفذ</option>
                  <option value="مشطوب">مشطوب</option>
                </select>
              </div>
              <button
                type="button"
                onClick={saveExecutedStatus}
                disabled={execStatusBusy}
                className="bg-blue-700 hover:bg-blue-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {execStatusBusy ? 'جارِ الحفظ...' : 'حفظ الحالة'}
              </button>
            </div>
          ) : null}
        </div>
      ) : (
        <div className="bg-white rounded-xl shadow p-5 mt-6">
          <h3 className="font-bold text-gray-800 mb-3">الحالة</h3>
          <p className="text-gray-800">{buildStatusSummary(doc)}</p>
          <p className="text-xs text-gray-500 mt-2">لتغيير الحالة اضغط زر «تعديل»</p>
        </div>
      )}

      {!isExecuted && (
        <div className="bg-white rounded-xl shadow p-5 mt-6">
          <h3 className="font-bold text-gray-800 mb-3">توليد المستندات التنفيذية</h3>
        {downloadError && <p className="text-red-600 text-sm mb-3">{downloadError}</p>}
        {downloadSuccess && <p className="text-emerald-700 text-sm mb-3">{downloadSuccess}</p>}

        <div className="space-y-4">
          <div className="border border-gray-200 rounded-lg overflow-hidden">
            <div className="bg-gray-800 text-white px-4 py-2 font-bold">المستندات الأساسية</div>
            {BASIC_DOCS.map((row) => (
              <div
                key={row.code}
                className="flex items-center justify-between gap-3 px-4 py-2 border-b border-gray-100 last:border-0"
              >
                <button
                  onClick={() => (row.code === 'PS' ? generateSeizure() : generateBasic(row.code, row.label))}
                  disabled={generating !== ''}
                  className="bg-gray-800 hover:bg-gray-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                >
                  {generating === row.code ? 'جارِ التوليد...' : 'توليد'}
                </button>
                <span className="font-bold text-gray-800">{row.label}</span>
              </div>
            ))}
            <div className="px-4 py-3 border-t border-gray-100">
              <p className="text-gray-600 text-sm mb-2">اختر العقارات التي تريد الحجز عليها :</p>
              <EstateSelection estates={doc.realEstates} selected={estateSel} onToggle={toggleEstate} />
            </div>
          </div>

          <div className="border border-gray-200 rounded-lg overflow-hidden">
            <div className="bg-red-800 text-white px-4 py-2 font-bold">إخطار تنفيذي</div>
            <div className="flex flex-col md:flex-row gap-4 p-4">
              <div className="flex flex-col gap-2 md:w-56">
                <button
                  onClick={() => generateNotice('003')}
                  disabled={generating !== ''}
                  className="bg-red-800 hover:bg-red-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                >
                  {generating === '003' ? 'جارِ التوليد...' : 'توليد إخطار تنفيذي'}
                </button>
                <button
                  onClick={() => generateNotice('007')}
                  disabled={generating !== ''}
                  className="bg-red-800 hover:bg-red-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                >
                  {generating === '007' ? 'جارِ التوليد...' : 'توليد إخطار بالصحف'}
                </button>
              </div>
              <div className="flex-1">
                <p className="text-gray-600 text-sm">اختر المنفَّذ عليهم الذين تريد تسطير إخطار تنفيذي لهم :</p>
                <div className="mt-2 border border-gray-200 rounded-lg p-3 space-y-2">
                  {noticePersons.length === 0 ? (
                    <p className="text-gray-400 text-sm">لا يوجد أشخاص — أدخل المقترض والكفلاء أولاً</p>
                  ) : (
                    noticePersons.map((p) => (
                      <label key={p.number} className="inline-flex items-center gap-2 text-sm cursor-pointer min-h-11">
                        <input
                          type="checkbox"
                          checked={noticeSel.includes(p.number)}
                          onChange={() => toggleNotice(p.number)}
                        />
                        {p.label}
                      </label>
                    ))
                  )}
                </div>
              </div>
            </div>
          </div>

          <div className="border border-gray-200 rounded-lg overflow-hidden">
            <div className="bg-orange-700 text-white px-4 py-2 font-bold">إخطار بيع أموال غير منقولة</div>
            <div className="flex flex-col md:flex-row gap-4 p-4">
              <div className="flex flex-col gap-2 md:w-56">
                <button
                  onClick={() => generateEstateNotice('005')}
                  disabled={generating !== ''}
                  className="bg-orange-700 hover:bg-orange-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                >
                  {generating === '005' ? 'جارِ التوليد...' : 'توليد إخطار بيع غير منقولة'}
                </button>
                <button
                  onClick={() => generateEstateNotice('006')}
                  disabled={generating !== ''}
                  className="bg-orange-700 hover:bg-orange-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                >
                  {generating === '006' ? 'جارِ التوليد...' : 'توليد إخطار بيع بالصحف'}
                </button>
              </div>
              <div className="flex-1">
                <p className="text-gray-600 text-sm">
                  اختر العقارات التي تريد تسطير إخطار بيع أموال غير منقولة بالنسبة لها{' '}
                  <span className="text-red-600 font-bold">(يجب أن تكون لنفس المالك)</span>
                </p>
                <div className="mt-2 border border-gray-200 rounded-lg p-3 space-y-2">
                  <EstateSelection estates={doc.realEstates} selected={estateSel} onToggle={toggleEstate} />
                  {multiOwner && (
                    <p className="text-red-600 text-xs">⚠️  العقارات لمالكين مختلفين — اختر عقارات مالك واحد فقط</p>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
      )}

      {actionsOpen && id !== undefined && (
        <ExecutionActionsModal
          documentId={Number(id)}
          onClose={() => setActionsOpen(false)}
          onChanged={load}
        />
      )}

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
    </div>
  );
}
