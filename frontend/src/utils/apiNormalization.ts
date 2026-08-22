import type {
  ApplicantPublicEntityDto,
  DocumentOccurrenceDto,
  DocumentResponse,
  ExecutedNaturalPersonDto,
  ExecutedPublicEntityDto,
  ExecutionApplicantDto,
  GuarantorDto,
  HeirDto,
  AssetDto,
} from '../types';

const asArray = <T,>(value: T[] | undefined | null): T[] => (Array.isArray(value) ? value : []);

/**
 * استجابة الملف بعد التطبيع: كل القوائم المتعاقد عليها مضمونة كمصفوفات فعلية.
 */
export type NormalizedDocumentResponse = DocumentResponse & {
  guarantors: GuarantorDto[];
  assets: AssetDto[];
  borrowerHeirs: HeirDto[];
  executionApplicants: ExecutionApplicantDto[];
  executedPublicEntities: ExecutedPublicEntityDto[];
  applicantPublicEntities: ApplicantPublicEntityDto[];
  executedNaturalPersons: ExecutedNaturalPersonDto[];
  occurrences: DocumentOccurrenceDto[];
};

/**
 * تطبيع استجابة الخادم عند حد الثقة الوحيد (الشبكة): تُضمن كل القوائم المتعاقد عليها
 * كمصفوفات فعلية حتى لو أتت استجابة شاذة ناقصة، فتُقرأ بعدها بأمان في كل الصفحات
 * دون حمايات مبعثرة. أي حقل قائمة جديد يضاف هنا مرة واحدة فقط.
 */
export function normalizeDocumentResponse(d: DocumentResponse): NormalizedDocumentResponse {
  return {
    ...d,
    guarantors: asArray(d.guarantors),
    assets: asArray(d.assets),
    borrowerHeirs: asArray(d.borrowerHeirs),
    executionApplicants: asArray(d.executionApplicants),
    executedPublicEntities: asArray(d.executedPublicEntities),
    applicantPublicEntities: asArray(d.applicantPublicEntities),
    executedNaturalPersons: asArray(d.executedNaturalPersons),
    occurrences: asArray(d.occurrences),
  };
}
