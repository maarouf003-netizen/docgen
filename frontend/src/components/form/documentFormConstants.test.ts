import { describe, it, expect } from 'vitest';
import { MAX_GUARANTORS, MAX_TOTAL_DEBTORS, hasStoredRepresentative } from './documentFormConstants';

describe('documentFormConstants · سقوف الأطراف', () => {
  it('يطابق الخلفية PartyLimitsCatalog: إجمالي 50 مدينًا شاملًا المقترض', () => {
    expect(MAX_TOTAL_DEBTORS).toBe(50);
    expect(MAX_GUARANTORS).toBe(49);
    expect(MAX_TOTAL_DEBTORS).toBe(MAX_GUARANTORS + 1);
  });
});

describe('documentFormConstants · كشف الممثل المخزَّن', () => {
  it('الصفة وحدها (تفعيل الزر بلا اسم) ليست مخزَّنة — تطابق IsEmptyRepresentative الخلفي', () => {
    // الصفة تُمرَّر من النموذج لكن التوقيع لا يقبلها: تفعيل الزر وحده لا يُخزَّن.
    const capacityOnly = {
      representativeName: '',
      representativeFather: '',
      representativeFamily: '',
      representativeCapacity: 'ولي',
    };
    expect(hasStoredRepresentative(capacityOnly)).toBe(false);
    expect(hasStoredRepresentative({})).toBe(false);
  });

  it('الاسم الثلاثي (ولو جزئيًا) مخزَّن', () => {
    expect(hasStoredRepresentative({ representativeName: 'سمير' })).toBe(true);
    expect(hasStoredRepresentative({ representativeFather: 'حسن' })).toBe(true);
    expect(hasStoredRepresentative({ representativeFamily: 'علي' })).toBe(true);
  });
});
