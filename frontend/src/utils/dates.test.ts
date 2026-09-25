import { describe, it, expect } from 'vitest';
import { formatDate, formatDateTime, todayLocalKey } from './dates';

describe('dates', () => {
  describe('formatDate', () => {
    it('يعيد قيمة فارغة عند غياب القيمة أو البديل المحدد', () => {
      expect(formatDate(undefined)).toBe('');
      expect(formatDate('')).toBe('');
      expect(formatDate('', '—')).toBe('—');
    });

    it('يعيد النص كما هو عند القيمة غير الصالحة (كي لا تضيع بيانات مدخلة يدويًا)', () => {
      expect(formatDate('ليس تاريخًا')).toBe('ليس تاريخًا');
      expect(formatDate('2026-13-99')).toBe('2026-13-99');
    });

    it('يصيغ التاريخ الصالح بالعربية دون إرجاع النص الخام', () => {
      const out = formatDate('2026-08-04');
      expect(out).not.toBe('');
      expect(out).not.toBe('2026-08-04');
    });
  });

  describe('formatDateTime', () => {
    it('يعيد قيمة فارغة عند غياب القيمة أو البديل المحدد', () => {
      expect(formatDateTime(undefined)).toBe('');
      expect(formatDateTime('', '—')).toBe('—');
    });

    it('يعيد النص كما هو عند القيمة غير الصالحة', () => {
      expect(formatDateTime('2026-13-99')).toBe('2026-13-99');
    });

    it('يصيغ التاريخ والوقت الصالحين بالعربية دون إرجاع النص الخام', () => {
      const out = formatDateTime('2026-08-04T10:00:00');
      expect(out).not.toBe('');
      expect(out).not.toBe('2026-08-04T10:00:00');
    });
  });

  describe('todayLocalKey', () => {
    it('يبني yyyy-MM-dd بالتوقيت المحلي لا UTC', () => {
      expect(todayLocalKey(new Date(2026, 5, 15, 12, 0, 0))).toBe('2026-06-15');
      expect(todayLocalKey(new Date(2026, 0, 5, 1, 2, 3))).toBe('2026-01-05');
    });
  });
});
