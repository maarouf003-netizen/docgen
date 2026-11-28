import { describe, expect, it } from 'vitest';
import { nextTabIndex, rovingTabIndex } from './useTabListKeyboard';

describe('useTabListKeyboard — حساب التبويب التالي', () => {
  it('في RTL: السهم الأيسر «التالي» والأيمن «السابق» مع الدوران', () => {
    expect(nextTabIndex('ArrowLeft', 0, 3)).toBe(1);
    expect(nextTabIndex('ArrowLeft', 2, 3)).toBe(0);
    expect(nextTabIndex('ArrowRight', 0, 3)).toBe(2);
    expect(nextTabIndex('ArrowRight', 1, 3)).toBe(0);
  });

  it('Home/End ينتقلان إلى الأول/الأخير', () => {
    expect(nextTabIndex('Home', 2, 4)).toBe(0);
    expect(nextTabIndex('End', 0, 4)).toBe(3);
  });

  it('أي مفتاح آخر (أو قائمة فارغة/فهرس خارجها) بلا تنقّل', () => {
    expect(nextTabIndex('Tab', 0, 3)).toBeNull();
    expect(nextTabIndex('Enter', 0, 3)).toBeNull();
    expect(nextTabIndex('ArrowLeft', 0, 0)).toBeNull();
    expect(nextTabIndex('ArrowLeft', -1, 3)).toBeNull();
  });

  it('roving tabindex: عنصر واحد فقط (النشط) قابل لـTab', () => {
    expect(rovingTabIndex(true)).toBe(0);
    expect(rovingTabIndex(false)).toBe(-1);
  });
});
