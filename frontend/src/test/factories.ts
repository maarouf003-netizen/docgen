import type { DocumentResponse } from '../types';

/** وثيقة افتراضية صالحة لاختبارات صفحات القوائم؛ كل حقل قابل للتجاوز جزئيًا أو كليًا. */
export function makeDocument(overrides: Partial<DocumentResponse> = {}): DocumentResponse {
  return {
    id: 1,
    createdAt: '2026-07-31',
    updatedAt: '2026-07-31',
    documentType: 'متداول - مقترض',
    isDraft: false,
    amountNumeric: 0,
    amount2Numeric: 0,
    amount3Numeric: 0,
    inclusionAmountNumeric: 0,
    inclusionAmount2Numeric: 0,
    inclusionAmount3Numeric: 0,
    viewCount: 0,
    printCount: 0,
    borrowerName: 'أحمد',
    borrowerFather: 'خالد',
    borrowerFamily: 'الخطيب',
    applicant: 'المدعي',
    court: 'دمشق',
    fileNumber: '99',
    fileType: 'حقوق',
    fileYear: '2026',
    guarantors: [],
    assets: [],
    executionActions: [],
    executionApplicants: [],
    executedPublicEntities: [],
    executedNaturalPersons: [],
    ...overrides,
  };
}

/** وثيقة محذوفة (تاريخ حذف افتراضي) لاختبارات صفحة الملفات المحذوفة. */
export function makeDeletedDocument(overrides: Partial<DocumentResponse> = {}): DocumentResponse {
  return makeDocument({ deletedAt: '2026-08-04T10:00:00', ...overrides });
}

/** وثيقة مشطوبة (صفة منفذ عليها + منفذ طبيعي افتراضي) لاختبارات صفحة الملفات المشطوبة. */
export function makeStruckOffDocument(overrides: Partial<DocumentResponse> = {}): DocumentResponse {
  return makeDocument({
    generalEntitySide: 'executed',
    executedStatus: 'مشطوب',
    struckOffDate: '2026-08-04T10:00:00',
    executedNaturalPersons: [{ name: 'محمود', father: 'علي', family: 'حسن' }],
    ...overrides,
  });
}

/** وثيقة منفذة (صفة منفذ عليها بحالة «منفذ» + منفذ طبيعي افتراضي) لاختبارات صفحة الملفات المنفذة. */
export function makeExecutedDocument(overrides: Partial<DocumentResponse> = {}): DocumentResponse {
  return makeDocument({
    generalEntitySide: 'executed',
    executedStatus: 'منفذ',
    executedExecutionDate: '2026-08-04',
    executedNaturalPersons: [{ name: 'محمود', father: 'علي', family: 'حسن' }],
    ...overrides,
  });
}
