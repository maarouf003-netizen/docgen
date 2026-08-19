/** قيم حالات الإنابة الصادرة من الخلفية — الأصل الوحيد للحرفيات في الواجهة. */
export const DELEGATION_STATUS_PENDING = 'بانتظار رئيس القسم';
export const DELEGATION_STATUS_ASSIGNED = 'محالة';
export const DELEGATION_STATUS_REGISTERED = 'مسجلة أصولًا';
export const DELEGATION_STATUS_EXECUTED = 'منفذ إنابة';

export type DelegationStatus = typeof DELEGATION_STATUS_PENDING | typeof DELEGATION_STATUS_ASSIGNED | typeof DELEGATION_STATUS_REGISTERED | typeof DELEGATION_STATUS_EXECUTED;

export const DELEGATION_STATUS_BADGES: Record<DelegationStatus, { text: string; cls: string }> = {
  [DELEGATION_STATUS_PENDING]: { text: DELEGATION_STATUS_PENDING, cls: 'bg-amber-100 text-amber-700' },
  [DELEGATION_STATUS_ASSIGNED]: { text: DELEGATION_STATUS_ASSIGNED, cls: 'bg-blue-100 text-blue-700' },
  [DELEGATION_STATUS_REGISTERED]: { text: DELEGATION_STATUS_REGISTERED, cls: 'bg-violet-100 text-violet-700' },
  [DELEGATION_STATUS_EXECUTED]: { text: DELEGATION_STATUS_EXECUTED, cls: 'bg-green-100 text-green-700' },
};

export const DELEGATION_STATUS_ORDER: DelegationStatus[] = [
  DELEGATION_STATUS_PENDING,
  DELEGATION_STATUS_ASSIGNED,
  DELEGATION_STATUS_REGISTERED,
  DELEGATION_STATUS_EXECUTED,
];

/** شارة الحالة المألوفة للإنابة (تعرض الحالة كما هي إن لم تكن معروفة، حفاظًا على المعلومات). */
export function delegationStatusBadge(status: string | undefined): { text: string; cls: string } {
  return (
    DELEGATION_STATUS_BADGES[status as DelegationStatus] ?? {
      text: status || 'غير معروفة',
      cls: 'bg-gray-200 text-gray-700',
    }
  );
}

/** هل الإنابة لا تزال معلّقة (قابلة للتعديل والحذف من محامي الملف المنيب)؟ */
export function isDelegationPending(status: string | undefined): boolean {
  return status === DELEGATION_STATUS_PENDING;
}