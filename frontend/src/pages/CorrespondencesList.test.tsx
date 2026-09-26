import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import CorrespondencesList from './CorrespondencesList';
import type { CorrespondenceListItemDto } from '../types';

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

const listCalls = () =>
  (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.filter(
    ([url]) => typeof url === 'string' && url.includes('/correspondence?'),
  );

const linkedItem = (): CorrespondenceListItemDto => ({
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
  creatorName: 'المحامي الأول',
  targetName: 'مندوب الجهة',
  snippet: 'نطلب موافاتنا بالبيانات',
  lastKind: 'letter',
  viewStatus: 'pending',
  canMarkSeen: false,
  canReply: false,
  messagesCount: 1,
  administrativeBranchName: 'دمشق',
  governorate: 'دمشق',
  updatedAt: '2026-08-01T09:00:00Z',
});

const generalItem = (): CorrespondenceListItemDto => ({
  ...linkedItem(),
  id: 2,
  correspondenceNumber: 'دمشق-2026-5678',
  importance: 'normal',
  documentId: null,
  fileContext: null,
  viewStatus: 'seen',
  canMarkSeen: false,
  canReply: false,
});

function renderList(portal = false) {
  return render(
    <MemoryRouter>
      <CorrespondencesList portal={portal} />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('CorrespondencesList', () => {
  it('يعرض سطور المراسلات بالصيغة المعتمدة مع الرقم والتاريخ والأهمية (محامي)', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', id: 3 }, hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [linkedItem(), generalItem()], page: 1, perPage: 20, totalCount: 2 },
    });
    renderList();

    expect(await screen.findByText(/مراسلة بملف \(أحمد محمد العلي\) رقم 77\/2026/)).toBeInTheDocument();
    expect(screen.getByText('مراسلة عامة غير مرتبطة بملف')).toBeInTheDocument();
    expect(screen.getAllByText('DAM-2026-1234').length).toBeGreaterThan(0);
    // شارة العاجل + شارة حالة الاطلاع (المستلم لم يوثّق بعد)
    expect(screen.getAllByText('عاجل').length).toBeGreaterThan(0);
    expect(screen.getByText('بانتظار المشاهدة')).toBeInTheDocument();
    // المرسل يقرأ الحالة ولا يُميَّز بتنبيه حجز الفعل عليه — والعاجل لم يعد يحمل شارة اطلاع خاصة.
    expect(screen.getByText('تمت المشاهدة')).toBeInTheDocument();
    expect(screen.queryByText('عاجل بلا مشاهدة')).not.toBeInTheDocument();
    // «عادي» شارةً وخيار فلتر معًا
    expect(screen.getAllByText('عادي').length).toBeGreaterThanOrEqual(2);
    // الطرفان ظاهران
    expect(screen.getAllByText(/مندوب الجهة/).length).toBeGreaterThan(0);
    expect(api.get).toHaveBeenCalledWith('/correspondence?page=1&perPage=20', expect.any(Object));
  });

  it('يعرض زر «مراسلة جديدة» لأدوار الكتابة ويضيف فلتر الأهمية للطلب', async () => {
    const { userEvent } = await import('@testing-library/user-event');
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ user: { role: 'head', id: 5 }, hasFullAccess: false, isHead: true });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [], page: 1, perPage: 20, totalCount: 0 },
    });
    renderList();

    expect(await screen.findByRole('button', { name: '+ مراسلة جديدة' })).toBeInTheDocument();

    // تسمية فلتر الأهمية المعتمدة وواحدتها الافتراضية.
    expect(screen.getByLabelText('فلتر الأهمية')).toHaveValue('');

    await user.selectOptions(screen.getByLabelText('فلتر الأهمية'), 'urgent');
    await waitFor(() => {
      const lastCall = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.at(-1)?.[0] as string;
      expect(lastCall).toContain('importance=urgent');
    });
  });

  it('لا يعرض أي مراسلة للمدير قبل اختيار المحافظة ولا يطلب القائمة', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'manager', id: 9 }, hasFullAccess: true, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { governorates: ['دمشق', 'حلب'] },
    });
    renderList();

    expect(await screen.findByText(/اختر المحافظة لعرض المراسلات/)).toBeInTheDocument();
    expect(screen.queryByText(/لا توجد مراسلات/)).not.toBeInTheDocument();
    expect(listCalls().length).toBe(0);
  });

  it('يطلب القائمة بالمحافظة بعد الاختيار ويعرضها (مدير)', async () => {
    const { userEvent } = await import('@testing-library/user-event');
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ user: { role: 'manager', id: 9 }, hasFullAccess: true, isHead: false });
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockImplementation((url: string) => {
      if (url.includes('filter-options')) {
        return Promise.resolve({ data: { governorates: ['دمشق', 'حلب'] } });
      }
      return Promise.resolve({
        data: {
          items: [{ ...linkedItem(), administrativeBranchName: 'دمشق' }],
          page: 1,
          perPage: 20,
          totalCount: 1,
        },
      });
    });
    renderList();

    const select = await screen.findByLabelText('فلتر المحافظة');
    await user.selectOptions(select, 'دمشق');

    await waitFor(() => {
      const calls = listCalls();
      expect(calls.length).toBeGreaterThanOrEqual(1);
      expect(calls.at(-1)?.[0]).toContain(`governorate=${encodeURIComponent('دمشق')}`);
    });
    const govParas = screen.getAllByText(/المحافظة:/);
    expect(govParas.length).toBeGreaterThan(0);
    expect(govParas[0]).toHaveTextContent('دمشق');
  });

  it('يبقى تنبيه الاطلاع هادئًا لقارئ ليس طرفًا في التوثيق (المرسل)', async () => {
    // المرسل يقرأ حالة مستلمه، لكن لا يُميَّز بتنبيه حجز الفعل عليه.
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', id: 3 }, hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [linkedItem()], page: 1, perPage: 20, totalCount: 1 },
    });
    const { container } = renderList();

    expect(await screen.findByText('بانتظار المشاهدة')).toBeInTheDocument();
    expect(screen.getByText('بانتظار المشاهدة').className).toContain('bg-amber-100');
    expect(container.querySelector('.bg-amber-100')).not.toBeNull();
  });

  it('ينبّه المستلم نابضًا قبل توثيقه', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'entitymanager', id: 11 }, hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [{ ...linkedItem(), canMarkSeen: true }], page: 1, perPage: 20, totalCount: 1 },
    });
    const { container } = renderList();

    expect(await screen.findByText('بانتظار المشاهدة')).toBeInTheDocument();
    const badge = screen.getByText('بانتظار المشاهدة');
    expect(badge.className).toContain('bg-red-600');
    expect(badge.querySelector('.motion-safe\\:animate-pulse')).not.toBeNull();
    expect(container.querySelector('.motion-safe\\:animate-pulse')).not.toBeNull();
  });

  it('يطلب مسار البوابة للمندوب ويخفي فلتر المحافظة', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'entitymanager', id: 11 }, hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [linkedItem()], page: 1, perPage: 20, totalCount: 1 },
    });
    renderList(true);

    expect(await screen.findByText(/مراسلة بملف/)).toBeInTheDocument();
    expect(screen.queryByLabelText('فلتر المحافظة')).not.toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith(
      '/portal/correspondence?page=1&perPage=20',
      expect.any(Object),
    );
  });

  it('يعرض زر «مراسلة جديدة» للمندوب في البوابة فقط لا في المسار الرئيسي', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'entitymanager', id: 11 }, hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [], page: 1, perPage: 20, totalCount: 0 },
    });

    const portalRender = renderList(true);
    expect(await screen.findByRole('button', { name: '+ مراسلة جديدة' })).toBeInTheDocument();
    portalRender.unmount();

    renderList(false);
    await waitFor(() => {
      expect(listCalls().length).toBeGreaterThanOrEqual(1);
    });
    expect(screen.queryByRole('button', { name: '+ مراسلة جديدة' })).not.toBeInTheDocument();
  });

  it('يخفي زر «مراسلة جديدة» عن المدير/المشرف (قراءة فقط)', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'manager', id: 9 }, hasFullAccess: true, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { governorates: ['دمشق'] },
    });
    renderList();

    expect(await screen.findByText(/اختر المحافظة لعرض المراسلات/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '+ مراسلة جديدة' })).not.toBeInTheDocument();
  });
});
