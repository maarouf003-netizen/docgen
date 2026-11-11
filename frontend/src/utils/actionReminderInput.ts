/**
 * منطق إدخال التذكير المشترك بين نافذتي الإجراءات (ملف/استئناف): تحديد هل غيّر المستخدم
 * التذكير فعلًا، وحارس «تذكير مفعّل بلا مدة/لون» — فلا يُكرر كل مكوّن القاعدة نفسها.
 * عقود الخادم: تعديل الإجراء لا يحمل حقول تذكير، والحقلان الفارغان على نقطة التذكير = إلغاء.
 */
export interface LoadedReminder {
  duration: string;
  color: string;
}

/** هل اختلفت حالة التذكير في النموذج عن القيم المحمّلة من الخادم؟ */
export function isReminderDirty(
  remind: boolean,
  duration: string,
  color: string,
  loaded: LoadedReminder | null,
): boolean {
  if (remind !== (loaded !== null)) return true;
  if (!remind) return false;
  return duration !== (loaded?.duration ?? '') || color !== (loaded?.color ?? '');
}

/**
 * تذكير مفعّل بلا مدة ولا لون = طلب فارغ يعامله الخادم إلغاءً؛ يُرفض أماميًا
 * بدل حفظ صامت بلا تذكير (والمستخدم يظن أنه ضبطه).
 */
export function isReminderIncomplete(remind: boolean, duration: string, color: string): boolean {
  return remind && !duration && !color;
}
