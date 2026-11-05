import type { Role } from '../types/common';

/**
 * مساعدات أدوار نقية — مصدر الحقيقة الوحيد لاشتقاق رايات الصلاحية من الدور.
 *
 * تُستخدم في `AuthContext` وفي الصفحات التي تحتاج الاشتقاق مباشرةً من `user.role`،
 * فلا تتكرر الشروط الحرفية في الملفات ولا تنحرف إحداها عن الأخرى. الاعتماد على
 * `user.role` (لا على رايات السياق) يُبقي السلوك صحيحًا مع أي `mock` اختباري
 * يوفّر الدور بلا الرايات المشتقة.
 */

/** محامٍ — الدور الوحيد الذي يملك التذكيرات والملفات الشخصية. */
export function isLawyerRole(role: Role | null | undefined): boolean {
  return role === 'lawyer';
}

/** رئيس قسم أو رئيس شعبة — نطاق إشرافي فرعي (لا يشمل المدير/المشرف). */
export function isHeadOrSubHeadRole(role: Role | null | undefined): boolean {
  return role === 'head' || role === 'subhead';
}

/** صلاحية كاملة على كل الفروع — المدير أو مشرف النظام فقط. */
export function hasFullAccessRole(role: Role | null | undefined): boolean {
  return role === 'manager' || role === 'admin';
}
