import type {
  ApplicantPublicEntityDto,
  AssetDto,
  DocumentResponse,
  DocumentUpsertRequest,
  ExecutedHeirDto,
  ExecutedNaturalPersonDto,
  ExecutedPublicEntityDto,
  ExecutionApplicantDto,
  GuarantorDto,
  HeirDto,
} from '../../types';

export const FILE_YEARS = ['2026', '2027', '2028', '2029', '2030'];

/** خيار قائمة منسدلة يحمل قيمة مخزنة وتسمية عرض (لتغيير التسمية دون كسر القيمة المخزنة). */
export type LabelValueOption = { value: string; label: string };

/** طبيعة طرف الملف (مقترض/كفيل/طالب تنفيذ): شخص طبيعي أو شخص اعتباري. */
export const PARTY_NATURE_OPTIONS: LabelValueOption[] = [
  { value: 'natural', label: 'شخص طبيعي' },
  { value: 'legal', label: 'شخص اعتباري' },
];

/** طبيعة المنفذ عليه في وضع «منفذ عليه»: جهة عامة أو شخص اعتباري. */
export const ENTITY_NATURE_OPTIONS: LabelValueOption[] = [
  { value: 'public', label: 'جهة عامة' },
  { value: 'legal', label: 'شخص اعتباري' },
];

/** نوع عنوان الشخص (مقترض/كفيل): «يمثله» مخزَّنة وتُعرض «وكيله القانوني». */
export const ADDRESS_TYPE_OPTIONS: LabelValueOption[] = [
  { value: 'موطن مختار', label: 'موطن مختار' },
  { value: 'عنوان', label: 'عنوان' },
  { value: 'يمثله', label: 'وكيله القانوني' },
];

/** نوع عنوان وريث «طالبة التنفيذ»: يضاف له «موطن مختار» و«وكيل» مخزَّنة وتُعرض «وكيله القانوني». */
export const HEIR_ADDRESS_TYPE_OPTIONS: LabelValueOption[] = [
  { value: 'عنوان', label: 'عنوان' },
  { value: 'موطن مختار', label: 'موطن مختار' },
  { value: 'وكيل', label: 'وكيله القانوني' },
];

/** نوع عنوان ورثة وضع «منفذ عليه» والشخص الطبيعي: بلا «موطن مختار»، «وكيل» تُعرض «وكيله القانوني». */
export const EXECUTED_HEIR_ADDRESS_TYPE_OPTIONS: LabelValueOption[] = [
  { value: 'عنوان', label: 'عنوان' },
  { value: 'وكيل', label: 'وكيله القانوني' },
];

/** نوع عنوان الممثل الشرعي: موطن مختار / عنوان / وكيل قانوني. */
export const REPRESENTATIVE_ADDRESS_TYPE_OPTIONS: LabelValueOption[] = [
  { value: 'موطن مختار', label: 'موطن مختار' },
  { value: 'عنوان', label: 'عنوان' },
  { value: 'وكيل قانوني', label: 'وكيل قانوني' },
];

export const HEIR_CAPACITIES = ['أصالة', 'إضافة لتركة', 'أصالة وإضافة'] as const;
export const REPRESENTATIVE_CAPACITIES = ['ولي', 'وصي', 'قيم'] as const;

/** الأنواع المدعومة للأموال (مطابقة AssetKindCatalog في الخلفية). */
export const ASSET_KINDS = {
  realEstate: 'عقار',
  vehicle: 'مركبة',
  shop: 'متجر',
  salaryGuarantee: 'كفالة رواتب',
  unregisteredShop: 'متجر غير مسجل',
} as const;

/** الأنواع الحاملة لمقدار الحصة (تمام/حصة سهمية). */
export const SHAREABLE_ASSET_KINDS = new Set<string>([ASSET_KINDS.realEstate, ASSET_KINDS.vehicle, ASSET_KINDS.shop]);

/** قيمة «تمام» الخاصة بنوع الأصل. */
export function fullShareLabel(kind: string | undefined): string {
  if (kind === ASSET_KINDS.vehicle) return 'تمام المركبة';
  if (kind === ASSET_KINDS.shop) return 'تمام المتجر';
  return 'تمام العقار';
}

/** خيارات مقدار الحصة حسب نوع الأصل (العقار/المركبة/المتجر فقط). */
export function shareTypesFor(kind: string | undefined): string[] {
  if (!kind || !SHAREABLE_ASSET_KINDS.has(kind)) return [];
  return [fullShareLabel(kind), 'حصة سهمية'];
}

/** سقف عدد الأصول من كل نوع على حدة. */
export const MAX_ASSETS_PER_KIND = 20;

/** سقف عدد الكفلاء. */
export const MAX_GUARANTORS = 4;

/** أنواع الأموال التي تُعرض في «منفذ جبريا» (كفالة الرواتب مستثناة). */
export function isAuctionableKind(kind: string | undefined): boolean {
  return Boolean(kind && kind !== ASSET_KINDS.salaryGuarantee);
}

export function emptyAsset(kind: string): AssetDto {
  const base: AssetDto = { assetKind: kind, owners: [] };
  if (SHAREABLE_ASSET_KINDS.has(kind)) base.shareType = fullShareLabel(kind);
  return base;
}
export const REPRESENTATION_TYPES = ['أصالة', 'إضافة لتركة', 'أصالة وإضافة'] as const;
export const EXECUTED_STATUS_OPTIONS = [
  { value: '', label: 'متداول' },
  { value: 'منفذ', label: 'منفذ' },
  { value: 'مشطوب', label: 'مشطوب' },
] as const;

export function emptyGuarantor(): GuarantorDto {
  return { guarantorNumber: 1, name: '', father: '', family: '', mother: '', birth: '', register: '', nationalId: '', address: '', addressType: 'موطن مختار', heirs: [], nature: 'natural', registrationNumber: '', representedBy: '' };
}

export function emptyHeir(): HeirDto {
  return { name: '', father: '', family: '', capacity: 'أصالة', addressType: 'عنوان', address: '' };
}

export function addressLabelOf(addressType: string | undefined): string {
  return addressType === 'يمثله' ? 'الوكيل القانوني' : 'العنوان';
}

/** تسمية حقل عنوان الوريث: «الوكيل القانوني»/«الموطن المختار»/«العنوان» حسب النوع المخزن. */
export function heirAddressLabelOf(addressType: string | undefined): string {
  if (addressType === 'وكيل') return 'الوكيل القانوني';
  if (addressType === 'موطن مختار') return 'الموطن المختار';
  return 'العنوان';
}

/** تسمية حقل عنوان الممثل الشرعي: «الوكيل القانوني»/«الموطن المختار»/«العنوان». */
export function representativeAddressLabelOf(addressType: string | undefined): string {
  if (addressType === 'وكيل قانوني') return 'الوكيل القانوني';
  if (addressType === 'موطن مختار') return 'الموطن المختار';
  return 'العنوان';
}

/** هل الممثل الشرعي حاضر (أي حقل من اسمه الثلاثي أو صفته غير فارغ)؟ */
export function hasRepresentative(rep: {
  representativeName?: string;
  representativeFather?: string;
  representativeFamily?: string;
  representativeCapacity?: string;
}): boolean {
  return Boolean(
    (rep.representativeName ?? '').trim()
    || (rep.representativeFather ?? '').trim()
    || (rep.representativeFamily ?? '').trim()
    || (rep.representativeCapacity ?? '').trim(),
  );
}

/** هل الوريث حاضر (اسمه أو أبيه أو نسبته غير فارغ)؟ */
export function hasHeirName(h: { name?: string; father?: string; family?: string }): boolean {
  return Boolean((h.name ?? '').trim() || (h.father ?? '').trim() || (h.family ?? '').trim());
}

export function emptyExecutedHeir(): ExecutedHeirDto {
  return { heirName: '', heirFather: '', heirFamily: '', addressType: 'عنوان', heirAddress: '' };
}

export function emptyExecutionApplicant(): ExecutionApplicantDto {
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
    nature: 'natural',
    registrationNumber: '',
    representedBy: '',
    addressType: '',
    address: '',
    registryId: null,
  };
}

export function emptyExecutedPublicEntity(): ExecutedPublicEntityDto {
  return { entityName: '', entityBranch: '', nature: 'public', registrationNumber: '', representedBy: '', addressType: '', address: '', registryId: null };
}

export function emptyApplicantPublicEntity(): ApplicantPublicEntityDto {
  return { name: '', branch: '', registryId: null };
}

export function emptyExecutedNaturalPerson(): ExecutedNaturalPersonDto {
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

/** مفاتيح المبالغ (حتى ثلاثة) بعملاتها في المواضع الثلاثة في النموذج. */
export const requiredAmountKeys = [
  'executedRequiredAmount',
  'executedRequiredAmount2',
  'executedRequiredAmount3',
] as const;
export const requiredCurrencyKeys = [
  'executedRequiredCurrency',
  'executedRequiredCurrency2',
  'executedRequiredCurrency3',
] as const;
/** مفاتيح المبالغ المدفوعة (حتى ثلاثة) بعملاتها في وضع «منفذ عليه»/«عرض وايداع». */
export const paidAmountKeys = [
  'executedPaidAmount',
  'executedPaidAmount2',
  'executedPaidAmount3',
] as const;
export const paidCurrencyKeys = [
  'executedPaidCurrency',
  'executedPaidCurrency2',
  'executedPaidCurrency3',
] as const;
export const bankingAmountKeys = [
  'amountNumeric',
  'amount2Numeric',
  'amount3Numeric',
] as const;
export const bankingCurrencyKeys = ['currency', 'currency2', 'currency3'] as const;
export const ordinaryAmountKeys = [
  'inclusionAmountNumeric',
  'inclusionAmount2Numeric',
  'inclusionAmount3Numeric',
] as const;
export const ordinaryCurrencyKeys = [
  'inclusionCurrency',
  'inclusionCurrency2',
  'inclusionCurrency3',
] as const;

/** تحويل مستند مُحمَّل إلى طلب تحديث يملأ النموذج، مع ترك القوائم المركّبة فارغة تنتظر تفصيلها. */
export function toUpsert(d: DocumentResponse): DocumentUpsertRequest {
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
    borrowerRepresentativeName: d.borrowerRepresentativeName ?? '',
    borrowerRepresentativeFather: d.borrowerRepresentativeFather ?? '',
    borrowerRepresentativeFamily: d.borrowerRepresentativeFamily ?? '',
    borrowerRepresentativeCapacity: d.borrowerRepresentativeCapacity ?? '',
    borrowerRepresentativeAddressType: d.borrowerRepresentativeAddressType ?? '',
    borrowerRepresentativeAddress: d.borrowerRepresentativeAddress ?? '',
    borrowerNature: d.borrowerNature ?? 'natural',
    borrowerRegistrationNumber: d.borrowerRegistrationNumber ?? '',
    borrowerRepresentedBy: d.borrowerRepresentedBy ?? '',
    contractType: d.contractType ?? '',
    contractTypeSelector: d.contractTypeSelector ?? 'مصرفي',
    contractNumber: d.contractNumber ?? '',
    contractDate: d.contractDate ?? '',
    annexType: d.annexType ?? '',
    annexNumber: d.annexNumber ?? '',
    annexDate: d.annexDate ?? '',
    inclusionText: d.inclusionText ?? '',
    amountNumeric: d.amountNumeric,
    amountWords: d.amountWords ?? '',
    currency: d.currency ?? 'ليرة سورية',
    amount2Numeric: d.amount2Numeric,
    amount2Words: d.amount2Words ?? '',
    currency2: d.currency2 ?? 'دولار أمريكي',
    amount3Numeric: d.amount3Numeric,
    amount3Words: d.amount3Words ?? '',
    currency3: d.currency3 ?? 'ليرة سورية',
    inclusionAmountNumeric: d.inclusionAmountNumeric,
    inclusionAmountWords: d.inclusionAmountWords ?? '',
    inclusionCurrency: d.inclusionCurrency ?? 'ليرة سورية',
    inclusionAmount2Numeric: d.inclusionAmount2Numeric,
    inclusionAmount2Words: d.inclusionAmount2Words ?? '',
    inclusionCurrency2: d.inclusionCurrency2 ?? 'ليرة سورية',
    inclusionAmount3Numeric: d.inclusionAmount3Numeric,
    inclusionAmount3Words: d.inclusionAmount3Words ?? '',
    inclusionCurrency3: d.inclusionCurrency3 ?? 'ليرة سورية',
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
    assets: [],
    generalEntitySide: d.generalEntitySide ?? 'applicant',
    executedStatus: d.executedStatus ?? '',
    struckOffDate: d.struckOffDate?.slice(0, 10) ?? '',
    renewalFileReceiptNumber: d.renewalFileReceiptNumber ?? '',
    renewalFileReceiptDate: d.renewalFileReceiptDate?.slice(0, 10) ?? '',
    renewalFileNumber: d.renewalFileNumber ?? '',
    renewalFileType: d.renewalFileType ?? '',
    renewalDate: d.renewalDate?.slice(0, 10) ?? '',
    executedDescription: d.executedDescription ?? '',
    fileReceiptDate: d.fileReceiptDate?.slice(0, 10) ?? '',
    fileReceiptNumber: d.fileReceiptNumber ?? '',
    executedRequiredAmount: d.executedRequiredAmount,
    executedRequiredCurrency: d.executedRequiredCurrency ?? 'ليرة سورية',
    executedRequiredAmount2: d.executedRequiredAmount2,
    executedRequiredCurrency2: d.executedRequiredCurrency2 ?? 'ليرة سورية',
    executedRequiredAmount3: d.executedRequiredAmount3,
    executedRequiredCurrency3: d.executedRequiredCurrency3 ?? 'ليرة سورية',
    executedPaidAmount: d.executedPaidAmount,
    executedPaidCurrency: d.executedPaidCurrency ?? 'ليرة سورية',
    executedPaidAmount2: d.executedPaidAmount2,
    executedPaidCurrency2: d.executedPaidCurrency2 ?? 'ليرة سورية',
    executedPaidAmount3: d.executedPaidAmount3,
    executedPaidCurrency3: d.executedPaidCurrency3 ?? 'ليرة سورية',
    executedDepositDate: d.executedDepositDate?.slice(0, 10) ?? '',
    executionApplicants: [],
    executedPublicEntities: [],
    executedNaturalPersons: [],
    applicantPublicEntities: [],
    fileArrivalNumber: d.fileArrivalNumber ?? '',
    fileArrivalDate: d.fileArrivalDate ?? '',
  };
}
