import { describe, it, expect } from 'vitest';
import { delegationStatusBadge, isDelegationPending } from './delegationStatus';

describe('delegationStatusBadge', () => {
  it('يعطي شارة الحالة العربية المعتمدة لكل حالة', () => {
    expect(delegationStatusBadge('بانتظار رئيس القسم')).toEqual({
      text: 'بانتظار رئيس القسم',
      cls: 'bg-amber-100 text-amber-700',
    });
    expect(delegationStatusBadge('محالة')).toEqual({
      text: 'محالة',
      cls: 'bg-blue-100 text-blue-700',
    });
    expect(delegationStatusBadge('مسجلة أصولًا')).toEqual({
      text: 'مسجلة أصولًا',
      cls: 'bg-violet-100 text-violet-700',
    });
    expect(delegationStatusBadge('منفذ إنابة')).toEqual({
      text: 'منفذ إنابة',
      cls: 'bg-green-100 text-green-700',
    });
  });

  it('يعرض الحالة كما هي دون كسر عند قيمة غير معروفة أو فارغة', () => {
    expect(delegationStatusBadge('حالة مستقبلية')).toEqual({
      text: 'حالة مستقبلية',
      cls: 'bg-gray-200 text-gray-700',
    });
    expect(delegationStatusBadge(undefined)).toEqual({
      text: 'غير معروفة',
      cls: 'bg-gray-200 text-gray-700',
    });
    expect(delegationStatusBadge('')).toEqual({
      text: 'غير معروفة',
      cls: 'bg-gray-200 text-gray-700',
    });
  });
});

describe('isDelegationPending', () => {
  it('يرجّع true للمعلّقة فقط', () => {
    expect(isDelegationPending('بانتظار رئيس القسم')).toBe(true);
    expect(isDelegationPending('محالة')).toBe(false);
    expect(isDelegationPending('مسجلة أصولًا')).toBe(false);
    expect(isDelegationPending('منفذ إنابة')).toBe(false);
    expect(isDelegationPending(undefined)).toBe(false);
  });
});
