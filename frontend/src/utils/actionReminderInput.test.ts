import { describe, expect, it } from 'vitest';
import { isReminderDirty, isReminderIncomplete } from './actionReminderInput';

describe('isReminderDirty', () => {
  it('تذكير جديد على إجراء بلا تذكير ⇒ متغيّر', () => {
    expect(isReminderDirty(true, 'أسبوع', 'أحمر', null)).toBe(true);
  });

  it('بلا تذكير سابق وبلا تفعيل ⇒ غير متغيّر (لا يُرسل طلب تذكير)', () => {
    expect(isReminderDirty(false, '', '', null)).toBe(false);
  });

  it('قيم مطابقة محمّلة ⇒ غير متغيّر (تعديل نص/تاريخ فقط)', () => {
    expect(isReminderDirty(true, 'أسبوع', 'أحمر', { duration: 'أسبوع', color: 'أحمر' })).toBe(false);
  });

  it('تغيير المدة أو اللون ⇒ متغيّر', () => {
    expect(isReminderDirty(true, 'شهر', 'أحمر', { duration: 'أسبوع', color: 'أحمر' })).toBe(true);
    expect(isReminderDirty(true, 'أسبوع', 'أصفر', { duration: 'أسبوع', color: 'أحمر' })).toBe(true);
  });

  it('إلغاء تذكير قائم ⇒ متغيّر', () => {
    expect(isReminderDirty(false, '', '', { duration: 'أسبوع', color: 'أحمر' })).toBe(true);
  });

  it('تذكير محمّل بلون فقط ومطابق ⇒ غير متغيّر', () => {
    expect(isReminderDirty(true, '', 'أصفر', { duration: '', color: 'أصفر' })).toBe(false);
  });
});

describe('isReminderIncomplete', () => {
  it('مفعّل بلا مدة ولا لون ⇒ ناقص', () => {
    expect(isReminderIncomplete(true, '', '')).toBe(true);
  });

  it('مفعّل بمدة أو لون ⇒ غير ناقص', () => {
    expect(isReminderIncomplete(true, 'أسبوع', '')).toBe(false);
    expect(isReminderIncomplete(true, '', 'أحمر')).toBe(false);
  });

  it('غير مفعّل ⇒ غير ناقص مهما كانت القيم', () => {
    expect(isReminderIncomplete(false, '', '')).toBe(false);
    expect(isReminderIncomplete(false, 'أسبوع', 'أحمر')).toBe(false);
  });
});
