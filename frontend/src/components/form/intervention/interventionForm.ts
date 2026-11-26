import { normalizeArabicDigits } from '../../../utils/arabicDigits';
import type {
  DocumentInterventionUpsertRequest,
  HeirDto,
  InterventionNature,
  PublicEntityEntryDto,
} from '../../../types';
import { emptyIntervention, hasHeirName } from '../documentFormConstants';

/**
 * منطق نموذج المتدخل (نقي وقابل للاختبار): انتقالات الحالة، التحقق قبل الحفظ،
 * وبناء الحمولة المطبَّعة. الفصل يبقي النافذة تركيبًا وصفيًا، ويثبت العقود
 * (تصفير العنوان عند الممثل/الورثة، وتجاهل حقول الصفة غير المختارة) باختبار وحدة.
 */
export type InterventionForm = DocumentInterventionUpsertRequest;

/**
 * العنوان يُخفى ويُصفَّر إذا وُجد ممثل شرعي (عنوانه هو المعتبر) أو وُجد وريث مسمّى
 * (وفاة المتدخل: عنوان الوريث هو المعتبر).
 */
export function interventionAddressSuppressed(
  form: InterventionForm,
  showRepresentative: boolean,
): boolean {
  return showRepresentative || (form.heirs ?? []).some(hasHeirName);
}

/** تبديل الصفة يصفّر حقول الطرف ويُبقي بيانات الطلب المشتركة (كما يطبّعها الخادم). */
export function withInterventionNature(
  prev: InterventionForm,
  nature: InterventionNature,
): InterventionForm {
  return {
    ...emptyIntervention(),
    nature,
    receiptNumber: prev.receiptNumber,
    receiptDate: prev.receiptDate,
    acceptanceDate: prev.acceptanceDate,
    subject: prev.subject,
    notes: prev.notes,
  };
}

/** تفعيل الممثل يفرّغ الورثة والعنوان (الممثل والورثة لا يجتمعان — حارس خلفي كذلك). */
export function withRepresentativeAdded(prev: InterventionForm): InterventionForm {
  return {
    ...prev,
    heirs: [],
    addressType: '',
    address: '',
    representativeCapacity: prev.representativeCapacity || 'ولي',
    representativeAddressType: prev.representativeAddressType || 'عنوان',
  };
}

export function withRepresentativeRemoved(prev: InterventionForm): InterventionForm {
  return {
    ...prev,
    representativeName: '',
    representativeFather: '',
    representativeFamily: '',
    representativeCapacity: '',
    representativeAddressType: '',
    representativeAddress: '',
  };
}

export function withHeirAdded(prev: InterventionForm): InterventionForm {
  return {
    ...prev,
    heirs: [
      ...(prev.heirs ?? []),
      { name: '', father: '', family: '', capacity: 'أصالة', addressType: 'عنوان', address: '' },
    ],
  };
}

/** أول وريث مسمّى يعني وفاة المتدخل: يُصفَّر عنوانه فورًا (عنوان الوريث هو المعتبر). */
export function withHeirSet(
  prev: InterventionForm,
  index: number,
  key: keyof HeirDto,
  value: string,
): InterventionForm {
  const heirs = (prev.heirs ?? []).map((heir, i) =>
    i === index ? { ...heir, [key]: value } : heir,
  );
  return heirs.some(hasHeirName)
    ? { ...prev, heirs, addressType: '', address: '' }
    : { ...prev, heirs };
}

export function withHeirRemoved(prev: InterventionForm, index: number): InterventionForm {
  return { ...prev, heirs: (prev.heirs ?? []).filter((_, i) => i !== index) };
}

/** اختيار جهة من السجل يملأ حقول الجهة العامة ويحفظ معرّف القيد. */
export function withRegistryPick(
  prev: InterventionForm,
  entry: PublicEntityEntryDto,
): InterventionForm {
  return {
    ...prev,
    entityName: entry.canonicalName,
    entityBranch: entry.branchName,
    governorate: entry.governorate,
    registryId: entry.id,
  };
}

/**
 * تحقق ما قبل الحفظ — يعيد أول رسالة خطأ أو سلسلة فارغة.
 * نفس ترتيب ورسائل النافذة السابقة حرفيًا (عقد اختبارات الواجهة).
 */
export function validateIntervention(
  form: InterventionForm,
  showRepresentative: boolean,
): string {
  if (form.nature === 'public') {
    if (form.registryId == null) return 'اختر جهة عامة صالحة من السجل المرجعي';
  } else {
    const partyName =
      form.nature === 'natural' ? (form.name ?? '').trim() : (form.entityName ?? '').trim();
    if (!partyName) {
      return form.nature === 'natural' ? 'اسم المتدخل مطلوب' : 'اسم الشخص الاعتباري مطلوب';
    }
  }
  if (!(form.receiptDate ?? '').trim()) return 'تاريخ ورود طلب التدخل مطلوب';
  if (showRepresentative && !(form.representativeName ?? '').trim()) {
    return 'اسم الممثل الشرعي مطلوب عند إضافة ممثل';
  }
  return '';
}

/**
 * بناء الحمولة: تطبيع الأرقام العربية في التواريخ/الرقم، قصّ الفراغات،
 * وإرسال حقول الصفة المختارة فقط. `version` يُرسل في التعديل فقط
 * (الخادم يتجاهله في الإنشاء).
 */
export function buildInterventionPayload({
  form,
  version,
  showRepresentative,
  addressSuppressed,
}: {
  form: InterventionForm;
  version?: number;
  showRepresentative: boolean;
  addressSuppressed: boolean;
}): DocumentInterventionUpsertRequest {
  return {
    nature: form.nature,
    version,
    receiptNumber: normalizeArabicDigits(form.receiptNumber ?? '').trim(),
    receiptDate: normalizeArabicDigits(form.receiptDate ?? '').trim(),
    acceptanceDate: normalizeArabicDigits(form.acceptanceDate ?? '').trim(),
    subject: (form.subject ?? '').trim(),
    notes: (form.notes ?? '').trim(),
    ...(form.nature === 'natural'
      ? {
          name: (form.name ?? '').trim(),
          father: (form.father ?? '').trim(),
          family: (form.family ?? '').trim(),
          mother: (form.mother ?? '').trim(),
          birth: (form.birth ?? '').trim(),
          register: (form.register ?? '').trim(),
          nationalId: (form.nationalId ?? '').trim(),
          addressType: addressSuppressed ? '' : form.addressType,
          address: addressSuppressed ? '' : (form.address ?? '').trim(),
          representativeName: showRepresentative ? (form.representativeName ?? '').trim() : '',
          representativeFather: showRepresentative ? (form.representativeFather ?? '').trim() : '',
          representativeFamily: showRepresentative ? (form.representativeFamily ?? '').trim() : '',
          representativeCapacity: showRepresentative ? form.representativeCapacity : '',
          representativeAddressType: showRepresentative ? form.representativeAddressType : '',
          representativeAddress: showRepresentative ? (form.representativeAddress ?? '').trim() : '',
          heirs: showRepresentative ? [] : (form.heirs ?? []),
        }
      : {}),
    ...(form.nature === 'legal'
      ? {
          entityName: (form.entityName ?? '').trim(),
          registrationNumber: (form.registrationNumber ?? '').trim(),
          representedBy: (form.representedBy ?? '').trim(),
          governorate: (form.governorate ?? '').trim(),
          addressType: form.addressType,
          address: (form.address ?? '').trim(),
        }
      : {}),
    ...(form.nature === 'public'
      ? {
          entityName: (form.entityName ?? '').trim(),
          entityBranch: (form.entityBranch ?? '').trim(),
          governorate: (form.governorate ?? '').trim(),
          registryId: form.registryId,
        }
      : {}),
  };
}
