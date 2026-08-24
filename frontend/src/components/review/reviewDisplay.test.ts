import { describe, it, expect } from 'vitest';
import { reviewLetterTitle } from './reviewDisplay';

describe('reviewLetterTitle', () => {
  it('يبني صيغة «مطالعة بملف…» للكتاب المربوط بملف', () => {
    expect(
      reviewLetterTitle({
        executedName: 'أحمد محمد العلي',
        fileNumber: '77/2026',
        fileType: 'تنفيذي',
        fileYear: '2026',
        court: 'دائرة تنفيذ دمشق',
      }),
    ).toBe('مطالعة بملف (أحمد محمد العلي) رقم 77/2026 نوع تنفيذي لعام 2026 دائرة تنفيذ دائرة تنفيذ دمشق');
  });

  it('يحذف الأجزاء الناقصة من سياق الملف دون كسر الصيغة', () => {
    expect(reviewLetterTitle({ executedName: 'أحمد العلي', fileNumber: null, fileType: null, fileYear: '2026', court: null }))
      .toBe('مطالعة بملف (أحمد العلي) لعام 2026');
  });

  it('يعيد صيغة الكتاب العام عندما لا يوجد سياق ملف', () => {
    expect(reviewLetterTitle(null)).toBe('كتاب مطالعة عام غير مرتبط بملف');
    expect(reviewLetterTitle(undefined)).toBe('كتاب مطالعة عام غير مرتبط بملف');
  });
});
