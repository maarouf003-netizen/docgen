/**
 * حذف محارف التنسيق/التحكم غير المرئية من اسم المستخدم قبل الإرسال.
 *
 * واجهات RTL (وبعض نسخ النصوص من Markdown) تُلصق محارف اتجاه مثل U+2066…U+2069
 * أو U+200B…U+200F أو U+FEFF، فيبدو الاسم صحيحًا لكن المطابقة في الخادم تفشل.
 * لا نغيّر الحروف/الأرقام، ونقصّ المسافات الطرفية أيضًا.
 */
const INVISIBLE_FORMATTING = /[\u200B-\u200F\u202A-\u202E\u2066-\u2069\uFEFF]/g;

export function normalizeUsernameInput(value: string): string {
  return value.replace(INVISIBLE_FORMATTING, '').trim();
}
