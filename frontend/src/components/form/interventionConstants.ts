import type { DocumentInterventionDto, DocumentInterventionUpsertRequest } from '../../types';
import type { LabelValueOption } from './formLabelTypes';

/** صفات المتدخل الثلاث في قائمة «صفة المتدخل» بنافذة الإضافة/التعديل. */
export const INTERVENTION_NATURE_OPTIONS: LabelValueOption[] = [
  { value: 'natural', label: 'شخص طبيعي' },
  { value: 'legal', label: 'شخص اعتباري' },
  { value: 'public', label: 'جهة عامة' },
];

/** حدود حقول المتدخل في الواجهة — مرآة `InterventionLimitsCatalog` في الخلفية. */
export const INTERVENTION_LIMITS = {
  subject: 2000,
  notes: 2000,
  name: 100,
  address: 300,
} as const;

/** الحالة الابتدائية لنافذة متدخل جديد — ومرجع «إعادة التعيين». */
export function emptyIntervention(): DocumentInterventionUpsertRequest {
  return {
    nature: 'natural',
    receiptNumber: '',
    receiptDate: '',
    acceptanceDate: '',
    subject: '',
    notes: '',
    name: '',
    father: '',
    family: '',
    mother: '',
    birth: '',
    register: '',
    nationalId: '',
    addressType: 'موطن مختار',
    address: '',
    representativeName: '',
    representativeFather: '',
    representativeFamily: '',
    representativeCapacity: '',
    representativeAddressType: '',
    representativeAddress: '',
    entityName: '',
    entityBranch: '',
    registrationNumber: '',
    representedBy: '',
    governorate: '',
    registryId: null,
    heirs: [],
  };
}

/**
 * تحويل متدخل محفوظ إلى حالة نافذة التعديل: تُنسخ الحقول المعروفة فقط
 * (بلا `id`/`natureLabel`/`displayName` التي ليست جزءًا من عقد الطلب).
 */
export function interventionToRequest(dto: DocumentInterventionDto): DocumentInterventionUpsertRequest {
  return {
    ...emptyIntervention(),
    nature: dto.nature,
    receiptNumber: dto.receiptNumber ?? '',
    receiptDate: dto.receiptDate ?? '',
    acceptanceDate: dto.acceptanceDate ?? '',
    subject: dto.subject ?? '',
    notes: dto.notes ?? '',
    name: dto.name ?? '',
    father: dto.father ?? '',
    family: dto.family ?? '',
    mother: dto.mother ?? '',
    birth: dto.birth ?? '',
    register: dto.register ?? '',
    nationalId: dto.nationalId ?? '',
    addressType: dto.addressType ?? 'موطن مختار',
    address: dto.address ?? '',
    representativeName: dto.representativeName ?? '',
    representativeFather: dto.representativeFather ?? '',
    representativeFamily: dto.representativeFamily ?? '',
    representativeCapacity: dto.representativeCapacity ?? '',
    representativeAddressType: dto.representativeAddressType ?? '',
    representativeAddress: dto.representativeAddress ?? '',
    entityName: dto.entityName ?? '',
    entityBranch: dto.entityBranch ?? '',
    registrationNumber: dto.registrationNumber ?? '',
    representedBy: dto.representedBy ?? '',
    governorate: dto.governorate ?? '',
    registryId: dto.registryId ?? null,
    heirs: (dto.heirs ?? []).map((h) => ({ ...h })),
  };
}
