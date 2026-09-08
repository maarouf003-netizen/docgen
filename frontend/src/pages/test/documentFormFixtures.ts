import type { DocumentResponse } from '../../types';

// بيانات خام مشتركة بين ملفات اختبار DocumentForm الموزعة.
// يُحظر وضع أي vi.mock/vi.hoisted هنا — هذه البيانات فقط (غير اختبارية بلا محاكاة).

export const mockDoc: DocumentResponse = {
  id: 1,
  createdAt: '2026-07-31',
  updatedAt: '2026-07-31',
  isDraft: false,
  documentType: 'سند دين',
  borrowerName: 'أحمد',
  borrowerFather: 'محمد',
  borrowerFamily: 'الخطيب',
  amountNumeric: 0,
  amount2Numeric: 0,
  amount3Numeric: 0,
  inclusionAmountNumeric: 0,
  inclusionAmount2Numeric: 0,
  inclusionAmount3Numeric: 0,
  viewCount: 0,
  printCount: 0,
  guarantors: [],
  assets: [],
  executionApplicants: [],
  executedPublicEntities: [],
  executedNaturalPersons: [],
};

export const docWithHeirs: DocumentResponse = {
  ...mockDoc,
  borrowerHeirs: [{ id: 10, name: 'محمود الحلبي', addressType: 'عنوان', address: 'المزة' }],
  guarantors: [
    {
      id: 5,
      guarantorNumber: 1,
      name: 'سمير',
      father: 'حسن',
      family: 'علي',
      address: 'حلب',
      addressType: 'موطن مختار',
      heirs: [{ id: 11, name: 'فارس الخالد', addressType: 'وكيل', address: 'المحامي سامر' }],
    },
  ],
};

export const docWithOldOwner: DocumentResponse = {
  ...mockDoc,
  assets: [
    {
      id: 7,
      assetKind: 'عقار',
      owners: ['سمير حسن علي'],
      property: 'منزل',
      propertyNumber: '12',
      propertyDistrict: 'المزة',
      landRegistry: 'سجل 3',
      shareType: 'تمام العقار',
    },
  ],
};

export const docWithInvalidShare: DocumentResponse = {
  ...mockDoc,
  assets: [
    {
      id: 8,
      assetKind: 'عقار',
      owners: ['سمير حسن علي', 'أحمد محمد خالد'],
      property: 'منزل',
      propertyNumber: '12',
      propertyDistrict: 'المزة',
      landRegistry: 'سجل 3',
      shareType: 'تمام العقار',
    },
  ],
};
