import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useNavigate, useParams, Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { normalizeDocumentResponse } from '../utils/apiNormalization';
import { useAuth } from '../auth/useAuth';
import { ApplicantSideSections } from '../components/form/ApplicantSideSections';
import {
  ASSET_KINDS,
  emptyAsset,
  FILE_YEARS,
  MAX_ASSETS_PER_KIND,
  MAX_GUARANTORS,
  SHAREABLE_ASSET_KINDS,
  bankingCurrencyKeys,
  emptyExecutedNaturalPerson,
  emptyExecutedPublicEntity,
  emptyExecutedHeir,
  emptyExecutionApplicant,
  emptyApplicantPublicEntity,
  emptyGuarantor,
  emptyHeir,
  hasHeirName,
  hasRepresentative,
  ordinaryCurrencyKeys,
  requiredCurrencyKeys,
  toUpsert,
} from '../components/form/documentFormConstants';
import { ExecutedSideSections } from '../components/form/ExecutedSideSections';
import { OccurrencesEditor } from '../components/form/OccurrencesEditor';
import { makeFieldHelpers } from '../components/form/formFields';
import { FormSectionTitle } from '../components/form/FormSectionTitle';
import { PublicEntityPickerModal } from '../components/entity/PublicEntityPickerModal';
import { slotDefaultCurrency } from '../utils/amountCurrencies';
import { normalizeArabicDigits } from '../utils/arabicDigits';
import { tripleName, isExecutedLike } from '../utils/documentDisplay';
import { governorateFromBranch } from '../utils/governorate';
import type {
  ApplicantPublicEntityDto,
  AssetDto,
  DocumentOccurrenceDto,
  DocumentResponse,
  DocumentUpsertRequest,
  ExecutedHeirDto,
  ExecutedNaturalPersonDto,
  ExecutedPublicEntityDto,
  ExecutionApplicantDto,
  GeneralEntitySide,
  GuarantorDto,
  HeirDto,
  PartyNature,
  EntityNature,
  PublicEntityEntryDto,
} from '../types';

export default function DocumentForm() {
  const { id } = useParams();
  const isEdit = Boolean(id);
  const navigate = useNavigate();
  const { user } = useAuth();
  // «المحافظة» للجهات العامة تُملأ تلقائيًا من فرع المحامي (دمشق/حلب...) وقابلة للتعديل،
  // فقد تكون الجهة تابعة لمحافظة أخرى. تُحفظ كمرجع ثابت لاستخدامها في معالجات الأحداث.
  const defaultGovernorateRef = useRef(governorateFromBranch(user?.branchName));
  const freshApplicantEntity = (): ApplicantPublicEntityDto => ({
    ...emptyApplicantPublicEntity(),
    governorate: defaultGovernorateRef.current,
  });
  const freshExecutedEntity = (nature: EntityNature = 'public'): ExecutedPublicEntityDto => ({
    ...emptyExecutedPublicEntity(),
    nature,
    governorate: defaultGovernorateRef.current,
  });
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [deleteBusy, setDeleteBusy] = useState(false);
  // نافذة اختيار الجهة العامة من السجل المرجعي (المرحلة 2): الجهة المستهدفة
  // من الطرفين ورقم صفها، وتُملأ حقولها النصية من القيد المختار مع ربطه.
  const [registryPicker, setRegistryPicker] = useState<
    { side: 'applicant' | 'executed'; index: number } | null
  >(null);
  const [guarantors, setGuarantors] = useState<GuarantorDto[]>([emptyGuarantor()]);
  const [borrowerHeirs, setBorrowerHeirs] = useState<HeirDto[]>([]);
  const [assets, setAssets] = useState<AssetDto[]>([]);
  const [executionApplicants, setExecutionApplicants] = useState<ExecutionApplicantDto[]>([emptyExecutionApplicant()]);
  const [executedPublicEntities, setExecutedPublicEntities] = useState<ExecutedPublicEntityDto[]>([freshExecutedEntity()]);
  const [applicantPublicEntities, setApplicantPublicEntities] = useState<ApplicantPublicEntityDto[]>([freshApplicantEntity()]);
  const [executedNaturalPersons, setExecutedNaturalPersons] = useState<ExecutedNaturalPersonDto[]>([]);
  const [showInclusionAmount, setShowInclusionAmount] = useState(false);
  const [paidAmountSlots, setPaidAmountSlots] = useState(1);
  const [showRequiredAmount, setShowRequiredAmount] = useState(false);
  const [requiredAmountSlots, setRequiredAmountSlots] = useState(1);
  const [wasOriginallyStruckOff, setWasOriginallyStruckOff] = useState(false);
  const [occurrences, setOccurrences] = useState<DocumentOccurrenceDto[]>([]);
  const [bankingAmountSlots, setBankingAmountSlots] = useState(1);
  const [ordinaryAmountSlots, setOrdinaryAmountSlots] = useState(1);
  const [form, setForm] = useState<DocumentUpsertRequest>({
    guarantors: [],
    assets: [],
    currency: 'ليرة سورية',
    currency2: 'دولار أمريكي',
    inclusionCurrency: 'ليرة سورية',
    contractTypeSelector: 'مصرفي',
    borrowerAddressType: 'موطن مختار',
    borrowerNature: 'natural',
    // «فرع الملف» قيمة نظامية مشتقة من فرع المحامي المنشئ — لا يُكتب يدويًا.
    branchName: user?.branchName ?? '',
    generalEntitySide: 'applicant',
    executedStatus: '',
    executionApplicants: [],
    executedPublicEntities: [],
    executedNaturalPersons: [],
  });
  useEffect(() => {
    if (!isEdit) return;
    api
      .get<DocumentResponse>(`/documents/${id}`)
      .then((r) => {
        const d = normalizeDocumentResponse(r.data);
        setForm(toUpsert(d));
        setGuarantors(d.guarantors.length ? d.guarantors : [emptyGuarantor()]);
        setBorrowerHeirs(d.borrowerHeirs);
        // تصحيح أي بيانات قديمة متناقضة عند التحميل: تمام الأصل لا يكون إلا لمالك واحد،
        // والأنواع غير الحصصية (كفالة الرواتب والمتجر غير المسجل) لا تحمل مقدار حصة أصلًا.
        setAssets(
          d.assets.map((a) => ({
            ...a,
            shareType:
              SHAREABLE_ASSET_KINDS.has(a.assetKind ?? '') && (a.owners ?? []).length > 1
                ? 'حصة سهمية'
                : a.shareType,
          })),
        );
        setShowInclusionAmount(
          Boolean(d.inclusionAmountNumeric || d.inclusionAmountWords),
        );
        // المبالغ المدفوعة (حتى ثلاثة) تظهر عند التعديل بحسب ما هو محفوظ منها.
        setPaidAmountSlots(
          Math.max(1, Number(Boolean(d.executedPaidAmount))
            + Number(Boolean(d.executedPaidAmount2))
            + Number(Boolean(d.executedPaidAmount3))),
        );
        // مبالغ «طالبة التنفيذ»: المصرفي (المطالب به) والعادي (المتضمن) حتى ثلاثة
        // بحسب ما هو محفوظ منها عند التعديل.
        setBankingAmountSlots(
          Math.max(1, Number(Boolean(d.amountNumeric))
            + Number(Boolean(d.amount2Numeric))
            + Number(Boolean(d.amount3Numeric))),
        );
        setOrdinaryAmountSlots(
          Math.max(1, Number(Boolean(d.inclusionAmountNumeric))
            + Number(Boolean(d.inclusionAmount2Numeric))
            + Number(Boolean(d.inclusionAmount3Numeric))),
        );
        // المبالغ المطلوب دفعها (حتى ثلاثة) تظهر عند التعديل بحسب ما هو محفوظ منها.
        setShowRequiredAmount(
          Boolean(d.executedRequiredAmount || d.executedRequiredAmount2 || d.executedRequiredAmount3),
        );
        setRequiredAmountSlots(
          Math.max(1, Number(Boolean(d.executedRequiredAmount))
            + Number(Boolean(d.executedRequiredAmount2))
            + Number(Boolean(d.executedRequiredAmount3))),
        );
        // طالب تنفيذ واحد وجهة عامة واحدة يظهران دائمًا (وإن لم يكن للملف أي منهما محفوظًا).
        setExecutionApplicants(d.executionApplicants.length ? d.executionApplicants : [emptyExecutionApplicant()]);
        setExecutedPublicEntities(d.executedPublicEntities.length ? d.executedPublicEntities : [{ ...emptyExecutedPublicEntity(), governorate: defaultGovernorateRef.current }]);
        setApplicantPublicEntities(d.applicantPublicEntities.length ? d.applicantPublicEntities : [{ ...emptyApplicantPublicEntity(), governorate: defaultGovernorateRef.current }]);
        setExecutedNaturalPersons(d.executedNaturalPersons);
        setWasOriginallyStruckOff(d.executedStatus === 'مشطوب');
        setOccurrences(d.occurrences);
      })
      .catch((err) => setError(getApiErrorMessage(err)));
  }, [id, isEdit]);

  const set = (key: keyof DocumentUpsertRequest, value: unknown) =>
    setForm((f) => ({ ...f, [key]: value }));

  // صفة الملف تُثبَّت عند الإنشاء ولا تُغيَّر عند التعديل. عند اختيار «الجهة العامة منفذ عليها»
  // أو «عرض وايداع» يُفرض نوع السند «عادي» فورًا (المصرفي محظور في هذا الوضع).
  const setSide = (side: GeneralEntitySide) =>
    setForm((f) => ({
      ...f,
      generalEntitySide: side,
      contractTypeSelector: side === 'executed' || side === 'deposit' ? 'عادي' : f.contractTypeSelector ?? 'مصرفي',
    }));

  const setG = (i: number, key: keyof GuarantorDto, value: string) =>
    setGuarantors((gs) => gs.map((g, idx) => (idx === i ? { ...g, [key]: value } : g)));

  const setE = (i: number, key: keyof AssetDto, value: string) =>
    setAssets((as) => as.map((a, idx) => (idx === i ? { ...a, [key]: value } : a)));

  const toggleOwner = (i: number, name: string) =>
    setAssets((as) =>
      as.map((a, idx) => {
        if (idx !== i) return a;
        const current = a.owners ?? [];
        const owners = current.includes(name)
          ? current.filter((o) => o !== name)
          : [...current, name];
        return {
          ...a,
          owners,
          // تمام الأصل لا يكون إلا لمالك واحد؛ عند تعدد الملاك تُفرض الحصة السهمية تلقائيًا
          // (للأنواع الحصصية فقط) حتى لو نسي المحامي اختيارها، مع عدم الرجوع عنها عند النقص.
          shareType:
            SHAREABLE_ASSET_KINDS.has(a.assetKind ?? '') && owners.length > 1
              ? 'حصة سهمية'
              : a.shareType,
        };
      }),
    );

  const setSingleOwner = (i: number, name: string) =>
    setAssets((as) => as.map((a, idx) => (idx === i ? { ...a, owners: name ? [name] : [] } : a)));

  const setBorrowerHeir = (i: number, key: keyof HeirDto, value: string) =>
    setBorrowerHeirs((hs) => hs.map((h, idx) => (idx === i ? { ...h, [key]: value } : h)));

  const addBorrowerHeir = () => setBorrowerHeirs((hs) => [...hs, emptyHeir()]);

  const removeBorrowerHeir = (i: number) =>
    setBorrowerHeirs((hs) => hs.filter((_, idx) => idx !== i));

  const setApplicant = (i: number, key: keyof ExecutionApplicantDto, value: string) =>
    setExecutionApplicants((xs) => xs.map((x, idx) => (idx === i ? { ...x, [key]: value } : x)));

  const addApplicant = (nature: PartyNature = 'natural') =>
    setExecutionApplicants((xs) => [...xs, { ...emptyExecutionApplicant(), nature }]);

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

  // حقول الهوية النصية للجهة: أي تحرير يدوي لها يفكّ ربط السجل المرجعي تلقائيًا
  // حتى لا يبقى الملف مربوطًا بقيد لم يعد يطابق نصَّه.
  const APPLICANT_IDENTITY_KEYS: ReadonlyArray<keyof ApplicantPublicEntityDto> = ['name', 'branch', 'governorate'];
  const EXECUTED_IDENTITY_KEYS: ReadonlyArray<keyof ExecutedPublicEntityDto> = [
    'entityName', 'entityBranch', 'governorate',
  ];

  const setExecutedEntity = (i: number, key: keyof ExecutedPublicEntityDto, value: string) =>
    setExecutedPublicEntities((xs) =>
      xs.map((x, idx) =>
        idx === i
          ? {
              ...x,
              [key]: value,
              ...(EXECUTED_IDENTITY_KEYS.includes(key) && x.registryId != null ? { registryId: null } : {}),
            }
          : x,
      ),
    );

  const addExecutedEntity = (nature: EntityNature = 'public') =>
    setExecutedPublicEntities((xs) => [...xs, freshExecutedEntity(nature)]);

  const removeExecutedEntity = (i: number) =>
    setExecutedPublicEntities((xs) => xs.filter((_, idx) => idx !== i));

  const setApplicantPublicEntity = (i: number, key: keyof ApplicantPublicEntityDto, value: string) =>
    setApplicantPublicEntities((xs) =>
      xs.map((x, idx) =>
        idx === i
          ? {
              ...x,
              [key]: value,
              ...(APPLICANT_IDENTITY_KEYS.includes(key) && x.registryId != null ? { registryId: null } : {}),
            }
          : x,
      ),
    );

  const addApplicantPublicEntity = () => setApplicantPublicEntities((xs) => [...xs, freshApplicantEntity()]);

  const removeApplicantPublicEntity = (i: number) =>
    setApplicantPublicEntities((xs) => xs.filter((_, idx) => idx !== i));

  // ربط صف جهة بالقيد المختار من نافذة السجل: تُملأ حقول الهوية النصية من القيد
  // المعتمد نفسه فتظل الأعمدة النصية متسقة مع السجل، ويُحفظ معرّف الربط.
  const applyRegistryPick = (entry: PublicEntityEntryDto) => {
    if (!registryPicker) return;
    const { side, index } = registryPicker;
    if (side === 'applicant') {
      setApplicantPublicEntities((xs) =>
        xs.map((x, idx) =>
          idx === index
            ? { ...x, name: entry.canonicalName, branch: entry.branchName, governorate: entry.governorate, registryId: entry.id }
            : x,
        ),
      );
    } else {
      setExecutedPublicEntities((xs) =>
        xs.map((x, idx) =>
          idx === index
            ? { ...x, entityName: entry.canonicalName, entityBranch: entry.branchName, governorate: entry.governorate, registryId: entry.id }
            : x,
        ),
      );
    }
    setRegistryPicker(null);
  };

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

  const activateBorrowerRep = () =>
    setForm((f) => ({ ...f, borrowerRepresentativeCapacity: 'ولي', borrowerRepresentativeAddressType: 'عنوان' }));

  const removeBorrowerRep = () =>
    setForm((f) => ({
      ...f,
      borrowerRepresentativeName: '',
      borrowerRepresentativeFather: '',
      borrowerRepresentativeFamily: '',
      borrowerRepresentativeCapacity: '',
      borrowerRepresentativeAddressType: '',
      borrowerRepresentativeAddress: '',
    }));

  const activateGuarantorRep = (i: number) =>
    setGuarantors((gs) =>
      gs.map((g, idx) =>
        idx === i ? { ...g, representativeCapacity: 'ولي', representativeAddressType: 'عنوان' } : g,
      ),
    );

  const removeGuarantorRep = (i: number) =>
    setGuarantors((gs) =>
      gs.map((g, idx) =>
        idx === i
          ? {
              ...g,
              representativeName: '',
              representativeFather: '',
              representativeFamily: '',
              representativeCapacity: '',
              representativeAddressType: '',
              representativeAddress: '',
            }
          : g,
      ),
    );

  const activateApplicantRep = (i: number) =>
    setExecutionApplicants((xs) =>
      xs.map((x, idx) => (idx === i ? { ...x, representativeCapacity: 'ولي' } : x)),
    );

  const removeApplicantRep = (i: number) =>
    setExecutionApplicants((xs) =>
      xs.map((x, idx) =>
        idx === i
          ? {
              ...x,
              representativeName: '',
              representativeFather: '',
              representativeFamily: '',
              representativeCapacity: '',
              representativeLegalRepresentative: '',
            }
          : x,
      ),
    );

  const activatePersonRep = (i: number) =>
    setExecutedNaturalPersons((xs) =>
      xs.map((x, idx) =>
        idx === i ? { ...x, representativeCapacity: 'ولي', representativeAddressType: 'عنوان' } : x,
      ),
    );

  const removePersonRep = (i: number) =>
    setExecutedNaturalPersons((xs) =>
      xs.map((x, idx) =>
        idx === i
          ? {
              ...x,
              representativeName: '',
              representativeFather: '',
              representativeFamily: '',
              representativeCapacity: '',
              representativeAddressType: '',
              representativeAddress: '',
            }
          : x,
      ),
    );

  const addGuarantor = (nature: PartyNature = 'natural') => {
    if (guarantors.length >= MAX_GUARANTORS) return;
    setGuarantors((gs) => [...gs, { ...emptyGuarantor(), nature }]);
  };

  const removeGuarantor = (i: number) => {
    if (guarantors.length <= 1) return;
    setGuarantors((gs) => gs.filter((_, idx) => idx !== i));
  };

  const addEstate = (kind: string) => {
    const count = assets.filter((a) => a.assetKind === kind).length;
    if (count >= MAX_ASSETS_PER_KIND) return;
    setAssets((as) => [...as, emptyAsset(kind)]);
  };

  const removeEstate = (i: number) => setAssets((as) => as.filter((_, idx) => idx !== i));

  const resetForm = () => {
    setForm({
      guarantors: [],
      assets: [],
      currency: 'ليرة سورية',
      currency2: 'دولار أمريكي',
      inclusionCurrency: 'ليرة سورية',
      contractTypeSelector: 'مصرفي',
      borrowerAddressType: 'موطن مختار',
      borrowerNature: 'natural',
      branchName: user?.branchName ?? '',
      generalEntitySide: 'applicant',
      executedStatus: '',
      executionApplicants: [],
      executedPublicEntities: [],
      executedNaturalPersons: [],
    });
    setGuarantors([emptyGuarantor()]);
    setBorrowerHeirs([]);
    setAssets([]);
    setExecutionApplicants([emptyExecutionApplicant()]);
    setExecutedPublicEntities([freshExecutedEntity()]);
    setApplicantPublicEntities([freshApplicantEntity()]);
    setExecutedNaturalPersons([]);
    setShowInclusionAmount(false);
    setPaidAmountSlots(1);
    setRequiredAmountSlots(1);
    setBankingAmountSlots(1);
    setOrdinaryAmountSlots(1);
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

    const isExecutedSubmit = form.generalEntitySide === 'executed' || form.generalEntitySide === 'deposit';
    const isDepositSubmit = form.generalEntitySide === 'deposit';
    const sideLabel = isDepositSubmit ? 'عرض وايداع' : 'الجهة العامة منفذ عليها';

    if (
      isExecutedSubmit &&
      (!(form.fileNumber ?? '').trim() || !(form.fileYear ?? '').trim())
    ) {
      setError(`ملف «${sideLabel}» يجب أن يكون مقيدًا برقم وسنة الملف`);
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

    // العودة من مشطوب إلى متداول تستلزم رقم الملف الجديد (تجديد) — يتحقق مسبقًا
    // قبل إرسال النموذج ليطابق اشتراط الخلفية (رقم الملف الجديد إلزامي).
    if (isEdit && wasOriginallyStruckOff && !(form.executedStatus ?? '') && !(form.renewalFileNumber ?? '').trim()) {
      setError('رقم الملف الجديد مطلوب عند إعادة الملف المشطوب إلى المتداول');
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
      const borrowerHasHeirs = borrowerHeirs.length > 0;
      const borrowerHasRep = hasRepresentative({
        representativeName: form.borrowerRepresentativeName,
        representativeFather: form.borrowerRepresentativeFather,
        representativeFamily: form.borrowerRepresentativeFamily,
        representativeCapacity: form.borrowerRepresentativeCapacity,
      });
      const payload: DocumentUpsertRequest = {
        ...form,
        // تطبيع الأرقام العربية/الفارسية إلى ASCII في حقول التواريخ قبل الإرسال،
        // ليتسق المخزَّن مع ما يُعرض ولتقبله الخلفية في تحليلها (تحافظ على الحقول الأخرى كما هي).
        borrowerBirth: normalizeArabicDigits(form.borrowerBirth ?? ''),
        contractDate: normalizeArabicDigits(form.contractDate ?? ''),
        annexDate: normalizeArabicDigits(form.annexDate ?? ''),
        fileIncomingDate: normalizeArabicDigits(form.fileIncomingDate ?? ''),
        fileRegistrationDate: normalizeArabicDigits(form.fileRegistrationDate ?? ''),
        seizureDate: normalizeArabicDigits(form.seizureDate ?? ''),
        // تاريخان من نوع DateTime? في الخلفية: السلسلة الفارغة تُعطّل فك JSON،
        // فلا تُرسل إلا القيمة غير الفارغة (وإلا يُحذف المفتاح فيؤول null على الخادم).
        ...((form.fileReceiptDate ?? '').trim() ? { fileReceiptDate: normalizeArabicDigits(form.fileReceiptDate ?? '').trim() } : {}),
        ...((form.struckOffDate ?? '').trim() ? { struckOffDate: normalizeArabicDigits(form.struckOffDate ?? '').trim() } : {}),
        ...((form.executedDepositDate ?? '').trim() ? { executedDepositDate: normalizeArabicDigits(form.executedDepositDate ?? '').trim() } : {}),
        ...((form.renewalFileReceiptDate ?? '').trim() ? { renewalFileReceiptDate: normalizeArabicDigits(form.renewalFileReceiptDate ?? '').trim() } : {}),
        ...((form.renewalDate ?? '').trim() ? { renewalDate: normalizeArabicDigits(form.renewalDate ?? '').trim() } : {}),
        // عملة المبالغ: «ليرة سورية» افتراضيًا للخانة الأولى، ولكل خانة لاحقة أول العملات
        // غير المستعملة في الخانات السابقة (لا تكرار للعملة) — للمبالغ المطلوب دفعها في وضع
        // «منفذ عليه» وللمصرفي/العادي في وضع «طالبة التنفيذ».
        executedRequiredCurrency: slotDefaultCurrency(form, requiredCurrencyKeys, 0),
        executedRequiredCurrency2: slotDefaultCurrency(form, requiredCurrencyKeys, 1),
        executedRequiredCurrency3: slotDefaultCurrency(form, requiredCurrencyKeys, 2),
        currency: slotDefaultCurrency(form, bankingCurrencyKeys, 0),
        currency2: slotDefaultCurrency(form, bankingCurrencyKeys, 1),
        currency3: slotDefaultCurrency(form, bankingCurrencyKeys, 2),
        inclusionCurrency: slotDefaultCurrency(form, ordinaryCurrencyKeys, 0),
        inclusionCurrency2: slotDefaultCurrency(form, ordinaryCurrencyKeys, 1),
        inclusionCurrency3: slotDefaultCurrency(form, ordinaryCurrencyKeys, 2),
        // وضع «منفذ عليه»: عادي فقط، بلا مقترض/كفلاء/أموال (مطابق لتحقق الخلفية).
        contractTypeSelector: isExecutedSubmit ? 'عادي' : (form.contractTypeSelector ?? 'مصرفي'),
        // عنوان المقترض يُصفَّر عند وجود وريث أو ممثل شرعي (الوريث/الممثل هو الحامل الفعلي للعنوان).
        borrowerAddressType: isExecutedSubmit || borrowerHasHeirs || borrowerHasRep ? '' : form.borrowerAddressType,
        borrowerAddress: isExecutedSubmit || borrowerHasHeirs || borrowerHasRep ? '' : form.borrowerAddress,
        guarantors: isExecutedSubmit ? [] : guarantors
          .filter((g) => g.name?.trim())
          .map((g, i) => {
            const gHasRep = hasRepresentative(g);
            const gHasHeirs = (g.heirs ?? []).length > 0;
            return {
              ...g,
              guarantorNumber: i + 1,
              addressType: (gHasHeirs || gHasRep) ? '' : g.addressType,
              address: (gHasHeirs || gHasRep) ? '' : g.address,
              heirs: (g.heirs ?? []).filter(hasHeirName).map((h) => ({
                ...h,
                addressType: gHasRep ? '' : h.addressType,
                address: gHasRep ? '' : h.address,
              })),
            };
          }),
        borrowerHeirs: isExecutedSubmit
          ? []
          : borrowerHeirs.filter(hasHeirName).map((h) => ({
              ...h,
              addressType: borrowerHasRep ? '' : h.addressType,
              address: borrowerHasRep ? '' : h.address,
            })),
        assets: isExecutedSubmit ? [] : assets
          .filter((a) => {
            const kind = a.assetKind;
            if (kind === ASSET_KINDS.realEstate) return a.propertyNumber?.trim() || (a.owners ?? []).some((o) => (o ?? '').trim());
            if (kind === ASSET_KINDS.vehicle) return a.plateNumber?.trim() || (a.owners ?? []).some((o) => (o ?? '').trim());
            if (kind === ASSET_KINDS.shop) return a.registerNumber?.trim() || (a.owners ?? []).some((o) => (o ?? '').trim());
            if (kind === ASSET_KINDS.salaryGuarantee) return (a.owners ?? []).some((o) => (o ?? '').trim());
            if (kind === ASSET_KINDS.unregisteredShop) return a.licenseNumber?.trim() || (a.owners ?? []).some((o) => (o ?? '').trim());
            return (a.owners ?? []).some((o) => (o ?? '').trim());
          })
          .map((a) => ({
            ...a,
            property: `${a.propertyNumber ?? ''} ${a.propertyDistrict ?? ''}`.trim(),
            // تطبيع الأرقام العربية/الفارسية في تاريخَي المتجر قبل الإرسال (تتقبلها الخلفية كتواريخ حرة).
            registrationDate: normalizeArabicDigits(a.registrationDate ?? '').trim(),
            licenseDate: normalizeArabicDigits(a.licenseDate ?? '').trim(),
            seizureDate: normalizeArabicDigits(a.seizureDate ?? '').trim(),
          })),
        executionApplicants: isExecutedSubmit
          ? executionApplicants
              .filter((a) => (a.name ?? '').trim())
              .map((a) => ({
                ...a,
                legalRepresentative: hasRepresentative(a) ? '' : a.legalRepresentative,
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
        applicantPublicEntities: isExecutedSubmit
          ? []
          : applicantPublicEntities.filter((a) => (a.name ?? '').trim()),
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

  const { field, selectField } = makeFieldHelpers(form, set);

  const isOrdinary = form.contractTypeSelector === 'عادي';
  const isExecuted = isExecutedLike(form.generalEntitySide);
  const executedSide = form.generalEntitySide === 'deposit' ? 'deposit' : 'executed';
  const sideLabel = form.generalEntitySide === 'deposit'
    ? 'عرض وايداع'
    : form.generalEntitySide === 'executed'
      ? 'الجهة العامة منفذ عليها'
      : 'الجهة العامة طالبة التنفيذ';
  const guarantorLabel = isOrdinary ? 'منفذ عليه' : 'كفيل';
  const remainingGuarantors = MAX_GUARANTORS - guarantors.length;

  const ownerOptions = () => {
    const opts: string[] = [];
    // الاسم الثلاثي الكامل للمقترض والكفيل يطابق ما يحفظه الخلفية ويعرضه العرض،
    // وبه تتطابق مطابقة الورثة (ورثة المتوفى) في توليد 005/006.
    const borrowerFull = tripleName(form.borrowerName, form.borrowerFather, form.borrowerFamily);
    if (borrowerFull) opts.push(borrowerFull);
    borrowerHeirs.forEach((h) => {
      const full = tripleName(h.name, h.father, h.family);
      if (full && !opts.includes(full)) opts.push(full);
    });
    guarantors.forEach((g) => {
      const full = tripleName(g.name, g.father, g.family);
      if (full && !opts.includes(full)) opts.push(full);
      (g.heirs ?? []).forEach((h) => {
        const fullHeir = tripleName(h.name, h.father, h.family);
        if (fullHeir && !opts.includes(fullHeir)) opts.push(fullHeir);
      });
    });
    return opts;
  };

  const debtorFullName = tripleName(form.borrowerName, form.borrowerFather, form.borrowerFamily);

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

      <form onSubmit={submit} noValidate className="bg-white rounded-xl shadow p-6">
        <FormSectionTitle title="⚖️ صفة الجهة العامة" />
        <div className="rounded-lg bg-gray-50 border border-gray-200 p-4 mb-2">
          {isEdit ? (
            <p className="text-sm text-gray-700">
              صفة الجهة العامة:{' '}
              <span className="font-bold">{sideLabel}</span>
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
                  checked={form.generalEntitySide === 'applicant'}
                  onChange={() => setSide('applicant')}
                  className="h-4 w-4 text-emerald-600"
                />
                الجهة العامة طالبة التنفيذ
              </label>
              <label className="flex items-center gap-2 min-h-11 cursor-pointer text-sm text-gray-700">
                <input
                  type="radio"
                  name="generalEntitySide"
                  checked={form.generalEntitySide === 'executed'}
                  onChange={() => setSide('executed')}
                  className="h-4 w-4 text-emerald-600"
                />
                الجهة العامة منفذ عليها
              </label>
              <label className="flex items-center gap-2 min-h-11 cursor-pointer text-sm text-gray-700">
                <input
                  type="radio"
                  name="generalEntitySide"
                  checked={form.generalEntitySide === 'deposit'}
                  onChange={() => setSide('deposit')}
                  className="h-4 w-4 text-emerald-600"
                />
                عرض وايداع
              </label>
            </div>
          )}
        </div>

        <FormSectionTitle title="🏛️ المعلومات الأساسية" />
        <div className="grid md:grid-cols-5 gap-4 items-end">
          {field('دائرة التنفيذ', 'court')}
          {field('رقم الملف', 'fileNumber', 'رقم الملف...')}
          {selectField('سنة الملف', 'fileYear', ['', ...FILE_YEARS], form.fileYear ?? '', (v) => set('fileYear', v))}
          {field('نوع الملف', 'fileType', 'نوع الملف...')}
          {isExecuted ? (
            <>
              {field('رقم ورود الإخطار التنفيذي', 'fileReceiptNumber', 'رقم ورود الإخطار التنفيذي...')}
              {field('تاريخ ورود الاخطار', 'fileReceiptDate', 'مثال: 1/8/2026')}
            </>
          ) : (
            <>
              <div className="md:col-span-5">
                <span className="block text-xs font-bold text-gray-600 mb-1">طالب التنفيذ (الجهات العامة)</span>
                {applicantPublicEntities.map((a, i) => (
                  <div key={i} className="flex flex-wrap gap-2 mb-2">
                    <input
                      aria-label={`اسم الجهة ${i + 1}`}
                      value={a.name ?? ''}
                      onChange={(e) => setApplicantPublicEntity(i, 'name', e.target.value)}
                      placeholder="اسم الجهة"
                      className="w-full sm:w-64 min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    />
                    <input
                      aria-label={`فرع الجهة ${i + 1}`}
                      value={a.branch ?? ''}
                      onChange={(e) => setApplicantPublicEntity(i, 'branch', e.target.value)}
                      placeholder="فرع الجهة"
                      className="w-full sm:w-40 min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    />
                    <input
                      aria-label={`المحافظة ${i + 1}`}
                      value={a.governorate ?? ''}
                      onChange={(e) => setApplicantPublicEntity(i, 'governorate', e.target.value)}
                      placeholder="المحافظة"
                      className="w-full sm:w-40 min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    />
                    {applicantPublicEntities.length > 1 && (
                      <button
                        type="button"
                        onClick={() => removeApplicantPublicEntity(i)}
                        className="text-red-500 text-xs hover:underline min-h-11 px-2"
                      >
                        ✖ حذف
                      </button>
                    )}
                    <button
                      type="button"
                      onClick={() => setRegistryPicker({ side: 'applicant', index: i })}
                      className="border border-emerald-200 text-emerald-800 hover:bg-emerald-50 rounded-lg px-3 py-2 text-xs min-h-11"
                    >
                      اختيار من السجل…
                    </button>
                    {a.registryId != null && (
                      <span className="self-center rounded-full bg-emerald-50 border border-emerald-100 text-emerald-700 px-2 py-0.5 text-[11px] whitespace-nowrap">
                        مرتبطة بالسجل ✓
                      </span>
                    )}
                  </div>
                ))}
                <button
                  type="button"
                  onClick={addApplicantPublicEntity}
                  className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
                >
                  ➕ إضافة جهة
                </button>
              </div>
              {field('رقم ورود الملف', 'fileArrivalNumber')}
              {field('تاريخ ورود الملف', 'fileArrivalDate', 'مثال: 1/8/2026')}
              {field('رقم كتاب الجهة العامة', 'fileIncoming')}
              {field('تاريخ كتاب الجهة العامة', 'fileIncomingDate')}
              {field('رقم تحت رفع', 'underFilingNumber')}
              {field('تاريخ قيد الملف', 'fileRegistrationDate', 'مثال: 1/8/2026')}
              {field('تاريخ إلقاء حجز المنظومة', 'seizureDate', 'مثال: 1/8/2026')}
            </>
          )}
        </div>

        {isExecuted ? (
          <ExecutedSideSections
            side={executedSide}
            form={form}
            set={set}
            isEdit={isEdit}
            showRequiredAmount={showRequiredAmount}
            setShowRequiredAmount={setShowRequiredAmount}
            requiredAmountSlots={requiredAmountSlots}
            setRequiredAmountSlots={setRequiredAmountSlots}
            executionApplicants={executionApplicants}
            onApplicantSet={setApplicant}
            onApplicantAdd={addApplicant}
            onApplicantRemove={removeApplicant}
            onApplicantHeirSet={setApplicantHeir}
            onApplicantHeirAdd={addApplicantHeir}
            onApplicantHeirRemove={removeApplicantHeir}
            onApplicantRepActivate={activateApplicantRep}
            onApplicantRepRemove={removeApplicantRep}
            executedPublicEntities={executedPublicEntities}
            onEntitySet={setExecutedEntity}
            onEntityAdd={addExecutedEntity}
            onEntityRemove={removeExecutedEntity}
            onPickRegistry={(i) => setRegistryPicker({ side: 'executed', index: i })}
            executedNaturalPersons={executedNaturalPersons}
            onPersonSet={setExecutedPerson}
            onPersonAdd={addExecutedPerson}
            onPersonRemove={removeExecutedPerson}
            onPersonHeirSet={setExecutedPersonHeir}
            onPersonHeirAdd={addExecutedPersonHeir}
            onPersonHeirRemove={removeExecutedPersonHeir}
            onPersonRepActivate={activatePersonRep}
            onPersonRepRemove={removePersonRep}
            paidAmountSlots={paidAmountSlots}
            setPaidAmountSlots={setPaidAmountSlots}
            wasOriginallyStruckOff={wasOriginallyStruckOff}
          />
        ) : (
          <ApplicantSideSections
            form={form}
            set={set}
            isOrdinary={isOrdinary}
            guarantorLabel={guarantorLabel}
            remainingGuarantors={remainingGuarantors}
            showInclusionAmount={showInclusionAmount}
            setShowInclusionAmount={setShowInclusionAmount}
            ordinaryAmountSlots={ordinaryAmountSlots}
            setOrdinaryAmountSlots={setOrdinaryAmountSlots}
            bankingAmountSlots={bankingAmountSlots}
            setBankingAmountSlots={setBankingAmountSlots}
            borrowerHeirs={borrowerHeirs}
            onBorrowerHeirSet={setBorrowerHeir}
            onBorrowerHeirAdd={addBorrowerHeir}
            onBorrowerHeirRemove={removeBorrowerHeir}
            guarantors={guarantors}
            onGuarantorSet={setG}
            onGuarantorHeirSet={setGHeir}
            onGuarantorHeirAdd={addGHeir}
            onGuarantorHeirRemove={removeGHeir}
            onGuarantorAdd={addGuarantor}
            onGuarantorRemove={removeGuarantor}
            onBorrowerRepActivate={activateBorrowerRep}
            onBorrowerRepRemove={removeBorrowerRep}
            onGuarantorRepActivate={activateGuarantorRep}
            onGuarantorRepRemove={removeGuarantorRep}
            assets={assets}
            onEstateSet={setE}
            onEstateRemove={removeEstate}
            onOwnerToggle={toggleOwner}
            onSingleOwnerSet={setSingleOwner}
            onEstateAdd={addEstate}
            ownerOptions={ownerOptions}
          />
        )}

        {isEdit && id !== undefined && (
          <OccurrencesEditor documentId={Number(id)} initial={occurrences} />
        )}

        {!isExecuted && (
          <>
            <FormSectionTitle title="الإجراءات التي تمت بنتيجة التنفيذ الفوري" />
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
            <FormSectionTitle title="📝 الملاحظات" />
            <textarea
              value={form.notes ?? ''}
              onChange={(e) => set('notes', e.target.value)}
              rows={3}
              aria-label="الملاحظات"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
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

      {registryPicker && (
        <PublicEntityPickerModal
          onClose={() => setRegistryPicker(null)}
          onPick={applyRegistryPick}
        />
      )}
    </div>
  );
}
