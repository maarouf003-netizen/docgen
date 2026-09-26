import { describe, it, expect } from 'vitest';
import { PARENT_BRANCH_NAME, publicEntityBranchLabel } from './publicEntityBranchLabel';

/**
 * الحالات هنا مرآة حرفية لـ `PublicEntityBranchCatalogTests` في الخلفية
 * (`backend/tests/DocGenerator.Application.Tests/PublicEntityBranchCatalogTests.cs`).
 * التقارب مقصود: هو ما يجعل تغيّر الدلالة في إحدى الجهتين يفشل اختبار الأخرى
 * بدل أن تختلف صيغة العمود عن صيغة الواجهة بصمت.
 */
describe('publicEntityBranchLabel', () => {
  it('يعرض «المحافظة/الفرع» لفرع عادي', () => {
    expect(publicEntityBranchLabel('دمشق', 'فرع 1')).toBe('دمشق/فرع 1');
    expect(publicEntityBranchLabel('دمشق', 'حلب')).toBe('دمشق/حلب');
  });

  it('يعرض المحافظة وحدها للجهة الأم (بلا تكرار فرعي)', () => {
    expect(publicEntityBranchLabel('دمشق', 'الجهة الأم')).toBe('دمشق');
  });

  it('يعرض المحافظة وحدها لفرع فارغ أو فراغ', () => {
    expect(publicEntityBranchLabel('دمشق', '')).toBe('دمشق');
    expect(publicEntityBranchLabel('دمشق', null)).toBe('دمشق');
    expect(publicEntityBranchLabel('دمشق', undefined)).toBe('دمشق');
    expect(publicEntityBranchLabel('دمشق', '   ')).toBe('دمشق');
  });

  it('يعرض اسم الفرع وحده حين لا محافظة (بلا «/» معلّقة)', () => {
    expect(publicEntityBranchLabel('', 'فرع 1')).toBe('فرع 1');
    expect(publicEntityBranchLabel(null, 'فرع 1')).toBe('فرع 1');
  });

  it('يعرض نصًا فارغًا حين لا محافظة ولا فرع', () => {
    expect(publicEntityBranchLabel('', '')).toBe('');
    expect(publicEntityBranchLabel(null, null)).toBe('');
    expect(publicEntityBranchLabel(null, undefined)).toBe('');
  });

  it('يقرلم الطرفين فلا تظهر مسافات طرفية في التسمية', () => {
    expect(publicEntityBranchLabel(' دمشق ', ' فرع 1 ')).toBe('دمشق/فرع 1');
    expect(publicEntityBranchLabel('  ', 'فرع 1')).toBe('فرع 1');
  });

  it('يعامل «الجهة الأم» المجرَّمة كفرع أم (بلا لاحقة فرعية)', () => {
    expect(publicEntityBranchLabel('دمشق', ` ${PARENT_BRANCH_NAME} `)).toBe('دمشق');
  });

  it('يثبّت اسم الجهة الأم الحرفيًّا (مرآة الخلفية)', () => {
    expect(PARENT_BRANCH_NAME).toBe('الجهة الأم');
  });
});
