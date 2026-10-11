/** تحويل نص تاريخ إلى Date صالح أو null. */
function parseDate(value: string): Date | null {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

/**
 * تاريخ بالعربية (سوري): الفراغ يعيد emptyFallback، والقيمة غير الصالحة تُعيد النص كما هو
 * (كي لا تضيع بيانات أدخلها المستخدم يدويًا)، والصالح يُعرض كتاريخ محلي.
 */
export function formatDate(value?: string, emptyFallback = ''): string {
  if (!value) return emptyFallback;
  const date = parseDate(value);
  return date ? date.toLocaleDateString('ar-SY') : value;
}

/** تاريخ ووقت بالعربية (سوري) بنفس قواعد formatDate. */
export function formatDateTime(value?: string, emptyFallback = ''): string {
  if (!value) return emptyFallback;
  const date = parseDate(value);
  return date ? date.toLocaleString('ar-SY') : value;
}

/**
 * طابع نسبي عربي («منذ…») لتيار المنتدى: تحت الدقيقة «الآن»، وتحت الساعة
 * بالدقائق، وتحت اليوم بالساعات، وتحت الأسبوع بالأيام، وفوق ذلك التاريخ
 * الكامل عبر `formatDate` — والقيمة غير الصالحة تُعاد كما هي.
 */
export function formatRelativeTime(value?: string, nowMs = Date.now()): string {
  if (!value) return '';
  const date = parseDate(value);
  if (!date) return value;
  const diffMs = nowMs - date.getTime();
  if (diffMs < 0) return formatDate(value);
  const minute = 60_000;
  const hour = 60 * minute;
  const day = 24 * hour;
  if (diffMs < minute) return 'الآن';
  if (diffMs < hour) {
    const n = Math.floor(diffMs / minute);
    return n === 1 ? 'منذ دقيقة' : n === 2 ? 'منذ دقيقتين' : `منذ ${n} دقائق`;
  }
  if (diffMs < day) {
    const n = Math.floor(diffMs / hour);
    return n === 1 ? 'منذ ساعة' : n === 2 ? 'منذ ساعتين' : `منذ ${n} ساعات`;
  }
  if (diffMs < 7 * day) {
    const n = Math.floor(diffMs / day);
    return n === 1 ? 'أمس' : `منذ ${n} أيام`;
  }
  return formatDate(value);
}

/**
 * مفتاح فاصل اليوم المحلي `yyyy-MM-dd` (بتوقيت المتصفح) — لتجميع رسائل التيار
 * تحت عناوين «اليوم/أمس/التاريخ».
 */
export function dayLocalKey(value: string): string {
  const date = parseDate(value);
  if (!date) return value;
  return todayLocalKey(date);
}

/**
 * مفتاح اليوم المحلي `yyyy-MM-dd` (بتوقيت المتصفح لا UTC) — لأسماء ملفات
 * التصدير المولّدة عميلًا، فيطابق تاريخ الخادم المحلي ولا ينزاح يومًا.
 */
export function todayLocalKey(date = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}
