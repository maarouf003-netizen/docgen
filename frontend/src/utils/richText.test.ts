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

  it('يحافظ على الخط وحجم الخط ويرفض قيمهما غير الآمنة', () => {
    const html =
      '<span style="font-family: \'Traditional Arabic\', Arial; font-size: 16px">سليم</span>';
    expect(sanitizeRichText(html)).toContain('font-family: \'Traditional Arabic\', Arial');
    expect(sanitizeRichText(html)).toContain('font-size: 16px');

    const evil = '<span style="font-family: url(evil); font-size: expression(alert(1))">خبيث</span>';
    const out = sanitizeRichText(evil);
    expect(out).not.toContain('url');
    expect(out).not.toContain('expression');

    const invalid = '<span style="font-size: 100%">كبير</span>';
    expect(sanitizeRichText(invalid)).not.toContain('font-size');
  });

  it('يحذف سمة style بالكامل إذا لم يبق فيها أي خاصية مسموحة', () => {
    const html = '<span style="margin-top: 4px">نص</span>';
    expect(sanitizeRichText(html)).toBe('<span>نص</span>');
  });

  it('يبقي علامات التنسيق الأساسية ويحذف غير المسموح', () => {
    const html = '<ul><li><strong>بند</strong></li></ul><h1>عنوان</h1>';
    const out = sanitizeRichText(html);
    expect(out).toContain('<ul>');
    expect(out).toContain('<strong>');
    expect(out).not.toContain('<h1');
  });
});
