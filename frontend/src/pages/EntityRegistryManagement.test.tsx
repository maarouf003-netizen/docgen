import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import EntityRegistryManagement from './EntityRegistryManagement';
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
import { stubMobile } from '../test/stubMobile';

function entryItem(overrides: Partial<PublicEntityEntryDto> = {}): PublicEntityEntryDto {
  return {
    id: 1,
    groupId: 10,
    canonicalName: 'وزارة التعليم',
    entityType: 'ministry',
    governorate: 'دمشق',
    branchName: 'الفرع الرئيسي',
    citationFormula: 'add-to-job',
    status: 'final',
    isActive: true,
    createdAt: '2026-08-24T00:00:00Z',
    aliases: ['وزاره التعليم'],
    ...overrides,
  };
}

const listResponse = (items: PublicEntityEntryDto[]) => ({
  data: { items, page: 1, perPage: 20, totalCount: items.length, totalPages: 1 },
});

beforeEach(() => {
  vi.clearAllMocks();
  stubMobile(false);
  useAuthMock.mockReturnValue({ user: { id: 9, role: 'admin' } });
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(listResponse([entryItem()]));
});

describe('EntityRegistryManagement', () => {
  it('يعرض قائمة القيود مع الحالة والصيغة', async () => {
    render(<EntityRegistryManagement />);

    expect(await screen.findByText('وزارة التعليم')).toBeInTheDocument();
    expect(screen.getByText('إضافة لوظيفته')).toBeInTheDocument();
    // «نهائي» تظهر في شارة القيد وفي خيار الفلتر معًا.
    expect(screen.getAllByText('نهائي').length).toBeGreaterThanOrEqual(1);
  });

  it('ينشئ قيدًا جديدًا بالحقول المعتمدة', async () => {
    const user = userEvent.setup();
    render(<EntityRegistryManagement />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة جهة' }));
    await user.type(screen.getByLabelText('اسم الجهة المعتمد'), 'مديرية النقل');
    await user.selectOptions(screen.getByLabelText('نوع الجهة'), 'administration');
    await user.selectOptions(screen.getByLabelText('المحافظة'), 'حمص');
    await user.clear(screen.getByLabelText('الفرع'));
    await user.type(screen.getByLabelText('الفرع'), 'فرع حمص');
    await user.click(screen.getByRole('button', { name: 'إنشاء القيد' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-registry', expect.objectContaining({
        canonicalName: 'مديرية النقل',
        entityType: 'administration',
        governorate: 'حمص',
        branchName: 'فرع حمص',
      }));
    });
  });

  it('يرفض الإنشاء دون محافظة', async () => {
    const user = userEvent.setup();
    render(<EntityRegistryManagement />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة جهة' }));
    await user.type(screen.getByLabelText('اسم الجهة المعتمد'), 'هيئة جديدة');
    await user.click(screen.getByRole('button', { name: 'إنشاء القيد' }));

    expect(await screen.findByText(/المحافظة مطلوبة/)).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يعدّل القيد عبر نافذة التعديل (إعادة تسمية جماعية)', async () => {
    const user = userEvent.setup();
    render(<EntityRegistryManagement />);

    await user.click(await screen.findByRole('button', { name: 'تعديل' }));
    const dialog = screen.getByRole('dialog', { name: /تعديل جهة/ });
    expect(dialog).toBeInTheDocument();

    await user.clear(screen.getByLabelText('الاسم المعتمد'));
    await user.type(screen.getByLabelText('الاسم المعتمد'), 'وزارة التربية');
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));

    await waitFor(() => {
      expect(api.put).toHaveBeenCalledWith('/entity-registry/1', expect.objectContaining({
        canonicalName: 'وزارة التربية',
        governorate: 'دمشق',
      }));
    });
  });

  it('يضيف اسمًا بديلًا للقيد من نافذة التعديل', async () => {
    const user = userEvent.setup();
    render(<EntityRegistryManagement />);

    await user.click(await screen.findByRole('button', { name: 'تعديل' }));
    await user.type(screen.getByLabelText('إضافة اسم بديل'), 'اسم قديم');
    await user.click(screen.getByRole('button', { name: 'إضافة' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-registry/1/aliases', { aliasText: 'اسم قديم' });
    });
  });

  it('لا يظهر زر الاستيراد لغير المدير والمشرف', async () => {
    useAuthMock.mockReturnValue({ user: { id: 5, role: 'head' } });
    render(<EntityRegistryManagement />);

    await screen.findByText('وزارة التعليم');
    expect(screen.queryByRole('button', { name: /استيراد النصوص التاريخية/ })).not.toBeInTheDocument();
  });

  it('يعرض بطاقات على الجوال بدل الجدول', async () => {
    stubMobile(true);
    render(<EntityRegistryManagement />);

    expect(await screen.findByRole('heading', { name: 'وزارة التعليم' })).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('يعرض حالة الانتظار بشارة مميزة', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(
      listResponse([entryItem({ status: 'pending' })]),
    );
    render(<EntityRegistryManagement />);

    expect(await screen.findByText('بانتظار الاعتماد')).toBeInTheDocument();
  });
});
