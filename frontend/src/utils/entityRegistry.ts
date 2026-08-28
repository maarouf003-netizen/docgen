import type { CitationFormula, PublicEntityType } from '../types';

/** خيارات نوع الجهة (كتالوج الخمسة المعتمد) بتسمياتها العربية. */
export const ENTITY_TYPE_OPTIONS: ReadonlyArray<{ value: PublicEntityType; label: string }> = [
  { value: 'ministry', label: 'وزارة' },
  { value: 'administration', label: 'إدارة' },
  { value: 'authority', label: 'هيئة' },
  { value: 'foundation', label: 'مؤسسة' },
  { value: 'company', label: 'شركة' },
];

/** صيغ مناداة ممثل الجهة القانونية (د8). */
export const CITATION_FORMULA_OPTIONS: ReadonlyArray<{ value: CitationFormula; label: string }> = [
  { value: 'add-to-job', label: 'إضافة لوظيفته' },
  { value: 'add-to-position', label: 'إضافة لمنصبه' },
];

const ENTITY_TYPE_LABELS = new Map<string, string>(
  ENTITY_TYPE_OPTIONS.map((o) => [o.value, o.label]),
);
const CITATION_LABELS = new Map<string, string>(
  CITATION_FORMULA_OPTIONS.map((o) => [o.value, o.label]),
);

export function entityTypeLabel(value: string | null | undefined): string {
  return ENTITY_TYPE_LABELS.get(value ?? '') ?? value ?? '';
}

export function citationFormulaLabel(value: string | null | undefined): string {
  return CITATION_LABELS.get(value ?? '') ?? value ?? '';
}

export function publicEntityStatusLabel(status: string | null | undefined): string {
  switch (status) {
    case 'final':
      return 'نهائي';
    case 'pending':
      return 'بانتظار المراجعة';
    default:
      return status ?? '';
  }
}

/**
 * هل القيد «بانتظار مراجعة/اعتماد» بصريًا؟ صحيح إذا كان مخزَّنًا بـ status=pending
 * (النموذج القديم) أو بـ status=final لكن needsReview=true (النموذج الحوكمي الحالي:
 * ما أدخله المحامي يُخزَّن نهائيًا لكنه لا يظهر لبوات المندوبين قبل إقفال المراجعة —
 * انظر المواصفة §6bis). تُستخدم في نافذة الاختيار لتمييز القيد بصريًا (د4/§5.3).
 */
export function isEntryPendingReview(entry: {
  status?: string | null;
  needsReview?: boolean | null;
}): boolean {
  return entry.status === 'pending' || entry.needsReview === true;
}

/** اسم المحافظة المعروض: CoverageLabel إن وُجد، وإلا المحافظة الأصلية. */
export function formatEntityCoverage(entry: { coverageLabel?: string | null; governorate: string }): string {
  return entry.coverageLabel?.trim() || entry.governorate;
}
