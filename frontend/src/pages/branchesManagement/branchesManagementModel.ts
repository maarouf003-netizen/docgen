/** تسميات أحداث سجل التعاقب (قيم الخادم → عربي للعرض). */
export const SUCCESSION_EVENT_LABELS: Record<string, string> = {
  appointed: 'تعيين',
  deactivated: 'تعطيل',
  succeeded: 'إحلال خلف',
  'circuit-transferred': 'نقل دائرة',
  renamed: 'إعادة تسمية',
};

/**
 * تحقق حقول الفرع المشتركة بين الإنشاء والتعديل — يعيد أول رسالة خطأ أو سلسلة فارغة.
 * الترتيب (اسم ← كود ← محافظة) ورسائله عقد اختبارات الصفحة ولا يتغيّر.
 */
export function validateBranchFields(
  name: string,
  code: string,
  governorate: string,
): string {
  if (!name.trim()) return 'اسم الفرع مطلوب';
  if (!code.trim()) return 'كود الفرع مطلوب';
  if (!governorate) return 'المحافظة مطلوبة — اختر محافظة الفرع من القائمة';
  return '';
}
