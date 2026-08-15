import { describe, it, expect } from 'vitest';
import { governorateFromBranch } from './governorate';

describe('governorateFromBranch', () => {
  it('يستخرج المحافظة من اسم الفرع عندما تحمل اسم محافظة', () => {
    expect(governorateFromBranch('الفرع الرئيسي - دمشق')).toBe('دمشق');
    expect(governorateFromBranch('فرع حلب')).toBe('حلب');
    expect(governorateFromBranch('دمشق')).toBe('دمشق');
    expect(governorateFromBranch('الفرع الرئيسي - اللاذقية')).toBe('اللاذقية');
  });

  it('يُطابق «ريف دمشق» قبل «دمشق» عند تضمن اسم الفرع لكلاهما', () => {
    expect(governorateFromBranch('فرع ريف دمشق')).toBe('ريف دمشق');
  });

  it('يُعرّف الصيغ البديلة الشائعة ويعيد التسمية الرسمية', () => {
    expect(governorateFromBranch('فرع حماه')).toBe('حماة');
  });

  it('يعيد نصًا فارغًا عندما لا يحمل اسم الفرع أي محافظة معروفة', () => {
    expect(governorateFromBranch('فرع المزة')).toBe('');
    expect(governorateFromBranch('')).toBe('');
  });

  it('يعيد نصًا فارغًا للقيم الخالية', () => {
    expect(governorateFromBranch(null)).toBe('');
    expect(governorateFromBranch(undefined)).toBe('');
  });
});
