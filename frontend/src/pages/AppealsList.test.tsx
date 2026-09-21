import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AppealsList from './AppealsList';
import { makeDocument } from '../test/factories';
import { stubMobile } from '../test/stubMobile';
import type { AppealDto } from '../types';

const { apiMock, errorMessageMock, authMock } = vi.hoisted(() => ({
  apiMock: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  errorMessageMock: vi.fn(() => 'خطأ'),
  authMock: vi.fn(),
}));

vi.mock('../api/client', () => ({ api: apiMock, getApiErrorMessage: errorMessageMock }));
vi.mock('../auth/useAuth', () => ({ useAuth: () => authMock() }));

vi.mock('react-router-dom', async (importOriginal) => {
  const original = await importOriginal<typeof import('react-router-dom')>();
  return { ...original, Link: ({ to, children, ...rest }: any) => <a href={to} {...rest}>{children}</a> };
});

function makeAppeal(overrides: Partial<AppealDto> = {}): AppealDto {
  const doc = makeDocument();
  return {
    id: 1,
    documentId: doc.id,
    documentLabel: 'أحمد خالد الخطيب',
    fileNumber: doc.fileNumber,
    fileType: doc.fileType,
    fileYear: doc.fileYear,
    court: doc.court,
    direction: 'appellants',
    directionLabel: 'مستأنِفين',
    status: 'pending',
    statusLabel: 'منظور',
    appellants: [{ kind: 'applicant-entity', partyId: 1, name: 'المؤسسة العامة للكهرباء' }],
    appellees: [
      { kind: 'borrower', partyId: 9, name: 'أحمد خالد الخطيب' },
      { kind: 'guarantor', partyId: 10, name: 'كفيل آخر' },
    ],
    appealedDecisionText: 'نص القرار',
    appealedDecisionSummary: 'ملخص القرار المستأنف',
    needsRotation: false,
    createdAt: '2026-08-01T00:00:00Z',
    createdById: 55,
    ...overrides,
  };
}

function setup(role: 'lawyer' | 'head' | 'manager', userId = 7) {
  authMock.mockReturnValue({ user: { id: userId, role } });
}

beforeEach(() => {
  stubMobile(false);
  vi.clearAllMocks();
  apiMock.get.mockResolvedValue({ data: { items: [], totalCount: 0, totalPages: 1 } });
});

describe('AppealsList', () => {
  it('يعرض الأعمدة المعتمدة والشارات والنتيجة الملونة للمدير', async () => {
    setup('manager');
    apiMock.get.mockResolvedValueOnce({
      data: {
        items: [
          makeAppeal(),
          makeAppeal({ id: 2, status: 'decided', outcome: 'against', outcomeLabel: 'للضد', decisionNumber: 'قرار-9' }),
        ],
        totalCount: 2,
        totalPages: 1,
      },
    });
    render(<AppealsList />);

    expect(await screen.findByRole('table')).toBeInTheDocument();
    for (const header of ['الأساس والنوع', 'المستأنف', 'المستأنف عليهم', 'الحسم', 'نتيجة الاستئناف', 'المحامي المختص']) {
      expect(screen.getByRole('columnheader', { name: header })).toBeInTheDocument();
    }
    // الأعمدة المحذوفة بالدمج (C2): لا «نوع الاستئناف» ولا «تاريخ قرار الحسم» ولا «إجراءات» مستقلة.
    for (const gone of ['نوع الاستئناف', 'تاريخ قرار الحسم', 'إجراءات']) {
      expect(screen.queryByRole('columnheader', { name: gone })).not.toBeInTheDocument();
    }
    // صف الإجراءات: خلية واحدة بعرض الجدول (9 + عمود المحامي المختص للمدير).
    const spanned = document.querySelectorAll('td[colspan]');
    expect(spanned).toHaveLength(2);
    expect(spanned[0].getAttribute('colspan')).toBe('10');
    expect(screen.getAllByText('منظور').length).toBeGreaterThan(0);
    expect(screen.getByText('للضد')).toHaveClass('text-red-700');
    expect(screen.getByText('قرار-9')).toBeInTheDocument();
    // الاسم الأول فقط للمستأنف عليهم (باسمه الأول دون بقية الأسماء).
    expect(screen.getAllByText('أحمد خالد الخطيب').length).toBeGreaterThan(0);
    expect(screen.queryByText('كفيل آخر')).not.toBeInTheDocument();
  });

  it('يخفي عمود المحامي المختص عن المحامي ويعرض أزرار المتابع فقط له', async () => {
    setup('lawyer', 7);
    apiMock.get.mockResolvedValueOnce({
      data: { items: [makeAppeal({ assignedLawyerId: 7 })], totalCount: 1, totalPages: 1 },
    });
    render(<AppealsList />);

    await screen.findByRole('table');
    expect(screen.queryByRole('columnheader', { name: 'المحامي المختص' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'حسم' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'مشطوب' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تعديل القيد' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /إسناد لمحام/ })).not.toBeInTheDocument();
  });

  it('يرسل معاملات البحث والحالة في الطلب', async () => {
    setup('lawyer');
    const user = userEvent.setup();
    render(<AppealsList />);

    // الفلتر الابتدائي منظور: أول طلب يحمل status=pending دون تدخل المستخدم.
    await vi.waitFor(() => {
      expect(apiMock.get).toHaveBeenCalled();
    });
    expect(apiMock.get.mock.calls[0]?.[0]).toBe('/appeals');
    expect(apiMock.get.mock.calls[0]?.[1]?.params).toMatchObject({ status: 'pending' });

    await user.type(await screen.findByLabelText(/بحث في الاستئنافات/), 'المؤسسة');
    await user.selectOptions(await screen.findByLabelText(/فلتر الحالة/), 'pending');

    await vi.waitFor(() => {
      const last = apiMock.get.mock.calls.at(-1);
      expect(last?.[0]).toBe('/appeals');
      expect(last?.[1]?.params).toMatchObject({ q: 'المؤسسة', status: 'pending', perPage: 20 });
    });
  });

  it('يفتح نافذة الإسناد لرئيس القسم ويُرسل للمحامي المختار', async () => {
    setup('head', 99);
    apiMock.get.mockImplementation((url: string) => {
      if (url === '/users/lawyers') return Promise.resolve({ data: [{ id: 3, fullName: 'محامي فرع', isActive: true }] });
      if (url === '/appeals')
        return Promise.resolve({ data: { items: [{ ...makeAppeal(), assignedLawyerId: undefined } as unknown as AppealDto], totalCount: 1, totalPages: 1 } });
      return Promise.resolve({ data: {} });
    });
    apiMock.post.mockResolvedValueOnce({ data: { ...makeAppeal(), assignedLawyerId: 3, assignedLawyerName: 'محامي فرع' } });
    const user = userEvent.setup();
    render(<AppealsList />);

    await screen.findByRole('table');
    await user.click(screen.getByRole('button', { name: 'إسناد لمحامٍ' }));

    const dialog = await screen.findByRole('dialog', { name: /إسناد الاستئناف لمحام/ });
    await user.selectOptions(within(dialog).getByLabelText(/المحامي المختص للمتابعة/), '3');
    await user.click(within(dialog).getByRole('button', { name: 'إسناد' }));

    await vi.waitFor(() => {
      expect(apiMock.post).toHaveBeenCalledWith('/appeals/1/assign', { assignedLawyerId: 3 });
    });
  });

  it('زر التدوير للمسند المنظور فقط: أحمر عند الحاجة ومحايد otherwise', async () => {
    setup('lawyer', 7);
    apiMock.get.mockResolvedValueOnce({
      data: {
        items: [
          makeAppeal({ id: 1, assignedLawyerId: 7, needsRotation: true, currentBaseNumber: '100/2025' }),
          makeAppeal({ id: 2, assignedLawyerId: 7, needsRotation: false, currentBaseNumber: '200/2026' }),
          // المنشئ غير المسند: لا زر تدوير.
          makeAppeal({ id: 3, assignedLawyerId: 9, createdById: 7 }),
          // المحسوم المسند: لا زر تدوير.
          makeAppeal({ id: 4, assignedLawyerId: 7, status: 'decided' }),
        ],
        totalCount: 4,
        totalPages: 1,
      },
    });
    render(<AppealsList />);

    await screen.findByRole('table');
    // زرّا التدوير في صفّي الإجراءات (اسمهما المُعلن من aria-label) — للمسندين المنظورين فقط.
    const rotationButtons = screen.getAllByRole('button', { name: /تدوير رقم الأساس الاستئنافي/ });
    expect(rotationButtons).toHaveLength(2);
    expect(rotationButtons[0]).toHaveClass('text-red-700');
    expect(rotationButtons[1]).not.toHaveClass('text-red-700');
  });

  it('يخفي زر النقل لرئيس القسم عن الاستئناف المحسوم بدل تعطيله', async () => {
    setup('head', 99);
    apiMock.get.mockResolvedValueOnce({
      data: { items: [makeAppeal({ status: 'decided', assignedLawyerId: 7 })], totalCount: 1, totalPages: 1 },
    });
    render(<AppealsList />);

    await screen.findByRole('table');
    expect(screen.queryByRole('button', { name: 'نقل المحامي' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إسناد لمحامٍ' })).not.toBeInTheDocument();
  });
});
