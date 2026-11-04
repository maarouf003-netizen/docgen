import { describe, it, expect } from 'vitest';
import { hasFullAccessRole, isHeadOrSubHeadRole, isLawyerRole } from './roles';
import type { Role } from '../types/common';

const ROLES: Role[] = ['lawyer', 'head', 'subhead', 'manager', 'admin', 'entitymanager'];

describe('مساعدات الأدوار', () => {
  it('isLawyerRole: المحامي فقط', () => {
    for (const role of ROLES) {
      expect(isLawyerRole(role)).toBe(role === 'lawyer');
    }
  });

  it('isHeadOrSubHeadRole: رئيس القسم ورئيس الشعبة فقط', () => {
    for (const role of ROLES) {
      expect(isHeadOrSubHeadRole(role)).toBe(role === 'head' || role === 'subhead');
    }
  });

  it('hasFullAccessRole: المدير ومشرف النظام فقط', () => {
    for (const role of ROLES) {
      expect(hasFullAccessRole(role)).toBe(role === 'manager' || role === 'admin');
    }
  });

  it('كل المساعدات تعيد false للقيم الفارغة', () => {
    for (const role of [null, undefined] as const) {
      expect(isLawyerRole(role)).toBe(false);
      expect(isHeadOrSubHeadRole(role)).toBe(false);
      expect(hasFullAccessRole(role)).toBe(false);
    }
  });
});
