/** حدّ نص رسالة المنتدى (يطابق عمود القاعدة والتحقق الخلفي). */
export const FORUM_BODY_MAX = 2000;

const DRAFT_KEY = 'forum:composer-draft';

/** تحميل مسودة حقل الكتابة المحفوظة محليًا (آمن مع حظر التخزين). */
export function loadForumDraft(): string {
  try {
    return localStorage.getItem(DRAFT_KEY) ?? '';
  } catch {
    return '';
  }
}

/** حفظ المسودة عند كل ضغطة (الفارغ يمحو المفتاح). */
export function saveForumDraft(value: string): void {
  try {
    if (value) localStorage.setItem(DRAFT_KEY, value);
    else localStorage.removeItem(DRAFT_KEY);
  } catch {
    /* التخزين المحلي قد يكون محظورًا — المسودة تبقى في الذاكرة فقط. */
  }
}
