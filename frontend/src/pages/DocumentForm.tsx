import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useParams, Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import AutoResizeTextarea from '../components/AutoResizeTextarea';
import type {
  DocumentResponse,
  DocumentUpsertRequest,
  ExecutedHeirDto,
  ExecutedNaturalPersonDto,
  ExecutedPublicEntityDto,
  ExecutionApplicantDto,
  GeneralEntitySide,
  GuarantorDto,
  HeirDto,
  RealEstateDto,
} from '../types';

const FILE_YEARS = ['2026', '2027', '2028', '2029', '2030'];
const CURRENCIES = ['ليرة سورية', 'دولار أمريكي', 'يورو'];
const ADDRESS_TYPES = ['موطن مختار', 'عنوان', 'يمثله'];
const HEIR_ADDRESS_TYPES = ['عنوان', 'وكيل'];
const SHARE_TYPES = ['تمام العقار', 'حصة سهمية'];
const MAX_GUARANTORS = 4;
const MAX_ESTATES = 20;
const REPRESENTATION_TYPES = ['أصالة', 'إضافة لتركة'] as const;
const EXECUTED_STATUS_OPTIONS = [
  { value: '', label: 'متداول' },
  { value: 'منفذ', label: 'منفذ' },
  { value: 'مشطوب', label: 'مشطوب' },
] as const;

function emptyGuarantor(): GuarantorDto {
  return { guarantorNumber: 1, name: '', father: '', family: '', mother: '', birth: '', register: '', nationalId: '', address: '', addressType: 'موطن مختار', heirs: [] };
}

function emptyHeir(): HeirDto {
  return { name: '', addressType: 'عنوان', address: '' };
}

function addressLabelOf(addressType: string | undefined): string {
  return addressType === 'يمثله' ? 'الوكيل' : 'العنوان';
}

function emptyEstate(): RealEstateDto {
  return { owners: [], property: '', propertyNumber: '', propertyDistrict: '', landRegistry: '', shareType: 'تمام العقار' };
}

function emptyExecutedHeir(): ExecutedHeirDto {
  return { heirName: '', heirFather: '', heirFamily: '', addressType: 'عنوان', heirAddress: '' };
}

function emptyExecutionApplicant(): ExecutionApplicantDto {
  return {
    name: '',
    father: '',
    family: '',
    legalRepresentative: '',
    representationType: 'أصالة',
    deceasedName: '',
    deceasedFather: '',
    deceasedFamily: '',
    heirs: [],
  };
}

function emptyExecutedPublicEntity(): ExecutedPublicEntityDto {
  return { entityName: '', entityBranch: '' };
}

function emptyExecutedNaturalPerson(): ExecutedNaturalPersonDto {
  return {
    name: '',
    father: '',
    family: '',
    addressType: 'عنوان',
    addressOrRepresentative: '',
    representationType: 'أصالة',
    deceasedName: '',
    deceasedFather: '',
    deceasedFamily: '',
    heirs: [],
  };
}

type StatusFormKey =
  | 'baraetNumber'
  | 'baraetDate'
  | 'baraetRegNumber'
  | 'baraetRegDate'
  | 'tarithNumber'
  | 'tarithDate'
  | 'tarithRegNumber'
  | 'tarithRegDate';

type StatusField = { key: StatusFormKey; label: string };

const STATUS_CHOICES = ['منفذ جبريا', 'منفذ بالتسوية', 'تريث'] as const;
const EXEC_SUB_CHOICES = ['منفذ جزئيا', 'منفذ كاملا'] as const;
type ExecStatusChoice = (typeof STATUS_CHOICES)[number];
type ExecSubChoice = (typeof EXEC_SUB_CHOICES)[number];

type StatusFormState = {
  status: ExecStatusChoice;
  execSubStatus: ExecSubChoice;
  collectedAmount: string;
  baraetNumber: string;
  baraetDate: string;
  baraetRegNumber: string;
  baraetRegDate: string;
  tarithNumber: string;
  tarithDate: string;
  tarithRegNumber: string;
  tarithRegDate: string;
};

const BARAET_FIELDS: StatusField[] = [
  { key: 'baraetNumber', label: 'رقم كتاب براءة الذمة' },
  { key: 'baraetDate', label: 'تاريخ كتاب براءة الذمة' },
  { key: 'baraetRegNumber', label: 'رقم ورود كتاب براءة الذمة' },
  { key: 'baraetRegDate', label: 'تاريخ ورود كتاب براءة الذمة' },
];

const TARITH_FIELDS: StatusField[] = [
  { key: 'tarithNumber', label: 'رقم كتاب التريث' },
  { key: 'tarithDate', label: 'تاريخ كتاب التريث' },
  { key: 'tarithRegNumber', label: 'رقم ورود كتاب التريث' },
  { key: 'tarithRegDate', label: 'تاريخ ورود كتاب التريث' },
];

function FieldInput({
  id,
  label,
  value,
  onChange,
  type = 'text',
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
}) {
  return (
    <div>
      <label htmlFor={id} className="block text-xs font-bold text-gray-600 mb-1">
        {label}
      </label>
      <input
        id={id}
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
      />
    </div>
  );
}

function SelectInput({
  id,
  label,
  value,
  onChange,
  options,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: readonly string[];
}) {
  return (
    <div>
      <label htmlFor={id} className="block text-xs font-bold text-gray-600 mb-1">
        {label}
      </label>
      <select
        id={id}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none"
      >
        {options.map((o) => (
          <option key={o} value={o}>
            {o}
          </option>
        ))}
      </select>
    </div>
  );
}

function HeirsEditor({
  heirs,
  onSet,
  onAdd,
  onRemove,
  idPrefix,
}: {
  heirs: HeirDto[];
  onSet: (i: number, key: keyof HeirDto, value: string) => void;
  onAdd: () => void;
  onRemove: (i: number) => void;
  idPrefix: string;
}) {
  return (
    <div className="mt-4 rounded-lg bg-gray-50 border border-gray-200 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2 mb-3">
        <span className="text-sm font-medium text-gray-700">ورثة المتوفى</span>
        <button
          type="button"
          onClick={onAdd}
          className="bg-gray-500 hover:bg-gray-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
        >
          ＋ إضافة وريث
        </button>
      </div>
      {heirs.length === 0 && (
        <p className="text-xs text-gray-400">أضف ورثة لمنفذ عليه متوفى ليحلوا محله في المستندات</p>
      )}
      {heirs.map((h, i) => (
        <div key={i} className="grid grid-cols-1 md:grid-cols-5 gap-3 mb-3 last:mb-0">
          <div className="md:col-span-2">
            <label
              htmlFor={`${idPrefix}-heir-name-${i}`}
              className="block text-xs font-bold text-gray-600 mb-1"
            >
              الاسم الثلاثي للوريث
            </label>
            <input
              id={`${idPrefix}-heir-name-${i}`}
              value={h.name ?? ''}
              onChange={(e) => onSet(i, 'name', e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label
              htmlFor={`${idPrefix}-heir-type-${i}`}
              className="block text-xs font-bold text-gray-600 mb-1"
            >
              نوع العنوان
            </label>
            <select
              id={`${idPrefix}-heir-type-${i}`}
              value={h.addressType ?? 'عنوان'}
              onChange={(e) => onSet(i, 'addressType', e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {HEIR_ADDRESS_TYPES.map((o) => (
                <option key={o} value={o}>
                  {o}
                </option>
              ))}
            </select>
          </div>
          <div className="md:col-span-1">
            <label
              htmlFor={`${idPrefix}-heir-address-${i}`}
              className="block text-xs font-bold text-gray-600 mb-1"
            >
              {h.addressType === 'وكيل' ? 'الوكيل' : 'العنوان'}
            </label>
            <input
              id={`${idPrefix}-heir-address-${i}`}
              value={h.address ?? ''}
              onChange={(e) => onSet(i, 'address', e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div className="flex items-end">
            <button
              type="button"
              onClick={() => onRemove(i)}
              className="text-red-500 text-xs hover:underline min-h-11"
            >
              ✖ حذف
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}

/** محرر ورثة المورث المتوفى في وضع «منفذ عليه» (طالب تنفيذ أو منفذ عليه طبيعي متوفى). */
function ExecutedHeirsEditor({
  heirs,
  onSet,
  onAdd,
  onRemove,
  idPrefix,
  allowAdd = true,
}: {
  heirs: ExecutedHeirDto[];
  onSet: (i: number, key: keyof ExecutedHeirDto, value: string) => void;
  onAdd: () => void;
  onRemove: (i: number) => void;
  idPrefix: string;
  allowAdd?: boolean;
}) {
  if (heirs.length === 0 && !allowAdd) return null;

  return (
    <div className="mt-4 rounded-lg bg-gray-50 border border-gray-200 p-4">
      {allowAdd && (
        <div className="flex flex-wrap items-center justify-end gap-2 mb-3">
          <button
            type="button"
            onClick={onAdd}
            className="bg-gray-500 hover:bg-gray-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
          >
            ＋ إضافة وريث
          </button>
        </div>
      )}
      {heirs.map((h, i) => (
        <div key={i} className="grid grid-cols-1 md:grid-cols-7 gap-3 mb-3 last:mb-0">
          <div className="md:col-span-2">
            <label
              htmlFor={`${idPrefix}-heir-name-${i}`}
              className="block text-xs font-bold text-gray-600 mb-1"
            >
              الاسم
            </label>
            <input
              id={`${idPrefix}-heir-name-${i}`}
              value={h.heirName ?? ''}
              onChange={(e) => onSet(i, 'heirName', e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div className="md:col-span-1">
            <label
              htmlFor={`${idPrefix}-heir-father-${i}`}
              className="block text-xs font-bold text-gray-600 mb-1"
            >
              اسم الأب
            </label>
            <input
              id={`${idPrefix}-heir-father-${i}`}
              value={h.heirFather ?? ''}
              onChange={(e) => onSet(i, 'heirFather', e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div className="md:col-span-1">
            <label
              htmlFor={`${idPrefix}-heir-family-${i}`}
              className="block text-xs font-bold text-gray-600 mb-1"
            >
              النسبة
            </label>
            <input
              id={`${idPrefix}-heir-family-${i}`}
              value={h.heirFamily ?? ''}
              onChange={(e) => onSet(i, 'heirFamily', e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label
              htmlFor={`${idPrefix}-heir-type-${i}`}
              className="block text-xs font-bold text-gray-600 mb-1"
            >
              نوع العنوان
            </label>
            <select
              id={`${idPrefix}-heir-type-${i}`}
              value={h.addressType ?? 'عنوان'}
              onChange={(e) => onSet(i, 'addressType', e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {HEIR_ADDRESS_TYPES.map((o) => (
                <option key={o} value={o}>
                  {o}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label
              htmlFor={`${idPrefix}-heir-address-${i}`}
              className="block text-xs font-bold text-gray-600 mb-1"
            >
              {h.addressType === 'وكيل' ? 'الوكيل' : 'العنوان'}
            </label>
            <input
              id={`${idPrefix}-heir-address-${i}`}
              value={h.heirAddress ?? ''}
              onChange={(e) => onSet(i, 'heirAddress', e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div className="flex items-end">
            <button
              type="button"
              onClick={() => onRemove(i)}
              className="text-red-500 text-xs hover:underline min-h-11"
            >
              ✖ حذف
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}

function toUpsert(d: DocumentResponse): DocumentUpsertRequest {
  return {
    documentType: d.documentType ?? '',
    borrowerName: d.borrowerName ?? '',
    borrowerFather: d.borrowerFather ?? '',
    borrowerFamily: d.borrowerFamily ?? '',
    borrowerMother: d.borrowerMother ?? '',
    borrowerBirth: d.borrowerBirth ?? '',
    borrowerRegister: d.borrowerRegister ?? '',
    borrowerNationalId: d.borrowerNationalId ?? '',
    borrowerAddress: d.borrowerAddress ?? '',
    borrowerAddressType: d.borrowerAddressType ?? 'موطن مختار',
    contractType: d.contractType ?? '',
    contractTypeSelector: d.contractTypeSelector ?? 'مصرفي',
    contractNumber: d.contractNumber ?? '',
    contractDate: d.contractDate ?? '',
    inclusionText: d.inclusionText ?? '',
    amountNumeric: d.amountNumeric,
    amountWords: d.amountWords ?? '',
    currency: d.currency ?? 'ليرة سورية',
    amount2Numeric: d.amount2Numeric,
    amount2Words: d.amount2Words ?? '',
    currency2: d.currency2 ?? 'دولار أمريكي',
    inclusionAmountNumeric: d.inclusionAmountNumeric,
    inclusionAmountWords: d.inclusionAmountWords ?? '',
    inclusionCurrency: d.inclusionCurrency ?? 'ليرة سورية',
    court: d.court ?? '',
    applicant: d.applicant ?? '',
    fileNumber: d.fileNumber ?? '',
    fileType: d.fileType ?? '',
    fileYear: d.fileYear ?? '',
    fileIncoming: d.fileIncoming ?? '',
    fileIncomingDate: d.fileIncomingDate ?? '',
    underFilingNumber: d.underFilingNumber ?? '',
    fileRegistrationDate: d.fileRegistrationDate ?? '',
    branchName: d.branchName ?? '',
    seizureDate: d.seizureDate ?? '',
    immediateActions: d.immediateActions ?? '',
    notes: d.notes ?? '',
    guarantors: [],
    realEstates: [],
    generalEntitySide: d.generalEntitySide ?? 'applicant',
    executedStatus: d.executedStatus ?? '',
    struckOffDate: d.struckOffDate?.slice(0, 10) ?? '',
    executedDescription: d.executedDescription ?? '',
    fileReceiptDate: d.fileReceiptDate?.slice(0, 10) ?? '',
    executedRequiredAmount: d.executedRequiredAmount,
    executedPaidAmount: d.executedPaidAmount,
    executionApplicants: [],
    executedPublicEntities: [],
    executedNaturalPersons: [],
  };
}

export default function DocumentForm() {
  const { id } = useParams();
  const isEdit = Boolean(id);
  const navigate = useNavigate();
  const { user } = useAuth();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [guarantors, setGuarantors] = useState<GuarantorDto[]>([emptyGuarantor()]);
  const [borrowerHeirs, setBorrowerHeirs] = useState<HeirDto[]>([]);
  const [estates, setEstates] = useState<RealEstateDto[]>([]);
  const [executionApplicants, setExecutionApplicants] = useState<ExecutionApplicantDto[]>([emptyExecutionApplicant()]);
  const [executedPublicEntities, setExecutedPublicEntities] = useState<ExecutedPublicEntityDto[]>([emptyExecutedPublicEntity()]);
  const [executedNaturalPersons, setExecutedNaturalPersons] = useState<ExecutedNaturalPersonDto[]>([]);
  const [showInclusionAmount, setShowInclusionAmount] = useState(false);
  const [showAmount2, setShowAmount2] = useState(false);
  const [showPaidAmount, setShowPaidAmount] = useState(false);
  const [form, setForm] = useState<DocumentUpsertRequest>({
    guarantors: [],
    realEstates: [],
    currency: 'ليرة سورية',
    currency2: 'دولار أمريكي',
    inclusionCurrency: 'ليرة سورية',
    contractTypeSelector: 'مصرفي',
    borrowerAddressType: 'موطن مختار',
    generalEntitySide: 'applicant',
    executedStatus: '',
    executionApplicants: [],
    executedPublicEntities: [],
    executedNaturalPersons: [],
  });
  const [statusError, setStatusError] = useState('');
  const [statusBusy, setStatusBusy] = useState(false);
  const [hasStatus, setHasStatus] = useState(false);
  const [statusForm, setStatusForm] = useState<StatusFormState>({
    status: 'منفذ جبريا',
    execSubStatus: 'منفذ كاملا',
    collectedAmount: '',
    baraetNumber: '',
    baraetDate: '',
    baraetRegNumber: '',
    baraetRegDate: '',
    tarithNumber: '',
    tarithDate: '',
    tarithRegNumber: '',
    tarithRegDate: '',
  });

  useEffect(() => {
    if (!isEdit) return;
    api
      .get<DocumentResponse>(`/documents/${id}`)
      .then((r) => {
        const d = r.data;
        setForm(toUpsert(d));
        setGuarantors(d.guarantors.length ? d.guarantors : [emptyGuarantor()]);
        setBorrowerHeirs(d.borrowerHeirs ?? []);
        // تصحيح أي بيانات قديمة متناقضة عند التحميل: تمام العقار لا يكون إلا لمالك واحد.
        setEstates(
          (d.realEstates ?? []).map((r) => ({
            ...r,
            shareType: (r.owners ?? []).length > 1 ? 'حصة سهمية' : r.shareType,
          })),
        );
        setShowInclusionAmount(
          Boolean(d.inclusionAmountNumeric || d.inclusionAmountWords),
        );
        setShowAmount2(Boolean(d.amount2Numeric || d.amount2Words));
        setShowPaidAmount(Boolean(d.executedPaidAmount));
        // طالب تنفيذ واحد وجهة عامة واحدة يظهران دائمًا (وإن لم يكن للملف أي منهما محفوظًا).
        setExecutionApplicants(d.executionApplicants?.length ? d.executionApplicants : [emptyExecutionApplicant()]);
        setExecutedPublicEntities(d.executedPublicEntities?.length ? d.executedPublicEntities : [emptyExecutedPublicEntity()]);
        setExecutedNaturalPersons(d.executedNaturalPersons ?? []);
        setHasStatus(Boolean(d.execStatus));
        setStatusForm({
          status: (d.execStatus || 'منفذ جبريا') as ExecStatusChoice,
          execSubStatus: (d.execSubStatus || 'منفذ كاملا') as ExecSubChoice,
          collectedAmount: d.collectedAmount != null ? String(d.collectedAmount) : '',
          baraetNumber: d.baraetNumber || '',
          baraetDate: d.baraetDate || '',
          baraetRegNumber: d.baraetRegNumber || '',
          baraetRegDate: d.baraetRegDate || '',
          tarithNumber: d.tarithNumber || '',
          tarithDate: d.tarithDate || '',
          tarithRegNumber: d.tarithRegNumber || '',
          tarithRegDate: d.tarithRegDate || '',
        });
      })
      .catch((err) => setError(getApiErrorMessage(err)));
  }, [id, isEdit]);

  const set = (key: keyof DocumentUpsertRequest, value: unknown) =>
    setForm((f) => ({ ...f, [key]: value }));

  // صفة الملف تُثبَّت عند الإنشاء ولا تُغيَّر عند التعديل. عند اختيار «الجهة العامة منفذ عليها»
  // يُفرض نوع السند «عادي» فورًا (المصرفي محظور في هذا الوضع).
  const setSide = (side: GeneralEntitySide) =>
    setForm((f) => ({
      ...f,
      generalEntitySide: side,
      contractTypeSelector: side === 'executed' ? 'عادي' : f.contractTypeSelector ?? 'مصرفي',
    }));

  const setG = (i: number, key: keyof GuarantorDto, value: string) =>
    setGuarantors((gs) => gs.map((g, idx) => (idx === i ? { ...g, [key]: value } : g)));

  const setE = (i: number, key: keyof RealEstateDto, value: string) =>
    setEstates((es) => es.map((e, idx) => (idx === i ? { ...e, [key]: value } : e)));

  const toggleOwner = (i: number, name: string) =>
    setEstates((es) =>
      es.map((e, idx) => {
        if (idx !== i) return e;
        const current = e.owners ?? [];
        const owners = current.includes(name)
          ? current.filter((o) => o !== name)
          : [...current, name];
        return {
          ...e,
          owners,
          // تمام العقار لا يكون إلا لمالك واحد؛ عند تعدد الملاك تُفرض الحصة السهمية تلقائيًا
          // حتى لو نسي المحامي اختيارها، مع عدم الرجوع عنها عند النقص من ملاك متعددين.
          shareType: owners.length > 1 ? 'حصة سهمية' : e.shareType,
        };
      }),
    );

  const setBorrowerHeir = (i: number, key: keyof HeirDto, value: string) =>
    setBorrowerHeirs((hs) => hs.map((h, idx) => (idx === i ? { ...h, [key]: value } : h)));

  const addBorrowerHeir = () => setBorrowerHeirs((hs) => [...hs, emptyHeir()]);

  const removeBorrowerHeir = (i: number) =>
    setBorrowerHeirs((hs) => hs.filter((_, idx) => idx !== i));

  const setApplicant = (i: number, key: keyof ExecutionApplicantDto, value: string) =>
    setExecutionApplicants((xs) => xs.map((x, idx) => (idx === i ? { ...x, [key]: value } : x)));

  const addApplicant = () => setExecutionApplicants((xs) => [...xs, emptyExecutionApplicant()]);

  const removeApplicant = (i: number) =>
    setExecutionApplicants((xs) => xs.filter((_, idx) => idx !== i));

  const setApplicantHeir = (i: number, hi: number, key: keyof ExecutedHeirDto, value: string) =>
    setExecutionApplicants((xs) =>
      xs.map((x, idx) =>
        idx === i
          ? {
              ...x,
              heirs: (x.heirs ?? []).map((h, hIdx) =>
                hIdx === hi ? { ...h, [key]: value } : h,
              ),
            }
          : x,
      ),
    );

  const addApplicantHeir = (i: number) =>
    setExecutionApplicants((xs) =>
      xs.map((x, idx) =>
        idx === i ? { ...x, heirs: [...(x.heirs ?? []), emptyExecutedHeir()] } : x,
      ),
    );

  const removeApplicantHeir = (i: number, hi: number) =>
    setExecutionApplicants((xs) =>
      xs.map((x, idx) =>
        idx === i ? { ...x, heirs: (x.heirs ?? []).filter((_, hIdx) => hIdx !== hi) } : x,
      ),
    );

  const setExecutedEntity = (i: number, key: keyof ExecutedPublicEntityDto, value: string) =>
    setExecutedPublicEntities((xs) => xs.map((x, idx) => (idx === i ? { ...x, [key]: value } : x)));

  const addExecutedEntity = () => setExecutedPublicEntities((xs) => [...xs, emptyExecutedPublicEntity()]);

  const removeExecutedEntity = (i: number) =>
    setExecutedPublicEntities((xs) => xs.filter((_, idx) => idx !== i));

  const setExecutedPerson = (i: number, key: keyof ExecutedNaturalPersonDto, value: string) =>
    setExecutedNaturalPersons((xs) => xs.map((x, idx) => (idx === i ? { ...x, [key]: value } : x)));

  const addExecutedPerson = () => setExecutedNaturalPersons((xs) => [...xs, emptyExecutedNaturalPerson()]);

  const removeExecutedPerson = (i: number) =>
    setExecutedNaturalPersons((xs) => xs.filter((_, idx) => idx !== i));

  const setExecutedPersonHeir = (i: number, hi: number, key: keyof ExecutedHeirDto, value: string) =>
    setExecutedNaturalPersons((xs) =>
      xs.map((x, idx) =>
        idx === i
          ? {
              ...x,
              heirs: (x.heirs ?? []).map((h, hIdx) =>
                hIdx === hi ? { ...h, [key]: value } : h,
              ),
            }
          : x,
      ),
    );

  const addExecutedPersonHeir = (i: number) =>
    setExecutedNaturalPersons((xs) =>
      xs.map((x, idx) =>
        idx === i ? { ...x, heirs: [...(x.heirs ?? []), emptyExecutedHeir()] } : x,
      ),
    );

  const removeExecutedPersonHeir = (i: number, hi: number) =>
    setExecutedNaturalPersons((xs) =>
      xs.map((x, idx) =>
        idx === i ? { ...x, heirs: (x.heirs ?? []).filter((_, hIdx) => hIdx !== hi) } : x,
      ),
    );

  const setGHeir = (gi: number, hi: number, key: keyof HeirDto, value: string) =>
    setGuarantors((gs) =>
      gs.map((g, idx) =>
        idx === gi
          ? {
              ...g,
              heirs: (g.heirs ?? []).map((h, hIdx) =>
                hIdx === hi ? { ...h, [key]: value } : h,
              ),
            }
          : g,
      ),
    );

  const addGHeir = (gi: number) =>
    setGuarantors((gs) =>
      gs.map((g, idx) =>
        idx === gi ? { ...g, heirs: [...(g.heirs ?? []), emptyHeir()] } : g,
      ),
    );

  const removeGHeir = (gi: number, hi: number) =>
    setGuarantors((gs) =>
      gs.map((g, idx) =>
        idx === gi ? { ...g, heirs: (g.heirs ?? []).filter((_, hIdx) => hIdx !== hi) } : g,
      ),
    );

  const addGuarantor = () => {
    if (guarantors.length >= MAX_GUARANTORS) return;
    setGuarantors((gs) => [...gs, emptyGuarantor()]);
  };

  const removeGuarantor = (i: number) => {
    if (guarantors.length <= 1) return;
    setGuarantors((gs) => gs.filter((_, idx) => idx !== i));
  };

  const addEstate = () => {
    if (estates.length >= MAX_ESTATES) return;
    setEstates((es) => [...es, emptyEstate()]);
  };

  const resetForm = () => {
    setForm({
      guarantors: [],
      realEstates: [],
      currency: 'ليرة سورية',
      currency2: 'دولار أمريكي',
      inclusionCurrency: 'ليرة سورية',
      contractTypeSelector: 'مصرفي',
      borrowerAddressType: 'موطن مختار',
      generalEntitySide: 'applicant',
      executedStatus: '',
      executionApplicants: [],
      executedPublicEntities: [],
      executedNaturalPersons: [],
    });
    setGuarantors([emptyGuarantor()]);
    setBorrowerHeirs([]);
    setEstates([]);
    setExecutionApplicants([emptyExecutionApplicant()]);
    setExecutedPublicEntities([emptyExecutedPublicEntity()]);
    setExecutedNaturalPersons([]);
    setShowInclusionAmount(false);
    setShowAmount2(false);
    setShowPaidAmount(false);
    setError('');
  };

  // الحذف متاح في وضع التعديل للمحامي فقط (نفس قيد صفحة العرض)، ويُعزل في نهاية الصفحة
  // بعد «تغيير الحالة» كمنطقة خطر، مع تأكيد صريح ومنع الإرسال المزدوج.
  const canDelete = isEdit && user?.role === 'lawyer';

  const deleteDoc = async () => {
    if (!isEdit || !id) return;
    if (!window.confirm('هل أنت متأكد من حذف هذا المستند؟')) return;
    setDeleteBusy(true);
    setError('');
    try {
      await api.delete(`/documents/${id}`);
      navigate('/documents');
    } catch {
      setError('فشل الحذف');
      setDeleteBusy(false);
    }
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');

    const isExecutedSubmit = form.generalEntitySide === 'executed';

    if (
      isExecutedSubmit &&
      (!(form.fileNumber ?? '').trim() || !(form.fileYear ?? '').trim())
    ) {
      setError('ملف «الجهة العامة منفذ عليها» يجب أن يكون مقيدًا برقم وسنة الملف');
      return;
    }

    if (
      !isExecutedSubmit &&
      (form.fileNumber ?? '').trim() &&
      (form.fileYear ?? '').trim() &&
      !(form.fileRegistrationDate ?? '').trim()
    ) {
      setError('تاريخ قيد الملف مطلوب عند إدخال رقم الملف وسنة الملف');
      return;
    }

    setBusy(true);
    try {
      const initialActions = [
        ...((form.immediateActions ?? '').trim()
          ? [
              {
                type: 'action' as const,
                text: (form.immediateActions ?? '').trim(),
                actionDate: new Date().toISOString().slice(0, 10),
              },
            ]
          : []),
        ...(!isEdit && (form.notes ?? '').trim()
          ? [{ type: 'note' as const, text: (form.notes ?? '').trim() }]
          : []),
      ];
      const payload: DocumentUpsertRequest = {
        ...form,
        // تاريخان من نوع DateTime? في الخلفية: السلسلة الفارغة تُعطّل فك JSON،
        // فلا تُرسل إلا القيمة غير الفارغة (وإلا يُحذف المفتاح فيؤول null على الخادم).
        ...((form.fileReceiptDate ?? '').trim() ? { fileReceiptDate: (form.fileReceiptDate ?? '').trim() } : {}),
        ...((form.struckOffDate ?? '').trim() ? { struckOffDate: (form.struckOffDate ?? '').trim() } : {}),
        // وضع «منفذ عليه»: عادي فقط، بلا مقترض/كفلاء/عقارات (مطابق لتحقق الخلفية).
        contractTypeSelector: isExecutedSubmit ? 'عادي' : (form.contractTypeSelector ?? 'مصرفي'),
        guarantors: isExecutedSubmit ? [] : guarantors
          .filter((g) => g.name?.trim())
          .map((g, i) => ({
            ...g,
            guarantorNumber: i + 1,
            heirs: (g.heirs ?? []).filter((h) => (h.name ?? '').trim()),
          })),
        borrowerHeirs: isExecutedSubmit ? [] : borrowerHeirs.filter((h) => (h.name ?? '').trim()),
        realEstates: isExecutedSubmit ? [] : estates
          .filter((r) => r.propertyNumber?.trim() || (r.owners ?? []).some((o) => (o ?? '').trim()))
          .map((r) => ({
            ...r,
            property: `${r.propertyNumber ?? ''} ${r.propertyDistrict ?? ''}`.trim(),
          })),
        executionApplicants: isExecutedSubmit
          ? executionApplicants
              .filter((a) => (a.name ?? '').trim())
              .map((a) => ({
                ...a,
                heirs: (a.heirs ?? []).filter((h) => (h.heirName ?? '').trim()),
              }))
          : [],
        executedPublicEntities: isExecutedSubmit
          ? executedPublicEntities.filter((e) => (e.entityName ?? '').trim())
          : [],
        executedNaturalPersons: isExecutedSubmit
          ? executedNaturalPersons
              .filter((p) => (p.name ?? '').trim())
              .map((p) => ({
                ...p,
                heirs: (p.heirs ?? []).filter((h) => (h.heirName ?? '').trim()),
              }))
          : [],
        ...(initialActions.length > 0 ? { initialActions } : {}),
      };
      if (isEdit) await api.put(`/documents/${id}`, payload);
      else await api.post('/documents', payload);
      navigate('/documents');
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } }).response?.data?.message;
      setError(msg || 'حدث خطأ في حفظ المستند');
    } finally {
      setBusy(false);
    }
  };

  const saveStatus = async () => {
    setStatusError('');
    const fields: Record<string, string> = {};
    if (statusForm.status === 'منفذ جبريا') {
      fields.execSubStatus = statusForm.execSubStatus;
      if (statusForm.collectedAmount) fields.collectedAmount = statusForm.collectedAmount;
    } else if (statusForm.status === 'منفذ بالتسوية') {
      if (!statusForm.baraetNumber.trim() || !statusForm.baraetDate.trim()) {
        setStatusError('يجب إدخال رقم وتاريخ كتاب براءة الذمة على الأقل');
        return;
      }
      for (const key of ['baraetNumber', 'baraetDate', 'baraetRegNumber', 'baraetRegDate'] as const) {
        if (statusForm[key]) fields[key] = statusForm[key];
      }
      if (statusForm.collectedAmount) fields.collectedAmount = statusForm.collectedAmount;
    } else {
      if (!statusForm.tarithNumber.trim() || !statusForm.tarithDate.trim()) {
        setStatusError('يجب إدخال رقم وتاريخ كتاب التريث على الأقل');
        return;
      }
      for (const key of ['tarithNumber', 'tarithDate', 'tarithRegNumber', 'tarithRegDate'] as const) {
        if (statusForm[key]) fields[key] = statusForm[key];
      }
    }
    setStatusBusy(true);
    try {
      await api.post(`/documents/${id}/status`, { status: statusForm.status, fields });
      setStatusError('');
      setHasStatus(true);
    } catch {
      setStatusError('فشل تحديث الحالة');
    } finally {
      setStatusBusy(false);
    }
  };

  const cancelStatus = async () => {
    setStatusBusy(true);
    try {
      await api.post(`/documents/${id}/cancel-status`);
      setStatusError('');
      setHasStatus(false);
    } catch {
      setStatusError('فشل إلغاء الحالة');
    } finally {
      setStatusBusy(false);
    }
  };

  const section = (title: string) => (
    <h3 className="text-lg font-bold text-amber-700 bg-gray-100 rounded-lg px-4 py-2.5 mb-3 mt-8 first:mt-0">
      {title}
    </h3>
  );

  const input = (
    key: keyof DocumentUpsertRequest,
    placeholder = '',
    type = 'text',
    cls = '',
  ) => (
    <input
      id={key}
      type={type}
      value={(form[key] as string | number | undefined) ?? ''}
      onChange={(e) => set(key, type === 'number' ? Number(e.target.value) : e.target.value)}
      {...(placeholder ? { placeholder } : {})}
      className={cls || 'w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500'}
    />
  );

  const field = (label: string, key: keyof DocumentUpsertRequest, placeholder = '', type = 'text') => (
    <div>
      <label htmlFor={key} className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
      {input(key, placeholder, type)}
    </div>
  );

  const selectField = (
    label: string,
    id: string,
    options: string[],
    value: string | undefined,
    onChange: (v: string) => void,
    extraClass = '',
  ) => (
    <div className={extraClass}>
      <label htmlFor={id} className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
      <select
        id={id}
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value)}
        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
      >
        {options.map((o) => (
          <option key={o}>{o}</option>
        ))}
      </select>
    </div>
  );

  const isOrdinary = form.contractTypeSelector === 'عادي';
  const isBanking = !isOrdinary;
  const isExecuted = form.generalEntitySide === 'executed';
  const guarantorLabel = isOrdinary ? 'منفذ عليه' : 'كفيل';
  const remainingGuarantors = MAX_GUARANTORS - guarantors.length;

  const ownerOptions = () => {
    const opts: string[] = [];
    // الاسم الثلاثي الكامل للمقترض والكفيل يطابق ما يحفظه الخلفية ويعرضه العرض،
    // وبه تتطابق مطابقة الورثة (ورثة المتوفى) في توليد 005/006.
    const borrowerFull = [form.borrowerName, form.borrowerFather, form.borrowerFamily]
      .filter(Boolean)
      .join(' ');
    if (borrowerFull) opts.push(borrowerFull);
    borrowerHeirs.forEach((h) => {
      const full = (h.name ?? '').trim();
      if (full && !opts.includes(full)) opts.push(full);
    });
    guarantors.forEach((g) => {
      const full = [g.name, g.father, g.family].filter(Boolean).join(' ');
      if (full && !opts.includes(full)) opts.push(full);
      (g.heirs ?? []).forEach((h) => {
        const fullHeir = (h.name ?? '').trim();
        if (fullHeir && !opts.includes(fullHeir)) opts.push(fullHeir);
      });
    });
    return opts;
  };

  const debtorFullName = [form.borrowerName, form.borrowerFather, form.borrowerFamily]
    .filter(Boolean)
    .join(' ');

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-2xl font-bold text-gray-800">
          {isEdit
            ? `تعديل ملف «${debtorFullName || `#${id}`}»`
            : 'إدخال ملف جديد'}
        </h2>
        <Link to="/documents" className="inline-flex items-center min-h-11 text-emerald-800 text-sm hover:underline">
          ← عودة للقائمة
        </Link>
      </div>

      {error && (
        <div className="bg-red-50 text-red-700 border border-red-200 rounded-lg p-3 mb-4">{error}</div>
      )}

      <form onSubmit={submit} className="bg-white rounded-xl shadow p-6">
        {section('⚖️ صفة الجهة العامة')}
        <div className="rounded-lg bg-gray-50 border border-gray-200 p-4 mb-2">
          {isEdit ? (
            <p className="text-sm text-gray-700">
              صفة الجهة العامة:{' '}
              <span className="font-bold">{isExecuted ? 'الجهة العامة منفذ عليها' : 'الجهة العامة طالبة التنفيذ'}</span>
              <span className="block text-xs text-gray-500 mt-1">
                لا يمكن تغيير صفة الجهة العامة بعد إنشائها
              </span>
            </p>
          ) : (
            <div role="radiogroup" aria-label="صفة الجهة العامة" className="flex flex-wrap gap-x-6 gap-y-2">
              <label className="flex items-center gap-2 min-h-11 cursor-pointer text-sm text-gray-700">
                <input
                  type="radio"
                  name="generalEntitySide"
                  checked={!isExecuted}
                  onChange={() => setSide('applicant')}
                  className="h-4 w-4 text-emerald-600"
                />
                الجهة العامة طالبة التنفيذ
              </label>
              <label className="flex items-center gap-2 min-h-11 cursor-pointer text-sm text-gray-700">
                <input
                  type="radio"
                  name="generalEntitySide"
                  checked={isExecuted}
                  onChange={() => setSide('executed')}
                  className="h-4 w-4 text-emerald-600"
                />
                الجهة العامة منفذ عليها
              </label>
            </div>
          )}
        </div>

        {section('🏛️ المعلومات الأساسية')}
        <div className="grid md:grid-cols-5 gap-4 items-end">
          {field('دائرة التنفيذ', 'court')}
          {field('رقم الملف', 'fileNumber', 'رقم الملف...')}
          {selectField('سنة الملف', 'fileYear', ['', ...FILE_YEARS], form.fileYear ?? '', (v) => set('fileYear', v))}
          {field('نوع الملف', 'fileType', 'نوع الملف...')}
          {isExecuted ? (
            field('تاريخ ورود الملف', 'fileReceiptDate', 'مثال: 1/8/2026')
          ) : (
            <>
              {field('طالب التنفيذ', 'applicant')}
              {field('الفرع', 'branchName')}
              {field('رقم كتاب الجهة العامة', 'fileIncoming')}
              {field('تاريخ كتاب الجهة العامة', 'fileIncomingDate')}
              {field('رقم تحت رفع', 'underFilingNumber')}
              {field('تاريخ قيد الملف', 'fileRegistrationDate', 'مثال: 1/8/2026')}
              {field('تاريخ إلقاء حجز المنظومة', 'seizureDate', 'مثال: 1/8/2026')}
            </>
          )}
        </div>

        {isExecuted ? (
          <>
            {section('📄 بيانات السند التنفيذي')}
            <div className="grid md:grid-cols-3 gap-4 items-end">
              {field('المحكمة مصدرة القرار', 'contractType')}
              {field('رقم القرار', 'contractNumber')}
              {field('تاريخ القرار', 'contractDate')}
            </div>
            <div className="mt-4 rounded-lg bg-gray-50 border border-gray-200 p-4">
              <div>
                <label htmlFor="inclusionText" className="block text-xs font-bold text-gray-600 mb-1">
                  المتضمن
                </label>
                <AutoResizeTextarea
                  id="inclusionText"
                  value={form.inclusionText ?? ''}
                  onChange={(v) => set('inclusionText', v)}
                  placeholder="أدخل خلاصة القرار باختصار..."
                  minRows={1}
                  maxHeight={200}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <div className="mt-4 rounded-lg bg-white border border-gray-200 p-4">
                {field('المبلغ المطلوب دفعه من الجهة العامة', 'executedRequiredAmount', '', 'number')}
              </div>
            </div>

            {section('👤 طالب التنفيذ')}
            {executionApplicants.map((a, i) => (
              <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
                <div className="flex justify-between items-center mb-3">
                  <span className="font-medium text-gray-700 text-sm">طالب التنفيذ {i + 1}</span>
                  {executionApplicants.length > 1 && (
                    <button type="button" onClick={() => removeApplicant(i)} className="text-red-500 text-xs hover:underline min-h-11">
                      ✖ حذف
                    </button>
                  )}
                </div>
                <div className="grid md:grid-cols-3 gap-3">
                  {([['name', 'الاسم'], ['father', 'اسم الأب'], ['family', 'النسبة']] as const).map(([k, label]) => (
                    <div key={k}>
                      <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                      <input value={a[k] ?? ''} onChange={(e) => setApplicant(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                    </div>
                  ))}
                </div>
                <div className="grid md:grid-cols-3 gap-3 mt-3">
                  <div className="md:col-span-2">
                    <label className="block text-xs font-bold text-gray-600 mb-1">الوكيل القانوني</label>
                    <input value={a.legalRepresentative ?? ''} onChange={(e) => setApplicant(i, 'legalRepresentative', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-gray-600 mb-1">نوع التمثيل</label>
                    <select value={a.representationType ?? 'أصالة'} onChange={(e) => setApplicant(i, 'representationType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none">
                      {REPRESENTATION_TYPES.map((o) => (
                        <option key={o}>{o}</option>
                      ))}
                    </select>
                  </div>
                </div>
                {a.representationType === 'إضافة لتركة' && (
                  <div className="mt-4 rounded-lg bg-white border border-gray-200 p-4 grid md:grid-cols-3 gap-3">
                    {([['deceasedName', 'اسم المورث المتوفى'], ['deceasedFather', 'اسم أب المورث'], ['deceasedFamily', 'نسبة المورث']] as const).map(([k, label]) => (
                      <div key={k}>
                        <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                        <input value={a[k] ?? ''} onChange={(e) => setApplicant(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                      </div>
                    ))}
                    <div className="md:col-span-3">
                      <ExecutedHeirsEditor
                        idPrefix={`applicant-${i}`}
                        heirs={a.heirs ?? []}
                        onSet={(hi, k, v) => setApplicantHeir(i, hi, k, v)}
                        onAdd={() => addApplicantHeir(i)}
                        onRemove={(hi) => removeApplicantHeir(i, hi)}
                        allowAdd={isEdit}
                      />
                    </div>
                  </div>
                )}
              </div>
            ))}
            <button
              type="button"
              onClick={addApplicant}
              className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
            >
              ＋ إضافة طالب تنفيذ
            </button>

            {section('🏛️ المنفذ عليه')}
            {executedPublicEntities.map((e, i) => (
              <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
                <div className="flex justify-between items-center mb-3">
                  <span className="font-medium text-gray-700 text-sm">جهة عامة {i + 1}</span>
                  {executedPublicEntities.length > 1 && (
                    <button type="button" onClick={() => removeExecutedEntity(i)} className="text-red-500 text-xs hover:underline min-h-11">
                      ✖ حذف
                    </button>
                  )}
                </div>
                <div className="grid md:grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs font-bold text-gray-600 mb-1">اسم الجهة العامة</label>
                    <input value={e.entityName ?? ''} onChange={(ev) => setExecutedEntity(i, 'entityName', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-gray-600 mb-1">الفرع</label>
                    <input value={e.entityBranch ?? ''} onChange={(ev) => setExecutedEntity(i, 'entityBranch', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                  </div>
                </div>
              </div>
            ))}
            <div className="flex flex-wrap gap-3 items-center">
              <button
                type="button"
                onClick={addExecutedEntity}
                className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
              >
                ＋ إضافة جهة عامة
              </button>
              <button
                type="button"
                onClick={addExecutedPerson}
                className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
              >
                ＋ إضافة شخص طبيعي
              </button>
            </div>

            {executedNaturalPersons.map((p, i) => (
              <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
                <div className="flex justify-between items-center mb-3">
                  <span className="font-medium text-gray-700 text-sm">شخص طبيعي {i + 1}</span>
                  {executedNaturalPersons.length > 1 && (
                    <button type="button" onClick={() => removeExecutedPerson(i)} className="text-red-500 text-xs hover:underline min-h-11">
                      ✖ حذف
                    </button>
                  )}
                </div>
                <div className="grid md:grid-cols-3 gap-3">
                  {([['name', 'الاسم'], ['father', 'اسم الأب'], ['family', 'النسبة']] as const).map(([k, label]) => (
                    <div key={k}>
                      <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                      <input value={p[k] ?? ''} onChange={(e) => setExecutedPerson(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                    </div>
                  ))}
                </div>
                <div className="grid md:grid-cols-3 gap-3 mt-3">
                  <div>
                    <label className="block text-xs font-bold text-gray-600 mb-1">نوع العنوان</label>
                    <select value={p.addressType ?? 'عنوان'} onChange={(e) => setExecutedPerson(i, 'addressType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none">
                      {HEIR_ADDRESS_TYPES.map((o) => (
                        <option key={o}>{o}</option>
                      ))}
                    </select>
                  </div>
                  <div className="md:col-span-1">
                    <label className="block text-xs font-bold text-gray-600 mb-1">
                      {p.addressType === 'وكيل' ? 'الوكيل' : 'العنوان'}
                    </label>
                    <input value={p.addressOrRepresentative ?? ''} onChange={(e) => setExecutedPerson(i, 'addressOrRepresentative', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-gray-600 mb-1">نوع التمثيل</label>
                    <select value={p.representationType ?? 'أصالة'} onChange={(e) => setExecutedPerson(i, 'representationType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none">
                      {REPRESENTATION_TYPES.map((o) => (
                        <option key={o}>{o}</option>
                      ))}
                    </select>
                  </div>
                </div>
                {p.representationType === 'إضافة لتركة' && (
                  <div className="mt-4 rounded-lg bg-white border border-gray-200 p-4 grid md:grid-cols-3 gap-3">
                    {([['deceasedName', 'اسم المورث المتوفى'], ['deceasedFather', 'اسم أب المورث'], ['deceasedFamily', 'نسبة المورث']] as const).map(([k, label]) => (
                      <div key={k}>
                        <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                        <input value={p[k] ?? ''} onChange={(e) => setExecutedPerson(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                      </div>
                    ))}
                    <div className="md:col-span-3">
                      <ExecutedHeirsEditor
                        idPrefix={`executed-person-${i}`}
                        heirs={p.heirs ?? []}
                        onSet={(hi, k, v) => setExecutedPersonHeir(i, hi, k, v)}
                        onAdd={() => addExecutedPersonHeir(i)}
                        onRemove={(hi) => removeExecutedPersonHeir(i, hi)}
                        allowAdd={isEdit}
                      />
                    </div>
                  </div>
                )}
              </div>
            ))}

            {section('📋 حالة الملف')}
            <div className="rounded-lg bg-gray-50 border border-gray-200 p-4">
              <div className="grid md:grid-cols-3 gap-4 items-end">
                <div>
                  <label htmlFor="executedStatus" className="block text-xs font-bold text-gray-600 mb-1">
                    الحالة
                  </label>
                  <select
                    id="executedStatus"
                    value={form.executedStatus ?? ''}
                    onChange={(e) => set('executedStatus', e.target.value)}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  >
                    {EXECUTED_STATUS_OPTIONS.map((o) => (
                      <option key={o.value} value={o.value}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              {form.executedStatus === 'منفذ' && (
                <div className="mt-4 rounded-lg bg-white border border-gray-200 p-4">
                  <label htmlFor="executedDescription" className="block text-xs font-bold text-gray-600 mb-1">
                    كيفية تنفيذ الملف
                  </label>
                  <AutoResizeTextarea
                    id="executedDescription"
                    value={form.executedDescription ?? ''}
                    onChange={(v) => set('executedDescription', v)}
                    placeholder="كيف تم تنفيذ الملف..."
                    minRows={2}
                    maxHeight={200}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  />
                  <div className="mt-4">
                    <button
                      type="button"
                      onClick={() => setShowPaidAmount((v) => !v)}
                      className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 shrink-0 min-h-11"
                    >
                      {showPaidAmount ? '− إخفاء المبلغ' : '➕ إضافة مبلغ'}
                    </button>
                  </div>
                  {showPaidAmount && (
                    <div className="mt-4">
                      {field('المبلغ الذي دفعته الجهة العامة', 'executedPaidAmount', '', 'number')}
                    </div>
                  )}
                </div>
              )}
              {form.executedStatus === 'مشطوب' && (
                <div className="mt-4 grid md:grid-cols-3 gap-4 items-end">
                  {field('تاريخ الشطب', 'struckOffDate', 'مثال: 1/8/2026')}
                </div>
              )}
            </div>
          </>
        ) : (
          <>
            {section('📄 بيانات السند التنفيذي')}
        <div className="grid md:grid-cols-4 gap-4">
          {selectField('نوع السند', 'contractTypeSelector', ['مصرفي', 'عادي'], form.contractTypeSelector ?? 'مصرفي', (v) => set('contractTypeSelector', v))}
          {field(isOrdinary ? 'المحكمة مصدرة القرار' : 'نوع العقد', 'contractType')}
          {field(isOrdinary ? 'رقم القرار' : 'رقم العقد', 'contractNumber')}
          {field(isOrdinary ? 'تاريخ القرار' : 'تاريخ العقد', 'contractDate')}
        </div>

        {isOrdinary && (
          <div className="mt-4 rounded-lg bg-gray-50 border border-gray-200 p-4">
            <div className="flex gap-3 items-end">
              <div className="flex-1">
                <label htmlFor="inclusionText" className="block text-xs font-bold text-gray-600 mb-1">
                  المتضمن
                </label>
                <AutoResizeTextarea
                  id="inclusionText"
                  value={form.inclusionText ?? ''}
                  onChange={(v) => set('inclusionText', v)}
                  placeholder="أدخل خلاصة القرار باختصار..."
                  minRows={1}
                  maxHeight={200}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <button
                type="button"
                onClick={() => setShowInclusionAmount((v) => !v)}
                className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 shrink-0 min-h-11"
              >
                {showInclusionAmount ? '− إخفاء المبلغ' : '➕ إضافة مبلغ'}
              </button>
            </div>
            {showInclusionAmount && (
              <div className="mt-4 rounded-lg bg-white border border-gray-200 p-4 grid md:grid-cols-2 gap-4">
                {field('المبلغ', 'inclusionAmountNumeric', '', 'number')}
                {selectField('العملة', 'inclusionCurrency', CURRENCIES, form.inclusionCurrency ?? 'ليرة سورية', (v) => set('inclusionCurrency', v))}
              </div>
            )}
          </div>
        )}

        {isBanking && (
          <div className="mt-4 rounded-lg bg-gray-50 border border-gray-200 p-4">
            <div className="flex gap-3 items-end">
              {field('المبلغ المطالب به', 'amountNumeric', '', 'number')}
              {selectField('العملة', 'currency', CURRENCIES, form.currency ?? 'ليرة سورية', (v) => set('currency', v))}
              <button
                type="button"
                onClick={() => setShowAmount2((v) => !v)}
                className="bg-blue-700 hover:bg-blue-600 text-white text-xs font-bold rounded-md px-3 py-2 shrink-0 min-h-11"
              >
                {showAmount2 ? '− إخفاء المبلغ الثاني' : '➕ مبلغ ثانٍ'}
              </button>
            </div>
            {showAmount2 && (
              <div className="mt-4 rounded-lg bg-white border border-gray-200 p-4 grid md:grid-cols-2 gap-4">
                {field('المبلغ الثاني', 'amount2Numeric', 'المبلغ الثاني...', 'number')}
                {selectField('العملة', 'currency2', CURRENCIES, form.currency2 ?? 'دولار أمريكي', (v) => set('currency2', v))}
              </div>
            )}
          </div>
        )}

        {section(isOrdinary ? '👤 بيانات المنفذ عليه' : '👤 بيانات المقترض')}
        <div className="grid md:grid-cols-5 gap-4">
          {field('الاسم', 'borrowerName')}
          {field('اسم الأب', 'borrowerFather')}
          {field('النسبة', 'borrowerFamily')}
          {field('اسم الأم', 'borrowerMother')}
          {field('مكان وتاريخ الولادة', 'borrowerBirth')}
          {field('مكان ورقم القيد', 'borrowerRegister')}
          {field('الرقم الوطني', 'borrowerNationalId')}
          {selectField('نوع العنوان', 'borrowerAddressType', ADDRESS_TYPES, form.borrowerAddressType ?? 'موطن مختار', (v) => set('borrowerAddressType', v))}
          {field(addressLabelOf(form.borrowerAddressType), 'borrowerAddress')}
        </div>
        <HeirsEditor
          idPrefix="borrower"
          heirs={borrowerHeirs}
          onSet={setBorrowerHeir}
          onAdd={addBorrowerHeir}
          onRemove={removeBorrowerHeir}
        />

        {section(isOrdinary ? '👥 المنفذ عليهم الآخرون' : '👥 الكفلاء')}
        {guarantors.map((g, i) => (
          <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
            <div className="flex justify-between items-center mb-3">
              <span className="font-medium text-gray-700 text-sm">
                {guarantorLabel} {isOrdinary ? i + 2 : i + 1}
              </span>
              {guarantors.length > 1 && (
                <button type="button" onClick={() => removeGuarantor(i)} className="text-red-500 text-xs hover:underline min-h-11">
                  ✖ حذف
                </button>
              )}
            </div>
            <div className="grid md:grid-cols-5 gap-3">
              {([
                ['name', 'الاسم'],
                ['father', 'اسم الأب'],
                ['family', 'النسبة'],
                ['mother', 'اسم الأم'],
                ['birth', 'مكان وتاريخ الولادة'],
                ['register', 'مكان ورقم القيد'],
                ['nationalId', 'الرقم الوطني'],
              ] as const).map(([k, label]) => (
                <div key={k}>
                  <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                  <input value={g[k] ?? ''} onChange={(e) => setG(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
              ))}
              <div>
                <label className="block text-xs font-bold text-gray-600 mb-1">نوع العنوان</label>
                <select value={g.addressType ?? 'موطن مختار'} onChange={(e) => setG(i, 'addressType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none">
                  {ADDRESS_TYPES.map((o) => (
                    <option key={o}>{o}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-bold text-gray-600 mb-1">{addressLabelOf(g.addressType)}</label>
                <input value={g.address ?? ''} onChange={(e) => setG(i, 'address', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
              </div>
            </div>
            <HeirsEditor
              idPrefix={`guarantor-${i}`}
              heirs={g.heirs ?? []}
              onSet={(hi, k, v) => setGHeir(i, hi, k, v)}
              onAdd={() => addGHeir(i)}
              onRemove={(hi) => removeGHeir(i, hi)}
            />
          </div>
        ))}
        <div className="flex gap-4 items-center">
          <button
            type="button"
            onClick={addGuarantor}
            disabled={guarantors.length >= MAX_GUARANTORS}
            className="bg-emerald-700 hover:bg-emerald-600 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
          >
            {guarantors.length >= MAX_GUARANTORS ? '🛑 الحد الأقصى' : `➕ إضافة ${guarantorLabel}`}
          </button>
          <span className="text-xs text-gray-500">
            {remainingGuarantors > 0 ? `متبقي: ${remainingGuarantors} من ${MAX_GUARANTORS}` : 'وصلت الحد الأقصى'}
          </span>
        </div>

        {section('العقارات')}
        {estates.map((e, i) => (
          <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
            <div className="flex justify-between items-center mb-3">
              <span className="font-medium text-gray-700 text-sm">عقار {i + 1}</span>
              <button type="button" onClick={() => setEstates((es) => es.filter((_, idx) => idx !== i))} className="text-red-500 text-xs hover:underline min-h-11">
                ✖ حذف
              </button>
            </div>
            <div className="grid md:grid-cols-5 gap-3">
              {([
                ['propertyNumber', 'رقم العقار'],
                ['propertyDistrict', 'المنطقة العقارية'],
                ['landRegistry', 'المصالح العقارية'],
              ] as const).map(([k, label]) => (
                <div key={k}>
                  <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                  <input value={e[k] ?? ''} onChange={(ev) => setE(i, k, ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
              ))}
              <div>
                <label className="block text-xs font-bold text-gray-600 mb-1">مقدار الحصة</label>
                <select value={e.shareType ?? 'تمام العقار'} onChange={(ev) => setE(i, 'shareType', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none">
                  {SHARE_TYPES.map((o) => (
                    <option key={o}>{o}</option>
                  ))}
                </select>
              </div>
            </div>
            <div className="mt-3">
              <span className="block text-xs font-bold text-gray-600 mb-1">مالكو العقار</span>
              {(() => {
                // الخيارات: الأسماء المتاحة (مقترض/كفلاء/ورثة) + أي مالك محفوظ سابقًا
                // لم يعد من بينها، حتى لا يُفقد عند حفظ التعديل.
                const options = [...new Set([...ownerOptions(), ...(e.owners ?? [])])];
                return options.length > 0 ? (
                  <div className="flex flex-wrap gap-x-5 gap-y-1">
                    {options.map((o) => (
                      <label key={o} className="flex items-center gap-2 min-h-11 text-sm text-gray-700">
                        <input
                          type="checkbox"
                          checked={(e.owners ?? []).includes(o)}
                          onChange={() => toggleOwner(i, o)}
                          className="h-4 w-4 text-emerald-600"
                        />
                        {o}
                      </label>
                    ))}
                  </div>
                ) : (
                  <p className="text-xs text-gray-400">أدخل اسم المقترض أو الكفيل أو الورثة أولًا لاختيار المالك</p>
                );
              })()}
            </div>
          </div>
        ))}
        <button
          type="button"
          onClick={addEstate}
          disabled={estates.length >= MAX_ESTATES}
          className="bg-red-700 hover:bg-red-600 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
        >
          {estates.length >= MAX_ESTATES ? '🛑 الحد الأقصى' : '🏡 إضافة عقار'}
        </button>
          </>
        )}

        {!isExecuted && (
          <>
            {section('⚡ اكتب ما تم من اجراءات لإضافتها الى الإخطار التنفيذي')}
            <textarea
              value={form.immediateActions ?? ''}
              onChange={(e) => set('immediateActions', e.target.value)}
              rows={3}
              aria-label="الإجراءات المطلوب إضافتها إلى الإخطار التنفيذي"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
              placeholder="اكتب ما تم من اجراءات لإضافتها الى الإخطار التنفيذي..."
            />
          </>
        )}

        {!isEdit && (
          <>
            {section('📝 الملاحظات')}
            <textarea
              value={form.notes ?? ''}
              onChange={(e) => set('notes', e.target.value)}
              rows={3}
              aria-label="الملاحظات"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </>
        )}

        {isEdit && !isExecuted && (
          <>
            {section('⚙️ تغيير الحالة')}
            {statusError && <p className="text-red-600 text-sm mb-3">{statusError}</p>}
            <div className="flex flex-wrap items-end gap-3">
              <SelectInput
                id="status"
                label="الحالة"
                value={statusForm.status}
                onChange={(v) => setStatusForm((f) => ({ ...f, status: v as ExecStatusChoice }))}
                options={STATUS_CHOICES}
              />
              {statusForm.status === 'منفذ جبريا' && (
                <>
                  <SelectInput
                    id="execSubStatus"
                    label="نوع التنفيذ"
                    value={statusForm.execSubStatus}
                    onChange={(v) => setStatusForm((f) => ({ ...f, execSubStatus: v as ExecSubChoice }))}
                    options={EXEC_SUB_CHOICES}
                  />
                  <FieldInput
                    id="collectedAmount"
                    label="المبلغ المحصل"
                    type="number"
                    value={statusForm.collectedAmount}
                    onChange={(v) => setStatusForm((s) => ({ ...s, collectedAmount: v }))}
                  />
                </>
              )}
              {statusForm.status === 'منفذ بالتسوية' && (
                <>
                  {BARAET_FIELDS.map((f) => (
                    <FieldInput
                      key={f.key}
                      id={f.key}
                      label={f.label}
                      value={statusForm[f.key]}
                      onChange={(v) => setStatusForm((s) => ({ ...s, [f.key]: v }))}
                    />
                  ))}
                  <FieldInput
                    id="collectedAmount"
                    label="المبلغ المحصل"
                    type="number"
                    value={statusForm.collectedAmount}
                    onChange={(v) => setStatusForm((s) => ({ ...s, collectedAmount: v }))}
                  />
                </>
              )}
              {statusForm.status === 'تريث' && (
                <>
                  {TARITH_FIELDS.map((f) => (
                    <FieldInput
                      key={f.key}
                      id={f.key}
                      label={f.label}
                      value={statusForm[f.key]}
                      onChange={(v) => setStatusForm((s) => ({ ...s, [f.key]: v }))}
                    />
                  ))}
                </>
              )}
              <button
                type="button"
                onClick={saveStatus}
                disabled={statusBusy}
                className="bg-blue-700 hover:bg-blue-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {statusBusy ? 'جارِ الحفظ...' : 'حفظ الحالة'}
              </button>
              {hasStatus && (
                <button
                  type="button"
                  onClick={cancelStatus}
                  disabled={statusBusy}
                  className="border border-red-300 text-red-600 rounded-lg px-4 py-2 text-sm hover:bg-red-50 min-h-11"
                >
                  إلغاء الحالة
                </button>
              )}
            </div>
          </>
        )}

        <div className="mt-8 flex gap-3">
          <button type="submit" disabled={busy} className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white font-bold rounded-lg px-6 py-2.5 transition-colors min-h-11">
            {busy ? 'جارِ الحفظ...' : isEdit ? 'حفظ التعديلات' : '💾 حفظ'}
          </button>
          {!isEdit && (
            <button type="button" onClick={resetForm} className="bg-red-700 hover:bg-red-600 text-white font-bold rounded-lg px-6 py-2.5 transition-colors min-h-11">
              🗑️ إعادة تعيين
            </button>
          )}
          <Link to="/documents" className="border border-gray-300 rounded-lg px-6 py-2.5 text-gray-700 hover:bg-gray-50 min-h-11 inline-flex items-center">
            إلغاء
          </Link>
        </div>

        {canDelete && (
          <div className="mt-8 border-t border-gray-200 pt-6">
            <button
              type="button"
              onClick={deleteDoc}
              disabled={deleteBusy}
              className="bg-red-600 hover:bg-red-500 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              {deleteBusy ? 'جارِ الحذف...' : '🗑️ حذف الملف'}
            </button>
          </div>
        )}
      </form>
    </div>
  );
}
