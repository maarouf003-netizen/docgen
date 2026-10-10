import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import AppealDetail from './AppealDetail';
import type { AppealDto } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn(), patch: vi.fn() },
  getApiErrorMessage: () => 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

function appeal(overrides: Partial<AppealDto> = {}): AppealDto {
  return {
    id: 7,
    documentId: 10,
    documentLabel: 'ملف 10',
    direction: 'appellants',
    directionLabel: 'المستأنفون',
    status: 'pending',
    statusLabel: 'منظور',
    appellants: [],
    appellees: [],
    needsRotation: false,
    partiesDegraded: false,
    createdById: 3,
    createdAt: '2026-08-01',
    forwardState: 'Owned',
    ...overrides,
  } as AppealDto;
}

function renderDetail() {
  return render(
    <MemoryRouter initialEntries={['/appeals/7']}>
      <Routes>
        <Route path="/appeals/:id" element={<AppealDetail />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('AppealDetail — الإحالة لرئيس القسم (§6)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
  });

  function mockAppeal(a: AppealDto) {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/appeals/7') return Promise.resolve({ data: a });
      return Promise.resolve({ data: { id: 10, executionActions: [] } });
    });
  }

  it('رئيس الشعبة يرى زر الإحالة على المنظور غير المحال', async () => {
    useAuthMock.mockReturnValue({ user: { id: 6, role: 'subhead' } });
    mockAppeal(appeal());
    renderDetail();

    expect(await screen.findByRole('button', { name: 'إحالة لرئيس القسم' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'استرجاع الإحالة' })).not.toBeInTheDocument();
  });

  it('الإحالة تُرسَل بنسخة التزامن وتُحدَّث الشارة', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ user: { id: 6, role: 'subhead' } });
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockImplementation((url: string) => {
      if (url === '/appeals/7') return Promise.resolve({ data: appeal() });
      return Promise.resolve({ data: { id: 10, executionActions: [] } });
    });
    renderDetail();

    await user.click(await screen.findByRole('button', { name: 'إحالة لرئيس القسم' }));
    expect(api.post).toHaveBeenCalledWith('/appeals/7/forward', { version: null });
  });

  it('المحيل يسترجع إحالته قبل الإسناد مع شارة البقاء حتى الحسم', async () => {
    useAuthMock.mockReturnValue({ user: { id: 6, role: 'subhead' } });
    mockAppeal(appeal({ forwardState: 'ForwardedToHead', assignedLawyerId: undefined }));
    renderDetail();

    expect(await screen.findByRole('button', { name: 'استرجاع الإحالة' })).toBeInTheDocument();
    expect(screen.getByText(/يبقى مرئيًا حتى الحسم النهائي/)).toBeInTheDocument();
  });

  it('رئيس القسم يعيد الإحالة للشعبة قبل الإسناد', async () => {
    useAuthMock.mockReturnValue({ user: { id: 5, role: 'head' } });
    mockAppeal(appeal({ forwardState: 'ForwardedToHead', assignedLawyerId: undefined }));
    renderDetail();

    expect(await screen.findByRole('button', { name: 'إعادة الإحالة للشعبة' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إحالة لرئيس القسم' })).not.toBeInTheDocument();
  });

  it('الإسناد متاح لرئيس القسم والشعبة على المنظور', async () => {
    useAuthMock.mockReturnValue({ user: { id: 5, role: 'head' } });
    mockAppeal(appeal());
    const { unmount } = renderDetail();
    expect(await screen.findByRole('button', { name: 'إسناد لمحامٍ' })).toBeInTheDocument();
    unmount();

    useAuthMock.mockReturnValue({ user: { id: 6, role: 'subhead' } });
    mockAppeal(appeal());
    renderDetail();
    expect(await screen.findByRole('button', { name: 'إسناد لمحامٍ' })).toBeInTheDocument();
  });

  it('غير المتابع (رئيس) يرى زر عرض الإجراءات لا زر الإدخال', async () => {
    useAuthMock.mockReturnValue({ user: { id: 5, role: 'head' } });
    mockAppeal(appeal());
    renderDetail();

    expect(await screen.findByRole('button', { name: 'عرض الإجراءات والملاحظات' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إدخال ملاحظات وإجراءات جديدة' })).not.toBeInTheDocument();
  });

  it('المتابع يرى زر الإدخال', async () => {
    useAuthMock.mockReturnValue({ user: { id: 7, role: 'lawyer' } });
    mockAppeal(appeal({ assignedLawyerId: 7 }));
    renderDetail();

    expect(await screen.findByRole('button', { name: 'إدخال ملاحظات وإجراءات جديدة' })).toBeInTheDocument();
  });
});
