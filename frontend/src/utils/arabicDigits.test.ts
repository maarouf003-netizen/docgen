import { describe, expect, it } from 'vitest';
import { normalizeArabicDigits } from './arabicDigits';

describe('normalizeArabicDigits', () => {
  it('يحوّل الأرقام العربية (٠-٩) إلى ASCII', () => {
    expect(normalizeArabicDigits('٠١٢٣٤٥٦٧٨٩')).toBe('0123456789');
  });

  it('يحوّل الأرقام الفارسية (۰-۹) إلى ASCII', () => {
    expect(normalizeArabicDigits('۰۱۲۳۴۵۶۷۸۹')).toBe('0123456789');
  });

  it('يطبّع تواريخ مكتوبة بأرقام عربية', () => {
    expect(normalizeArabicDigits('١/٨/٢٠٢٦')).toBe('1/8/2026');
    expect(normalizeArabicDigits('٢٩/٢/٢٠٢٤')).toBe('29/2/2024');
  });

  it('يطبّع مبالغ بفواصل عربية دون مسها', () => {
    expect(normalizeArabicDigits('١٢٥٬٥٠٠٫٥٠')).toBe('125٬500٫50');
  });

  it('يمرّر النص ASCII دون تغيير', () => {
    const text = '1/8/2026 أحمد 12';
    expect(normalizeArabicDigits(text)).toBe(text);
  });

  it('لا يمس المحارف غير الرقمية', () => {
    expect(normalizeArabicDigits('السنة ٢٠٢٦ - شارع٥')).toBe('السنة 2026 - شارع5');
  });

  it('يتعامل مع السلسلة الفارغة', () => {
    expect(normalizeArabicDigits('')).toBe('');
  });
});
