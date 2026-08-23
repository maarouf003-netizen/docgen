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
    for (const header of ['رقم الأساس الاستئنافي', 'المستأنف', 'المستأنف عليهم', 'نتيجة الاستئناف', 'المحامي المختص']) {
      expect(screen.getByRole('columnheader', { name: header })).toBeInTheDocument();
    }
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
});
