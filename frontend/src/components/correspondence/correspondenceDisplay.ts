import type {
  CorrespondenceFileContext,
  CorrespondenceImportance,
  CorrespondenceMessageKind,
} from '../../types';

/** حدث تُطلق عند تأكيد مشاهدة مراسلة، لتحديث عدّاد الجرس فورًا في Layout. */
export const CORRESPONDENCE_UNSEEN_EVENT = 'correspondence:unseen-changed';

/** تسميات أنواع رسائل المراسلة كما تظهر في الواجهة. */
export const CORRESPONDENCE_MESSAGE_KIND_LABELS: Record<CorrespondenceMessageKind, string> = {
  letter: 'المراسلة',
  addendum: 'لاحق',
  reply: 'رد الطرف المستلم',
};

/** تسميات الأهمية كما تظهر في الواجهة. */
export const CORRESPONDENCE_IMPORTANCE_LABELS: Record<CorrespondenceImportance, string> = {
  normal: 'عادي',
  important: 'هام',
  urgent: 'عاجل',
};

/** أدوار أطراف المراسلة كما تظهر في الواجهة. */
export function correspondenceRoleLabel(role: string): string {
  switch (role) {
    case 'lawyer':
      return 'محامٍ';
    case 'head':
      return 'رئيس قسم';
    case 'entitymanager':
      return 'مندوب جهة';
    case 'manager':
      return 'مدير';
    case 'admin':
      return 'مشرف';
    default:
      return role || '—';
  }
}

/**
 * صيغة العنوان الموحدة:
 * مربوطة بملف → «مراسلة بملف (الاسم الثلاثي) رقم.. نوع.. لعام.. دائرة تنفيذ..»
 * عامة → «مراسلة عامة غير مرتبطة بملف».
 */
export function correspondenceTitle(fileContext?: CorrespondenceFileContext | null): string {
  if (!fileContext) return 'مراسلة عامة غير مرتبطة بملف';
  return [
    `مراسلة بملف (${fileContext.executedName})`,
    fileContext.fileNumber ? `رقم ${fileContext.fileNumber}` : null,
    fileContext.fileType ? `نوع ${fileContext.fileType}` : null,
    fileContext.fileYear ? `لعام ${fileContext.fileYear}` : null,
    fileContext.court ? `دائرة تنفيذ ${fileContext.court}` : null,
  ]
    .filter(Boolean)
    .join(' ');
}
