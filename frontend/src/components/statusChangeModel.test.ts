import { describe, expect, it } from 'vitest';
import { COLLECTED_AMOUNT_KEYS, COLLECTED_CURRENCY_KEYS, emptyFields } from './statusChangeModel';
import { SUB_STATUS_FULL } from '../utils/documentStatus';

describe('statusChangeModel', () => {
  it('يعيد الحالة الابتدائية بعملاتها الافتراضية الثلاث', () => {
    const fields = emptyFields();
    expect(fields.collectedCurrency).toBe('ليرة سورية');
    expect(fields.collectedCurrency2).toBe('دولار أمريكي');
    expect(fields.collectedCurrency3).toBe('يورو');
    expect(fields.execSubStatus).toBe(SUB_STATUS_FULL);
    expect(fields.soldAssetIds).toEqual([]);
  });

  it('يحاذي مفاتيح المبالغ والعملات الثلاثة', () => {
    expect(COLLECTED_AMOUNT_KEYS).toHaveLength(3);
    expect(COLLECTED_CURRENCY_KEYS).toHaveLength(3);
  });
});
