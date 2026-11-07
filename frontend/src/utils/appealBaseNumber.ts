import { normalizeArabicDigits } from './arabicDigits';
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

/**
 * تحقق موحّد من «لعام» قبل الإرسال (مطابق لرسائل الخادم) — يعيد رسالة الخطأ أو `null`.
 * السنة اختيارية؛ الفارغ ليس خطأ، وغير الرقمي/غير الموجب/المستقبلي خطأ.
 */
export function appealYearError(raw: string, currentYear: number): string | null {
  const text = normalizeArabicDigits(raw).trim();
  if (!text) return null;

  const year = Number(text);
  if (!Number.isInteger(year) || year <= 0) {
    return 'لعام غير صالح — أدخل سنة مثل 2026';
  }
  if (year > currentYear) {
    return `لا يمكن إدخال رقم أساس لسنة مستقبلية — السنة الحالية ${currentYear}`;
  }
  return null;
}
