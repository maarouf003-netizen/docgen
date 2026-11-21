/** يُحوّل القيمة الفارغة/البياض إلى null لتُهملها الخلفية في جسم الطلب (البيانات غير المُدخلة). */
export function trimNull(value: string | null | undefined): string | null {
  return value?.trim() ? value.trim() : null;
}