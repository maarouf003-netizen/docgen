import { describe, it, expect } from 'vitest';
import { sanitizeRichText, richToPlainText } from './richText';

describe('richText', () => {
  it('يستخرج النص العادي مع ضغط الفراغات', () => {
    expect(richToPlainText('<p>إجراء <strong>هام</strong></p>')).toBe('إجراء هام');
    expect(richToPlainText('<p>سطر\nأول   وثانٍ</p>')).toBe('سطر أول وثانٍ');
  });

  it('يرجع سلسلة فارغة للنص الفارغ', () => {
    expect(richToPlainText('')).toBe('');
    expect(richToPlainText('<p><br></p>')).toBe('');
  });

  it('يزيل الوسوم والسمات الخطرة من التعقيم', () => {
    const html = '<p onclick="alert(1)">نص</p><script>evil()</script><img src=x onerror="alert(1)">';
    const out = sanitizeRichText(html);
    expect(out).not.toContain('<script');
    expect(out).not.toContain('<img');
    expect(out).not.toContain('onerror');
    expect(out).not.toContain('onclick');
    expect(out).toContain('نص');
  });

  it('يحافظ على لون النص ويعقّم بقية خصائص CSS', () => {
    const html = '<span style="color:#dc2626;background:url(javascript:evil())">نص</span>';
    const out = sanitizeRichText(html);
    expect(out).toContain('color');
    expect(out).not.toContain('url');
    expect(out).not.toContain('javascript');
  });

  it('يبقي علامات التنسيق الأساسية ويحذف غير المسموح', () => {
    const html = '<ul><li><strong>بند</strong></li></ul><h1>عنوان</h1>';
    const out = sanitizeRichText(html);
    expect(out).toContain('<ul>');
    expect(out).toContain('<strong>');
    expect(out).not.toContain('<h1');
  });
});
