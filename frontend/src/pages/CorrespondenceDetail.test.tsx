import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import CorrespondenceDetail from './CorrespondenceDetail';
import type { CorrespondenceDto } from '../types';

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

const detail = (overrides: Partial<CorrespondenceDto> = {}): CorrespondenceDto => ({
  id: 1,
  correspondenceNumber: 'DAM-2026-1234',
  correspondenceDate: '2026-08-01T09:00:00Z',
  importance: 'urgent',
  documentId: 7,
  fileContext: {
    executedName: 'أحمد محمد العلي',
    fileNumber: '77/2026',
    fileType: 'تنفيذي',
    fileYear: '2026',
    court: 'دائرة تنفيذ دمشق',
  },
  branchId: 2,
  governorate: 'دمشق',
  administrativeBranchName: null,
  creatorId: 3,
  creatorName: 'المحامي الأول',
  creatorRole: 'lawyer',
  targetUserId: 11,
  targetName: 'مندوب الجهة',
  targetRole: 'entitymanager',
  viewStatus: 'pending',
  canMarkSeen: false,
  canReply: false,
  messages: [
    {
      id: 10,
      kind: 'letter',
      bodyHtml: '<p>نطلب موافاتنا بالبيانات</p>',
      messageNumber: 'DAM-2026-1234',
      messageDate: '2026-08-01T09:00:00Z',
      authorId: 3,
      authorName: 'المحامي الأول',
      authorRole: 'lawyer',
    },
  ],
  receipts: [],
  createdAt: '2026-08-01T09:00:00Z',
  ...overrides,
});

function renderDetail() {
  return render(
    <MemoryRouter initialEntries={['/correspondence/1']}>
      <Routes>
        <Route path="/correspondence/:id" element={<CorrespondenceDetail />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('CorrespondenceDetail', () => {
  it('يعرض الرأس الكامل: العنوان والرقم والأهمية والطرفين وزر المشاهدة', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'entitymanager', id: 11 }, hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: detail({ canMarkSeen: true, canReply: true }) });
    renderDetail();

    expect(await screen.findByText(/مراسلة بملف \(أحمد محمد العلي\)/)).toBeInTheDocument();
    // الرقم في الرأس ورقم الرسالة الأصلية معًا
    expect(screen.getAllByText('DAM-2026-1234').length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText('عاجل')).toBeInTheDocument();
    expect(screen.getAllByText(/المحامي الأول/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/مندوب الجهة/).length).toBeGreaterThan(0);
    // المستلم يرى زر الرد ولا يرى زر اللاحق
    expect(screen.getByRole('button', { name: 'الرد على المراسلة' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إضافة لاحق' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تمت المشاهدة' })).toBeInTheDocument();
  });

  it('زر «تمت المشاهدة» يوثّق ويعرض التأكيد بدل الزر', async () => {
    const { userEvent } = await import('@testing-library/user-event');
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ user: { role: 'entitymanager', id: 11 }, hasFullAccess: false, isHead: false });
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockResolvedValue({ data: detail({ canMarkSeen: true }) });
    (api.post as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url.endsWith('/mark-seen')) {
        getMock.mockResolvedValue({
          data: detail({
            canMarkSeen: true,
            viewStatus: 'seen',
            receipts: [{ userId: 11, userName: 'مندوب الجهة', seenAt: '2026-08-02T10:00:00Z' }],
          }),
        });
        return Promise.resolve({ data: { userId: 11, userName: 'مندوب الجهة', seenAt: '2026-08-02T10:00:00Z' } });
      }
      return Promise.resolve({ data: {} });
    });
    renderDetail();

    await user.click(await screen.findByRole('button', { name: 'تمت المشاهدة' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/correspondence/1/mark-seen');
    });
    expect(await screen.findByText(/أكّدتَ مشاهدتها/)).toBeInTheDocument();
    expect(await screen.findByText('مشاهَدات موثقة (1)')).toBeInTheDocument();
  });

  it('المنشئ يرى زر اللاحق ولا يرى زر الرد', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', id: 3 }, hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: detail() });
    renderDetail();

    expect(await screen.findByRole('button', { name: 'إضافة لاحق' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'الرد على المراسلة' })).not.toBeInTheDocument();
  });

  it('لا يظهر زر التوثيق لغير المستلم مع بقاء حالة الاطلاع معروضة', async () => {
    // المرسل يقرأ حالة المستلم بلا زر: التوثيق فعل المستلم وحده (يفرضه الخادم
    // بـ403 أيضًا، فاختفاء الزر ليس تجميلًا بل منع إجراء غير مسموح).
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', id: 3 }, hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: detail() });
    renderDetail();

    expect(await screen.findByText('بانتظار المشاهدة')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تمت المشاهدة' })).not.toBeInTheDocument();
    expect(screen.queryByText(/أكّدتَ مشاهدتها/)).not.toBeInTheDocument();
  });

  it('المدير يقرأ حالة اطلاع المستلم بلا زر توثيق', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'manager', id: 9 }, hasFullAccess: true, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: detail({
        viewStatus: 'seen',
        receipts: [{ userId: 11, userName: 'مندوب الجهة', seenAt: '2026-08-02T10:00:00Z' }],
      }),
    });
    renderDetail();

    expect(await screen.findByText('مشاهَدات موثقة (1)')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تمت المشاهدة' })).not.toBeInTheDocument();
  });

  it('لا تُشتق صلاحية التوثيق من صلاحية الرد: المستلم بلا حق خادم لا يرى زر المشاهدة', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'entitymanager', id: 11 }, hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: detail({ canMarkSeen: false, canReply: true }) });
    renderDetail();

    expect(await screen.findByRole('button', { name: 'الرد على المراسلة' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تمت المشاهدة' })).not.toBeInTheDocument();
    expect(screen.queryByText(/أكّدتَ مشاهدتها/)).not.toBeInTheDocument();
  });

  it('تُعلن حالة الاطلاع لقارئ الشاشة من منطقة حية في صفحة التفاصيل', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'entitymanager', id: 11 }, hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: detail({ canMarkSeen: true }) });
    renderDetail();

    const status = await screen.findByRole('status');
    expect(within(status).getByText('بانتظار المشاهدة')).toBeInTheDocument();
  });
});
