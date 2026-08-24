import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import EntityProposalsQueue from './EntityProposalsQueue';
import type { PublicEntityProposalDto } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../api/client';
import { stubMobile } from '../test/stubMobile';

function proposal(overrides: Partial<PublicEntityProposalDto> = {}): PublicEntityProposalDto {
  return {
    id: 1,
    proposedName: 'هيئة التفتيش',
    entityType: 'authority',
    governorate: 'دمشق',
    branchName: 'الفرع الرئيسي',
    citationFormula: 'add-to-job',
    proposedById: 7,
    proposedByName: 'محامي دمشق',
    sourceDocumentId: null,
    status: 'pending',
    createdAt: '2026-08-24T00:00:00Z',
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  stubMobile(false);
  useAuthMock.mockReturnValue({ user: { id: 2, role: 'head' } });
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
    data: [proposal(), proposal({ id: 2, proposedName: 'مؤسسة المياه', governorate: 'حلب', entityType: 'foundation' })],
  });
});

describe('EntityProposalsQueue', () => {
  it('يعرض الاقتراحات المعلقة مع مقدمها', async () => {
    render(<EntityProposalsQueue />);

    expect(await screen.findByText('هيئة التفتيش')).toBeInTheDocument();
    expect(screen.getByText('مؤسسة المياه')).toBeInTheDocument();
    expect(screen.getAllByText(/قُدِّر من: محامي دمشق/).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('بانتظار الاعتماد')).toHaveLength(2);
  });

  it('يعتمد اقتراحًا وينشئ القيد النهائي', async () => {
    const user = userEvent.setup();
    render(<EntityProposalsQueue />);

    await user.click((await screen.findAllByRole('button', { name: 'اعتماد وإنشاء القيد' }))[0]);

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-registry/proposals/1/approve');
    });
    expect(await screen.findByRole('status')).toBeInTheDocument();
  });

  it('يرفض اقتراحًا بسبب إلزامي', async () => {
    const user = userEvent.setup();
    render(<EntityProposalsQueue />);

    await user.click((await screen.findAllByRole('button', { name: 'رفض…' }))[0]);
    const dialog = screen.getByRole('dialog', { name: /رفض اقتراح/ });
    expect(dialog).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'تأكيد الرفض' }));
    expect(await screen.findByText(/سبب الرفض مطلوب/)).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalledWith('/entity-registry/proposals/1/reject', expect.anything());

    await user.type(screen.getByLabelText('سبب الرفض'), 'جهة مكررة');
    await user.click(screen.getByRole('button', { name: 'تأكيد الرفض' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-registry/proposals/1/reject', { reason: 'جهة مكررة' });
    });
  });

  it('يعرض الحالة الفارغة عند لا اقتراحات', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });
    render(<EntityProposalsQueue />);

    expect(await screen.findByText('لا توجد اقتراحات معلّقة')).toBeInTheDocument();
  });
});
