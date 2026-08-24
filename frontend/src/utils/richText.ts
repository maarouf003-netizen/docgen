import DOMPurify from 'dompurify';

const ALLOWED_TAGS = ['p', 'br', 'strong', 'b', 'em', 'i', 'u', 's', 'span', 'ul', 'ol', 'li'];
const ALLOWED_ATTR = ['style'];

/** خصائص CSS المسموحة في سمة style مع نمط القيمة الآمنة لكل منها. */
const ALLOWED_STYLE_PROPERTIES: Record<string, RegExp> = {
  color: /^[\w#().,%\s-]+$/,
  'font-family': /^[\w\u0600-\u06FF'"\s,-]+$/,
  'font-size': /^\d+(\.\d+)?(px|pt|rem|em)$/,
};

/**
 * تعقيم نص HTML صادر عن محرر التنسيق (قائمة بيضاء صارمة):
 * نصوص عادية + عريض/مائل/تحته خط/مشطوب، وخصائص CSS محددة عبر span[style]
 * (اللون والخط وحجم الخط فقط). DOMPurify لا يدعم حصر خصائص CSS،
 * لذا نُبقي بعد التعقيم الخصائص البيضاء ذات القيم الآمنة حصرًا.
 */
export function sanitizeRichText(html: string): string {
  const clean = DOMPurify.sanitize(html, {
    ALLOWED_TAGS,
    ALLOWED_ATTR,
    FORBID_TAGS: ['style', 'script', 'iframe', 'object', 'embed', 'link', 'meta'],
    FORBID_ATTR: ['onerror', 'onclick', 'onload', 'onmouseover', 'onfocus', 'onblur', 'srcdoc'],
  });
  return filterStyles(clean);
}

/** فحص أمان قيمة خاصية CSS (منع url() والوسائط الخبيثة). */
function isSafeDeclarationValue(value: string): boolean {
  return !/\burl\s*\(|expression|javascript:|@import/i.test(value);
}

function filterDeclarations(styleValue: string): string {
  const kept: string[] = [];
  for (const declaration of styleValue.split(';')) {
    const separatorIndex = declaration.indexOf(':');
    if (separatorIndex === -1) continue;
    const property = declaration.slice(0, separatorIndex).trim().toLowerCase();
    const value = declaration.slice(separatorIndex + 1).trim();
    if (!value) continue;
    const safeValuePattern = ALLOWED_STYLE_PROPERTIES[property];
    if (!safeValuePattern || !isSafeDeclarationValue(value)) continue;
    if (!safeValuePattern.test(value)) continue;
    kept.push(`${property}: ${value}`);
  }
  return kept.join('; ');
}

/** إبقاء خصائص القائمة البيضاء فقط في كل سمة style، وحذف السمة إن خلت. */
function filterStyles(html: string): string {
  const wrapper = document.createElement('div');
  wrapper.innerHTML = html;
  wrapper.querySelectorAll('[style]').forEach((node) => {
    const el = node as HTMLElement;
    const filtered = filterDeclarations(el.getAttribute('style') ?? '');
    if (filtered) el.setAttribute('style', filtered);
    else el.removeAttribute('style');
  });
  return wrapper.innerHTML;
}

/** استخراج نص عادي (بلا وسوم) للمعاينات المختصرة في الجداول والتذكيرات. */
export function richToPlainText(html: string): string {
  const sanitized = sanitizeRichText(html);
  if (!sanitized) return '';
  const el = document.createElement('div');
  el.innerHTML = sanitized;
  return (el.textContent ?? '').replace(/\s+/g, ' ').trim();
}
