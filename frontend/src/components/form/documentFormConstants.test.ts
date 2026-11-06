import { describe, it, expect } from 'vitest';
import {
  MAX_GUARANTORS,
  MAX_TOTAL_DEBTORS,
  guarantorAddState,
  hasStoredRepresentative,
  nextGuarantorNumber,
  representativeHeirConflictMessage,
  toExecutedHeirPayload,
  toHeirPayload,
} from './documentFormConstants';

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

describe('documentFormConstants · حمولة الوريث والممثل', () => {
  it('بلا ممثل: لا تُرسل مفاتيح الممثل، ويبقى عنوان الوريث', () => {
    const payload = toHeirPayload({ name: 'محمود', addressType: 'عنوان', address: 'المزة' });
    expect(payload).toEqual({ name: 'محمود', addressType: 'عنوان', address: 'المزة' });
    expect('representativeName' in payload).toBe(false);
  });

  it('مع ممثل: يُصفَّر عنوان الوريث وتُرسل حقول الممثل كاملة', () => {
    const payload = toHeirPayload({
      name: 'محمود',
      addressType: 'عنوان',
      address: 'المزة',
      representativeName: 'سمير',
      representativeFather: 'حسن',
      representativeFamily: 'علي',
      representativeCapacity: 'ولي',
      representativeAddressType: 'عنوان',
      representativeAddress: 'حلب',
    });
    expect(payload.addressType).toBe('');
    expect(payload.address).toBe('');
    expect(payload.representativeName).toBe('سمير');
    expect(payload.representativeAddress).toBe('حلب');
  });

  it('نفس القاعدة لوريث وضع «منفذ عليه»', () => {
    const payload = toExecutedHeirPayload({
      heirName: 'وريث',
      addressType: 'عنوان',
      heirAddress: 'المزة',
      representativeName: 'سمير',
      representativeCapacity: 'وصي',
      representativeAddressType: 'موطن مختار',
      representativeAddress: 'حلب',
    });
    expect(payload.addressType).toBe('');
    expect(payload.heirAddress).toBe('');
    expect(payload.representativeName).toBe('سمير');
    expect(('representativeName' in toExecutedHeirPayload({ heirName: 'وريث' }))).toBe(false);
  });

  it('يكشف تعارض ممثل المقترض مع ورثته', () => {
    expect(representativeHeirConflictMessage({
      borrowerNature: 'natural',
      borrowerHasHeirs: true,
      borrowerHasRep: true,
      guarantors: [],
      executionApplicants: [],
      executedNaturalPersons: [],
    })).toContain('المقترض');
  });

  it('يكشف تعارض ممثل الكفيل مع ورثته', () => {
    expect(representativeHeirConflictMessage({
      borrowerHasHeirs: false,
      borrowerHasRep: false,
      guarantors: [{ guarantorNumber: 2, representativeName: 'ممثل', heirs: [{ name: 'وريث' }] }],
      executionApplicants: [],
      executedNaturalPersons: [],
    })).toContain('الكفيل 2');
  });

  it('يكشف تعارض ممثل طالب التنفيذ مع التمثيل بالتركة', () => {
    expect(representativeHeirConflictMessage({
      borrowerHasHeirs: false,
      borrowerHasRep: false,
      guarantors: [],
      executionApplicants: [{ name: 'طالب', representationType: 'إضافة لتركة', representativeName: 'ممثل' }],
      executedNaturalPersons: [],
    })).toContain('طالب التنفيذ طالب');
  });

  it('يسمح بممثل بلا ورثة', () => {
    expect(representativeHeirConflictMessage({
      borrowerHasHeirs: false,
      borrowerHasRep: true,
      guarantors: [],
      executionApplicants: [],
      executedNaturalPersons: [],
    })).toBeNull();
  });
});

describe('documentFormConstants · حالة زر الإضافة', () => {
  const row = (n: number) => ({ guarantorNumber: n });

  it('العادي: العدد وحده هو الحد (الترقيم موضعي عند الحفظ)', () => {
    expect(guarantorAddState([], true)).toEqual({ canAdd: true, remaining: 49 });
    expect(guarantorAddState(Array.from({ length: 49 }, (_, i) => row(i + 1)), true)).toEqual({
      canAdd: false,
      remaining: 0,
    });
  });

  it('المصرفي المتصل: كالعادي', () => {
    expect(guarantorAddState([row(1), row(2), row(3)], false)).toEqual({
      canAdd: true,
      remaining: 46,
    });
  });

  it('المصرفي ذو الفجوات: الرقم المستنفَد يمنع الإضافة قبل امتلاء الصفوف', () => {
    // حذف أوسط متكرر يدفع max إلى 49 وطول القائمة أقل — الرقم التالي 50 بلا خانة.
    const gapped = [row(48), row(49)];
    expect(nextGuarantorNumber(gapped)).toBe(50);
    expect(guarantorAddState(gapped, false)).toEqual({ canAdd: false, remaining: 0 });
  });

  it('المصرفي: المتبقي هو min(الصفوف، الأرقام)', () => {
    // أرقام {45،46،47}: الصفوف تسمح بـ46 إضافة لكن الأرقام باثنتين فقط.
    expect(guarantorAddState([row(45), row(46), row(47)], false)).toEqual({
      canAdd: true,
      remaining: 2,
    });
  });
});
