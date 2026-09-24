import type { DocumentResponse } from '../types';
import { isExecutedLike } from './documentDisplay';

/** قيم حقول حالة التنفيذ (execStatus/execSubStatus) الصادرة من الخلفية — الأصل الوحيد للحرفيات في الواجهة. */
export const EXEC_STATUS_FORCIBLY = 'منفذ جبريا';
export const EXEC_STATUS_SETTLED = 'منفذ بالتسوية';
export const EXEC_STATUS_DEFERRED = 'تريث';
export const EXEC_STATUS_STRUCK_OFF = 'مشطوب';
/** حالة الملف المناب عند إتمام إنابته: حالة نهائية تُعامل منفذًا في القوائم والإحصاءات. */
export const EXEC_STATUS_DELEGATION_EXECUTED = 'منفذ إنابة';
/** حالة «مسترد» للملف المناب عند إعادة الملف إلى الدائرة المنيبة: حالة نهائية تُعامل منفذًا. */
export const EXEC_STATUS_RECOVERED = 'مسترد';
/** حالة «محال الى البداية»: أُحيل الملف لقسم البداية لعدم وجود أموال للتنفيذ عليها — حالة محايدة
 * تُظهر عدّادًا وفلترًا مستقلين، ولا تُسجَّل عليها إنابة سارية، وتخرج عبر نقطة العودة المخصصة. */
export const EXEC_STATUS_REFERRED_TO_START = 'محال الى البداية';
export const SUB_STATUS_PARTIAL = 'منفذ جزئيا';
/** قيمة «منفذ كاملا» للحقل الفرعي (الافتراضي في نماذج «منفذ جبريا»). */
export const SUB_STATUS_FULL = 'منفذ كاملا';
/** حالة «منفذ» في عائلة «منفذ عليه/عرض وايداع» (تُخزَّن كقيمة في `executedStatus`). */
export const EXECUTED_STATUS_EXECUTED = 'منفذ';
/** حالات العرض الأولية لنظام «طالبة تنفيذ». */
export const STATE_DRAFT = 'تحت رفع';
export const STATE_CIRCULATING = 'متداول';
/** إجراءات الانتقال الخاصة بنافذة «تغيير الحالة» (ليست حالات مخزَّنة بذاتها). */
export const STATUS_ACTION_REVERT = 'تراجع';
export const STATUS_ACTION_COMPLETE_SALE = 'منفذ كاملا بهذا البيع';
export const STATUS_ACTION_RETURN_CIRCULATING = 'العودة إلى المتداول';
export const STATUS_ACTION_RETURN_PARTIAL = 'العودة إلى منفذ جزئيا';

export type DocumentStatus = 'منفذ' | 'تريث' | 'تحت رفع' | 'متداول' | 'متداول / منفذ جزئيا' | 'مسترد' | 'مشطوب' | 'محال الى البداية';

// الملفات «المنفذة» و«المحالة الى البداية» و«المشطوبة» لها صفحاتها الخاصة
// («الملفات المنفذة»/«الملفات المحالة الى البداية»/«الملفات المشطوبة»)، فتُستبعد من فلتر
// الحالة في القائمة الرئيسية — ولا تظهر فيها إلا عند البحث النصي. بقي الخيار «متداول» للعمل
// الحالي و«تريث» و«تحت رفع»، أما «منفذ» و«محال الى البداية» فيُداران من صفحتيهما.
export const STATUS_OPTIONS: Exclude<DocumentStatus, 'متداول / منفذ جزئيا' | 'مشطوب' | 'منفذ' | 'محال الى البداية'>[] = [EXEC_STATUS_DEFERRED, STATE_DRAFT, STATE_CIRCULATING];

/** هل قيمة الحالة خيارٌ معتمد في فلتر «الحالة» الخاص بالقائمة الرئيسية؟ — لتعقيم القيمة
 * المستعادة من الجلسة عند إزالة خيار سابق («محال الى البداية» بعد نقله لصفحته)، فيُمنع سقوط
 * المستخدم في فرع فلتر خاطئ (أي قيمة خارج خيارات الفلتر تُعامل كأنها «لا فلتر»). */
export function isStatusOption(value: string): boolean {
  return (STATUS_OPTIONS as readonly string[]).includes(value);
}

export const STATUS_BADGES: Record<DocumentStatus, { text: string; cls: string }> = {
  منفذ: { text: EXECUTED_STATUS_EXECUTED, cls: 'bg-green-100 text-green-700' },
  تريث: { text: EXEC_STATUS_DEFERRED, cls: 'bg-red-100 text-red-700' },
  'تحت رفع': { text: STATE_DRAFT, cls: 'bg-amber-100 text-amber-700' },
  متداول: { text: STATE_CIRCULATING, cls: 'bg-blue-100 text-blue-700' },
  'متداول / منفذ جزئيا': { text: 'متداول / منفذ جزئيا', cls: 'bg-cyan-100 text-cyan-700' },
  مسترد: { text: EXEC_STATUS_RECOVERED, cls: 'bg-fuchsia-100 text-fuchsia-800' },
  مشطوب: { text: EXEC_STATUS_STRUCK_OFF, cls: 'bg-gray-200 text-gray-700' },
  'محال الى البداية': { text: EXEC_STATUS_REFERRED_TO_START, cls: 'bg-purple-100 text-purple-700' },
};

export type StatusSource = Pick<
  DocumentResponse,
  'execStatus' | 'execSubStatus' | 'isDraft' | 'generalEntitySide' | 'executedStatus'
>;

/** هل القيمة الفرعية تعني «منفذ جزئيا»؟ — الأصل الوحيد لمقارنة `execSubStatus` الجزئية. */
export function isPartialExecSubStatus(value: string | null | undefined): boolean {
  return value === SUB_STATUS_PARTIAL;
}

/** هل الملف هدف متداول (بلا `execStatus` مخزَّن)؟ — الأصل الوحيد لشرط `(null || '')` في العرض. */
export function isCirculatingTarget(doc: Pick<StatusSource, 'execStatus'>): boolean {
  return doc.execStatus == null || doc.execStatus === '';
}

/** حالة وضع «منفذ عليه»/«عرض وايداع» (متداول/منفذ/مشطوب)، معزولة تمامًا عن نظام «طالبة تنفيذ». */
export function getExecutedStatus(doc: StatusSource): DocumentStatus {
  if (doc.executedStatus === EXEC_STATUS_STRUCK_OFF) return EXEC_STATUS_STRUCK_OFF;
  if (doc.executedStatus === EXECUTED_STATUS_EXECUTED) return EXECUTED_STATUS_EXECUTED;
  return STATE_CIRCULATING;
}

/** تسمية حالة وضع «الجهة العامة منفذ عليها» الحالية (الفارغ «متداول» لا يُخزَّن كقيمة). */
export function currentExecutedLabelOf(doc: Pick<StatusSource, 'executedStatus'>): string {
  if (doc.executedStatus === EXECUTED_STATUS_EXECUTED) return EXECUTED_STATUS_EXECUTED;
  if (doc.executedStatus === EXEC_STATUS_STRUCK_OFF) return EXEC_STATUS_STRUCK_OFF;
  return STATE_CIRCULATING;
}

/** قيمة الحالة المُرسَلة: «متداول» سلسلة فارغة لأنها لا تُخزَّن كقيمة في الخلفية. */
export function executedStatusValue(target: string): string {
  return target === STATE_CIRCULATING ? '' : target;
}

export function getDocumentStatus(doc: StatusSource): DocumentStatus {
  // المصدر الوحيد للقواعد هو الخلفية (DocumentStatusResolver) عبر displayStatus.
  // المسار الاحتياطي أدناه للـmocks/البيانات العتيقة فقط — يُحذف مع أول تنظيف لاحق.
  const server = (doc as { displayStatus?: string }).displayStatus;
  if (server) return server as DocumentStatus;

  if (isExecutedLike(doc.generalEntitySide)) return getExecutedStatus(doc);
  // «مشطوب» في نظام «طالبة تنفيذ» موحّد مع صفحة «الملفات المشطوبة».
  if (doc.execStatus === EXEC_STATUS_STRUCK_OFF) return EXEC_STATUS_STRUCK_OFF;
  if (doc.execStatus === EXEC_STATUS_RECOVERED) return EXEC_STATUS_RECOVERED;
  if (doc.execStatus === EXEC_STATUS_DEFERRED) return EXEC_STATUS_DEFERRED;
  if (doc.execStatus === EXEC_STATUS_REFERRED_TO_START) return EXEC_STATUS_REFERRED_TO_START;
  if (doc.execStatus === EXEC_STATUS_FORCIBLY && doc.execSubStatus === SUB_STATUS_PARTIAL) return 'متداول / منفذ جزئيا';
  if (doc.execStatus === EXEC_STATUS_FORCIBLY || doc.execStatus === EXEC_STATUS_SETTLED)
    return EXECUTED_STATUS_EXECUTED;
  // «منفذ إنابة» (الملف المناب عند إتمام الإنابة): حالة نهائية تُعامل منفذًا في القوائم
  // والإحصاءات كباقي المنفذين — تظهر في صفحة «الملفات المنفذة» بشارة «منفذ».
  if (doc.execStatus === EXEC_STATUS_DELEGATION_EXECUTED) return EXECUTED_STATUS_EXECUTED;
  return doc.isDraft ? STATE_DRAFT : STATE_CIRCULATING;
}

export function getDocumentBadge(doc: StatusSource) {
  return STATUS_BADGES[getDocumentStatus(doc)];
}

/** هل يصح تسطير إنابة على هذا الملف؟ — مطابقة `ValidateSourceForDelegation` في الخلفية:
 * ليس تحت رفع، صفة «طالبة تنفيذ»، غير منفذ (منفذ جبريا كاملًا/بالتسوية/إنابة) وغير مشطوب
 * وغير متريث («لا يمكن تسطير انابة في ملف تريث» E4) وغير مسترد (حالة نهائية بلا تسطير) وغير
 * «محال الى البداية» («لا يمكن تسطير انابة في ملف «محال الى البداية»»).
 * «منفذ جبريا (منفذ جزئيا)» يبقى قابلًا للتسطير (قرار 9). */
export function canDelegateSource(doc: StatusSource): boolean {
  if (doc.isDraft) return false;
  if (isExecutedLike(doc.generalEntitySide)) return false;
  if (doc.execStatus === EXEC_STATUS_STRUCK_OFF) return false;
  if (doc.execStatus === EXEC_STATUS_DEFERRED) return false;
  if (doc.execStatus === EXEC_STATUS_REFERRED_TO_START) return false;
  if (doc.execStatus === EXEC_STATUS_RECOVERED) return false;
  if (doc.execStatus === EXEC_STATUS_SETTLED) return false;
  if (doc.execStatus === EXEC_STATUS_DELEGATION_EXECUTED) return false;
  if (doc.execStatus === EXEC_STATUS_FORCIBLY && doc.execSubStatus !== SUB_STATUS_PARTIAL) return false;
  return true;
}

/** الحالات المتاحة من «حالة منفذ عليه/عرض وايداع» الحالية (كخيارات نموذج/نافذة التعديل، بلا الحالة
 * الحالية نفسها). «منفذ عليها»: حالة «منفذ» نهائية لا تُغيَّر. «عرض وايداع»: من منفذه يُعاد إلى
 * متداول فقط (لا يُشطب)، بكتاب الجهة العامة بالسير بالملف. «مشطوب» يُعاد إلى متداول (تجديد) فقط —
 * لا انتقال مباشر إلى «منفذ» (يجب المرور بالتجديد أولًا). */
export function targetsOf(current: string, isDeposit: boolean): string[] {
  if (current === EXEC_STATUS_STRUCK_OFF) return [STATE_CIRCULATING];
  if (current === EXECUTED_STATUS_EXECUTED) return isDeposit ? [STATE_CIRCULATING] : [];
  return [EXECUTED_STATUS_EXECUTED, EXEC_STATUS_STRUCK_OFF];
}

/** الحالة الحالية لنظام «طالبة تنفيذ» (مطابقة لآلة الحالات في الخلفية) — الأصل الوحيد
 * لحساب نقطة انطلاق نافذة «تغيير الحالة». تختلف عن `getDocumentStatus` (حالة العرض التي
 * تفضّل `displayStatus` القادم من الخلفية): هذه تُرجع القيمة الخام للانتقال
 * (`تريث/مسترد/محال/تسوية/جبريا/مشطوب/تحت رفع/متداول`). */
export function currentStateOf(doc: StatusSource): string {
  if (doc.execStatus === EXEC_STATUS_STRUCK_OFF || doc.executedStatus === EXEC_STATUS_STRUCK_OFF)
    return EXEC_STATUS_STRUCK_OFF;
  if (doc.execStatus === EXEC_STATUS_DEFERRED) return EXEC_STATUS_DEFERRED;
  if (doc.execStatus === EXEC_STATUS_RECOVERED) return EXEC_STATUS_RECOVERED;
  if (doc.execStatus === EXEC_STATUS_REFERRED_TO_START) return EXEC_STATUS_REFERRED_TO_START;
  if (doc.execStatus === EXEC_STATUS_SETTLED) return EXEC_STATUS_SETTLED;
  if (doc.execStatus === EXEC_STATUS_FORCIBLY) return EXEC_STATUS_FORCIBLY;
  return doc.isDraft ? STATE_DRAFT : STATE_CIRCULATING;
}

/** الانتقالات المسموحة عبر نافذة «تغيير الحالة» — للملف المنيب (والمتداول يُسجَّل من التعديل).
 * الملف المناب مختلف: مناب متداول لا يخرج إلا إلى «مشطوب»، ومناب موروث-تريث
 * أو «مسترد» بلا حالات إطلاقًا (تلحق حالة المنيب اعتبارًا منفذًا أو تريثًا)،
 * و«تراجع»/«منفذ كاملا بهذا البيع» محجوبان عنه نهائيًا (الخلفية تحمي أيضًا).
 * «الإحالة إلى البداية» تدخل من متداول/تريث/منفذ-جبريا-جزئيا (فلتر الحمل الختامي بالترشيح أدناه)،
 * والخروج من «محال الى البداية» عبر العودتين فقط (نقطة العودة المخصصة — الخلفية
 * توجّه اللازمة: جزئيا ⇒ عودة منفذًا جزئيًا، وإلا عودة المتداول). */
export function allowedTargetsOf(state: string, isTarget: boolean, partial: boolean): string[] {
  if (isTarget && state === STATE_CIRCULATING) return [EXEC_STATUS_STRUCK_OFF];
  if (isTarget) return [];
  switch (state) {
    case STATE_DRAFT:
      return [EXEC_STATUS_DEFERRED, EXEC_STATUS_SETTLED];
    case STATE_CIRCULATING:
      return [
        EXEC_STATUS_DEFERRED,
        EXEC_STATUS_SETTLED,
        EXEC_STATUS_FORCIBLY,
        EXEC_STATUS_STRUCK_OFF,
        EXEC_STATUS_REFERRED_TO_START,
      ];
    case EXEC_STATUS_DEFERRED:
      return [EXEC_STATUS_SETTLED, STATUS_ACTION_REVERT, EXEC_STATUS_REFERRED_TO_START];
    case EXEC_STATUS_SETTLED:
      return [STATUS_ACTION_REVERT];
    case EXEC_STATUS_FORCIBLY:
      return [STATUS_ACTION_REVERT, EXEC_STATUS_REFERRED_TO_START, STATUS_ACTION_COMPLETE_SALE];
    case EXEC_STATUS_REFERRED_TO_START:
      // اللازمة: من دخل من «منفذ جبريا — منفذ جزئيا» يعود منفذًا جزئيًا، وإلا يعود للمتداول.
      return partial ? [STATUS_ACTION_RETURN_PARTIAL] : [STATUS_ACTION_RETURN_CIRCULATING];
    default:
      return [];
  }
}

/** هل الهدف مشروط بالجزئية فقط؟ — الأصل الوحيد لفلتر «الحمل الختامي» بعد `allowedTargetsOf`. */
function isPartialOnlyTarget(target: string, state: string): boolean {
  return (
    target === STATUS_ACTION_COMPLETE_SALE ||
    (target === EXEC_STATUS_REFERRED_TO_START && state === EXEC_STATUS_FORCIBLY) ||
    target === STATUS_ACTION_RETURN_PARTIAL
  );
}

/** يرشّح قائمة `allowedTargetsOf` بإسقاط الأهداف المشروطة بالجزئية عند غيابها — بلا تغيير سلوكي. */
export function filterTargetsByPartial(
  targets: readonly string[],
  state: string,
  partial: boolean,
): string[] {
  if (partial) return [...targets];
  return targets.filter((t) => !isPartialOnlyTarget(t, state));
}