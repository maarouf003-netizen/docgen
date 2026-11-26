import { describe, expect, it } from 'vitest';
import type { DocumentInterventionUpsertRequest, PublicEntityEntryDto } from '../../../types';
import { emptyIntervention } from '../documentFormConstants';
import {
  buildInterventionPayload,
  interventionAddressSuppressed,
  validateIntervention,
  withHeirAdded,
  withHeirSet,
  withInterventionNature,
  withRegistryPick,
  withRepresentativeAdded,
  withRepresentativeRemoved,
} from './interventionForm';

function form(overrides: Partial<DocumentInterventionUpsertRequest> = {}): DocumentInterventionUpsertRequest {
  return {
    ...emptyIntervention(),
    nature: 'natural',
    name: 'سامر',
    receiptDate: '1/8/2026',
    ...overrides,
  };
}

function registryEntry(): PublicEntityEntryDto {
  return {
    id: 11,
    groupId: 2,
    canonicalName: 'وزارة العدل',
    entityType: 'ministry',
    governorate: 'دمشق',
    branchName: 'مديرية التنفيذ',
    citationFormula: 'add-to-job',
    status: 'final',
    isActive: true,
    createdAt: '2026-08-01T00:00:00Z',
    aliases: [],
  };
}

describe('interventionForm — الحمولة', () => {
  it('يطبّع الأرقام العربية ويقصّ الفراغات ولا يرسل حقول صفة أخرى', () => {
    const payload = buildInterventionPayload({
      form: form({
        name: '  سامر  ',
        receiptNumber: '١٢٣',
        receiptDate: '١/٨/٢٠٢٦',
        acceptanceDate: ' 28/7/2026 ',
        subject: ' موضوع ',
        notes: ' ملاحظة ',
      }),
      showRepresentative: false,
      addressSuppressed: false,
    });

    expect(payload).toMatchObject({
      nature: 'natural',
      name: 'سامر',
      receiptNumber: '123',
      receiptDate: '1/8/2026',
      acceptanceDate: '28/7/2026',
      subject: 'موضوع',
      notes: 'ملاحظة',
    });
    expect(payload).not.toHaveProperty('registryId');
    expect(payload).not.toHaveProperty('entityName');
  });

  it('يصفّر العنوان ويُفرغ الورثة عند وجود ممثل شرعي', () => {
    const payload = buildInterventionPayload({
      form: form({
        addressType: 'موطن مختار',
        address: 'دمشق',
        representativeName: ' خالد ',
        heirs: [{ name: 'وليد', father: '', family: '', capacity: 'أصالة', addressType: 'عنوان', address: '' }],
      }),
      showRepresentative: true,
      addressSuppressed: true,
    });

    expect(payload).toMatchObject({
      addressType: '',
      address: '',
      representativeName: 'خالد',
      heirs: [],
    });
  });

  it('يحذف بيانات الممثل كليًا حين لا يكون مفعّلًا (إعادة تعيين الحالة)', () => {
    const payload = buildInterventionPayload({
      form: form({ representativeName: 'خالد', representativeCapacity: 'ولي' }),
      showRepresentative: false,
      addressSuppressed: false,
    });

    expect(payload).toMatchObject({
      representativeName: '',
      representativeCapacity: '',
      heirs: [],
    });
  });

  it('يبني حمولة الشخص الاعتباري بحقوله وحده', () => {
    const payload = buildInterventionPayload({
      form: form({
        nature: 'legal',
        entityName: ' شركة النور ',
        registrationNumber: '77',
        representedBy: ' المدير ',
        governorate: 'دمشق',
        addressType: 'مركز عمل',
        address: 'المزة',
      }),
      showRepresentative: false,
      addressSuppressed: false,
    });

    expect(payload).toMatchObject({
      nature: 'legal',
      entityName: 'شركة النور',
      registrationNumber: '77',
      representedBy: 'المدير',
      governorate: 'دمشق',
      addressType: 'مركز عمل',
      address: 'المزة',
    });
    expect(payload).not.toHaveProperty('name');
    expect(payload).not.toHaveProperty('registryId');
  });

  it('يبني حمولة الجهة العامة بمعرّف قيد السجل', () => {
    const payload = buildInterventionPayload({
      form: form({
        nature: 'public',
        entityName: 'وزارة العدل',
        entityBranch: 'مديرية التنفيذ',
        governorate: 'دمشق',
        registryId: 11,
      }),
      version: 3,
      showRepresentative: false,
      addressSuppressed: false,
    });

    expect(payload).toMatchObject({
      nature: 'public',
      entityName: 'وزارة العدل',
      entityBranch: 'مديرية التنفيذ',
      governorate: 'دمشق',
      registryId: 11,
      version: 3,
    });
    expect(payload).not.toHaveProperty('addressType');
  });
});

describe('interventionForm — التحقق', () => {
  it('يقبل النموذج الصالح', () => {
    expect(validateIntervention(form(), false)).toBe('');
  });

  it('يرفض الجهة العامة بلا اختيار من السجل', () => {
    expect(validateIntervention(form({ nature: 'public', registryId: null }), false)).toBe(
      'اختر جهة عامة صالحة من السجل المرجعي',
    );
  });

  it('يرفض الاسم الفارغ/الفراغ حسب الصفة', () => {
    expect(validateIntervention(form({ name: '   ' }), false)).toBe('اسم المتدخل مطلوب');
    expect(validateIntervention(form({ nature: 'legal', entityName: ' ' }), false)).toBe(
      'اسم الشخص الاعتباري مطلوب',
    );
  });

  it('يلزم تاريخ الورود ثم اسم الممثل بالترتيب نفسه', () => {
    expect(validateIntervention(form({ receiptDate: ' ' }), true)).toBe(
      'تاريخ ورود طلب التدخل مطلوب',
    );
    expect(
      validateIntervention(form({ representativeName: ' ' }), true),
    ).toBe('اسم الممثل الشرعي مطلوب عند إضافة ممثل');
  });
});

describe('interventionForm — انتقالات الحالة', () => {
  it('تبديل الصفة يبقي بيانات الطلب ويصفّر حقول الطرف', () => {
    const next = withInterventionNature(
      form({
        nature: 'natural',
        name: 'سامر',
        addressType: 'موطن مختار',
        address: 'دمشق',
        receiptNumber: '9',
        receiptDate: '1/8/2026',
        acceptanceDate: '28/7/2026',
        subject: 'موضوع',
        notes: 'ملاحظة',
      }),
      'legal',
    );

    expect(next).toMatchObject({
      nature: 'legal',
      receiptNumber: '9',
      receiptDate: '1/8/2026',
      acceptanceDate: '28/7/2026',
      subject: 'موضوع',
      notes: 'ملاحظة',
    });
    expect(next.name).toBe('');
    expect(next.address).toBe('');
  });

  it('تفعيل الممثل يفرّغ الورثة والعنوان ويضع قيمًا افتراضية للممثل', () => {
    const next = withRepresentativeAdded(
      form({
        addressType: 'موطن مختار',
        address: 'دمشق',
        heirs: [{ name: 'وليد', father: '', family: '', capacity: 'أصالة', addressType: 'عنوان', address: '' }],
      }),
    );

    expect(next.heirs).toEqual([]);
    expect(next.addressType).toBe('');
    expect(next.address).toBe('');
    expect(next.representativeCapacity).toBe('ولي');
    expect(next.representativeAddressType).toBe('عنوان');
  });

  it('إزالة الممثل تمسح كل حقوله', () => {
    const next = withRepresentativeRemoved(
      form({
        representativeName: 'خالد',
        representativeFather: 'أحمد',
        representativeFamily: 'الحسن',
        representativeCapacity: 'ولي',
        representativeAddressType: 'عنوان',
        representativeAddress: 'دمشق',
      }),
    );

    expect(next).toMatchObject({
      representativeName: '',
      representativeFather: '',
      representativeFamily: '',
      representativeCapacity: '',
      representativeAddressType: '',
      representativeAddress: '',
    });
  });

  it('أول وريث مسمّى يصفّر عنوان المتدخل', () => {
    const added = withHeirAdded(form({ addressType: 'موطن مختار', address: 'دمشق' }));
    expect(added.heirs).toHaveLength(1);

    const named = withHeirSet(added, 0, 'name', 'وليد');
    expect(named.addressType).toBe('');
    expect(named.address).toBe('');
    expect(named.heirs?.[0].name).toBe('وليد');
  });

  it('اختيار جهة من السجل يملأ الحقول ويحفظ المعرّف', () => {
    const next = withRegistryPick(form({ nature: 'public' }), registryEntry());

    expect(next).toMatchObject({
      entityName: 'وزارة العدل',
      entityBranch: 'مديرية التنفيذ',
      governorate: 'دمشق',
      registryId: 11,
    });
  });

  it('يكشف كبت العنوان بوجود الممثل أو وريث مسمّى فقط', () => {
    expect(interventionAddressSuppressed(form(), false)).toBe(false);
    expect(interventionAddressSuppressed(form(), true)).toBe(true);
    const withHeir = withHeirSet(withHeirAdded(form()), 0, 'name', 'وليد');
    expect(interventionAddressSuppressed(withHeir, false)).toBe(true);
  });
});
