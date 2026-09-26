import { describe, it, expect } from 'vitest';
import { getHomeForRole } from './roleHome';

describe('getHomeForRole', () => {
  it('يوجّه مندوب الجهة إلى بوابته القرائية لا إلى لوحة تحكم', () => {
    expect(getHomeForRole('entitymanager')).toBe('/portal/stats');
  });

  it('يوجّه بقية الأدوار إلى لوحة التحكم', () => {
    for (const role of ['lawyer', 'head', 'manager', 'admin'] as const) {
      expect(getHomeForRole(role)).toBe('/');
    }
  });

  it('غياب الدور يُعامل كضيف على اللوحة الافتراضية', () => {
    expect(getHomeForRole(undefined)).toBe('/');
  });
});
