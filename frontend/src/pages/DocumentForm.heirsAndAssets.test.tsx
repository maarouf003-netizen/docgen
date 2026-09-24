// ملف اختبار موزّع من DocumentForm.test.tsx (سابقًا ملفًا أحاديًا ضخمًا).
// vi.hoisted/vi.mock تُكرَّر عمدًا في كل ملف موزَّع — vitest يعزل الملفات وكذا تقلبات المحاكاة.
// كتلة الاستيراد كاملة إلزامية (لا تختصرها — أي نقص يكسر tsc/oxlint):
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import DocumentForm from './DocumentForm';
import { api } from '../api/client';
import type { DocumentResponse } from '../types';
import { mockDoc } from './test/documentFormFixtures'; // البيانات الخام المشتركة (§7.3)

const { navigateMock, paramsMock, useAuthMock } = vi.hoisted(() => ({
  navigateMock: vi.fn(),
  paramsMock: { id: undefined as string | undefined },
  useAuthMock: vi.fn(),
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => navigateMock,
  useParams: () => paramsMock,
  Link: ({ children, to }: { children: ReactNode; to: string }) => <a href={to}>{children}</a>,
}));

vi.mock('../auth/useAuth', () => ({ useAuth: () => useAuthMock() }));

vi.mock('../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/client')>();
  return { ...original, api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } };
});

describe('DocumentForm · الورثة والأصول', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    paramsMock.id = undefined;
    useAuthMock.mockReturnValue({ user: { role: 'lawyer' } });
  });

  async function renderEdit(doc: DocumentResponse = mockDoc) {
    paramsMock.id = '1';
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: doc });
render(<DocumentForm />);
    return screen.findByRole('button', { name: 'حفظ التعديلات' }, { timeout: 5000 });
  }

  it('يعرض زر «إضافة وريث» للمقترض ويُرسل ورثته عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);

    const heirName = screen.getByLabelText('اسم الوريث');
    await user.type(heirName, 'محمود');
    await user.type(screen.getByLabelText('اسم أب الوريث'), 'خالد');
    const heirRow = heirName.closest('.grid') as HTMLElement;
    await user.type(
      Array.from(heirRow.querySelectorAll('input')).find(
        (el) => el.previousElementSibling?.textContent === 'النسبة',
      ) as HTMLInputElement,
      'الحلبي',
    );
    const heirAddress = Array.from(heirRow.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'العنوان',
    ) as HTMLInputElement;
    await user.type(heirAddress, 'المزة');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.borrowerHeirs).toEqual([
      { name: 'محمود', father: 'خالد', family: 'الحلبي', capacity: 'أصالة', addressType: 'عنوان', address: 'المزة' },
    ]);
  });


  it('يرسل ورثة الكفيل مع بيانات الكفيل عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    const card = screen.getByText('كفيل 1').closest('.rounded-xl') as HTMLElement;
    const gName = Array.from(card.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'الاسم',
    ) as HTMLInputElement;
    await user.type(gName, 'سمير');

    await user.click(within(card).getByRole('button', { name: '＋ إضافة وريث' }));
    await user.type(within(card).getByLabelText('اسم الوريث'), 'فارس');
    await user.type(within(card).getByLabelText('النسبة'), 'الخالد');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const guarantors = payload.guarantors as { heirs: unknown[] }[];
    expect(guarantors[0].heirs).toEqual([
      { name: 'فارس', father: '', family: 'الخالد', capacity: 'أصالة', addressType: 'عنوان', address: '' },
    ]);
  });


  it('يتجاهل ورثة بلا اسم ثلاثي عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.borrowerHeirs).toEqual([]);
  });


  it('يبدّل تسمية حقل الوريث بين «العنوان» و«الوكيل القانوني»', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);

    const heirName = screen.getByLabelText('اسم الوريث');
    const heirRow = heirName.closest('.grid') as HTMLElement;
    const typeSelect = within(heirRow).getByLabelText('نوع العنوان') as HTMLSelectElement;

    expect(Array.from(heirRow.querySelectorAll('input')).some((el) => el.previousElementSibling?.textContent === 'العنوان')).toBe(true);

    await user.selectOptions(typeSelect, 'وكيل');

    expect(Array.from(heirRow.querySelectorAll('input')).some((el) => el.previousElementSibling?.textContent === 'الوكيل القانوني')).toBe(true);
  });


  it('يعرض ورثة المقترض والكفيل المحفوظة عند التعديل', async () => {
    const docWithHeirs: DocumentResponse = {
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
    await renderEdit(docWithHeirs);

    expect(screen.getAllByLabelText('اسم الوريث')[0]).toHaveValue('محمود الحلبي');

    const card = screen.getByText('كفيل 1').closest('.rounded-xl') as HTMLElement;
    expect(within(card).getByLabelText('اسم الوريث')).toHaveValue('فارس الخالد');
    expect(within(card).getByLabelText('الوكيل القانوني')).toHaveValue('المحامي سامر');
  });


  it('يعرض ورثة المقترض في قائمة مالكي العقار ويُرسل المختارين عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.type(screen.getByLabelText('النسبة'), 'الخطيب');
    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);
    const heirName = screen.getByLabelText('اسم الوريث');
    await user.type(heirName, 'محمود');
    const heirRow = heirName.closest('.grid') as HTMLElement;
    await user.type(
      Array.from(heirRow.querySelectorAll('input')).find(
        (el) => el.previousElementSibling?.textContent === 'النسبة',
      ) as HTMLInputElement,
      'الحلبي',
    );

    await user.click(screen.getByRole('button', { name: /🏡 إضافة عقار/ }));

    const heirBox = screen.getByRole('checkbox', { name: 'محمود الحلبي' });
    const borrowerBox = screen.getByRole('checkbox', { name: 'أحمد الخطيب' });
    expect(heirBox).toBeInTheDocument();
    expect(borrowerBox).toBeInTheDocument();

    await user.click(heirBox);
    await user.click(borrowerBox);
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const assets = payload.assets as { owners: string[] }[];
    expect(assets[0].owners).toEqual(['محمود الحلبي', 'أحمد الخطيب']);
  });


  it('يحافظ على مالك محفوظ سابقًا غير موجود ضمن الخيارات عند التعديل', async () => {
    const docWithOldOwner: DocumentResponse = {
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
    await renderEdit(docWithOldOwner);

    expect(screen.getByRole('checkbox', { name: 'سمير حسن علي' })).toBeChecked();
  });


  it('يفرض «حصة سهمية» تلقائيًا عند اختيار أكثر من مالك ولا يرجعها عند النقص', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.type(screen.getByLabelText('النسبة'), 'الخطيب');

    const card = screen.getByText('كفيل 1').closest('.rounded-xl') as HTMLElement;
    const gName = Array.from(card.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'الاسم',
    ) as HTMLInputElement;
    await user.type(gName, 'سمير');
    const gFamily = Array.from(card.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'النسبة',
    ) as HTMLInputElement;
    await user.type(gFamily, 'علي');

    await user.click(screen.getByRole('button', { name: /🏡 إضافة عقار/ }));

    const shareSelect = () => {
      const div = screen.getByText('مقدار الحصة').closest('div');
      return div?.querySelector('select') as HTMLSelectElement;
    };
    expect(shareSelect()).toHaveValue('تمام العقار');

    await user.click(screen.getByRole('checkbox', { name: 'أحمد الخطيب' }));
    expect(shareSelect()).toHaveValue('تمام العقار');

    await user.click(screen.getByRole('checkbox', { name: 'سمير علي' }));
    expect(shareSelect()).toHaveValue('حصة سهمية');

    await user.click(screen.getByRole('checkbox', { name: 'أحمد الخطيب' }));
    expect(shareSelect()).toHaveValue('حصة سهمية');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const assets = payload.assets as { owners: string[]; shareType: string }[];
    expect(assets[0].owners).toEqual(['سمير علي']);
    expect(assets[0].shareType).toEqual('حصة سهمية');
  });


  it('يسمح بإدخال تاريخ تسجيل المتجر وترخيص المتجر غير المسجل كنص حر مع تطبيع الأرقام', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.type(screen.getByLabelText('النسبة'), 'الخطيب');

    await user.click(screen.getByRole('button', { name: /🏪 إضافة متجر/ }));
    const shopCard = screen.getByText('متجر 1').closest('.rounded-xl') as HTMLElement;
    const regDate = Array.from(shopCard.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'تاريخ التسجيل',
    ) as HTMLInputElement;
    expect(regDate).toHaveAttribute('placeholder', 'مثال: 1/8/2026');
    expect(regDate).toHaveAttribute('type', 'text');
    await user.type(regDate, '١/٨/٢٠٢٦');
    const regNumber = Array.from(shopCard.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'رقم السجل',
    ) as HTMLInputElement;
    await user.type(regNumber, '888');

    await user.click(screen.getByRole('button', { name: /🛒 إضافة متجر غير مسجل/ }));
    const unregCard = screen.getByText('متجر غير مسجل 2').closest('.rounded-xl') as HTMLElement;
    const licDate = Array.from(unregCard.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'تاريخ الترخيص',
    ) as HTMLInputElement;
    expect(licDate).toHaveAttribute('placeholder', 'مثال: 1/8/2026');
    expect(licDate).toHaveAttribute('type', 'text');
    await user.type(licDate, '15-1-2025');
    const licNumber = Array.from(unregCard.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'رقم الترخيص',
    ) as HTMLInputElement;
    await user.type(licNumber, '456');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const assets = payload.assets as { registrationDate?: string; licenseDate?: string }[];
    const shop = assets.find((a) => a.registrationDate);
    const unregistered = assets.find((a) => a.licenseDate);
    expect(shop?.registrationDate).toBe('1/8/2026');
    expect(unregistered?.licenseDate).toBe('15-1-2025');
  }, 12000);


  it('يصحّح حصة العقار إلى «حصة سهمية» عند التحميل لعقار بملاك متعددين', async () => {
    const docWithInvalidShare: DocumentResponse = {
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
    await renderEdit(docWithInvalidShare);

    const div = screen.getByText('مقدار الحصة').closest('div');
    expect(div?.querySelector('select')).toHaveValue('حصة سهمية');
  });


  it('يُظهر حقول «الممثل الشرعي» للمقترض ويخفي حقل عنوانه ويُرسل بياناته', async () => {
    const user = userEvent.setup();
    const { container } = render(<DocumentForm />);

    expect(container.querySelector('#borrowerAddress')).toBeTruthy();

    await user.click(screen.getAllByRole('button', { name: '＋ إضافة ممثل شرعي' })[0]);

    expect(container.querySelector('#borrowerAddress')).toBeNull();
    expect(screen.getByLabelText('اسم الممثل الشرعي')).toBeInTheDocument();
    expect(screen.getByLabelText('صفة الممثل الشرعي')).toBeInTheDocument();

    await user.type(screen.getByLabelText('اسم الممثل الشرعي'), 'الولي');
    await user.type(screen.getByLabelText('اسم أب الممثل الشرعي'), 'أب');
    await user.selectOptions(screen.getByLabelText('صفة الممثل الشرعي'), 'ولي');
    await user.selectOptions(screen.getByLabelText('نوع العنوان'), 'وكيل قانوني');
    await user.type(screen.getByLabelText('الوكيل القانوني'), 'المحامي سامر');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.borrowerRepresentativeName).toBe('الولي');
    expect(payload.borrowerRepresentativeFather).toBe('أب');
    expect(payload.borrowerRepresentativeCapacity).toBe('ولي');
    expect(payload.borrowerRepresentativeAddressType).toBe('وكيل قانوني');
    expect(payload.borrowerRepresentativeAddress).toBe('المحامي سامر');
    expect(payload.borrowerAddressType).toBe('');
    expect(payload.borrowerAddress).toBe('');
  });


  it('يخفي حقل عنوان المقترض عند إضافة وريث', async () => {
    const user = userEvent.setup();
    const { container } = render(<DocumentForm />);

    expect(container.querySelector('#borrowerAddress')).toBeTruthy();

    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);

    expect(container.querySelector('#borrowerAddress')).toBeNull();
  });


  it('يعرض قائمة «صفة الوريث» للوريث ويُرسل صفته عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);
    await user.type(screen.getByLabelText('اسم الوريث'), 'محمود');

    await user.selectOptions(screen.getByLabelText('صفة الوريث'), 'إضافة لتركة');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.borrowerHeirs).toEqual([
      { name: 'محمود', father: '', family: '', capacity: 'إضافة لتركة', addressType: 'عنوان', address: '' },
    ]);
  });


  it('يُرسل بيانات «الممثل الشرعي» للكفيل ويصفّر عنوان الكفيل', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    const card = screen.getByText('كفيل 1').closest('.rounded-xl') as HTMLElement;
    const gName = Array.from(card.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'الاسم',
    ) as HTMLInputElement;
    await user.type(gName, 'سمير');

    await user.click(within(card).getByRole('button', { name: '＋ إضافة ممثل شرعي' }));
    await user.type(within(card).getByLabelText('اسم الممثل الشرعي'), 'الوصي');
    await user.selectOptions(within(card).getByLabelText('صفة الممثل الشرعي'), 'وصي');
    await user.selectOptions(within(card).getByLabelText('نوع العنوان'), 'موطن مختار');
    await user.type(within(card).getByLabelText('الموطن المختار'), 'دمشق');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const guarantors = payload.guarantors as Record<string, unknown>[];
    expect(guarantors[0].representativeName).toBe('الوصي');
    expect(guarantors[0].representativeCapacity).toBe('وصي');
    expect(guarantors[0].representativeAddressType).toBe('موطن مختار');
    expect(guarantors[0].representativeAddress).toBe('دمشق');
    expect(guarantors[0].addressType).toBe('');
    expect(guarantors[0].address).toBe('');
  });


  it('يدخل جهة عمل كفالة الرواتب نصًا حرًا ويُرسل النص دون مفاتيح الربط', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.type(screen.getByLabelText('النسبة'), 'الخطيب');
    await user.click(screen.getByRole('button', { name: /💼 إضافة كفالة رواتب/ }));

    const salaryCard = screen.getByText(/كفالة رواتب 1/).closest('.rounded-xl') as HTMLElement;
    const ownerSelect = Array.from(salaryCard.querySelectorAll('select')).find(
      (el) => el.previousElementSibling?.textContent === 'صاحب الراتب',
    ) as HTMLSelectElement;
    await user.selectOptions(ownerSelect, 'أحمد الخطيب');

    const employer = screen.getByLabelText('الجهة العامة التي يعمل لديها');
    await user.type(employer, 'وزارة الصحة');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const assets = payload.assets as Record<string, unknown>[];
    expect(assets[0]).toEqual(expect.objectContaining({ assetKind: 'كفالة رواتب', publicEntity: 'وزارة الصحة' }));
    expect(assets[0]).not.toHaveProperty('publicEntityRegistryId');
  });


  it('يحفظ كفالة رواتب قديمة بجهة عمل نصية غير مرتبطة بالسجل (بلا إلزام ربط)', async () => {
    const user = userEvent.setup();
    await renderEdit({
      ...mockDoc,
      assets: [
        { id: 6, assetKind: 'كفالة رواتب', owners: ['أحمد محمد الخطيب'], publicEntity: 'وزارة الصحة' },
      ],
    } as DocumentResponse);

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.put).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.put).mock.calls[0] as [string, Record<string, unknown>];
    const assets = payload.assets as Record<string, unknown>[];
    expect(assets[0]).toEqual(expect.objectContaining({ assetKind: 'كفالة رواتب', publicEntity: 'وزارة الصحة' }));
  });

});

describe('DocumentForm · قفل تاريخ القاء الحجز بعد التسطير', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    paramsMock.id = undefined;
    useAuthMock.mockReturnValue({ user: { role: 'lawyer' } });
  });

  const seizedAsset = {
    id: 5, assetKind: 'عقار', owners: ['أحمد محمد الخطيب'],
    propertyNumber: '77', propertyDistrict: 'المزة', seizureDate: '1/8/2026',
  };
  const freeAsset = {
    id: 6, assetKind: 'عقار', owners: ['أحمد محمد الخطيب'],
    propertyNumber: '88', propertyDistrict: 'المزة', seizureDate: '',
  };
  const docWithAssets = { ...mockDoc, assets: [seizedAsset, freeAsset] } as DocumentResponse;
  // إنابة سارية على «عقار رقم 77» (مطابقة اللقطة بالنوع والوصف).
  const blockingDelegation = {
    id: 9,
    sourceDocumentId: 1,
    status: 'بانتظار رئيس القسم',
    isExternal: false,
    createdById: 7,
    createdAt: '2026-08-01',
    blocksAssets: true,
    assets: [{ id: 90, assetKind: 'عقار', assetLabel: 'عقار رقم 77', snapshotAdjusted: false }],
  };

  function mockEditGets(doc: DocumentResponse, delegations: unknown, failDelegations = false) {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/documents/1/delegations') {
        return failDelegations
          ? Promise.reject(new Error('network'))
          : Promise.resolve({ data: delegations });
      }
      return Promise.resolve({ data: doc });
    });
  }

  async function renderEditWith(doc: DocumentResponse, delegations: unknown, failDelegations = false) {
    paramsMock.id = '1';
    mockEditGets(doc, delegations, failDelegations);
    render(<DocumentForm />);
    await screen.findByRole('button', { name: 'حفظ التعديلات' }, { timeout: 5000 });
  }

  async function submittedAssets() {
    await waitFor(() => expect(api.put).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.put).mock.calls[0] as [string, Record<string, unknown>];
    return payload.assets as Record<string, unknown>[];
  }

  it('يمنع تفريغ تاريخ الحجز على الأصل المرجعي ويُبقي قيمته عند الحفظ', async () => {
    const user = userEvent.setup();
    await renderEditWith(docWithAssets, [blockingDelegation]);

    // التلميح التوضيحي يظهر تحت حقل الأصل المرجعي فقط.
    const notes = await screen.findAllByText('عليه إنابة سارية — التاريخ يُعدَّل ولا يُحذف');
    expect(notes).toHaveLength(1);

    const fields = screen.getAllByLabelText('تاريخ القاء الحجز');
    expect(fields).toHaveLength(2);
    // محاولة التفريغ تُتجاهل — تبقى القيمة السابقة.
    await user.clear(fields[0]);
    expect(fields[0]).toHaveValue('1/8/2026');

    await user.click(screen.getByRole('button', { name: 'حفظ التعديلات' }));
    const assets = await submittedAssets();
    expect(assets[0]).toEqual(expect.objectContaining({ seizureDate: '1/8/2026' }));
  });

  it('يسمح بتعديل تاريخ الحجز على الأصل المرجعي بقيمة جديدة', async () => {
    const user = userEvent.setup();
    await renderEditWith(docWithAssets, [blockingDelegation]);

    const fields = await screen.findAllByLabelText('تاريخ القاء الحجز');
    await user.tripleClick(fields[0]);
    await user.keyboard('5/8/2026');
    expect(fields[0]).toHaveValue('5/8/2026');

    await user.click(screen.getByRole('button', { name: 'حفظ التعديلات' }));
    const assets = await submittedAssets();
    expect(assets[0]).toEqual(expect.objectContaining({ seizureDate: '5/8/2026' }));
  });

  it('يسمح بتفريغ تاريخ الحجز على الأصل الحر', async () => {
    const user = userEvent.setup();
    await renderEditWith(
      { ...mockDoc, assets: [{ ...freeAsset, seizureDate: '2/8/2026' }] } as DocumentResponse,
      [blockingDelegation],
    );

    const fields = await screen.findAllByLabelText('تاريخ القاء الحجز');
    expect(fields).toHaveLength(1);
    await user.clear(fields[0]);
    expect(fields[0]).toHaveValue('');

    await user.click(screen.getByRole('button', { name: 'حفظ التعديلات' }));
    const assets = await submittedAssets();
    expect(assets[0]).toEqual(expect.objectContaining({ seizureDate: '' }));
  });

  it('فشل جلب الإنابات لا يعطل التحرير (مفتوح) والخلفي هو الضامن', async () => {
    const user = userEvent.setup();
    await renderEditWith(docWithAssets, [], true);

    // بلا تلميح قفل — والتحرير مسموح.
    expect(screen.queryByText('عليه إنابة سارية — التاريخ يُعدَّل ولا يُحذف')).not.toBeInTheDocument();
    const fields = screen.getAllByLabelText('تاريخ القاء الحجز');
    await user.clear(fields[0]);
    await waitFor(() => expect(fields[0]).toHaveValue(''));

    await user.click(screen.getByRole('button', { name: 'حفظ التعديلات' }));
    const assets = await submittedAssets();
    expect(assets[0]).toEqual(expect.objectContaining({ seizureDate: '' }));
  });
});
