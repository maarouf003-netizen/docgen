import type { AppealDto } from '../types';

/** أقصى طول لرقم الأساس — مطابق لعمود الخادم (`AppealBaseNumbers.BaseNumber`). */
export const APPEAL_BASE_NUMBER_MAX_LENGTH = 50;

/**
 * رقم الأساس المعروض: هوية السنة الحالية إن وُجدت وإلا آخر رقم مسجّل.
 * `currentBaseNumber` يغلّف القاعدة على الخادم، والاحتياطي يحمي من استجابة قديمة.
 */
export function appealBaseNumberOf(appeal: AppealDto): string | undefined {
  return appeal.currentBaseNumber ?? appeal.appealBaseNumber ?? undefined;
}

/** سنة رقم الأساس المعروض — تُستخدم لتهيئة «تعديل القيد» وعرض «لعام». */
export function appealBaseYearOf(appeal: AppealDto): string | undefined {
  return appeal.currentBaseYear ?? appeal.appealYear ?? undefined;
}
