import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import DocumentCorrespondenceCard from './DocumentCorrespondenceCard';
import { stubMobile } from '../../test/stubMobile';
import type { CorrespondenceListItemDto } from '../../types';

vi.mock('../../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api/client')>();
  return {
    ...original,
    api: {
      get: vi.fn(),
      post: vi.fn(),
    },
  };
});

import { api } from '../../api/client';

const getMock = () => api.get as unknown as ReturnType<typeof vi.fn>;

const seenItem = (): CorrespondenceListItemDto => ({
  id: 1,
  correspondenceNumber: 'DAM-2026-1234',
  correspondenceDate: '2026-08-01T09:00:00Z',
  importance: 'normal',
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
  viewStatus: 'seen',
  canMarkSeen: false,
  canReply: false,
  messagesCount: 2,
  administrativeBranchName: 'دمشق',
  governorate: 'دمشق',
  updatedAt: '2026-08-01T09:00:00Z',
});

const pendingUnseenItem = (): CorrespondenceListItemDto => ({
  ...seenItem(),
  id: 2,
  correspondenceNumber: 'DAM-2026-9999',
  importance: 'urgent',
  viewStatus: 'pending',
  canMarkSeen: true,
  canReply: true,
});

function renderCard(portal = false) {
  return render(
    <MemoryRouter>
      <DocumentCorrespondenceCard documentId={7} canCreate={false} portal={portal} />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('DocumentCorrespondenceCard', () => {
  it('يعرض المراسلات مع شارة اطلاع المستلم بجانب شارة الأهمية', async () => {
    getMock().mockResolvedValue({ data: [seenItem(), pendingUnseenItem()] });
    renderCard();

    expect(await screen.findByText('DAM-2026-1234')).toBeInTheDocument();
    expect(screen.getByText('DAM-2026-9999')).toBeInTheDocument();
    // شارة الاطلاع تعكس حالة المستلم لكل قارئ، لا «هل شاهدتها أنا».
    expect(screen.getAllByText('تمت المشاهدة')).toHaveLength(1);
    expect(screen.getByText('بانتظار المشاهدة')).toBeInTheDocument();
    // العاجل لم يعد يحمل شارة اطلاع خاصة به (كان ازدواجًا مع شارة الأهمية).
    expect(screen.queryByText('عاجل بلا مشاهدة')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: /DAM-2026-1234/ })).toHaveAttribute(
      'href',
      '/correspondence/1',
    );
  });

  it('لا يميّز شارة الانتباه إلا لمن يملك حق التوثيق', async () => {
    // قارئ غير المستلم (مرسل/رئيس/مدير) يرى «بانتظار المشاهدة» بلا نابض أحمر:
    // التنبيه فعلٌ مطلوب من المستلم وحده، أما الباقي فحالة معلوماتية.
    getMock().mockResolvedValue({
      data: [{ ...pendingUnseenItem(), canMarkSeen: false }],
    });
    renderCard();

    expect(await screen.findByText('DAM-2026-9999')).toBeInTheDocument();
    const badge = screen.getByText('بانتظار المشاهدة');
    expect(badge.className).toContain('bg-amber-100');
    expect(badge.className).not.toContain('bg-red-600');
  });

  it('يطلب مسار البوابة للمندوب ويوجّه التفاصيل إليه', async () => {
    getMock().mockResolvedValue({ data: [seenItem()] });
    renderCard(true);

    expect(await screen.findByText('DAM-2026-1234')).toBeInTheDocument();
    expect(getMock()).toHaveBeenCalledWith(
      '/portal/files/7/correspondence',
      expect.any(Object),
    );
    expect(screen.getByRole('link', { name: /DAM-2026-1234/ })).toHaveAttribute(
      'href',
      '/portal/correspondence/1',
    );
  });

  it('يختفي كليًا عن من لا يملك الصلاحية (403)', async () => {
    getMock().mockRejectedValue({ response: { status: 403 } });
    const { container } = renderCard();

    await waitFor(() => {
      expect(container).toBeEmptyDOMElement();
    });
  });

  it('يعرض الشارتين معًا على الجوال بلا فقد أيٍّ منهما', async () => {
    // الشارتان متجاورتان في صفّ واحد `flex-wrap`: على 375px ينلّف الصفّ ولا
    // يُقصّ. التحقق هنا من مسار الجوال ومن بقاء الشارتين قابلتين للقراءة،
    // لأن jsdom لا يحسب التخطيط (قياس التجاوز يحتاج متصفحًا حقيقيًا).
    stubMobile(true);
    getMock().mockResolvedValue({ data: [pendingUnseenItem()] });
    renderCard();

    const badge = await screen.findByText('بانتظار المشاهدة');
    expect(badge.className).toContain('whitespace-nowrap');
    expect(screen.getByText('عاجل')).toBeInTheDocument();
    // الصفّ قابل للالتفاف على الجوال (لا صفّ ثابت العرض).
    expect(badge.parentElement?.className).toContain('flex-wrap');
  });
});
