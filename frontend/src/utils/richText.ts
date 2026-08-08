import DOMPurify from 'dompurify';

const ALLOWED_TAGS = ['p', 'br', 'strong', 'b', 'em', 'i', 'u', 's', 'span', 'ul', 'ol', 'li'];
const ALLOWED_ATTR = ['style'];

/**
 * تعقيم نص HTML صادر عن محرر التنسيق (قائمة بيضاء صارمة):
 * نصوص عادية + عريض/مائل/تحته خط/مشطوب + لون عبر span[style] فقط.
 * DOMPurify لا يدعم حصر خصائص CSS، لذا نُبقي بعد التعقيم خاصية color فقط.
 */
export function sanitizeRichText(html: string): string {
  const clean = DOMPurify.sanitize(html, {
    ALLOWED_TAGS,
    ALLOWED_ATTR,
    FORBID_TAGS: ['style', 'script', 'iframe', 'object', 'embed', 'link', 'meta'],
    FORBID_ATTR: ['onerror', 'onclick', 'onload', 'onmouseover', 'onfocus', 'onblur', 'srcdoc'],
  });
  return filterStylesToColorOnly(clean);
}

const COLOR_PROPERTY_RE = /(?:^|;)\s*color\s*:\s*([^;]+)/i;
const SAFE_COLOR_VALUE_RE = /^[\w#().,%\s-]+$/;

/** استخراج قيمة خاصية color فقط والتحقق من أمانها. */
function keepColorOnly(styleValue: string): string {
  const match = styleValue.match(COLOR_PROPERTY_RE);
  if (!match) return '';
  const value = match[1].trim();
  if (/\burl\s*\(|expression|javascript:|@import/i.test(value)) return '';
  if (!SAFE_COLOR_VALUE_RE.test(value)) return '';
  return `color: ${value}`;
}

/** إبقاء خاصية color فقط في كل سمة style، وحذف السمة إن خلت. */
function filterStylesToColorOnly(html: string): string {
  const wrapper = document.createElement('div');
  wrapper.innerHTML = html;
  wrapper.querySelectorAll('[style]').forEach((node) => {
    const el = node as HTMLElement;
    const color = keepColorOnly(el.getAttribute('style') ?? '');
    if (color) el.setAttribute('style', color);
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
