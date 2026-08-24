import type { ReviewLetterFileContext, ReviewLetterMessageKind } from '../../types';

/** حدث يطلقه فتح كتاب مطالعة بعد الاطلاع على الرد، لتحديث عدّاد الشارة فورًا في Layout. */
export const REVIEWS_UNSEEN_EVENT = 'reviews:unseen-changed';

/** تسميات أنواع رسائل كتاب المطالعة كما تظهر في الواجهة. */
export const REVIEW_MESSAGE_KIND_LABELS: Record<ReviewLetterMessageKind, string> = {
  letter: 'كتاب المطالعة',
  addendum: 'لاحق',
  reply: 'رد رئيس القسم',
};

/**
 * صيغة العنوان الموحدة:
 * مربوط بملف → «مطالعة بملف (الاسم الثلاثي) رقم.. نوع.. لعام.. دائرة تنفيذ..»
 * عام → «كتاب مطالعة عام غير مرتبط بملف».
 */
export function reviewLetterTitle(fileContext?: ReviewLetterFileContext | null): string {
  if (!fileContext) return 'كتاب مطالعة عام غير مرتبط بملف';
  return [
    `مطالعة بملف (${fileContext.executedName})`,
    fileContext.fileNumber ? `رقم ${fileContext.fileNumber}` : null,
    fileContext.fileType ? `نوع ${fileContext.fileType}` : null,
    fileContext.fileYear ? `لعام ${fileContext.fileYear}` : null,
    fileContext.court ? `دائرة تنفيذ ${fileContext.court}` : null,
  ]
    .filter(Boolean)
    .join(' ');
}
