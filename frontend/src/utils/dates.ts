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
