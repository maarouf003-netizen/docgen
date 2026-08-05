import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useParams, Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import AutoResizeTextarea from '../components/AutoResizeTextarea';
import type { DocumentResponse, DocumentUpsertRequest, GuarantorDto, RealEstateDto } from '../types';

const FILE_YEARS = ['2026', '2027', '2028', '2029', '2030'];
const CURRENCIES = ['ليرة سورية', 'دولار أمريكي', 'يورو'];
const ADDRESS_TYPES = ['موطن مختار', 'عنوان'];
const SHARE_TYPES = ['تمام العقار', 'حصة سهمية'];
const MAX_GUARANTORS = 4;
const MAX_ESTATES = 20;

function emptyGuarantor(): GuarantorDto {
  return { guarantorNumber: 1, name: '', father: '', family: '', mother: '', birth: '', register: '', nationalId: '', address: '', addressType: 'موطن مختار' };
}

function emptyEstate(): RealEstateDto {
  return { owner: '', property: '', propertyNumber: '', propertyDistrict: '', landRegistry: '', shareType: 'تمام العقار' };
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
      <label htmlFor={id} className="block text-xs font-medium text-gray-600 mb-1">
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
      <label htmlFor={id} className="block text-xs font-medium text-gray-600 mb-1">
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
    lawyer: d.lawyer ?? '',
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
  };
}

export default function DocumentForm() {
  const { id } = useParams();
  const isEdit = Boolean(id);
  const navigate = useNavigate();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [guarantors, setGuarantors] = useState<GuarantorDto[]>([emptyGuarantor()]);
  const [estates, setEstates] = useState<RealEstateDto[]>([]);
  const [showInclusionAmount, setShowInclusionAmount] = useState(false);
  const [showAmount2, setShowAmount2] = useState(false);
  const [form, setForm] = useState<DocumentUpsertRequest>({
    guarantors: [],
    realEstates: [],
    currency: 'ليرة سورية',
    currency2: 'دولار أمريكي',
    inclusionCurrency: 'ليرة سورية',
    contractTypeSelector: 'مصرفي',
    borrowerAddressType: 'موطن مختار',
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
        setEstates(d.realEstates);
        setShowInclusionAmount(
          Boolean(d.inclusionAmountNumeric || d.inclusionAmountWords),
        );
        setShowAmount2(Boolean(d.amount2Numeric || d.amount2Words));
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

  const setG = (i: number, key: keyof GuarantorDto, value: string) =>
    setGuarantors((gs) => gs.map((g, idx) => (idx === i ? { ...g, [key]: value } : g)));

  const setE = (i: number, key: keyof RealEstateDto, value: string) =>
    setEstates((es) => es.map((e, idx) => (idx === i ? { ...e, [key]: value } : e)));

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
    });
    setGuarantors([emptyGuarantor()]);
    setEstates([]);
    setShowInclusionAmount(false);
    setShowAmount2(false);
    setError('');
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setBusy(true);
    try {
      const payload: DocumentUpsertRequest = {
        ...form,
        guarantors: guarantors
          .filter((g) => g.name?.trim())
          .map((g, i) => ({ ...g, guarantorNumber: i + 1 })),
        realEstates: estates
          .filter((r) => r.propertyNumber?.trim() || r.owner?.trim())
          .map((r) => ({
            ...r,
            property: `${r.propertyNumber ?? ''} ${r.propertyDistrict ?? ''}`.trim(),
          })),
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
      <label htmlFor={key} className="block text-xs font-medium text-gray-600 mb-1">{label}</label>
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
      <label htmlFor={id} className="block text-xs font-medium text-gray-600 mb-1">{label}</label>
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
  const guarantorLabel = isOrdinary ? 'منفذ عليه' : 'كفيل';
  const remainingGuarantors = MAX_GUARANTORS - guarantors.length;

  const ownerOptions = (current: string | undefined) => {
    const opts: string[] = [];
    const borrowerFull = `${form.borrowerName ?? ''} ${form.borrowerFamily ?? ''}`.trim();
    if (borrowerFull) opts.push(borrowerFull);
    guarantors.forEach((g) => {
      const full = `${g.name ?? ''} ${g.family ?? ''}`.trim();
      if (full && !opts.includes(full)) opts.push(full);
    });
    if (current && !opts.includes(current)) opts.push(current);
    if (!opts.length) opts.push('غير محدد');
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
        {section('🏛️ المعلومات الأساسية')}
        <div className="grid md:grid-cols-5 gap-4 items-end">
          {field('دائرة التنفيذ', 'court')}
          {field('رقم الملف', 'fileNumber', 'رقم الملف...')}
          {selectField('سنة الملف', 'fileYear', ['', ...FILE_YEARS], form.fileYear ?? '', (v) => set('fileYear', v))}
          {field('نوع الملف', 'fileType', 'نوع الملف...')}
          {field('طالب التنفيذ', 'applicant')}
          {field('المحامي المختص', 'lawyer')}
          {field('الفرع', 'branchName')}
          {field('رقم كتاب الجهة العامة', 'fileIncoming')}
          {field('تاريخ كتاب الجهة العامة', 'fileIncomingDate')}
          {field('رقم تحت رفع', 'underFilingNumber')}
          {field('تاريخ قيد الملف', 'fileRegistrationDate', 'مثال: 1/8/2026')}
          {field('تاريخ إلقاء حجز المنظومة', 'seizureDate', 'مثال: 1/8/2026')}
        </div>

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
                <label htmlFor="inclusionText" className="block text-xs font-medium text-gray-600 mb-1">
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
          {field('العنوان', 'borrowerAddress')}
          {selectField('نوع العنوان', 'borrowerAddressType', ADDRESS_TYPES, form.borrowerAddressType ?? 'موطن مختار', (v) => set('borrowerAddressType', v))}
        </div>

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
                ['address', 'العنوان'],
              ] as const).map(([k, label]) => (
                <div key={k}>
                  <label className="block text-xs font-medium text-gray-600 mb-1">{label}</label>
                  <input value={g[k] ?? ''} onChange={(e) => setG(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
              ))}
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">نوع العنوان</label>
                <select value={g.addressType ?? 'موطن مختار'} onChange={(e) => setG(i, 'addressType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none">
                  {ADDRESS_TYPES.map((o) => (
                    <option key={o}>{o}</option>
                  ))}
                </select>
              </div>
            </div>
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
                  <label className="block text-xs font-medium text-gray-600 mb-1">{label}</label>
                  <input value={e[k] ?? ''} onChange={(ev) => setE(i, k, ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
              ))}
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">مقدار الحصة</label>
                <select value={e.shareType ?? 'تمام العقار'} onChange={(ev) => setE(i, 'shareType', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none">
                  {SHARE_TYPES.map((o) => (
                    <option key={o}>{o}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">مالك العقار</label>
                <select value={e.owner ?? ''} onChange={(ev) => setE(i, 'owner', ev.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none">
                  {ownerOptions(e.owner).map((o) => (
                    <option key={o}>{o}</option>
                  ))}
                </select>
              </div>
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

        {section('⚡ اكتب ما تم من اجراءات لإضافتها الى الإخطار التنفيذي')}
        <textarea
          value={form.immediateActions ?? ''}
          onChange={(e) => set('immediateActions', e.target.value)}
          rows={3}
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          placeholder="اكتب ما تم من اجراءات لإضافتها الى الإخطار التنفيذي..."
        />

        {section('📝 الملاحظات')}
        <textarea value={form.notes ?? ''} onChange={(e) => set('notes', e.target.value)} rows={3} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />

        {isEdit && (
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
      </form>
    </div>
  );
}
