import { describe, it, expect } from 'vitest';
import { redactSensitiveText } from './redactSensitiveText';

describe('redactSensitiveText', () => {
  it('يحجب كوكي الجلسة/الحماية وترويسة CSRF و Bearer', () => {
    const text =
      'docgen_csrf=abc123; X-CSRF-Token: xyz; Authorization: Bearer eyJhbGciOi.payload.sig';
    const redacted = redactSensitiveText(text);

    expect(redacted).not.toContain('abc123');
    expect(redacted).not.toContain('xyz;');
    expect(redacted).not.toContain('eyJhbGciOi.payload.sig');
  });

  it('يحجب JWT وأسرار الاستعلام والبريد والهاتف', () => {
    const text =
      'فشل: https://x.test/api?token=super-secret&email=ali@example.com هاتف 0933333333';
    const redacted = redactSensitiveText(text);

    expect(redacted).not.toContain('super-secret');
    expect(redacted).not.toContain('ali@example.com');
    expect(redacted).not.toContain('0933333333');
    expect(redacted).toContain('token=<محجوب>');
  });

  it('يُبقي النص العربي العادي كما هو ويقصّ الطول', () => {
    expect(redactSensitiveText('تعذّر عرض الصفحة')).toBe('تعذّر عرض الصفحة');
    expect(redactSensitiveText('abcdef', 3)).toBe('abc');
  });

  it('يعيد نصًا فارغًا للمدخلات الفارغة ولا يرمي', () => {
    expect(redactSensitiveText(null)).toBe('');
    expect(redactSensitiveText(undefined)).toBe('');
    expect(redactSensitiveText({ toString: () => { throw new Error('no'); } })).toBe(
      '<نص غير قابل للقراءة>',
    );
  });
});
