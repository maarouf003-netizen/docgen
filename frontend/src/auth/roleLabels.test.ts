import { describe, it, expect } from 'vitest';
import { ROLE_LABELS, formatRoleScope } from './roleLabels';

describe('formatRoleScope', () => {
  it('رئيس الشعبة بشعبته وفرعه', () => {
    expect(
      formatRoleScope({ role: 'subhead', branchName: 'فرع اللاذقية', sectionName: 'جبلة' }),
    ).toBe('رئيس شعبة جبلة — فرع اللاذقية');
  });

  it('رئيس الشعبة بلا اسم شعبة يسقط للصيغة العامة', () => {
    expect(formatRoleScope({ role: 'subhead', branchName: 'فرع اللاذقية' })).toBe(
      'رئيس شعبة — فرع اللاذقية',
    );
  });

  it('بقية الأدوار: الدور — الفرع', () => {
    expect(formatRoleScope({ role: 'head', branchName: 'فرع حماة' })).toBe('رئيس قسم — فرع حماة');
    expect(formatRoleScope({ role: 'lawyer', branchName: null })).toBe('محامي — كل الفروع');
    expect(formatRoleScope(null)).toBe('');
  });

  it('الأدوار المجهولة تُعرض خامًا بدل الكسر', () => {
    expect(formatRoleScope({ role: 'ghost', branchName: 'دمشق' })).toBe('ghost — دمشق');
  });

  it('تسميات الأدوار الرسمية ثابتة', () => {
    expect(ROLE_LABELS.subhead).toBe('رئيس شعبة');
  });
});
