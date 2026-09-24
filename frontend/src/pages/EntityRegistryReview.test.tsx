import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import EntityRegistryReview from './EntityRegistryReview';
import type { PublicEntityEntryDto } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../api/client';

const NOTE =
  'يرجى التأكد من صحة البيانات وادخالها بدقة لأن تعديل قيود الجهات العامة مكلف ويتتطلب موافقات عدة';

function entry(overrides: Partial<PublicEntityEntryDto> = {}): PublicEntityEntryDto {
  return {
    id: 1,
    groupId: 10,
    canonicalName: 'وزارة النقل',
    entityType: 'ministry',
    governorate: 'دمشق',
    branchName: 'الجهة الأم',
    citationFormula: 'add-to-job',
    status: 'final',
    isActive: true,
    createdAt: '2026-08-01',
    aliases: [],
    createdByName: 'محامٍ',
    needsReview: true,
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter>
      <EntityRegistryReview />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  useAuthMock.mockReturnValue({ user: { id: 7, role: 'head' } });
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url === '/entity-registry/pending-review') return Promise.resolve({ data: [entry()] });
    return Promise.resolve({ data: { items: [] } });
  });
});

describe('EntityRegistryReview — تنبيه الدقة لرئيس القسم', () => {
  it('يعرض الملاحظة الحمراء فوق قائمة الاعتماد لرئيس القسم', async () => {
    renderPage();

    await screen.findByText('وزارة النقل');
    expect(screen.getByText(NOTE)).toBeInTheDocument();
  });

  it('يعرض الملاحظة داخل نموذج إدخال جهة عامة مسبقًا', async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText('وزارة النقل');
    await user.click(screen.getByRole('button', { name: '+ إدخال جهة عامة مسبقًا' }));

    const form = document.querySelector('form');
    expect(form).not.toBeNull();
    expect(within(form as HTMLElement).getByText(NOTE)).toBeInTheDocument();
  });

  it('يعرض الملاحظة داخل نافذة تعديل الجهة قيد المراجعة', async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText('وزارة النقل');
    await user.click(screen.getByRole('button', { name: 'تعديل التسمية/البيانات…' }));

    const dialog = await screen.findByRole('dialog', { name: 'تعديل جهة: وزارة النقل' });
    expect(within(dialog).getByText(NOTE)).toBeInTheDocument();
  });

  it('لا يعرض الملاحظة لغير رئيس القسم (مدير)', async () => {
    useAuthMock.mockReturnValue({ user: { id: 9, role: 'manager' } });
    renderPage();

    await screen.findByText('وزارة النقل');
    await waitFor(() => {
      expect(screen.queryByText(NOTE)).not.toBeInTheDocument();
    });
  });
});
