import type { Role } from '../types';

/**
 * التسميات العربية الرسمية لأدوار المستخدمين — المصدر الوحيد المعتمد.
 * تُستخدم في الشريط الجانبي وإدارة المستخدمين وصفحات الحساب.
 *
 * ملاحظة مقصودة: `correspondenceRoleLabel` في `correspondenceDisplay.ts`
 * خارج هذا التوحيد — صياغته (`محامٍ`/`مشرف`) خاصة بسياق عرض أطراف
 * المراسلات، فلا تُدمج هنا ولا يُعتمد عليها خارج المراسلات.
 */
export const ROLE_LABELS: Record<Role, string> = {
  lawyer: 'محامي',
  head: 'رئيس قسم',
  subhead: 'رئيس شعبة',
  manager: 'مدير',
  admin: 'مشرف نظام',
  entitymanager: 'مندوب جهة',
};

/**
 * سطر النطاق تحت الاسم (الشريط الجانبي + الحساب): الدور — الفرع، ورئيس
 * الشعبة بشعبته («رئيس شعبة جبلة — فرع اللاذقية») من `sectionName` المدقق
 * خلفيًا — لا يُبنى من مدخلات العميل إطلاقًا.
 */
export function formatRoleScope(user: {
  role: Role | string;
  branchName?: string | null;
  sectionName?: string | null;
} | null | undefined): string {
  if (!user) return '';
  const label = (ROLE_LABELS as Record<string, string>)[user.role] ?? user.role;
  const branch = user.branchName || 'كل الفروع';
  if (user.role === 'subhead' && user.sectionName) return `${label} ${user.sectionName} — ${branch}`;
  return `${label} — ${branch}`;
}
