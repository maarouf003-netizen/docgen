import type { DocumentResponse, HeadAlertDto, HeadAlertSummaryDto } from '../types';

/**
 * ملخّص تنبيه افتراضي مطابق لعقد الملخص (`headAlertSummary` في البيان المشترك):
 * معاينة فقط بلا نص كامل، وكل حقل قابل للتجاوز. يُستخدم في اختبارات اللوحة
 * والحساب وشريط الإنابة بدل تكرار 22 حقلًا في كل مِثال.
 */
export function makeHeadAlertSummary(overrides: Partial<HeadAlertSummaryDto> = {}): HeadAlertSummaryDto {
  return {
    id: 1,
    kind: 'manual_to_branch',
    messagePreview: 'تعميم اجتماع الفرع',
    messageLength: 20,
    previewTruncated: false,
    targetType: 'branch',
    documentId: null,
    documentTitle: null,
    targetLawyerId: null,
    targetLawyerName: null,
    isRead: null,
    recipientCount: null,
    unreadCount: null,
    createdAt: '2026-08-02T10:00:00Z',
    lastContentChangedAt: '2026-08-02T10:00:00Z',
    createdByName: 'رئيس القسم',
    appealId: null,
    reviewLetterId: null,
    delegationId: null,
    executionCircuitId: null,
    isArchived: false,
    archivedAt: null,
    ...overrides,
  };
}

/** وثيقة افتراضية صالحة لاختبارات صفحات القوائم؛ كل حقل قابل للتجاوز جزئيًا أو كليًا. */
export function makeDocument(overrides: Partial<DocumentResponse> = {}): DocumentResponse {
  return {
    id: 1,
    createdAt: '2026-07-31',
    updatedAt: '2026-07-31',
    version: 0,
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

/**
 * تفاصيل تنبيه افتراضية مطابقة لعقد التفاصيل (`headAlertDetail` — بلا عدّادي المستلمين):
 * العقد الوحيد الذي يحمل `message` كاملًا (`GET /alerts/{id}`)، وكل حقل قابل للتجاوز.
 */
export function makeHeadAlertDetail(overrides: Partial<HeadAlertDto> = {}): HeadAlertDto {
  return {
    id: 1,
    kind: 'manual_to_branch',
    message: 'نص التنبيه الكامل',
    targetType: 'branch',
    documentId: null,
    documentTitle: null,
    targetLawyerId: null,
    targetLawyerName: null,
    delegationId: null,
    appealId: null,
    reviewLetterId: null,
    publicEntityId: null,
    executionCircuitId: null,
    createdAt: '2026-08-02T10:00:00Z',
    lastContentChangedAt: '2026-08-02T10:00:00Z',
    isArchived: false,
    archivedAt: null,
    createdByName: 'رئيس القسم',
    isRead: null,
    canViewRecipients: false,
    ...overrides,
  };
}
