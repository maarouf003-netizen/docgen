/** صيغ الترحيب بالترتيب — تُعرض قبل الاسم الأول. */
export const GREETINGS = [
  'مرحبًا',
  'أهلًا',
  'طابت أوقاتك',
  'كيف حالك اليوم',
  'ماذا سننفذ اليوم',
  'مرحبًا بعودتك',
  'السلام عليكم',
];

/** مفتاح التخزين المحلي لموضع الترحيب (يتقدم مع كل دخول للوحة). */
export const GREETING_INDEX_KEY = 'greeting-index';

/**
 * الصيغة التالية لكل دخول للوحة: تقدّم دائري مثبّت محليًا —
 * الزيارة الأولى `مرحبًا`، والثانية `أهلًا`، وهكذا (بلا تبدل تلقائي داخل الزيارة).
 */
export function nextGreetingIndex(): number {
  let current = -1;
  try {
    const stored = localStorage.getItem(GREETING_INDEX_KEY);
    if (stored !== null) {
      const parsed = Number(stored);
      if (Number.isInteger(parsed)) current = parsed;
    }
  } catch {
    /* التخزين المعطوب يُعامَل كأول زيارة */
  }
  const next = (current + 1) % GREETINGS.length;
  try {
    localStorage.setItem(GREETING_INDEX_KEY, String(next));
  } catch {
    /* التفضيل محلي بحت — الفشل الصامت مقبول */
  }
  return next;
}
