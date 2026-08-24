import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import ReviewsList from './ReviewsList';
import type { ReviewLetterListItemDto } from '../types';

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
    },
  };
});

import { api } from '../api/client';

const linkedLetter = (): ReviewLetterListItemDto => ({
  id: 1,
  letterNumber: 'DAM-2026-1234',
  letterDate: '2026-08-01T09:00:00Z',
  isAnswered: false,
  documentId: 7,
  fileContext: {
    executedName: 'أحمد محمد العلي',
    fileNumber: '77/2026',
    fileType: 'تنفيذي',
    fileYear: '2026',
    court: 'دائرة تنفيذ دمشق',
  },
  lawyerName: 'المحامي الأول',
  snippet: 'نطلب التوجيه في الأمر',
  lastKind: 'letter',
  hasUnseenReply: false,
  messagesCount: 1,
  updatedAt: '2026-08-01T09:00:00Z',
});

const generalLetter = (): ReviewLetterListItemDto => ({
  ...linkedLetter(),
  id: 2,
  letterNumber: 'DAM-2026-5678',
  documentId: null,
  fileContext: null,
  isAnswered: true,
});

function renderList() {
  return render(
    <MemoryRouter>
      <ReviewsList />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('ReviewsList', () => {
  it('يعرض سطور الكتب بالصيغة المعتمدة مع رقم وتاريخ لكل كتاب (محامي)', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', id: 3 }, hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [linkedLetter(), generalLetter()], page: 1, perPage: 20, totalCount: 2 },
    });
    renderList();

    expect(await screen.findByText(/مطالعة بملف \(أحمد محمد العلي\) رقم 77\/2026/)).toBeInTheDocument();
    expect(screen.getByText('كتاب مطالعة عام غير مرتبط بملف')).toBeInTheDocument();
    expect(screen.getAllByText('DAM-2026-1234').length).toBeGreaterThan(0);
    expect(api.get).toHaveBeenCalledWith(
      '/review-letters?page=1&perPage=20',
      expect.any(Object),
    );
  });

  it('يعرض الشارة الحمراء «بانتظار رد» والخضراء «تم الرد»', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'head', id: 5 }, hasFullAccess: false, isHead: true });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [linkedLetter(), generalLetter()], page: 1, perPage: 20, totalCount: 2 },
    });
    renderList();

    await screen.findByText(/مطالعة بملف/);
    expect(await screen.findByText('بانتظار رد')).toBeInTheDocument();
    expect(screen.getByText('تم الرد')).toBeInTheDocument();
    // رئيس القسم يرى اسم المحامي الذي سطّر الكتاب
    const lawyerLines = screen.getAllByText(/سطّره:/);
    expect(lawyerLines.length).toBeGreaterThan(0);
    expect(lawyerLines[0]).toHaveTextContent('المحامي الأول');
  });

  it('يعرض زر «تسطير مطالعة» للمحامي فقط ويستدعي البحث عند الكتابة', async () => {
    const { userEvent } = await import('@testing-library/user-event');
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', id: 3 }, hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [], page: 1, perPage: 20, totalCount: 0 },
    });
    renderList();

    expect(await screen.findByRole('button', { name: '+ تسطير مطالعة' })).toBeInTheDocument();

    await user.type(screen.getByLabelText('بحث في كتب المطالعة'), 'أحمد');
    await waitFor(() => {
      const lastCall = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.at(-1)?.[0] as string;
      expect(lastCall).toContain('q=');
      expect(lastCall).toContain(encodeURIComponent('أحمد'));
    });
  });

  it('يخفي زر التسطير عن رئيس القسم والمدير', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'manager', id: 9 }, hasFullAccess: true, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [], page: 1, perPage: 20, totalCount: 0 },
    });
    renderList();

    await screen.findByText(/لا توجد كتب مطالعة/);
    expect(screen.queryByRole('button', { name: '+ تسطير مطالعة' })).not.toBeInTheDocument();
  });

  it('يعرض شارة «رد جديد» للمحامي على الكتب فيها ردّ لم يُطَّلع', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', id: 3 }, hasFullAccess: false, isHead: false });
    const withUnseen = { ...linkedLetter(), hasUnseenReply: true };
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [withUnseen], page: 1, perPage: 20, totalCount: 1 },
    });
    renderList();

    expect(await screen.findByText('رد جديد')).toBeInTheDocument();
    expect(screen.getByText('بانتظار رد')).toBeInTheDocument();
  });
});
