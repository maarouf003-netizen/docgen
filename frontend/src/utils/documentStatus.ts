import type { DocumentResponse } from '../types';
import { isExecutedLike } from './documentDisplay';

/** قيم حقول حالة التنفيذ (execStatus/execSubStatus) الصادرة من الخلفية — الأصل الوحيد للحرفيات في الواجهة. */
export const EXEC_STATUS_FORCIBLY = 'منفذ جبريا';
export const EXEC_STATUS_SETTLED = 'منفذ بالتسوية';
export const EXEC_STATUS_DEFERRED = 'تريث';
export const EXEC_STATUS_STRUCK_OFF = 'مشطوب';
/** حالة الملف المناب عند إتمام إنابته: حالة نهائية تُعامل منفذًا في القوائم والإحصاءات. */
export const EXEC_STATUS_DELEGATION_EXECUTED = 'منفذ إنابة';
export const SUB_STATUS_PARTIAL = 'منفذ جزئيا';

export type DocumentStatus = 'منفذ' | 'تريث' | 'تحت رفع' | 'متداول' | 'متداول / منفذ جزئيا' | 'مشطوب';

// الملفات «المنفذة» لها صفحتها الخاصة («الملفات المنفذة»)، فتُستبعد من فلتر الحالة في
// القائمة الرئيسية — ولا تظهر فيها إلا عند البحث النصي. بقي الخيار «متداول» للعمل الحالي
// و«تريث» و«تحت رفع»، أما «منفذ» فيُدار من صفحة الملفات المنفذة.
export const STATUS_OPTIONS: Exclude<DocumentStatus, 'متداول / منفذ جزئيا' | 'مشطوب' | 'منفذ'>[] = [EXEC_STATUS_DEFERRED, 'تحت رفع', 'متداول'];

export const STATUS_BADGES: Record<DocumentStatus, { text: string; cls: string }> = {
  منفذ: { text: 'منفذ', cls: 'bg-green-100 text-green-700' },
  تريث: { text: EXEC_STATUS_DEFERRED, cls: 'bg-red-100 text-red-700' },
  'تحت رفع': { text: 'تحت رفع', cls: 'bg-amber-100 text-amber-700' },
  متداول: { text: 'متداول', cls: 'bg-blue-100 text-blue-700' },
  'متداول / منفذ جزئيا': { text: 'متداول / منفذ جزئيا', cls: 'bg-cyan-100 text-cyan-700' },
  مشطوب: { text: EXEC_STATUS_STRUCK_OFF, cls: 'bg-gray-200 text-gray-700' },
};

export type StatusSource = Pick<
  DocumentResponse,
  'execStatus' | 'execSubStatus' | 'isDraft' | 'generalEntitySide' | 'executedStatus'
>;

/** حالة وضع «منفذ عليه»/«عرض وايداع» (متداول/منفذ/مشطوب)، معزولة تمامًا عن نظام «طالبة تنفيذ». */
export function getExecutedStatus(doc: StatusSource): DocumentStatus {
  if (doc.executedStatus === EXEC_STATUS_STRUCK_OFF) return 'مشطوب';
  if (doc.executedStatus === 'منفذ') return 'منفذ';
  return 'متداول';
}

export function getDocumentStatus(doc: StatusSource): DocumentStatus {
  // المصدر الوحيد للقواعد هو الخلفية (DocumentStatusResolver) عبر displayStatus.
  // المسار الاحتياطي أدناه للـmocks/البيانات العتيقة فقط — يُحذف مع أول تنظيف لاحق.
  const server = (doc as { displayStatus?: string }).displayStatus;
  if (server) return server as DocumentStatus;

  if (isExecutedLike(doc.generalEntitySide)) return getExecutedStatus(doc);
  // «مشطوب» في نظام «طالبة تنفيذ» موحّد مع صفحة «الملفات المشطوبة».
  if (doc.execStatus === EXEC_STATUS_STRUCK_OFF) return 'مشطوب';
  if (doc.execStatus === EXEC_STATUS_DEFERRED) return 'تريث';
  if (doc.execStatus === EXEC_STATUS_FORCIBLY && doc.execSubStatus === SUB_STATUS_PARTIAL) return 'متداول / منفذ جزئيا';
  if (doc.execStatus === EXEC_STATUS_FORCIBLY || doc.execStatus === EXEC_STATUS_SETTLED) return 'منفذ';
  // «منفذ إنابة» (الملف المناب عند إتمام الإنابة): حالة نهائية تُعامل منفذًا في القوائم
  // والإحصاءات كباقي المنفذين — تظهر في صفحة «الملفات المنفذة» بشارة «منفذ».
  if (doc.execStatus === EXEC_STATUS_DELEGATION_EXECUTED) return 'منفذ';
  return doc.isDraft ? 'تحت رفع' : 'متداول';
}

export function getDocumentBadge(doc: StatusSource) {
  return STATUS_BADGES[getDocumentStatus(doc)];
}