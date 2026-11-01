import type { HeadAlertSummaryDto } from '../../types';

/**
 * كتالوج أنواع التنبيهات الدلالية في الواجهة — مرآة `HeadAlertKindCatalog`
 * الخلفي (`backend/src/DocGenerator.Domain/Enums/HeadAlertKindCatalog.cs`).
 *
 * **لماذا كتالوج صريح؟** التسمية والتوجيه كانا يُستنتجان من الحقول أو من نص
 * الرسالة، فينكسر العرض عند بتر المعاينة أو تغيّر الصياغة. الرمز (`kind`) هو
 * العقد الدلالي الوحيد المستقر، فتُقرأ منه التسمية والوجهة.
 *
 * **الرمز المجهول لا يكسر العرض:** أي رمز غير مدرج (نحو رمز أُضيف خلفيًا ولم
 * يُزامَن بعد) يعود إلى تعريف احتياطي بتسمية عامة وبلا وجهة مباشرة — ثم تليه
 * سلسلة احتياط المعرّفات في `alertRoute` (كتاب مطالعة ← استئناف ← ملف).
 *
 * **حراسة التزامن:** `scripts/subhead-guards.mjs` يقارن مفاتيح هذا الكتالوج
 * بثوابت `HeadAlertKindCatalog` الخلفي ويفشل عند أي فرق، فلا تنحرف القائمتان.
 */

/** الحقول اللازمة لحساب وجهة الصف (لا نحتاج العقد كاملًا). */
export type AlertKindSource = Pick<
  HeadAlertSummaryDto,
  'documentId' | 'appealId' | 'reviewLetterId'
>;

export interface AlertKindDefinition {
  /** التسمية العربية المعروضة — مطابقة لـ`HeadAlertKindCatalog.ToLabel` الخلفي. */
  readonly label: string;
  /**
   * وجهة الكيان المرتبط بهذا النوع، أو `null` حين لا وجهة مباشرة للنوع
   * (تنبيهات الإنابة/الجهات/التعميمات نصية، والوجهة — إن وُجدت — تأتي من
   * معرّفات الصف عبر سلسلة الاحتياط).
   */
  readonly route: ((alert: AlertKindSource) => string | null) | null;
}

const toReviewLetter = (alert: AlertKindSource): string | null =>
  alert.reviewLetterId != null ? `/reviews/${alert.reviewLetterId}` : null;

const toAppeal = (alert: AlertKindSource): string | null =>
  alert.appealId != null ? `/appeals/${alert.appealId}` : null;

const toDocument = (alert: AlertKindSource): string | null =>
  alert.documentId != null ? `/documents/${alert.documentId}` : null;

/** ملاحظة إعادة القيد: تفتح صفحة المعلقات كاملة مجموعةً بالدوائر (لا ملفًا بعينه). */
const toPendingDocuments = (): string => '/pending-registrations';

/** تعريف نوع نصي بتسمية صريحة (بلا وجهة مباشرة). */
const text = (label: string): AlertKindDefinition => ({ label, route: null });

/**
 * الكتالوج: 39 رمزًا نظاميًا/يدويًا + الرمز المحجوز `legacy`.
 * الترتيب مطابق لتجميعات المصدر الخلفي (إنابة ← دوائر ← استئناف ← مطالعة ←
 * جهات ← يدوي ← تعميمات ← محجوز).
 */
export const ALERT_KINDS: Readonly<Record<string, AlertKindDefinition>> = {
  // ── أ. الإنابة (13) ──
  delegation_pending_approval: text('بانتظار اعتماد الإنابة'),
  delegation_sent_to_subhead: text('إنابة من اختصاص الشعبة'),
  delegation_assigned_to_lawyer: text('إسناد ملف إنابة'),
  delegation_rejected_wrong_court: text('رفض إنابة للدائرة الخطأ'),
  delegation_pending_completion: text('بانتظار إتمام الإنابة'),
  delegation_completed_returned: text('إعادة الإنابة المسطرة'),
  delegation_mirror_deferred: text('كتاب تريث (تأجيل)'),
  delegation_mirror_returned: text('عودة السير'),
  delegation_mirror_settled: text('تسوية المديونية'),
  delegation_mirror_fully_forcibly: text('استرداد جبري'),
  delegation_mirror_updated: text('تحديث نسخة المناب'),
  delegation_mirror_local_addition: text('إضافة محلية على المناب'),
  delegation_mirror_status_changed: text('تغيّر حالة المنيب العام'),

  // ── ب. دوائر التنفيذ (2) ──
  circuit_pending_documents: { label: 'ملفات دائرة بانتظار التحديث', route: toPendingDocuments },
  circuit_transferred: text('نقل دائرة'),

  // ── ج. الاستئناف (5) ──
  appeal_referral_to_head: { label: 'إحالة استئناف للإسناد', route: toAppeal },
  appeal_returned_to_head: { label: 'إعادة استئناف للنطاق', route: toAppeal },
  appeal_pending_to_owner: { label: 'استئناف بانتظار اختيار محامٍ', route: toAppeal },
  appeal_assigned_to_lawyer: { label: 'إسناد استئناف', route: toAppeal },
  appeal_final_to_base_lawyer: { label: 'نتيجة الاستئناف', route: toAppeal },

  // ── د. كتب المطالعة (1) ──
  review_letter_reply: { label: 'رد على كتاب المطالعة', route: toReviewLetter },

  // ── هـ. الجهات العامة (6) ──
  entity_rename_notice_to_creator: text('تغيير اسم الجهة (إشعار المنشئ)'),
  entity_branch_changed: text('تغيير فرع الجهة'),
  entity_entry_moved: text('نقل قيد'),
  entity_entries_moved_all: text('نقل جميع قيود'),
  entity_registry_entry_review: text('مراجعة قيد (متقاعد)'),
  entity_edit_proposal_review: text('اقتراح تعديل (متقاعد)'),

  // ── و. الإصدار اليدوي (4) ──
  manual_to_document: { label: 'تنبيه على ملف', route: toDocument },
  manual_to_lawyer: text('رسالة خاصة'),
  manual_to_branch: text('تعميم للفرع'),
  manual_to_head: text('توجيه لرئيس (محجوز)'),

  // ── ز. التعميمات الثمانية (8) ──
  entity_rename_lawyers: text('تعميم: إعادة تسمية جهة'),
  entity_rename_heads: text('تعميم رؤساء: إعادة تسمية جهة'),
  entity_merge_lawyers: text('تعميم: دمج جهات'),
  entity_merge_heads: text('تعميم رؤساء: دمج جهات'),
  entity_unify_lawyers: text('تعميم: توحيد تسمية جهة'),
  entity_unify_heads: text('تعميم رؤساء: توحيد تسمية جهة'),
  entity_abolish_lawyers: text('تعميم: حل جهة محل أخرى'),
  entity_abolish_heads: text('تعميم رؤساء: حل جهة محل أخرى'),

  // ── ح. الرمز المحجوز للصفوف التاريخية (لا يُكتب في مسار جديد) ──
  legacy: text('تنبيه سابق'),
};

/** التعريف الاحتياطي للرمز المجهول: تسمية عامة صادقة، بلا وجهة مباشرة. */
export const UNKNOWN_ALERT_KIND: AlertKindDefinition = { label: 'تنبيه', route: null };

/** هل الرمز مدرج في الكتالوج؟ (لا يُفترض شيء عن رموز لم تُزامَن بعد) */
export function isKnownAlertKind(kind: string | null | undefined): kind is string {
  return typeof kind === 'string' && Object.prototype.hasOwnProperty.call(ALERT_KINDS, kind);
}

/** تعريف الرمز أو التعريف الاحتياطي — لا يرمي أبدًا. */
export function resolveAlertKind(kind: string | null | undefined): AlertKindDefinition {
  return isKnownAlertKind(kind) ? ALERT_KINDS[kind] : UNKNOWN_ALERT_KIND;
}

/** التسمية العربية للعرض (احتياط عام للرمز المجهول). */
export function alertKindLabel(kind: string | null | undefined): string {
  return resolveAlertKind(kind).label;
}

/**
 * وجهة فتح التنبيه:
 * 1. وجهة الكتالوج إن وُجدت للنوع،
 * 2. ثم سلسلة الاحتياط بالمعرّفات (كتاب مطالعة ← استئناف ← ملف) كما كان السلوك
 *    القائم — فلا يُفقد رابط صف حقيقي حين يكون نوعه نصيًا.
 * `null` = لا وجهة: يُعرض النص غير قابل للنقر.
 */
export function alertRoute(alert: HeadAlertSummaryDto): string | null {
  const direct = resolveAlertKind(alert.kind).route?.(alert) ?? null;
  if (direct) return direct;
  return toReviewLetter(alert) ?? toAppeal(alert) ?? toDocument(alert);
}
