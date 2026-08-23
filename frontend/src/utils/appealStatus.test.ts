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

  it('يعامل الحالة الفارغة وغير المعروفة منظورًا', () => {
    expect(appealStatusLabel(undefined)).toBe('منظور');
    expect(appealStatusLabel('قيمة غريبة')).toBe('منظور');
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

  it('الحالة الفارغة تعامل منظورًا', () => {
    expect(appealStatusBadge(undefined).text).toBe('منظور');
  });
});

describe('appealDirectionLabel', () => {
  it('يميز الاتجاهين', () => {
    expect(appealDirectionLabel(APPEAL_DIRECTION_APPELLANTS)).toBe('مستأنِفين');
    expect(appealDirectionLabel(APPEAL_DIRECTION_AGAINST_US)).toBe('مستأنف علينا');
    expect(appealDirectionLabel(undefined)).toBe('مستأنِفين');
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
