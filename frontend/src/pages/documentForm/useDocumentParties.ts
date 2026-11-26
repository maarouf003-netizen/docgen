import { useCallback, useState } from 'react';
import {
  MAX_ASSETS_PER_KIND,
  SHAREABLE_ASSET_KINDS,
  emptyApplicantPublicEntity,
  emptyAsset,
  emptyExecutedHeir,
  emptyExecutedNaturalPerson,
  emptyExecutedPublicEntity,
  emptyExecutionApplicant,
  emptyGuarantor,
  emptyHeir,
  guarantorAddState,
  nextGuarantorNumber,
} from '../../components/form/documentFormConstants';
import type {
  ApplicantPublicEntityDto,
  AssetDto,
  EntityNature,
  ExecutedHeirDto,
  ExecutedNaturalPersonDto,
  ExecutedPublicEntityDto,
  ExecutionApplicantDto,
  GuarantorDto,
  HeirDto,
  PartyNature,
  PublicEntityEntryDto,
} from '../../types';
import type { NormalizedDocumentResponse } from '../../utils/apiNormalization';

/**
 * حالة الأطراف في نموذج المستند (كفلاء/ورثة/أموال/طالبو التنفيذ/الجهات العامة/
 * الأشخاص الطبيعيون) ومعالجاتها. مفصولة عن `DocumentForm` لتبقى كل وحدة تحت
 * حدّ الصيانة، ومعالجاتها منقولة حرفيًا بلا تغيير سلوكي.
 */
export function useDocumentParties(defaultGovernorate: string) {
  const freshApplicantEntity = useCallback(
    (): ApplicantPublicEntityDto => ({ ...emptyApplicantPublicEntity(), governorate: defaultGovernorate }),
    [defaultGovernorate],
  );
  const freshExecutedEntity = useCallback(
    (nature: EntityNature = 'public'): ExecutedPublicEntityDto => ({
      ...emptyExecutedPublicEntity(),
      nature,
      governorate: defaultGovernorate,
    }),
    [defaultGovernorate],
  );

  const [guarantors, setGuarantors] = useState<GuarantorDto[]>([emptyGuarantor()]);
  const [borrowerHeirs, setBorrowerHeirs] = useState<HeirDto[]>([]);
  const [assets, setAssets] = useState<AssetDto[]>([]);
  const [executionApplicants, setExecutionApplicants] = useState<ExecutionApplicantDto[]>([emptyExecutionApplicant()]);
  const [executedPublicEntities, setExecutedPublicEntities] = useState<ExecutedPublicEntityDto[]>([freshExecutedEntity()]);
  const [applicantPublicEntities, setApplicantPublicEntities] = useState<ApplicantPublicEntityDto[]>([freshApplicantEntity()]);
  const [executedNaturalPersons, setExecutedNaturalPersons] = useState<ExecutedNaturalPersonDto[]>([]);
  // الملف المناب (له إنابة مصدر SourceDelegationId): الحقول الجوهرية مقفلة ومُزامنة من
  // المنيب، والهوية والورثة/الممثلون الجدد والملاحظات تبقى قابلة للتحرير (قرار 5/7).
  const [isMirror, setIsMirror] = useState(false);
  // مفاتيح الممثلين الذين فعّلهم محامي المناب الآن (إضافة محلية) — تبقى قابلة للتحرير
  // رغم قفل الملف المناب؛ الممثل المخزَّن من المنيب يبقى مقفولًا (الحارس يرفض تعديله).
  const [draftPartyReps, setDraftPartyReps] = useState<ReadonlySet<string>>(() => new Set());

  const resetParties = useCallback(() => {
    setGuarantors([emptyGuarantor()]);
    setBorrowerHeirs([]);
    setAssets([]);
    setExecutionApplicants([emptyExecutionApplicant()]);
    setExecutedPublicEntities([freshExecutedEntity()]);
    setApplicantPublicEntities([freshApplicantEntity()]);
    setExecutedNaturalPersons([]);
    setIsMirror(false);
    setDraftPartyReps(new Set());
  }, [freshApplicantEntity, freshExecutedEntity]);

  const applyLoadedDocument = useCallback((d: NormalizedDocumentResponse) => {
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
    // طالب تنفيذ واحد وجهة عامة واحدة يظهران دائمًا (وإن لم يكن للملف أي منهما محفوظًا).
    setExecutionApplicants(d.executionApplicants.length ? d.executionApplicants : [emptyExecutionApplicant()]);
    setExecutedPublicEntities(
      d.executedPublicEntities.length ? d.executedPublicEntities : [freshExecutedEntity()],
    );
    setApplicantPublicEntities(
      d.applicantPublicEntities.length ? d.applicantPublicEntities : [freshApplicantEntity()],
    );
    setExecutedNaturalPersons(d.executedNaturalPersons);
    setIsMirror(d.sourceDelegationId != null);
  }, [freshApplicantEntity, freshExecutedEntity]);

  const setGuarantor = (i: number, key: keyof GuarantorDto, value: string) =>
    setGuarantors((gs) => gs.map((g, idx) => (idx === i ? { ...g, [key]: value } : g)));

  /** تعديل حقل أصل خام — قفل تاريخ الحجز يُركَّب في المستدعي (يحتاج الإنابات). */
  const setAssetField = (i: number, key: keyof AssetDto, value: string) =>
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
    setExecutionApplicants((xs) =>
      xs.map((x, idx) => (idx === i ? { ...x, [key]: value } : x)),
    );

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

  // حقول الهوية النصية للجهة العامة المنفذ عليها: أي تحرير يدوي يطالها يفكّ ربط السجل المرجعي
  // تلقائيًا حتى لا يبقى الملف مربوطًا بقيد لم يعد يطابق نصَّه (حقول «الطالب» أصبحت مقفلة فلا تحتاج حارسًا).
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

  const addApplicantPublicEntity = () => setApplicantPublicEntities((xs) => [...xs, freshApplicantEntity()]);

  const removeApplicantPublicEntity = (i: number) =>
    setApplicantPublicEntities((xs) => xs.filter((_, idx) => idx !== i));

  // «فك الربط»: يُفرّغ صف الجهة بالكامل (هوية + رابط) لأن الحقول النصية مقفلة فلا يمكن
  // ترك نص بلا رابط — الصف المفرَّغ يُتجاهل تلقائيًا عند البناء الذي يفلتر الأسماء الفارغة.
  const unlinkApplicantPublicEntity = (i: number) =>
    setApplicantPublicEntities((xs) =>
      xs.map((x, idx) =>
        idx === i ? { ...x, name: '', branch: '', governorate: defaultGovernorate, registryId: null } : x,
      ),
    );

  const unlinkExecutedEntity = (i: number) =>
    setExecutedPublicEntities((xs) =>
      xs.map((x, idx) =>
        idx === i
          ? { ...x, entityName: '', entityBranch: '', governorate: defaultGovernorate, registryId: null }
          : x,
      ),
    );

  // ربط صف جهة بالقيد المختار من نافذة السجل: تُملأ حقول الهوية النصية من القيد
  // المعتمد نفسه فتظل الأعمدة النصية متسقة مع السجل، ويُحفظ معرّف الربط.
  const linkApplicantEntity = (index: number, entry: PublicEntityEntryDto) =>
    setApplicantPublicEntities((xs) =>
      xs.map((x, idx) =>
        idx === index
          ? { ...x, name: entry.canonicalName, branch: entry.branchName, governorate: entry.governorate, registryId: entry.id }
          : x,
      ),
    );

  const linkExecutedEntity = (index: number, entry: PublicEntityEntryDto) =>
    setExecutedPublicEntities((xs) =>
      xs.map((x, idx) =>
        idx === index
          ? { ...x, entityName: entry.canonicalName, entityBranch: entry.branchName, governorate: entry.governorate, registryId: entry.id }
          : x,
      ),
    );

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

  const markPartyRepDraft = (key: string) =>
    setDraftPartyReps((prev) => {
      if (prev.has(key)) return prev;
      const next = new Set(prev);
      next.add(key);
      return next;
    });

  const clearPartyRepDraft = (key: string) =>
    setDraftPartyReps((prev) => {
      if (!prev.has(key)) return prev;
      const next = new Set(prev);
      next.delete(key);
      return next;
    });

  /** قفل ممثل الطرف على الملف المناب: المخزَّن فقط، لا الإضافة المحلية الجارية. */
  const partyRepLocked = (key: string) => isMirror && !draftPartyReps.has(key);

  const activateGuarantorRep = (i: number) => {
    markPartyRepDraft(`guarantor-${i}`);
    setGuarantors((gs) =>
      gs.map((g, idx) =>
        idx === i ? { ...g, representativeCapacity: 'ولي', representativeAddressType: 'عنوان' } : g,
      ),
    );
  };

  const removeGuarantorRep = (i: number) => {
    clearPartyRepDraft(`guarantor-${i}`);
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
  };

  const activateApplicantRep = (i: number) => {
    markPartyRepDraft(`applicant-${i}`);
    setExecutionApplicants((xs) =>
      xs.map((x, idx) => (idx === i ? { ...x, representativeCapacity: 'ولي' } : x)),
    );
  };

  const removeApplicantRep = (i: number) => {
    clearPartyRepDraft(`applicant-${i}`);
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
  };

  const activatePersonRep = (i: number) => {
    markPartyRepDraft(`person-${i}`);
    setExecutedNaturalPersons((xs) =>
      xs.map((x, idx) =>
        idx === i ? { ...x, representativeCapacity: 'ولي', representativeAddressType: 'عنوان' } : x,
      ),
    );
  };

  const removePersonRep = (i: number) => {
    clearPartyRepDraft(`person-${i}`);
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
  };

  const addGuarantor = (isOrdinary: boolean, nature: PartyNature = 'natural') => {
    // العدد والرقم معًا ضمن النطاق (العادي موضعي، والمصرفي قد تُستنفَد أرقامه
    // بالحذف الأوسط قبل الصفوف — ورقم بلا خانة يرفضه الخلفي لاحقًا).
    if (!guarantorAddState(guarantors, isOrdinary).canAdd) return;
    // رقم الكفيل هوية مستقرة تُحفظ في الصف (قرار 10/F1): الجديد = max + 1، والحذف
    // الأوسط يترك فجوة دون إزاحة — لا ترقيم موضعي عند الحفظ ولا إعادة أرقام.
    const nextNumber = nextGuarantorNumber(guarantors);
    setGuarantors((gs) => [...gs, { ...emptyGuarantor(), nature, guarantorNumber: nextNumber }]);
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

  return {
    guarantors,
    borrowerHeirs,
    assets,
    executionApplicants,
    executedPublicEntities,
    applicantPublicEntities,
    executedNaturalPersons,
    isMirror,
    resetParties,
    applyLoadedDocument,
    setGuarantor,
    setAssetField,
    toggleOwner,
    setSingleOwner,
    setBorrowerHeir,
    addBorrowerHeir,
    removeBorrowerHeir,
    setApplicant,
    addApplicant,
    removeApplicant,
    setApplicantHeir,
    addApplicantHeir,
    removeApplicantHeir,
    setExecutedEntity,
    addExecutedEntity,
    removeExecutedEntity,
    addApplicantPublicEntity,
    removeApplicantPublicEntity,
    unlinkApplicantPublicEntity,
    unlinkExecutedEntity,
    linkApplicantEntity,
    linkExecutedEntity,
    setExecutedPerson,
    addExecutedPerson,
    removeExecutedPerson,
    setExecutedPersonHeir,
    addExecutedPersonHeir,
    removeExecutedPersonHeir,
    setGHeir,
    addGHeir,
    removeGHeir,
    markPartyRepDraft,
    clearPartyRepDraft,
    activateGuarantorRep,
    removeGuarantorRep,
    activateApplicantRep,
    removeApplicantRep,
    activatePersonRep,
    removePersonRep,
    addGuarantor,
    removeGuarantor,
    addEstate,
    removeEstate,
    partyRepLocked,
  };
}
