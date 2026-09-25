import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import PortalFileDetail from './PortalFileDetail';
import { makeDocument } from '../test/factories';
import { stubMobile } from '../test/stubMobile';
import type {
  AppealDto,
  DelegationDto,
  DocumentResponse,
  PortalExecutionActionDto,
} from '../types';

const { apiMock, errorMessageMock } = vi.hoisted(() => ({
  apiMock: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  errorMessageMock: vi.fn(() => 'خطأ من الخادم'),
}));

vi.mock('../api/client', () => ({ api: apiMock, getApiErrorMessage: errorMessageMock }));

function portalDoc(overrides: Partial<DocumentResponse> = {}): DocumentResponse {
  return makeDocument({
    documentType: 'ملف تنفيذي',
    contractType: 'قرض',
    contractTypeSelector: 'تسليف',
    contractNumber: 'C1',
    contractDate: '2026-01-01',
    amountNumeric: 1000,
    amountWords: 'ألف',
    currency: 'ل.س',
    execStatus: '',
    lawyer: 'المحامي سامر',
    createdByName: 'المحامي سامر',
    displayFileNumber: '99',
    displayFileYear: '2026',
    fileType: 'حقوق',
    assets: [{ id: 1, assetKind: 'عقار', propertyNumber: '10', propertyDistrict: 'المزة' }],
    ...overrides,
  });
}

function portalAppeal(overrides: Partial<AppealDto> = {}): AppealDto {
  const doc = makeDocument();
  return {
    id: 5,
    documentId: doc.id,
    documentLabel: 'أحمد خالد الخطيب',
    fileNumber: doc.fileNumber,
    fileType: doc.fileType,
    fileYear: doc.fileYear,
    court: 'محكمة دمشق',
    direction: 'against-us',
    directionLabel: 'مستأنف علينا',
    status: 'pending',
    statusLabel: 'منظور',
    appealTypeLabel: 'استئناف',
    appellants: [{ kind: 'applicant-entity', partyId: 1, name: 'المؤسسة العامة للكهرباء' }],
    appellees: [{ kind: 'borrower', partyId: 9, name: 'أحمد خالد الخطيب' }],
    appealedDecisionText: 'نص القرار المستأنف',
    appealedDecisionDate: '2026-07-20',
    noticeNumber: 'N-9',
    noticeDate: '2026-07-21',
    defenseOpinion: 'رأي سري داخلي',
    // ملاحظات المحامي الحرة: تُصفَّر خلفيًا (ق10 الموسّعة) فلا تصل هنا أصلًا —
    // المصنع يحاكي عقد الخلفية الحقيقي (notes غائبة)، والضمان مُثبَت خلفيًا.
    assignedLawyerName: 'المحامي سامر',
    createdAt: '2026-08-01T00:00:00Z',
    createdByName: 'المدخل',
    createdById: 55,
    needsRotation: false,
    ...overrides,
  };
}

const action: PortalExecutionActionDto = {
  id: 1,
  text: 'إخطار الجهة العامة بموعد الجلسة',
  actionDate: '5/9/2026',
  createdByName: 'المحامي سامر',
  createdAt: '2026-09-01T10:00:00Z',
};

function baseDelegation(overrides: Partial<DelegationDto> = {}): DelegationDto {
  return {
    id: 10,
    sourceDocumentId: 2,
    sourceDocumentLabel: 'ملف رقم 2',
    sourceFileNumber: '55',
    sourceFileYear: '2025',
    sourceFileType: 'حقوق',
    sourceCourt: 'محكمة دمشق',
    delegatedCourt: 'محكمة دمشق',
    isExternal: false,
    delegationDate: '2026-07-01',
    delegationText: 'نص قرار الإنابة',
    status: 'assigned',
    createdAt: '2026-07-01T00:00:00Z',
    createdById: 7,
    createdByName: 'المحامي سامر',
    assets: [],
    ...overrides,
  };
}

function setEndpoints({
  doc = portalDoc(),
  delegations = [],
  appealDetails = [],
  executionActions = [],
  baseNumbers = [],
}: {
  doc?: DocumentResponse;
  delegations?: DelegationDto[];
  appealDetails?: AppealDto[];
  executionActions?: PortalExecutionActionDto[];
  baseNumbers?: Array<{ year: number; baseNumber: string }>;
} = {}) {
  (apiMock.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url === '/portal/files/1') return Promise.resolve({ data: doc });
    if (url === '/portal/files/1/delegations') return Promise.resolve({ data: delegations });
    if (url === '/portal/files/1/appeals/details') return Promise.resolve({ data: appealDetails });
    if (url === '/portal/files/1/execution-actions') {
      return Promise.resolve({ data: executionActions });
    }
    if (url === '/portal/files/1/base-numbers') return Promise.resolve({ data: baseNumbers });
    return Promise.reject(new Error(`unexpected GET ${url}`));
  });
}

function renderPage(entry = '/portal/files/1') {
  return render(
    <MemoryRouter initialEntries={[entry]}>
      <Routes>
        <Route path="/portal/files/:id" element={<PortalFileDetail />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  stubMobile(false);
});

describe('PortalFileDetail المرآة القرائية', () => {
  it('يعرض بطاقات المرآة القرائية السبع ويجلب نقاط البوابة الجديدة', async () => {
    setEndpoints({
      doc: portalDoc(),
      executionActions: [action],
      appealDetails: [portalAppeal()],
    });
    renderPage();

    for (const title of [
      'أطراف الملف التنفيذي',
      'بيانات الملف',
      'بيانات السند التنفيذي',
      'الأموال المنقولة وغير المنقولة',
      'وقوعات الملف',
      'الحالة',
      'الإجراءات التنفيذية',
    ]) {
      expect(await screen.findByText(title)).toBeInTheDocument();
    }

    // الإجراءات تظهر نصًا وتاريخًا ومحاميًا (ق7).
    expect(screen.getByText('إخطار الجهة العامة بموعد الجلسة')).toBeInTheDocument();
    expect(screen.getByText('5/9/2026')).toBeInTheDocument();
    expect(screen.getByText('· المحامي سامر')).toBeInTheDocument();

    // شريط الهوية: رقم/سنة/دائرة.
    expect(screen.getByText('99')).toBeInTheDocument();
    expect(screen.getByText('2026')).toBeInTheDocument();

    // النقاط الأربع كلها تُستدعى.
    expect(apiMock.get).toHaveBeenCalledWith('/portal/files/1', expect.anything());
    expect(apiMock.get).toHaveBeenCalledWith('/portal/files/1/delegations', expect.anything());
    expect(apiMock.get).toHaveBeenCalledWith('/portal/files/1/appeals/details', expect.anything());
    expect(apiMock.get).toHaveBeenCalledWith('/portal/files/1/execution-actions', expect.anything());
  });

  it('يخلو تمامًا من أزرار/بطاقات الاستثناءات (ق6)', async () => {
    setEndpoints();
    renderPage();
    await screen.findByText('أطراف الملف التنفيذي');

    for (const forbidden of [
      'تسطير إنابة',
      'استئناف ▾',
      'توليد مستندات',
      'الإجراءات والملاحظات',
      'تغيير الحالة',
      'كتب المطالعة',
      'سجل التعديلات',
      'تسجيل أصولًا',
      'إتمام الإنابة',
    ]) {
      expect(screen.queryByText(forbidden)).not.toBeInTheDocument();
    }
  });

  it('يعرض بديلًا صريحًا عند غياب الوقوعات والاستئنافات (ف9)', async () => {
    setEndpoints();
    renderPage();

    await screen.findByText('وقوعات الملف');
    expect(screen.getByText('لا توجد وقوعات أو استئنافات على هذا الملف')).toBeInTheDocument();
  });

  it('نافذة أرقام الأساس تجلب من نقطة البوابة (ف7)', async () => {
    setEndpoints({
      doc: portalDoc(),
      baseNumbers: [{ year: 2025, baseNumber: '55' }],
    });
    const user = userEvent.setup();
    renderPage();

    await user.click(
      await screen.findByRole('button', { name: 'عرض أرقام الأساس للسنوات السابقة' }),
    );

    expect(await screen.findByRole('dialog', { name: 'أرقام الأساس للسنوات السابقة' })).toBeInTheDocument();
    expect(apiMock.get).toHaveBeenCalledWith('/portal/files/1/base-numbers');
    expect(screen.getByText('55')).toBeInTheDocument();
  });

  it('بلاطة «المحامي المختص» تفتح سجل التعاقب من بيانات الاستجابة ذاتها (ف3/ف5)', async () => {
    setEndpoints({
      doc: portalDoc({
        assignments: [
          { id: 1, kind: 'create' as const, lawyerName: 'المحامي سامر', assignedAt: '2026-07-01' },
          { id: 2, kind: 'transfer' as const, lawyerName: 'المحامية لينا', assignedByName: 'رئيس القسم', assignedAt: '2026-08-01' },
        ],
      }),
    });
    const user = userEvent.setup();
    renderPage();

    await user.click(
      await screen.findByRole('button', { name: 'عرض سجل التعاقب على الملف' }),
    );

    const dialog = screen.getByRole('dialog', { name: 'سجل التعاقب على الملف' });
    expect(within(dialog).getByText('منشئ الملف')).toBeInTheDocument();
    expect(within(dialog).getByText('المحامية لينا')).toBeInTheDocument();
    expect(within(dialog).getByText('أحالها: رئيس القسم')).toBeInTheDocument();
  });

  it('نافذة الاستئناف تُبقي اسم المحامي وسطر «سطّره» وتخفي رأيه (ق9/ق10)', async () => {
    setEndpoints({ doc: portalDoc(), appealDetails: [portalAppeal()] });
    const user = userEvent.setup();
    renderPage();

    await user.click(
      await screen.findByRole('button', { name: /عرض تفاصيل استئناف/ }),
    );

    const dialog = await screen.findByRole('dialog', { name: 'تفاصيل الاستئناف رقم 5' });
    expect(within(dialog).getByText('المحامي المتابع')).toBeInTheDocument();
    expect(within(dialog).getByText('المحامي سامر')).toBeInTheDocument();
    expect(within(dialog).getByText(/سطّره: المدخل/)).toBeInTheDocument();
    expect(within(dialog).queryByText('رأي المحامي المتابع بأسباب الاستئناف')).not.toBeInTheDocument();
    expect(within(dialog).queryByText('رأي سري داخلي')).not.toBeInTheDocument();
    // ق10 الموسّعة: خلية «ملاحظات» الاستئناف غائبة عن نافذة البوابة (تُصفَّر خلفيًا).
    expect(within(dialog).queryByText('ملاحظات')).not.toBeInTheDocument();
  });

  it('تشعبات الملف قرائية: بلا تسطير ولا تعديل/حذف (ق3)', async () => {
    setEndpoints({ doc: portalDoc(), delegations: [baseDelegation()] });
    renderPage();

    expect(await screen.findByText('تشعبات الملف')).toBeInTheDocument();
    expect(screen.getByText('نص قرار الإنابة')).toBeInTheDocument();
    expect(screen.getByText('إنابة داخلية')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تسطير إنابة' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تعديل' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حذف' })).not.toBeInTheDocument();
  });

  it('معلومات الملف المنيب قرائية بلا زر «تسجيل أصولًا» (ق4)', async () => {
    setEndpoints({
      doc: portalDoc(),
      delegations: [baseDelegation({ targetDocumentId: 1 })],
    });
    renderPage();

    expect(await screen.findByText('معلومات الملف المنيب')).toBeInTheDocument();
    expect(screen.getByText('ملف رقم 2')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تسجيل أصولًا' })).not.toBeInTheDocument();
  });

  it('يعرض خطأ الجلب مع رابط رجوع', async () => {
    apiMock.get.mockRejectedValue({});
    renderPage();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'رجوع إلى الملفات التنفيذية' })).toBeInTheDocument();
  });

  it('يعرض شريط إعادة المحاولة عند تعذر تحميل الوقوعات/الاستئنافات ثم يعيد التحميل بنجاح', async () => {
    let appealsCalls = 0;
    (apiMock.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/portal/files/1') return Promise.resolve({ data: portalDoc() });
      if (url === '/portal/files/1/appeals/details') {
        appealsCalls += 1;
        return appealsCalls === 1
          ? Promise.reject(new Error('network'))
          : Promise.resolve({ data: [portalAppeal()] });
      }
      if (url.startsWith('/portal/files/1/')) return Promise.resolve({ data: [] });
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });
    const user = userEvent.setup();
    renderPage();

    const retry = await screen.findByRole('button', { name: 'إعادة المحاولة' });
    expect(
      screen.getByText('تعذر تحميل الوقوعات والاستئنافات — تفقّد الاتصال وأعد المحاولة.'),
    ).toBeInTheDocument();

    await user.click(retry);

    expect(
      await screen.findByRole('button', { name: /عرض تفاصيل استئناف قرار رئيس التنفيذ/ }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إعادة المحاولة' })).not.toBeInTheDocument();
  });

  it('يعرض شريط إعادة المحاولة عند تعذر تحميل الإجراءات التنفيذية ثم يعيد التحميل بنجاح', async () => {
    let actionsCalls = 0;
    (apiMock.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/portal/files/1') return Promise.resolve({ data: portalDoc() });
      if (url === '/portal/files/1/execution-actions') {
        actionsCalls += 1;
        return actionsCalls === 1
          ? Promise.reject(new Error('network'))
          : Promise.resolve({ data: [action] });
      }
      if (url.startsWith('/portal/files/1/')) return Promise.resolve({ data: [] });
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });
    const user = userEvent.setup();
    renderPage();

    const retry = await screen.findByRole('button', { name: 'إعادة المحاولة' });
    expect(
      screen.getByText('تعذر تحميل الإجراءات التنفيذية — تفقّد الاتصال وأعد المحاولة.'),
    ).toBeInTheDocument();

    await user.click(retry);

    expect(await screen.findByText('إخطار الجهة العامة بموعد الجلسة')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إعادة المحاولة' })).not.toBeInTheDocument();
  });

  it('يعرض زر إعادة المحاولة عند تعذر تحميل الملف الرئيسي ثم ينجح', async () => {
    let docCalls = 0;
    (apiMock.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/portal/files/1') {
        docCalls += 1;
        return docCalls === 1
          ? Promise.reject(new Error('network'))
          : Promise.resolve({ data: portalDoc() });
      }
      if (url.startsWith('/portal/files/1/')) return Promise.resolve({ data: [] });
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });
    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'إعادة المحاولة' }));

    // بعد نجاح إعادة المحاولة تُعرض بطاقات المرآة بدل شاشة الخطأ.
    expect(await screen.findByText('أطراف الملف التنفيذي')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('ينقل التركيز إلى القسم بعد نجاح إعادة تحميل الإجراءات التنفيذية', async () => {
    let actionsCalls = 0;
    (apiMock.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/portal/files/1') return Promise.resolve({ data: portalDoc() });
      if (url === '/portal/files/1/execution-actions') {
        actionsCalls += 1;
        return actionsCalls === 1
          ? Promise.reject(new Error('network'))
          : Promise.resolve({ data: [action] });
      }
      if (url.startsWith('/portal/files/1/')) return Promise.resolve({ data: [] });
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole('button', { name: 'إعادة المحاولة' }));

    await screen.findByText('إخطار الجهة العامة بموعد الجلسة');
    // التركيز انتقل إلى مجموعة القسم (يُعلن محتواها لقارئ الشاشة) بدل ضياعه.
    expect(screen.getByRole('group', { name: 'قسم الإجراءات التنفيذية' })).toHaveFocus();
  });

  it('على الجوال: يعرض التبويبات الأربعة وينقل بين الأقسام (فرع التبويبات)', async () => {    stubMobile(true);
    setEndpoints({ doc: portalDoc() });
    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByRole('tab', { name: 'المعلومات' })).toBeInTheDocument();
    for (const label of ['السند والأموال', 'الإنابات والوقوعات', 'الحالة']) {
      expect(screen.getByRole('tab', { name: label })).toBeInTheDocument();
    }
    expect(screen.getByRole('tabpanel', { name: 'المعلومات' })).toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'السند والأموال' }));
    expect(await screen.findByText('بيانات السند التنفيذي')).toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'الإنابات والوقوعات' }));
    expect(
      await screen.findByText('لا توجد وقوعات أو استئنافات على هذا الملف'),
    ).toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'الحالة' }));
    expect(await screen.findByText('الإجراءات التنفيذية')).toBeInTheDocument();
    expect(screen.getByText('لا توجد إجراءات تنفيذية')).toBeInTheDocument();
  });

  it('على الجوال: يفتح نافذة الاستئناف من تبويب «الإنابات والوقوعات»', async () => {
    stubMobile(true);
    setEndpoints({ doc: portalDoc(), appealDetails: [portalAppeal()] });
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole('tab', { name: 'الإنابات والوقوعات' }));
    await user.click(await screen.findByRole('button', { name: /عرض تفاصيل استئناف/ }));

    const dialog = await screen.findByRole('dialog', { name: 'تفاصيل الاستئناف رقم 5' });
    expect(within(dialog).getByText('المحامي المتابع')).toBeInTheDocument();
  });
});