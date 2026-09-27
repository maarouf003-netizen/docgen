import { describe, expect, it } from 'vitest';
import {
  APPEAL_DIRECTION_AGAINST_US,
  APPEAL_DIRECTION_APPELLANTS,
  APPEAL_OUTCOME_AGAINST,
  APPEAL_OUTCOME_IN_FAVOR,
  APPEAL_STATUS_DECIDED,
  APPEAL_STATUS_PENDING,
  APPEAL_STATUS_STRUCK_OFF,
  appealDirectionLabel,
  appealOutcomeCls,
  appealOutcomeLabel,
  appealStatusBadge,
  appealStatusLabel,
} from './appealStatus';

describe('appealStatusLabel', () => {
  it('يرجع التسميات العربية للحالات المعروفة', () => {
    expect(appealStatusLabel(APPEAL_STATUS_PENDING)).toBe('منظور');
    expect(appealStatusLabel(APPEAL_STATUS_DECIDED)).toBe('محسوم');
    expect(appealStatusLabel(APPEAL_STATUS_STRUCK_OFF)).toBe('مشطوب');
  });

  it('المجهول يُعرض خامه والغائب شرطة (لا «منظور» مخترعة)', () => {
    expect(appealStatusLabel(undefined)).toBe('—');
    expect(appealStatusLabel('')).toBe('—');
    expect(appealStatusLabel('قيمة غريبة')).toBe('قيمة غريبة');
  });
});

describe('appealStatusBadge', () => {
  it('منظور حمراء ومحسوم خضراء ومشطوب رمادية', () => {
    const pending = appealStatusBadge(APPEAL_STATUS_PENDING);
    expect(pending.text).toBe('منظور');
    expect(pending.cls).toContain('bg-red-100');

    const decided = appealStatusBadge(APPEAL_STATUS_DECIDED);
    expect(decided.text).toBe('محسوم');
    expect(decided.cls).toContain('bg-green-100');

    const struck = appealStatusBadge(APPEAL_STATUS_STRUCK_OFF);
    expect(struck.text).toBe('مشطوب');
    expect(struck.cls).toContain('bg-gray-200');
  });

  it('الحالة الغائبة شرطة محايدة والمجهولة بخامها', () => {
    expect(appealStatusBadge(undefined).text).toBe('—');
    const unknown = appealStatusBadge('قيمة غريبة');
    expect(unknown.text).toBe('قيمة غريبة');
    expect(unknown.cls).toContain('bg-gray-100');
  });
});

describe('appealDirectionLabel', () => {
  it('يميز الاتجاهين والمجهول خامه والغائب شرطة', () => {
    expect(appealDirectionLabel(APPEAL_DIRECTION_APPELLANTS)).toBe('مستأنِفين');
    expect(appealDirectionLabel(APPEAL_DIRECTION_AGAINST_US)).toBe('مستأنف علينا');
    expect(appealDirectionLabel(undefined)).toBe('—');
    expect(appealDirectionLabel('اتجاه غريب')).toBe('اتجاه غريب');
  });
});

describe('appealOutcomeLabel / appealOutcomeCls', () => {
  it('للصالح أخضر وللضد أحمر والغائب شرطة', () => {
    expect(appealOutcomeLabel(APPEAL_OUTCOME_IN_FAVOR)).toBe('للصالح');
    expect(appealOutcomeLabel(APPEAL_OUTCOME_AGAINST)).toBe('للضد');
    expect(appealOutcomeLabel(undefined)).toBe('—');

    expect(appealOutcomeCls(APPEAL_OUTCOME_IN_FAVOR)).toContain('text-green-700');
    expect(appealOutcomeCls(APPEAL_OUTCOME_AGAINST)).toContain('text-red-700');
    expect(appealOutcomeCls(undefined)).toBe('');
  });
});
