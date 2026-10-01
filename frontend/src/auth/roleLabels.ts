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
  manager: 'مدير',
  admin: 'مشرف نظام',
  entitymanager: 'مندوب جهة',
};
