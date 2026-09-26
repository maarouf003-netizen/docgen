import type { Role } from '../types';

/**
 * الصفحة الرئيسية حسب الدور — مصدر واحد للحقيقة يُستخدم في مسار `/`
 * وارتداد `RequireRole` وتوجيه ما بعد الدخول، حتى لا تتشتت قواعد
 * «أين يهبط كل دور» في ثلاثة مواضع.
 *
 * مندوب الجهة (`entitymanager`) لا يملك لوحة تحكم إطلاقًا — بوابته
 * القرائية هي وطنه. بقية الأدوار (وغياب الدور) تهبط على `/`.
 */
export function getHomeForRole(role: Role | string | undefined): string {
  return role === 'entitymanager' ? '/portal/stats' : '/';
}
