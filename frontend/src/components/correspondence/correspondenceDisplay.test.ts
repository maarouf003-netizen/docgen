import { describe, it, expect } from 'vitest';
import {
  CORRESPONDENCE_MAX_BODY_CHARS,
  correspondencePlainText,
} from './correspondenceDisplay';

describe('correspondencePlainText', () => {
  it('الحد الأقصى 10000 — مرآة الخلفية', () => {
    expect(CORRESPONDENCE_MAX_BODY_CHARS).toBe(10000);
  });

  it('الفراغ يبقى فراغًا', () => {
    expect(correspondencePlainText('')).toBe('');
    expect(correspondencePlainText('<p>   </p>')).toBe('');
  });

  it('الكتل العلوية تُجمع بفاصل وحيد (مرآة الخلفية)', () => {
    expect(correspondencePlainText('<p>أحمد</p><p>محمد</p>')).toBe('أحمد محمد');
  });

  it('الفراغات تُطبَّع والوسوم لا تُحتسب', () => {
    expect(correspondencePlainText('<p>  نص   <b>مهم</b>   جدًا  </p>')).toBe('نص مهم جدًا');
  });
});
