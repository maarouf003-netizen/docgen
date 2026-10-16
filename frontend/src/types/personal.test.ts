import { describe, it, expect } from 'vitest';
import { PERSONAL_COLORS, PERSONAL_RECURRENCES } from './personal';

const ARABIC_RE = /[\u0600-\u06FF]/;
// بصمة الترميز الفاسد: بايتات UTF-8 قُرئت كـ Latin-1 (تظهر كـ `ظ…ط`/`â`/`£`).
const MOJIBAKE_RE = /[£â€]/;

/**
 * انحدار ترميز `personal.ts`: القيم التنفيذية (لا التعليقات) يجب أن تطابق
 * `PersonalReminderCatalog` الخلفي حرفًا بحرف — وإلا رفضت الخلفية
 * (`ValidateRecurrence`/`ValidateColor` → `400`) وظهرت القوائم مشوهة.
 */
describe('personal catalog encoding', () => {
  it('قيم التكرار عربية سليمة ومطابقة للخلفية', () => {
    expect([...PERSONAL_RECURRENCES]).toEqual(['مرة واحدة', 'يومي', 'أسبوعي', 'شهري']);
  });

  it('قيم الألوان عربية سليمة ومطابقة للخلفية', () => {
    expect([...PERSONAL_COLORS]).toEqual(['أحمر', 'بنفسجي', 'أصفر', 'زمردي']);
  });

  it('لا أثر لترميز فاسد في أي قيمة', () => {
    for (const v of [...PERSONAL_RECURRENCES, ...PERSONAL_COLORS]) {
      expect(v).toMatch(ARABIC_RE);
      expect(v).not.toMatch(MOJIBAKE_RE);
    }
  });
});
