import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import DocumentView from './DocumentView';
import type { DocumentResponse } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/client')>();
  return {
    ...original,
    api: {
      get: vi.fn(),
      post: vi.fn(),
      delete: vi.fn(),
    },
  };
});

import { api } from '../api/client';

const mockDoc: DocumentResponse = {
  id: 1,
  createdAt: '2026-07-31',
  updatedAt: '2026-07-31',
  documentType: 'سند دين',
  isDraft: false,
  borrowerName: 'أحمد',
  borrowerFather: 'محمد',
  borrowerFamily: 'خالد',
  borrowerMother: 'فاطمة',
  borrowerBirth: '1980-01-01',
  borrowerRegister: '123',
  borrowerNationalId: 'N456',
  borrowerAddress: 'دمشق',
  borrowerAddressType: 'سكني',
  contractType: 'قرض',
  contractTypeSelector: 'تسليف',
  contractNumber: 'C1',
  contractDate: '2026-01-01',
  amountNumeric: 1000,
  amountWords: 'ألف',
  currency: 'ل.س',
  amount2Numeric: 0,
  amount2Words: '',
  currency2: '',
  amount3Numeric: 0,
  amount3Words: '',
  currency3: '',
  inclusionAmountNumeric: 0,
  inclusionAmountWords: '',
  inclusionCurrency: '',
  inclusionAmount2Numeric: 0,
  inclusionAmount2Words: '',
  inclusionCurrency2: '',
  inclusionAmount3Numeric: 0,
  inclusionAmount3Words: '',
  inclusionCurrency3: '',
  court: 'محكمة دمشق',
  applicant: 'المصرف',
  lawyer: 'المحامي سامر',
  fileNumber: '99',
  fileYear: '2026',
  fileIncoming: 'و-77',
  fileIncomingDate: '2026-07-30',
  underFilingNumber: 'ت-55',
  fileRegistrationDate: '2026-08-01',
  branchName: 'فرع المزة',
  execStatus: '',
  baraetNumber: '',
  baraetDate: '',
  baraetRegNumber: '',
  baraetRegDate: '',
  tarithNumber: '',
  tarithDate: '',
  tarithRegNumber: '',
  tarithRegDate: '',
  seizureDate: '2026-07-29',
  immediateActions: 'إجراء عاجل',
  notes: '',
  viewCount: 7,
  printCount: 0,
  createdByName: 'الرئيس',
  executionActions: [],
  guarantors: [
    {
      id: 1,
      guarantorNumber: 1,
      name: 'خالد',
      father: 'عمر',
      family: 'زكي',
      mother: 'سميرة',
      birth: '1975-05-05',
      register: '789',
      nationalId: 'N789',
      address: 'حلب',
      addressType: 'تجاري',
    },
    {
      id: 2,
      guarantorNumber: 2,
      name: 'لينا',
      father: 'فادي',
      family: 'نور',
      mother: 'هند',
      birth: '1985-06-06',
      register: '101',
      nationalId: 'N101',
      address: 'حمص',
      addressType: 'سكني',
    },
  ],
  realEstates: [
    {
      id: 1,
      owners: ['أحمد محمد خالد'],
      property: 'منزل',
      propertyNumber: '12',
      propertyDistrict: 'المزة',
      landRegistry: 'سجل 3',
      shareType: 'كامل',
    },
    {
      id: 2,
      owners: ['أحمد محمد خالد'],
      property: 'أرض',
      propertyNumber: '34',
      propertyDistrict: 'المزة',
      landRegistry: 'سجل 3',
      shareType: 'حصة سهمية',
    },
  ],
  executionApplicants: [],
  executedPublicEntities: [],
  executedNaturalPersons: [],
};

function renderView() {
  return render(
    <MemoryRouter initialEntries={['/documents/1']}>
      <Routes>
        <Route path="/documents/:id" element={<DocumentView />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  useAuthMock.mockReturnValue({ isHead: false, user: { role: 'lawyer' } });
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockDoc });
});

describe('DocumentView', () => {
  it('يعرض اسم المقترض في بطاقة أطراف الملف ويفتح نافذة بهويته الكاملة عند الضغط', async () => {
    const user = userEvent.setup();
    renderView();

    const heading = await screen.findByText('أطراف الملف التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getByText('أحمد محمد خالد')).toBeInTheDocument();

    await user.click(within(card).getByText('أحمد محمد خالد'));
    const dialog = screen.getByRole('dialog', { name: 'مقترض' });
    expect(within(dialog).getByText('أحمد محمد خالد')).toBeInTheDocument();
    expect(within(dialog).getByText('فاطمة')).toBeInTheDocument();
    expect(within(dialog).getByText('1980-01-01')).toBeInTheDocument();
    expect(within(dialog).getByText('123')).toBeInTheDocument();
    expect(within(dialog).getByText('N456')).toBeInTheDocument();
    expect(within(dialog).getByText('سكني')).toBeInTheDocument();
    expect(within(dialog).getByText('دمشق')).toBeInTheDocument();
    expect(within(dialog).getByText('مكان وتاريخ الولادة')).toBeInTheDocument();
    expect(within(dialog).getByText('مكان ورقم القيد')).toBeInTheDocument();
    expect(screen.queryByText('بيانات المقترض')).not.toBeInTheDocument();
  });

  it('يعرض حقل «وكيله القانوني» وحيدًا بدل «نوع العنوان» و«العنوان» عندما تكون الوكالة «يمثله»', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...mockDoc, borrowerAddress: 'المحامي فلان الفلاني', borrowerAddressType: 'يمثله' },
    });
    renderView();

    const heading = await screen.findByText('أطراف الملف التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    await user.click(within(card).getByText('أحمد محمد خالد'));
    const dialog = screen.getByRole('dialog', { name: 'مقترض' });
    expect(within(dialog).getByText('وكيله القانوني')).toBeInTheDocument();
    expect(within(dialog).getByText('المحامي فلان الفلاني')).toBeInTheDocument();
    expect(within(dialog).queryByText('نوع العنوان')).not.toBeInTheDocument();
    expect(within(dialog).queryByText('يمثله')).not.toBeInTheDocument();
  });

  it('يعرض تسميات هوية المقترض كاملة حتى لو كانت قيمها فارغة', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...mockDoc, borrowerMother: '', borrowerBirth: '', borrowerRegister: '', borrowerNationalId: '' },
    });
    renderView();

    const heading = await screen.findByText('أطراف الملف التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    await user.click(within(card).getByText('أحمد محمد خالد'));
    const dialog = screen.getByRole('dialog', { name: 'مقترض' });
    expect(within(dialog).getByText('الاسم الثلاثي')).toBeInTheDocument();
    expect(within(dialog).getByText('اسم الأم')).toBeInTheDocument();
    expect(within(dialog).getByText('مكان وتاريخ الولادة')).toBeInTheDocument();
    expect(within(dialog).getByText('مكان ورقم القيد')).toBeInTheDocument();
    expect(within(dialog).getByText('الرقم الوطني')).toBeInTheDocument();
  });

  it('يعرض قسم «بيانات السند التنفيذي» للمصرفي: نوع العقد ورقمه وتاريخه والمبلغ المطالب به', async () => {
    renderView();

    const heading = await screen.findByText('بيانات السند التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getByText('نوع السند')).toBeInTheDocument();
    expect(within(card).getByText('تسليف')).toBeInTheDocument();
    expect(within(card).getByText('نوع العقد')).toBeInTheDocument();
    expect(within(card).getByText('قرض')).toBeInTheDocument();
    expect(within(card).getByText('رقم العقد')).toBeInTheDocument();
    expect(within(card).getByText('C1')).toBeInTheDocument();
    expect(within(card).getByText('تاريخ العقد')).toBeInTheDocument();
    expect(within(card).getByText('2026-01-01')).toBeInTheDocument();
    expect(within(card).getByText('المبلغ المطالب به')).toBeInTheDocument();
    expect(within(card).getByText('ألف')).toBeInTheDocument();
    expect(within(card).queryByText('1000 ل.س')).not.toBeInTheDocument();
    expect(within(card).queryByText('رقم القرار')).not.toBeInTheDocument();
    expect(within(card).queryByText('المحكمة مصدرة القرار')).not.toBeInTheDocument();
    expect(within(card).queryByText('خلاصة الحكم')).not.toBeInTheDocument();
  });

  it('يعرض قسم «بيانات السند التنفيذي» للعادي: رقم وتاريخ القرار والمحكمة والخلاصة والمبلغ', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        contractTypeSelector: 'عادي',
        contractType: 'محكمة دمشق',
        contractNumber: 'ق-5',
        contractDate: '2026-02-02',
        inclusionText: 'حكم بخلاصة',
        inclusionAmountNumeric: 500,
        inclusionAmountWords: 'خمسمائة',
        inclusionCurrency: 'ل.س',
        amountNumeric: 0,
        amountWords: '',
      },
    });
    renderView();

    const heading = await screen.findByText('بيانات السند التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getByText('نوع السند')).toBeInTheDocument();
    expect(within(card).getByText('عادي')).toBeInTheDocument();
    expect(within(card).getByText('رقم القرار')).toBeInTheDocument();
    expect(within(card).getByText('ق-5')).toBeInTheDocument();
    expect(within(card).getByText('تاريخ القرار')).toBeInTheDocument();
    expect(within(card).getByText('2026-02-02')).toBeInTheDocument();
    expect(within(card).getByText('المحكمة مصدرة القرار')).toBeInTheDocument();
    expect(within(card).getByText('محكمة دمشق')).toBeInTheDocument();
    expect(within(card).getByText('خلاصة الحكم')).toBeInTheDocument();
    expect(within(card).getByText('حكم بخلاصة')).toBeInTheDocument();
    expect(within(card).getByText('المبلغ المطالب به')).toBeInTheDocument();
    expect(within(card).getByText('خمسمائة')).toBeInTheDocument();
    expect(within(card).queryByText('500 ل.س')).not.toBeInTheDocument();
    expect(within(card).queryByText('نوع العقد')).not.toBeInTheDocument();
    expect(within(card).queryByText('رقم العقد')).not.toBeInTheDocument();
    expect(within(card).queryByText('تاريخ العقد')).not.toBeInTheDocument();
  });

  it('لا يعرض «المبلغ المطالب به» للسند العادي إذا لم يُحدد مبلغ', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        contractTypeSelector: 'عادي',
        contractType: 'محكمة دمشق',
        contractNumber: 'ق-5',
        contractDate: '2026-02-02',
        inclusionText: 'حكم بخلاصة',
        inclusionAmountNumeric: 0,
        inclusionAmountWords: '',
        inclusionCurrency: '',
        amountNumeric: 0,
        amountWords: '',
      },
    });
    renderView();

    const heading = await screen.findByText('بيانات السند التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getByText('رقم القرار')).toBeInTheDocument();
    expect(within(card).queryByText('المبلغ المطالب به')).not.toBeInTheDocument();
  });

  it('يعرض المبلغين الثاني والثالث للمصرفي بعملتيهما عند وجودهما', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        amount2Numeric: 2000,
        amount2Words: 'ألفان',
        currency2: 'يورو',
        amount3Numeric: 3000,
        amount3Words: 'ثلاثة آلاف',
        currency3: 'دولار أمريكي',
      },
    });
    renderView();

    const heading = await screen.findByText('بيانات السند التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getByText('المبلغ المطالب به')).toBeInTheDocument();
    expect(within(card).getByText('ألف و ألفان و ثلاثة آلاف')).toBeInTheDocument();
    expect(within(card).queryByText('المبلغ الثاني')).not.toBeInTheDocument();
    expect(within(card).queryByText('2000 يورو')).not.toBeInTheDocument();
    expect(within(card).queryByText('المبلغ الثالث')).not.toBeInTheDocument();
    expect(within(card).queryByText('3000 دولار أمريكي')).not.toBeInTheDocument();
  });

  it('يعرض المبلغين الثاني والثالث للعادي بعملتيهما عند وجودهما', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        contractTypeSelector: 'عادي',
        contractType: 'محكمة دمشق',
        contractNumber: 'ق-5',
        contractDate: '2026-02-02',
        inclusionText: 'حكم بخلاصة',
        inclusionAmountNumeric: 500,
        inclusionAmountWords: 'خمسمائة',
        inclusionCurrency: 'ل.س',
        amountNumeric: 0,
        amountWords: '',
        inclusionAmount2Numeric: 600,
        inclusionAmount2Words: 'ستمائة',
        inclusionCurrency2: 'يورو',
        inclusionAmount3Numeric: 700,
        inclusionAmount3Words: 'سبعمائة',
        inclusionCurrency3: 'دولار أمريكي',
      },
    });
    renderView();

    const heading = await screen.findByText('بيانات السند التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getByText('المبلغ المطالب به')).toBeInTheDocument();
    expect(within(card).getByText('خمسمائة و ستمائة و سبعمائة')).toBeInTheDocument();
    expect(within(card).queryByText('المبلغ الثاني')).not.toBeInTheDocument();
    expect(within(card).queryByText('600 يورو')).not.toBeInTheDocument();
    expect(within(card).queryByText('المبلغ الثالث')).not.toBeInTheDocument();
    expect(within(card).queryByText('700 دولار أمريكي')).not.toBeInTheDocument();
  });

  it('يعرض «بيانات الملف» بدائرة التنفيذ المختصة ورقم الملف دون الإجراءات المستعجلة', async () => {
    renderView();

    expect(await screen.findByText('بيانات الملف')).toBeInTheDocument();
    expect(screen.queryByText('بيانات الدعوى')).not.toBeInTheDocument();
    expect(screen.getByText('دائرة التنفيذ المختصة')).toBeInTheDocument();
    expect(screen.queryByText('المحكمة')).not.toBeInTheDocument();
    expect(screen.getByText('رقم الملف ونوعه لعام 2026')).toBeInTheDocument();
    expect(screen.getByText('99 لعام 2026')).toBeInTheDocument();
    expect(screen.queryByText('المدعي')).not.toBeInTheDocument();
    expect(screen.getByText('تاريخ كتاب الجهة العامة')).toBeInTheDocument();
    expect(screen.getByText('2026-07-30')).toBeInTheDocument();
    expect(screen.getByText('رقم تحت رفع')).toBeInTheDocument();
    expect(screen.getByText('ت-55')).toBeInTheDocument();
    expect(screen.getByText('تاريخ قيد الملف')).toBeInTheDocument();
    expect(screen.getByText('2026-08-01')).toBeInTheDocument();
    expect(screen.queryByText('الإجراءات المستعجلة')).not.toBeInTheDocument();
    expect(screen.queryByText('إجراء عاجل')).not.toBeInTheDocument();
    expect(screen.queryByText('عدد المشاهدات')).not.toBeInTheDocument();
    expect(screen.queryByText('7')).not.toBeInTheDocument();
  });

  it('يجمع حقول كتب الجهات بجانب بعضها في بطاقة بيانات الملف', async () => {
    renderView();
    await screen.findByText('بيانات الملف');

    const gridOf = (label: string) => screen.getByText(label).closest('.grid');
    expect(gridOf('رقم كتاب الجهة العامة')).toBe(gridOf('تاريخ كتاب الجهة العامة'));
    expect(gridOf('رقم كتاب الجهة العامة')).toBe(gridOf('رقم ورود الملف'));
  });

  it('يعرض بطاقة «بيانات الملف» قبل بطاقة «بيانات السند التنفيذي» للمصرفي', async () => {
    renderView();

    await screen.findByText('بيانات الملف');
    const fileInfo = screen.getByText('بيانات الملف');
    const contract = screen.getByText('بيانات السند التنفيذي');
    expect(fileInfo.compareDocumentPosition(contract) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('يخفي «المحامي» و«منشئ المستند» عن المحامي ويعرض ملاحظة الإحالة فقط إذا أُحيل إليه الملف', async () => {
    const { unmount } = renderView();

    await screen.findByText('بيانات الملف');
    expect(screen.queryByText('المحامي')).not.toBeInTheDocument();
    expect(screen.queryByText('منشئ المستند')).not.toBeInTheDocument();
    expect(screen.queryByText(/أُحيل لك هذا الملف/)).not.toBeInTheDocument();
    unmount();

    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...mockDoc, referredFromLawyer: 'المحامي سامر', referredAt: '2026-08-05' },
    });
    renderView();

    await screen.findByText('بيانات الملف');
    expect(screen.queryByText('المحامي')).not.toBeInTheDocument();
    expect(screen.queryByText('منشئ المستند')).not.toBeInTheDocument();
    expect(screen.getByText(/أُحيل لك هذا الملف من المحامي سامر بتاريخ/)).toBeInTheDocument();
  });

  it('يعرض «المحامي» و«منشئ المستند» لرئيس القسم دون ملاحظة الإحالة', async () => {
    useAuthMock.mockReturnValue({ isHead: true, user: { role: 'head' } });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...mockDoc, referredFromLawyer: 'المحامي سامر', referredAt: '2026-08-05' },
    });
    renderView();

    await screen.findByText('بيانات الملف');
    expect(screen.getByText('المحامي المختص')).toBeInTheDocument();
    expect(screen.getByText('منشئ المستند')).toBeInTheDocument();
    expect(screen.queryByText(/أُحيل لك هذا الملف/)).not.toBeInTheDocument();
  });

  it('يعرض رقم الملف مع نوعه إذا وُجد: «99 سند مصارف لعام 2026»', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...mockDoc, fileType: 'سند مصارف' },
    });
    renderView();

    expect(await screen.findByText('بيانات الملف')).toBeInTheDocument();
    expect(screen.getByText('99 سند مصارف لعام 2026')).toBeInTheDocument();
  });

  it('يعرض رقم أساس السنة الحالية بدل رقم الملف: «1500 سند مصارف لعام 2026»', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...mockDoc, fileType: 'سند مصارف', displayFileNumber: '1500' },
    });
    renderView();

    expect(await screen.findByText('بيانات الملف')).toBeInTheDocument();
    expect(screen.getByText('1500 سند مصارف لعام 2026')).toBeInTheDocument();
    expect(screen.queryByText('99 سند مصارف لعام 2026')).not.toBeInTheDocument();
  });

  it('يفتح نافذة أرقام الأساس عند الضغط على رقم الملف ويعرض السنوات والأرقام والنوع', async () => {
    const user = userEvent.setup();
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockImplementation((url: string) => {
      if (url === '/documents/1/base-numbers') {
        return Promise.resolve({
          data: [
            { year: 2025, baseNumber: '900' },
            { year: 2026, baseNumber: '1500' },
          ],
        });
      }
      return Promise.resolve({ data: { ...mockDoc, fileType: 'سند مصارف' } });
    });
    renderView();

    const fileNumberButton = await screen.findByRole('button', {
      name: 'عرض أرقام الأساس للسنوات السابقة',
    });
    expect(screen.getByText('99 سند مصارف لعام 2026')).toBeInTheDocument();
    await user.click(fileNumberButton);

    const dialog = await screen.findByRole('dialog', {
      name: 'أرقام الأساس للسنوات السابقة',
    });
    expect(within(dialog).getByText('2025')).toBeInTheDocument();
    expect(within(dialog).getByText('900')).toBeInTheDocument();
    expect(within(dialog).getByText('2026')).toBeInTheDocument();
    expect(within(dialog).getByText('1500')).toBeInTheDocument();
    expect(within(dialog).getAllByText('سند مصارف')).toHaveLength(2);

    await user.click(within(dialog).getAllByRole('button', { name: 'إغلاق' })[0]);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('يعرض رسالة فارغة في نافذة أرقام الأساس عندما لا توجد أرقام مسجلة', async () => {
    const user = userEvent.setup();
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockImplementation((url: string) => {
      if (url === '/documents/1/base-numbers') return Promise.resolve({ data: [] });
      return Promise.resolve({ data: mockDoc });
    });
    renderView();

    const fileNumberButton = await screen.findByRole('button', {
      name: 'عرض أرقام الأساس للسنوات السابقة',
    });
    await user.click(fileNumberButton);

    expect(
      await screen.findByText('لا توجد أرقام أساس مسجلة لهذا الملف'),
    ).toBeInTheDocument();
  });

  it('يعرض ملخص الحالة الافتراضية في تفاصيل الملف', async () => {
    renderView();

    expect(await screen.findByText('الحالة')).toBeInTheDocument();
    expect(screen.getAllByText('متداول').length).toBeGreaterThan(0);
    expect(screen.getByText('لتغيير الحالة اضغط زر «تغيير الحالة»')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تحديث الحالة' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حفظ الحالة' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('نوع التنفيذ')).not.toBeInTheDocument();
  });

  it('يعرض زر «تغيير الحالة» للمحامي في ملف طالبة تنفيذ ويفتح نافذة الحالات', async () => {
    const user = userEvent.setup();
    renderView();

    const button = await screen.findByRole('button', { name: 'تغيير الحالة' });
    await user.click(button);

    expect(screen.getByRole('dialog', { name: 'تغيير الحالة' })).toBeInTheDocument();
    expect(screen.getByText('الحالة الحالية')).toBeInTheDocument();
  });

  it('لا يعرض زر «تغيير الحالة» للمدير', async () => {
    useAuthMock.mockReturnValue({ isHead: false, user: { role: 'manager' } });
    renderView();

    expect(await screen.findByText('الحالة')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تغيير الحالة' })).not.toBeInTheDocument();
  });

  it('يعرض بطاقة وقوعات الملف لملف طالبة تنفيذ حامل لوقعة تغيير حالة', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        execStatus: 'تريث',
        occurrences: [
          {
            id: 1,
            occurrenceType: 'deferred',
            occurrenceTypeLabel: 'تريث',
            eventDate: '2026-08-01',
            details: { tarithNumber: '33', tarithDate: '3/3/2024' },
          },
        ],
      },
    });
    renderView();

    expect(await screen.findByText('وقوعات الملف')).toBeInTheDocument();
    expect(screen.getByText(/تريث بموجب كتاب التريث رقم 33/)).toBeInTheDocument();
  });

  it('يعرض ملخص «منفذ بالتسوية» كاملاً في تفاصيل الملف', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        execStatus: 'منفذ بالتسوية',
        baraetNumber: '77',
        baraetDate: '1/1/2024',
        baraetRegNumber: '55',
        baraetRegDate: '2/2/2024',
      },
    });
    renderView();

    expect(await screen.findByText(
      'منفذ بموجب كتاب براءة الذمة رقم 77 تاريخ 1/1/2024 والمسجل برقم 55 تاريخ 2/2/2024',
    )).toBeInTheDocument();
  });

  it('يعرض ملخص «تريث» كاملاً في تفاصيل الملف', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        execStatus: 'تريث',
        tarithNumber: '33',
        tarithDate: '3/3/2024',
        tarithRegNumber: '44',
        tarithRegDate: '4/4/2024',
      },
    });
    renderView();

    expect(await screen.findByText(
      'تريث بموجب كتاب التريث رقم 33 تاريخ 3/3/2024 والمسجل برقم 44 تاريخ 4/4/2024',
    )).toBeInTheDocument();
  });

  it('يعرض ملخص «منفذ جبريا» مع نوع التنفيذ والمبلغ المحصل', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...mockDoc, execStatus: 'منفذ جبريا', execSubStatus: 'منفذ جزئيا', collectedAmount: 750 },
    });
    renderView();

    expect(await screen.findByText('منفذ جبريا (منفذ جزئيا) المبلغ المحصل: 750')).toBeInTheDocument();
  });

  it('يعرض شارة «متداول / منفذ جزئيا» للملف المنفذ جزئياً', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...mockDoc, execStatus: 'منفذ جبريا', execSubStatus: 'منفذ جزئيا' },
    });
    renderView();

    const heading = await screen.findByRole('heading', { name: /منفذ جزئيا/ });
    expect(heading.textContent).toContain('متداول / منفذ جزئيا');
  });

  it('يعرض شارة حالة الملف والاسم الثلاثي للمنفذ عليه في أعلى الصفحة', async () => {
    renderView();

    const heading = await screen.findByRole('heading', { name: /متداول/ });
    expect(heading.textContent).toContain('متداول');
    expect(heading.textContent).toContain('أحمد محمد خالد');
  });

  it('يعرض الكفلاء في بطاقة أطراف الملف مع صفتهم ويفتح نافذة هوية كلٍّ عند الضغط', async () => {
    const user = userEvent.setup();
    renderView();

    const heading = await screen.findByText('أطراف الملف التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getByText('خالد عمر زكي')).toBeInTheDocument();
    expect(within(card).getByText('لينا فادي نور')).toBeInTheDocument();

    await user.click(within(card).getByText('خالد عمر زكي'));
    const firstDialog = screen.getByRole('dialog', { name: 'كفيل 1' });
    expect(within(firstDialog).getAllByText('الاسم الثلاثي').length).toBeGreaterThan(0);
    expect(within(firstDialog).getAllByText('اسم الأم').length).toBeGreaterThan(0);
    expect(within(firstDialog).getAllByText('مكان وتاريخ الولادة').length).toBeGreaterThan(0);
    expect(within(firstDialog).getAllByText('مكان ورقم القيد').length).toBeGreaterThan(0);
    expect(within(firstDialog).getAllByText('الرقم الوطني').length).toBeGreaterThan(0);
    expect(within(firstDialog).getByText('خالد عمر زكي')).toBeInTheDocument();
    expect(within(firstDialog).getByText('سميرة')).toBeInTheDocument();
    expect(within(firstDialog).getByText('1975-05-05')).toBeInTheDocument();
    expect(within(firstDialog).getByText('789')).toBeInTheDocument();
    expect(within(firstDialog).getByText('N789')).toBeInTheDocument();
    expect(within(firstDialog).getByText('حلب')).toBeInTheDocument();
    await user.click(within(firstDialog).getByLabelText('إغلاق'));

    await user.click(within(card).getByText('لينا فادي نور'));
    const secondDialog = screen.getByRole('dialog', { name: 'كفيل 2' });
    expect(within(secondDialog).getByText('لينا فادي نور')).toBeInTheDocument();
    expect(within(secondDialog).getByText('هند')).toBeInTheDocument();
    expect(within(secondDialog).getByText('1985-06-06')).toBeInTheDocument();
    expect(within(secondDialog).getByText('101')).toBeInTheDocument();
    expect(within(secondDialog).getByText('N101')).toBeInTheDocument();
    expect(within(secondDialog).getByText('حمص')).toBeInTheDocument();
  });

  it('يعرض «المنفذ عليه» بصفته مع ترقيم يبدأ من 2 عند السند العادي', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...mockDoc, contractTypeSelector: 'عادي' },
    });
    renderView();

    const heading = await screen.findByText('أطراف الملف التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getByText('أحمد محمد خالد')).toBeInTheDocument();
    expect(within(card).getByText('خالد عمر زكي')).toBeInTheDocument();
    expect(within(card).getByText('لينا فادي نور')).toBeInTheDocument();
    expect(within(card).getByText('منفذ عليه')).toBeInTheDocument();
    expect(within(card).getByText('منفذ عليه 2')).toBeInTheDocument();
    expect(within(card).getByText('منفذ عليه 3')).toBeInTheDocument();
    expect(screen.queryByText('الكفلاء')).not.toBeInTheDocument();
    expect(screen.queryByText('المنفذ عليهم الآخرون')).not.toBeInTheDocument();
  });

  it('يعرض العقارات في قسم منفصل بكل تفاصيلها', async () => {
    renderView();

    const heading = await screen.findByText('العقارات');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getAllByText('رقم العقار').length).toBeGreaterThan(0);
    expect(within(card).getAllByText('المنطقة العقارية').length).toBeGreaterThan(0);
    expect(within(card).getAllByText('المصالح العقارية المختصة').length).toBeGreaterThan(0);
    expect(within(card).getAllByText('مالك العقار').length).toBeGreaterThan(0);
    expect(within(card).getByText('12')).toBeInTheDocument();
    expect(within(card).getByText('34')).toBeInTheDocument();
    expect(within(card).getAllByText('المزة').length).toBeGreaterThan(0);
    expect(within(card).getAllByText('سجل 3').length).toBeGreaterThan(0);
    expect(within(card).getAllByText('أحمد محمد خالد').length).toBeGreaterThan(0);
  });

  function findBasicRow(label: string) {
    return screen.getByText(label).closest('div') as HTMLElement;
  }

  function basicDocsCard() {
    const heading = screen.getByText('المستندات الأساسية');
    return heading.closest('div')!.parentElement as HTMLElement;
  }

  function estateNoticeCard() {
    const heading = screen.getByText('إخطار بيع أموال غير منقولة');
    return heading.closest('div')!.parentElement as HTMLElement;
  }

  it('ينزّل استدعاء تنفيذي عند النقر على «توليد» في المستندات الأساسية', async () => {
    const createObjectURL = vi.fn(() => 'blob:mock');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL });
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    (api.get as unknown as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ data: mockDoc })
      .mockResolvedValueOnce({
        data: new Blob(['docx']),
        headers: { 'content-disposition': 'attachment; filename="أحمد_001.docx"' },
      });

    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    const row = findBasicRow('استدعاء تنفيذي');
    await user.click(within(row).getByRole('button', { name: 'توليد' }));

    await waitFor(() => expect(createObjectURL).toHaveBeenCalled());
    expect(api.get).toHaveBeenCalledWith('/documents/1/generate',
      expect.objectContaining({ params: { template: '001', recipient: 0 }, responseType: 'blob' }));
    expect(clickSpy).toHaveBeenCalled();
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:mock');

    clickSpy.mockRestore();
    vi.unstubAllGlobals();
  });

  it('يعرض الأقسام الثلاثة بأسماء الأزرار والرسائل المطابقة لتطبيق سطح المكتب', async () => {
    renderView();

    expect(await screen.findByText('توليد المستندات التنفيذية')).toBeInTheDocument();
    expect(screen.getByText('المستندات الأساسية')).toBeInTheDocument();
    for (const label of ['استدعاء تنفيذي', 'محضر تنفيذي', 'حجز عقاري', 'حجز منظومة']) {
      expect(screen.getByText(label)).toBeInTheDocument();
    }
    expect(screen.getByText('إخطار تنفيذي')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'توليد إخطار تنفيذي' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'توليد إخطار بالصحف' })).toBeInTheDocument();
    expect(screen.getByText('إخطار بيع أموال غير منقولة')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'توليد إخطار بيع غير منقولة' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'توليد إخطار بيع بالصحف' })).toBeInTheDocument();
    expect(screen.getByText(/اختر العقارات التي تريد تسطير إخطار بيع أموال غير منقولة بالنسبة لها/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /طباعة/ })).not.toBeInTheDocument();
    expect(screen.queryByText('توليد مستند Word')).not.toBeInTheDocument();
  });

  it('يعرض أسماء المنفَّذ عليهم في قائمة الإخطار التنفيذي كما في سطح المكتب', async () => {
    renderView();

    expect(await screen.findByText(/اختر المنفَّذ عليهم الذين تريد تسطير إخطار تنفيذي لهم :/)).toBeInTheDocument();
    expect(screen.getByLabelText(/المقترض :\s+أحمد خالد/)).toBeInTheDocument();
    expect(screen.getByLabelText(/كفيل 1 :\s+خالد\s+زكي/)).toBeInTheDocument();
    expect(screen.getByLabelText(/كفيل 2 :\s+لينا\s+نور/)).toBeInTheDocument();
  });

  it('يعرض «منفذ عليه <ن>» في قائمة الإخطار عند السند العادي', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...mockDoc, contractTypeSelector: 'عادي' },
    });
    renderView();

    expect(await screen.findByLabelText(/منفذ عليه 1 :/)).toBeInTheDocument();
    expect(screen.getByLabelText(/منفذ عليه 2 :/)).toBeInTheDocument();
  });

  it('يعرض «لا يوجد أشخاص» عند غياب المقترض والكفلاء', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...mockDoc, borrowerName: '', borrowerFamily: '', guarantors: [] },
    });
    renderView();

    expect(await screen.findByText('لا يوجد أشخاص — أدخل المقترض والكفلاء أولاً')).toBeInTheDocument();
  });

  it('يُحذّر عند توليد إخطار تنفيذي دون اختيار شخص من قائمة المنفَّذ عليهم', async () => {
    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    await user.click(screen.getByRole('button', { name: 'توليد إخطار تنفيذي' }));

    expect(await screen.findByText('اختر شخصاً واحداً على الأقل من قائمة المنفَّذ عليهم')).toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalledWith(
      '/documents/1/generate',
      expect.objectContaining({ params: expect.objectContaining({ template: '003' }) }),
    );
  });

  it('ينزّل إخطار تنفيذي للمقترض وللكفيل المحدد عند اختيارهما', async () => {
    const createObjectURL = vi.fn(() => 'blob:mock');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    const apiGet = api.get as unknown as ReturnType<typeof vi.fn>;
    apiGet
      .mockResolvedValueOnce({ data: mockDoc })
      .mockResolvedValueOnce({
        data: new Blob(['docx']),
        headers: { 'content-disposition': 'attachment; filename="أحمد_003.docx"' },
      })
      .mockResolvedValueOnce({
        data: new Blob(['docx']),
        headers: { 'content-disposition': 'attachment; filename="أحمد_003.docx"' },
      });

    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    await user.click(screen.getByLabelText(/المقترض :/));
    await user.click(screen.getByLabelText(/كفيل 1 :/));
    await user.click(screen.getByRole('button', { name: 'توليد إخطار تنفيذي' }));

    await waitFor(() => expect(createObjectURL).toHaveBeenCalledTimes(2));
    expect(await screen.findByText('✅ تم إنشاء 2 إخطار بنجاح')).toBeInTheDocument();
    expect(apiGet).toHaveBeenNthCalledWith(2, '/documents/1/generate',
      expect.objectContaining({ params: { template: '003', recipient: 0 } }));
    expect(apiGet).toHaveBeenNthCalledWith(3, '/documents/1/generate',
      expect.objectContaining({ params: { template: '003', recipient: 1 } }));

    vi.unstubAllGlobals();
  });

  it('ينزّل إخطار تنفيذي بالصحف للمستلمين المختارين', async () => {
    const createObjectURL = vi.fn(() => 'blob:mock');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    const apiGet = api.get as unknown as ReturnType<typeof vi.fn>;
    apiGet
      .mockResolvedValueOnce({ data: mockDoc })
      .mockResolvedValueOnce({
        data: new Blob(['docx']),
        headers: { 'content-disposition': 'attachment; filename="أحمد_007.docx"' },
      });

    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    await user.click(screen.getByLabelText(/كفيل 2 :/));
    await user.click(screen.getByRole('button', { name: 'توليد إخطار بالصحف' }));

    await waitFor(() => expect(createObjectURL).toHaveBeenCalledTimes(1));
    expect(await screen.findByText('✅ تم إنشاء 1 إخطار تنفيذي بالصحف بنجاح')).toBeInTheDocument();
    expect(apiGet).toHaveBeenNthCalledWith(2, '/documents/1/generate',
      expect.objectContaining({ params: { template: '007', recipient: 2 } }));

    vi.unstubAllGlobals();
  });

  it('يُحذّر عند توليد إخطار بيع أموال غير منقولة دون اختيار عقار', async () => {
    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    await user.click(screen.getByRole('button', { name: 'توليد إخطار بيع غير منقولة' }));

    expect(await screen.findByText('اختر عقاراً واحداً على الأقل من قائمة العقارات')).toBeInTheDocument();
  });

  it('ينزّل إخطار بيع أموال غير منقولة للعقارات المختارة', async () => {
    const createObjectURL = vi.fn(() => 'blob:mock');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    const apiGet = api.get as unknown as ReturnType<typeof vi.fn>;
    apiGet
      .mockResolvedValueOnce({ data: mockDoc })
      .mockResolvedValueOnce({
        data: new Blob(['docx']),
        headers: { 'content-disposition': 'attachment; filename="أحمد_005.docx"' },
      });

    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    await user.click(within(estateNoticeCard()).getByLabelText(/منزل/));
    await user.click(screen.getByRole('button', { name: 'توليد إخطار بيع غير منقولة' }));

    await waitFor(() => expect(createObjectURL).toHaveBeenCalled());
    expect(await screen.findByText('✅ تم إنشاء إخطار بيع أموال غير منقولة بنجاح')).toBeInTheDocument();
    expect(apiGet).toHaveBeenNthCalledWith(2, '/documents/1/generate',
      expect.objectContaining({ params: { template: '005', recipient: 0, estateIds: [1] } }));

    vi.unstubAllGlobals();
  });

  it('يعرض تحذير المالكين المختلفين عند اختيار عقارات لمالكين مختلفين', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        realEstates: [
          mockDoc.realEstates[0],
          { ...mockDoc.realEstates[1], owners: ['لينا فادي نور'] },
        ],
      },
    });
    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    await user.click(within(estateNoticeCard()).getByLabelText(/منزل/));
    await user.click(within(estateNoticeCard()).getByLabelText(/أرض/));

    expect(await screen.findByText(/العقارات لمالكين مختلفين/)).toBeInTheDocument();
  });

  it('يُحذّر عند توليد إخطار بيع غير منقولة لعقارات لمالكين مختلفين', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        realEstates: [
          mockDoc.realEstates[0],
          { ...mockDoc.realEstates[1], owners: ['لينا فادي نور'] },
        ],
      },
    });
    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    await user.click(within(estateNoticeCard()).getByLabelText(/منزل/));
    await user.click(within(estateNoticeCard()).getByLabelText(/أرض/));
    await user.click(screen.getByRole('button', { name: 'توليد إخطار بيع غير منقولة' }));

    expect(await screen.findByText('يجب أن تكون العقارات لنفس المالك')).toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalledWith(
      '/documents/1/generate',
      expect.objectContaining({ params: expect.objectContaining({ template: '005' }) }),
    );
  });

  it('ينزّل مستند حجز عقاري للعقار المختار', async () => {
    const createObjectURL = vi.fn(() => 'blob:mock');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    const apiGet = api.get as unknown as ReturnType<typeof vi.fn>;
    apiGet
      .mockResolvedValueOnce({ data: mockDoc })
      .mockResolvedValueOnce({
        data: new Blob(['docx']),
        headers: { 'content-disposition': 'attachment; filename="أحمد_PS.docx"' },
      });

    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    await user.click(within(basicDocsCard()).getByLabelText(/منزل/));
    const row = findBasicRow('حجز عقاري');
    await user.click(within(row).getByRole('button', { name: 'توليد' }));

    await waitFor(() => expect(createObjectURL).toHaveBeenCalled());
    expect(await screen.findByText('✅ تم إنشاء 1 مستند حجز عقاري')).toBeInTheDocument();
    expect(apiGet).toHaveBeenNthCalledWith(2, '/documents/1/generate',
      expect.objectContaining({ params: { template: 'PS', recipient: 0, estateIds: [1] } }));

    vi.unstubAllGlobals();
  });

  it('يعرض مربعات اختيار العقارات بجوار زر «حجز عقاري» في المستندات الأساسية', async () => {
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    expect(within(basicDocsCard()).getByText(/اختر العقارات التي تريد الحجز عليها/)).toBeInTheDocument();
    expect(within(basicDocsCard()).getByLabelText(/منزل/)).toBeInTheDocument();
    expect(within(basicDocsCard()).getByLabelText(/أرض/)).toBeInTheDocument();
  });

  it('يعرض «حجز منظومة» قبل «حجز عقاري» وتظهر العقارات مباشرة أسفل «حجز عقاري»', async () => {
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    const card = basicDocsCard();
    const labels = within(card)
      .getAllByText(/^(استدعاء تنفيذي|محضر تنفيذي|حجز منظومة|حجز عقاري)$/)
      .map((el) => el.textContent);
    expect(labels).toEqual(['استدعاء تنفيذي', 'محضر تنفيذي', 'حجز منظومة', 'حجز عقاري']);

    const seizureRow = findBasicRow('حجز عقاري');
    const estateText = within(card).getByText(/اختر العقارات التي تريد الحجز عليها/);
    expect(estateText.compareDocumentPosition(seizureRow) & Node.DOCUMENT_POSITION_PRECEDING).toBeTruthy();
  });

  it('يُحذّر عند توليد حجز عقاري دون اختيار عقار', async () => {
    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    const row = findBasicRow('حجز عقاري');
    await user.click(within(row).getByRole('button', { name: 'توليد' }));

    expect(await screen.findByText('حجز عقاري: اختر عقاراً واحداً على الأقل')).toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalledWith(
      '/documents/1/generate',
      expect.objectContaining({ params: expect.objectContaining({ template: 'PS' }) }),
    );
  });

  it('يعرض رسالة فشل عند تعذر التوليد', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ data: mockDoc })
      .mockRejectedValueOnce(new Error('network'));

    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    const row = findBasicRow('محضر تنفيذي');
    await user.click(within(row).getByRole('button', { name: 'توليد' }));

    expect(await screen.findByText(/فشل توليد محضر تنفيذي/)).toBeInTheDocument();
  });

  it('يعرض رسالة تعذر الاتصال عند فشل تحميل المستند', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockRejectedValue({
      isAxiosError: true,
      response: undefined,
    });
    renderView();

    expect(await screen.findByText('تعذر الاتصال بالخادم. تحقق من الاتصال وأعد المحاولة')).toBeInTheDocument();
  });

  it('يفتح نافذة الإجراءات والملاحظات عند النقر على زرها', async () => {
    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    await user.click(screen.getByRole('button', { name: 'الإجراءات والملاحظات' }));

    expect((await screen.findAllByText('الإجراءات والملاحظات')).length).toBeGreaterThan(0);
    expect(api.get).toHaveBeenCalledWith('/documents/1/actions');
  });

  it('يعرض زر «نقل الملف» لرئيس القسم فقط ولا يعرضه للمحامي', async () => {
    const { unmount } = renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    expect(screen.queryByRole('button', { name: 'نقل الملف' })).not.toBeInTheDocument();
    unmount();

    useAuthMock.mockReturnValue({ isHead: true, user: { role: 'head' } });
    renderView();
    await screen.findByText('توليد المستندات التنفيذية');
    expect(screen.getByRole('button', { name: 'نقل الملف' })).toBeInTheDocument();
  });

  it('ينقل الملف إلى محامٍ مختار عبر النافذة المنبثقة', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/users/lawyers') {
        return Promise.resolve({
          data: [{ id: 2, username: 'lawyer2', fullName: 'محامي ثانٍ', isActive: true, branchId: 1, branchName: 'دمشق' }],
        });
      }
      return Promise.resolve({ data: mockDoc });
    });
    useAuthMock.mockReturnValue({ isHead: true, user: { role: 'head' } });
    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    await user.click(screen.getByRole('button', { name: 'نقل الملف' }));

    expect(screen.getByRole('dialog', { name: 'نقل الملف' })).toBeInTheDocument();
    await user.selectOptions(await screen.findByLabelText('المحامي المستهدف'), '2');
    await user.click(
      within(screen.getByRole('dialog', { name: 'نقل الملف' })).getByRole('button', {
        name: 'نقل الملف',
      }),
    );

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/documents/1/transfer', { targetLawyerId: 2 });
    });
  });

  it('يعرض زر «توجيه تنبيه» لرئيس القسم فقط ولا يعرضه للمحامي', async () => {
    const { unmount } = renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    expect(screen.queryByRole('button', { name: 'توجيه تنبيه' })).not.toBeInTheDocument();
    unmount();

    useAuthMock.mockReturnValue({ isHead: true, user: { role: 'head' } });
    renderView();
    await screen.findByText('توليد المستندات التنفيذية');
    expect(screen.getByRole('button', { name: 'توجيه تنبيه' })).toBeInTheDocument();
  });

  it('يفتح نافذة توجيه تنبيه معبأة بالملف ويرسل تنبيهاً مرتبطاً به', async () => {
    useAuthMock.mockReturnValue({ isHead: true, user: { role: 'head' } });
    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    await user.click(screen.getByRole('button', { name: 'توجيه تنبيه' }));

    const dialog = screen.getByRole('dialog', { name: 'توجيه تنبيه' });
    expect(within(dialog).getByText('أحمد محمد خالد')).toBeInTheDocument();
    expect(within(dialog).getByText('المحامي سامر')).toBeInTheDocument();

    await user.type(await within(dialog).findByLabelText('نص التنبيه'), 'راجع الملف');
    await user.click(within(dialog).getByRole('button', { name: 'إرسال التنبيه' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/alerts', {
        targetType: 'document',
        documentId: 1,
        targetLawyerId: null,
        message: 'راجع الملف',
      });
    });
  });

  it('يعرض «ورثة المتوفى» في بطاقة أطراف الملف ويفتح نافذة ورثتهم عند الضغط', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        borrowerHeirs: [
          { id: 10, name: 'محمود الحلبي', addressType: 'عنوان', address: 'المزة' },
          { id: 11, name: 'نور الدين', addressType: 'وكيل', address: 'المحامي سامر' },
        ],
      },
    });
    renderView();

    const heading = await screen.findByText('أطراف الملف التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getByText('ورثة المتوفى (أحمد محمد خالد)')).toBeInTheDocument();

    await user.click(within(card).getByText('ورثة المتوفى (أحمد محمد خالد)'));
    const dialog = screen.getByRole('dialog', { name: 'ورثة المتوفى (أحمد محمد خالد)' });
    expect(within(dialog).getByText('محمود الحلبي — عنوان: المزة')).toBeInTheDocument();
    expect(within(dialog).getByText('نور الدين — يمثله المحامي سامر')).toBeInTheDocument();
  });

  it('يستبدل المتوفى بورثته في قائمة المنفَّذ عليهم ولا يُدرج المتوفى نفسه', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        borrowerHeirs: [{ id: 10, name: 'محمود الحلبي', addressType: 'عنوان', address: 'المزة' }],
      },
    });
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    expect(screen.getByLabelText(/محمود الحلبي — إضافة لتركة أحمد محمد خالد/)).toBeInTheDocument();
    expect(screen.queryByLabelText(/المقترض :/)).not.toBeInTheDocument();
  });

  it('ينزّل إخطاراً تنفيذياً لكل وريث مع تمرير heirId', async () => {
    const createObjectURL = vi.fn(() => 'blob:mock');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    const apiGet = api.get as unknown as ReturnType<typeof vi.fn>;
    apiGet
      .mockResolvedValueOnce({
        data: {
          ...mockDoc,
          borrowerHeirs: [
            { id: 10, name: 'محمود الحلبي', addressType: 'عنوان', address: 'المزة' },
            { id: 11, name: 'نور الدين', addressType: 'وكيل', address: 'المحامي سامر' },
          ],
        },
      })
      .mockResolvedValueOnce({
        data: new Blob(['docx']),
        headers: { 'content-disposition': 'attachment; filename="مستند_003.docx"' },
      })
      .mockResolvedValueOnce({
        data: new Blob(['docx']),
        headers: { 'content-disposition': 'attachment; filename="مستند_003.docx"' },
      });

    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    await user.click(screen.getByLabelText(/محمود الحلبي/));
    await user.click(screen.getByLabelText(/نور الدين/));
    await user.click(screen.getByRole('button', { name: 'توليد إخطار تنفيذي' }));

    await waitFor(() => expect(createObjectURL).toHaveBeenCalledTimes(2));
    expect(await screen.findByText('✅ تم إنشاء 2 إخطار بنجاح')).toBeInTheDocument();
    expect(apiGet).toHaveBeenNthCalledWith(2, '/documents/1/generate',
      expect.objectContaining({ params: expect.objectContaining({ template: '003', heirId: 10 }) }));
    expect(apiGet).toHaveBeenNthCalledWith(3, '/documents/1/generate',
      expect.objectContaining({ params: expect.objectContaining({ template: '003', heirId: 11 }) }));

    vi.unstubAllGlobals();
  });

  it('ينزّل إخطار بيع أموال غير منقولة لكل وريث عند وجود ورثة للمالك', async () => {
    const createObjectURL = vi.fn(() => 'blob:mock');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    const apiGet = api.get as unknown as ReturnType<typeof vi.fn>;
    apiGet
      .mockResolvedValueOnce({
        data: {
          ...mockDoc,
          borrowerHeirs: [{ id: 10, name: 'محمود الحلبي', addressType: 'عنوان', address: 'المزة' }],
        },
      })
      .mockResolvedValueOnce({
        data: new Blob(['docx']),
        headers: { 'content-disposition': 'attachment; filename="مستند_005.docx"' },
      });

    const user = userEvent.setup();
    renderView();

    await screen.findByText('توليد المستندات التنفيذية');
    await user.click(within(estateNoticeCard()).getByLabelText(/منزل/));
    await user.click(screen.getByRole('button', { name: 'توليد إخطار بيع غير منقولة' }));

    await waitFor(() => expect(createObjectURL).toHaveBeenCalledTimes(1));
    expect(await screen.findByText('✅ تم إنشاء 1 إخطار بيع أموال غير منقولة بنجاح')).toBeInTheDocument();
    expect(apiGet).toHaveBeenNthCalledWith(2, '/documents/1/generate',
      expect.objectContaining({ params: expect.objectContaining({ template: '005', heirId: 10 }) }));

    vi.unstubAllGlobals();
  });

  it('يعرض بيانات وضع «منفذ عليه» بالتسميات الجديدة والمبالغ ورقم وتاريخ ورود الإخطار والورثة الثلاثي', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        generalEntitySide: 'executed',
        contractTypeSelector: 'عادي',
        fileReceiptNumber: '77',
        fileReceiptDate: '2026-08-02',
        executedStatus: 'منفذ',
        executedRequiredAmount: 5000,
        executedPaidAmount: 2000,
        executedDescription: 'تم التحصيل',
        executedPublicEntities: [{ id: 1, entityName: 'المصرف العقاري', entityBranch: 'المزة' }],
        executedNaturalPersons: [
          {
            id: 2,
            name: 'محمود',
            father: 'علي',
            family: 'حسن',
            addressType: 'عنوان',
            addressOrRepresentative: 'دمشق',
            representationType: 'إضافة لتركة',
            deceasedName: 'محمد',
            deceasedFather: 'خالد',
            deceasedFamily: 'الخطيب',
            heirs: [
              { id: 3, heirName: 'فارس', heirFather: 'أحمد', heirFamily: 'علي', addressType: 'عنوان', heirAddress: 'حلب' },
            ],
          },
        ],
      },
    });
    renderView();

    const dataHeading = await screen.findByText('بيانات الملف');
    const dataCard = dataHeading.closest('div') as HTMLElement;
    expect(within(dataCard).getByText('رقم ورود الاخطار التنفيذي')).toBeInTheDocument();
    expect(within(dataCard).getByText('77')).toBeInTheDocument();
    expect(within(dataCard).getByText('تاريخ ورود الاخطار التنفيذي')).toBeInTheDocument();
    expect(within(dataCard).getByText('كيفية تنفيذ الملف')).toBeInTheDocument();
    expect(within(dataCard).getByText('المبلغ الذي دفعته الجهة العامة')).toBeInTheDocument();
    expect(within(dataCard).getByText('2000')).toBeInTheDocument();

    // المبلغ المطلوب دفعه من الجهة العامة يعرض في بطاقة «بيانات السند التنفيذي».
    const execHeading = await screen.findByText('بيانات السند التنفيذي');
    const execCard = execHeading.closest('div') as HTMLElement;
    expect(within(execCard).getByText('المبلغ المطلوب دفعه من الجهة العامة')).toBeInTheDocument();
    expect(within(execCard).getByText('5000')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'حالة الملف' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حفظ الحالة' })).not.toBeInTheDocument();
    // «طالب التنفيذ» يعرض في بطاقة أطراف الملف — لا صفٌّ مكرر داخل «بيانات الملف».
    expect(screen.getByText('طالب التنفيذ')).toBeInTheDocument();
    expect(within(dataCard).queryByText('طالب التنفيذ')).not.toBeInTheDocument();

    const user = userEvent.setup();
    const partiesHeading = await screen.findByText('أطراف الملف التنفيذي');
    const partiesCard = partiesHeading.closest('div') as HTMLElement;
    expect(within(partiesCard).getByText('المصرف العقاري (المزة)')).toBeInTheDocument();
    expect(within(partiesCard).getByText('ورثة المتوفى (محمد خالد الخطيب)')).toBeInTheDocument();

    await user.click(within(partiesCard).getByText('ورثة المتوفى (محمد خالد الخطيب)'));
    const heirsDialog = screen.getByRole('dialog', { name: 'ورثة المتوفى (محمد خالد الخطيب)' });
    expect(within(heirsDialog).getByText('فارس أحمد علي — عنوان: حلب')).toBeInTheDocument();
  });

  it('لا يعرض وسيلة لتغيير الحالة في تفاصيل ملف وضع «منفذ عليه» (تعديل من صفحة «تعديل» حصرًا)', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...mockDoc, generalEntitySide: 'executed', executedStatus: 'منفذ' },
    });
    renderView();

    await screen.findByText('بيانات الملف');
    expect(screen.queryByRole('button', { name: 'حفظ الحالة' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إعادة الملف إلى المتداول' })).not.toBeInTheDocument();
  });

  it('يعرض تاريخ الشطب فقط دون زر إعادة في تفاصيل ملف «عرض وايداع» مشطوب', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...mockDoc, generalEntitySide: 'deposit', executedStatus: 'مشطوب', struckOffDate: '2026-08-05' },
    });
    renderView();

    await screen.findByText('بيانات الملف');
    // شارة الحالة في الترويسة تعرض «مشطوب».
    expect(screen.getByText('مشطوب')).toBeInTheDocument();
    // بطاقة وقوعات الملف تعرض تاريخ الشطب (بلا وقوعات).
    expect(screen.getByText(/تاريخ الشطب/)).toBeInTheDocument();
    // «طالب العرض» يعرض في بطاقة أطراف الملف — لا صفٌّ مكرر داخل «بيانات الملف».
    expect(screen.getByText('طالب العرض')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إعادة الملف إلى المتداول' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حفظ الحالة' })).not.toBeInTheDocument();
  });

  it('يعرض سرد وقوعات الملف في بطاقتها ويفتح نافذة التفاصيل عند الضغط', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        generalEntitySide: 'executed',
        executedStatus: 'متداول',
        struckOffDate: '2026-07-01',
        occurrences: [
          {
            id: 1,
            occurrenceType: 'struck-off',
            occurrenceTypeLabel: 'شطب',
            eventDate: '2026-07-01',
            fileNumber: '99',
            year: 2026,
          },
          {
            id: 2,
            occurrenceType: 'renewal',
            occurrenceTypeLabel: 'تجديد',
            eventDate: '2026-08-01',
            fileNumber: '150',
            fileType: 'سند جديد',
            year: 2026,
            receiptNumber: 'و-500',
            receiptDate: '2026-08-02',
          },
        ],
      },
    });
    renderView();

    const occHeading = await screen.findByText('وقوعات الملف');
    const occCard = occHeading.closest('div') as HTMLElement;
    expect(within(occCard).getByText(/تم شطب الملف بتاريخ/)).toBeInTheDocument();
    expect(within(occCard).getByText(/وجُدِّد الملف برقم 150/)).toBeInTheDocument();
    expect(within(occCard).getByRole('button', { name: 'عرض تفاصيل وقوعات الملف' })).toBeInTheDocument();

    const user = userEvent.setup();
    await user.click(within(occCard).getByRole('button', { name: 'عرض تفاصيل وقوعات الملف' }));
    const dialog = screen.getByRole('dialog', { name: 'وقوعات الملف' });
    expect(within(dialog).getByText('شطب')).toBeInTheDocument();
    expect(within(dialog).getByText('تجديد')).toBeInTheDocument();
    expect(within(dialog).getByText('الرقم المشطوب')).toBeInTheDocument();
    expect(within(dialog).getByText('99')).toBeInTheDocument();
    expect(within(dialog).getByText('رقم الملف الجديد')).toBeInTheDocument();
    expect(within(dialog).getByText('150')).toBeInTheDocument();
    expect(within(dialog).getByText('رقم ورود اخطار التجديد')).toBeInTheDocument();
    expect(within(dialog).getByText('و-500')).toBeInTheDocument();
  });

  it('يعرض الشخص الاعتباري المنفذ عليه بحقوله ويفتح نافذته الكاملة', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        generalEntitySide: 'executed',
        executedStatus: 'متداول',
        executedPublicEntities: [
          {
            id: 1,
            entityName: 'شركة الهدى',
            entityBranch: '',
            nature: 'legal',
            registrationNumber: '777',
            representedBy: 'المدير العام',
            addressType: 'يمثله',
            address: 'المحامي فلان',
          },
        ],
      },
    });
    renderView();

    const user = userEvent.setup();
    const heading = await screen.findByText('أطراف الملف التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    await user.click(within(card).getByText('شركة الهدى'));

    const dialog = screen.getByRole('dialog', { name: 'شخص اعتباري' });
    expect(within(dialog).getByText('شركة الهدى')).toBeInTheDocument();
    expect(within(dialog).getByText('777')).toBeInTheDocument();
    expect(within(dialog).getByText('المدير العام')).toBeInTheDocument();
    expect(within(dialog).getByText('المحامي فلان')).toBeInTheDocument();
  });

  it('يعرض المقترض الشخص الاعتباري في بطاقة أطراف الملف', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        borrowerName: 'شركة التجارة',
        borrowerFather: '',
        borrowerFamily: '',
        borrowerNature: 'legal',
        borrowerRegistrationNumber: '555',
        borrowerRepresentedBy: 'مديرها',
      },
    });
    renderView();

    const heading = await screen.findByText('أطراف الملف التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getByText('شركة التجارة')).toBeInTheDocument();
    expect(screen.queryByText('أحمد محمد الخطيب')).not.toBeInTheDocument();
  });

  it('يعرض محافظة الجهة العامة المنفذ عليها ويفتح نافذتها بحقل المحافظة', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        generalEntitySide: 'executed',
        executedPublicEntities: [
          { id: 1, entityName: 'المصرف العقاري', entityBranch: 'فرع المزة', governorate: 'دمشق' },
        ],
      },
    });
    renderView();

    const user = userEvent.setup();
    const heading = await screen.findByText('أطراف الملف التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getByText('محافظة دمشق')).toBeInTheDocument();

    await user.click(within(card).getByText(/المصرف العقاري/));
    const dialog = screen.getByRole('dialog', { name: 'الجهة العامة' });
    expect(within(dialog).getByText('المحافظة')).toBeInTheDocument();
    expect(within(dialog).getByText('دمشق')).toBeInTheDocument();
  });

  it('يعرض محافظة الشخص الاعتباري المنفذ عليه في نافذته الكاملة', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        generalEntitySide: 'executed',
        executedPublicEntities: [
          {
            id: 1,
            entityName: 'شركة الهدى',
            entityBranch: '',
            nature: 'legal',
            registrationNumber: '777',
            representedBy: 'المدير العام',
            governorate: 'حلب',
            addressType: 'يمثله',
            address: 'المحامي فلان',
          },
        ],
      },
    });
    renderView();

    const user = userEvent.setup();
    const heading = await screen.findByText('أطراف الملف التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getByText('محافظة حلب')).toBeInTheDocument();

    await user.click(within(card).getByText('شركة الهدى'));
    const dialog = screen.getByRole('dialog', { name: 'شخص اعتباري' });
    expect(within(dialog).getByText('المحافظة')).toBeInTheDocument();
    expect(within(dialog).getByText('حلب')).toBeInTheDocument();
  });

  it('يعرض محافظة الجهات العامة الطالبة للتنفيذ في بطاقة الأطراف', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        ...mockDoc,
        applicantPublicEntities: [
          { id: 1, name: 'المصرف التجاري السوري', branch: 'فرع 1', governorate: 'دمشق' },
          { id: 2, name: 'مديرية زراعة اللاذقية', branch: '', governorate: 'اللاذقية' },
        ],
      },
    });
    renderView();

    const heading = await screen.findByText('أطراف الملف التنفيذي');
    const card = heading.closest('div') as HTMLElement;
    expect(within(card).getByText('المصرف التجاري السوري (فرع 1) - محافظة دمشق')).toBeInTheDocument();
    expect(within(card).getByText('مديرية زراعة اللاذقية - محافظة اللاذقية')).toBeInTheDocument();
  });
});
