import { describe, expect, it } from 'vitest';
import { normalizeUsernameInput } from './usernameInput';

describe('normalizeUsernameInput', () => {
  it('يحذف محارف اتجاه RTL/LTR غير المرئية', () => {
    expect(normalizeUsernameInput('recoveryadmin\u2069')).toBe('recoveryadmin');
    expect(normalizeUsernameInput('\u200Brecovery\u200F')).toBe('recovery');
    expect(normalizeUsernameInput('\uFEFFlawyer1\u2066')).toBe('lawyer1');
  });

  it('يقصّ المسافات الطرفية ويحافظ على الحروف العربية', () => {
    expect(normalizeUsernameInput('  lawyer1  ')).toBe('lawyer1');
    expect(normalizeUsernameInput('محامي دمشق')).toBe('محامي دمشق');
  });

  it('يعيد سلسلة فارغة للمدخل الفارغ', () => {
    expect(normalizeUsernameInput('')).toBe('');
    expect(normalizeUsernameInput('   ')).toBe('');
  });
});
