import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import DocumentCorrespondenceCard from './DocumentCorrespondenceCard';
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
  seenByMe: true,
  isUrgentUnseen: false,
  messagesCount: 2,
  receiptsCount: 1,
  administrativeBranchName: 'دمشق',
  governorate: 'دمشق',
  updatedAt: '2026-08-01T09:00:00Z',
});

const urgentUnseenItem = (): CorrespondenceListItemDto => ({
  ...seenItem(),
  id: 2,
  correspondenceNumber: 'DAM-2026-9999',
  importance: 'urgent',
  seenByMe: false,
  isUrgentUnseen: true,
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
  it('يعرض المراسلات مع مؤشر «تمت المشاهدة» للمرئية وشارة العاجل لغير المرئية', async () => {
    getMock().mockResolvedValue({ data: [seenItem(), urgentUnseenItem()] });
    renderCard();

    expect(await screen.findByText('DAM-2026-1234')).toBeInTheDocument();
    expect(screen.getByText('DAM-2026-9999')).toBeInTheDocument();
    // مؤشر المشاهدة يظهر للمراسلة المرئية فقط.
    expect(screen.getAllByText('✓ تمت المشاهدة')).toHaveLength(1);
    expect(screen.getByText('عاجل بلا مشاهدة')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /DAM-2026-1234/ })).toHaveAttribute(
      'href',
      '/correspondence/1',
    );
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
});
