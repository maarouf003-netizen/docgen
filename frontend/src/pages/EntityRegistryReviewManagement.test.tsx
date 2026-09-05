import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import EntityRegistryReviewManagement from './EntityRegistryReviewManagement';
import type { PublicEntityGroupDto } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

vi.mock('../hooks/useCancellableRequest', () => ({
  useCancellableRequest: () => ({
    data: { items: [], page: 1, perPage: 20, totalCount: 0 },
    error: '',
  }),
}));

import { api } from '../api/client';

function group(overrides: Partial<PublicEntityGroupDto> = {}): PublicEntityGroupDto {
  return {
    groupId: 1,
    canonicalName: 'وزارة التعليم',
    entityType: 'ministry',
    isActive: true,
    entryCount: 2,
    governorates: ['دمشق'],
    ...overrides,
  };
}

const groupsResponse = (items: PublicEntityGroupDto[]) => ({
  data: { items, page: 1, perPage: 50, totalCount: items.length, totalPages: 1 },
});

function renderPage(initialEntry = '/entities/review-management') {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <EntityRegistryReviewManagement />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  useAuthMock.mockReturnValue({ user: { id: 9, role: 'admin' } });
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(groupsResponse([]));
});

describe('EntityRegistryReviewManagement', () => {
  it('يعرض التبويبات الأربعة مع بدء التعديل افتراضيًا', async () => {
    renderPage();
    expect(screen.getByRole('tab', { name: 'تعديل جهة عامة' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'إضافة جهة' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'توحيد تسميات' })).toBeEnabled();
    expect(screen.getByRole('tab', { name: 'سجل تغييرات الجهة' })).toBeInTheDocument();
  });

  it('يعرض تبويب التوحيد مع تبويبيه الفرعيين عند اختياره', async () => {
    renderPage('/entities/review-management?tab=unify');
    expect(screen.getByRole('tab', { name: 'تعديل جهة عامة' })).toBeInTheDocument();
    expect(await screen.findByRole('tab', { name: 'المجموعات المتشابهة' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'كافة الجهات العامة' })).toBeInTheDocument();
  });

  it('يبحث ويضيف جهة للقائمة المختارة ثم يعرضها', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(
      groupsResponse([group(), group({ groupId: 2, canonicalName: 'وزارة الصحة' })]),
    );
    renderPage();

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'وزارة');
    const resultButton = await screen.findByRole('button', { name: /وزارة التعليم/ });
    await user.click(resultButton);

    expect(await screen.findByRole('heading', { name: /الجهات المختارة/ })).toBeInTheDocument();
    expect(screen.getByText('وزارة التعليم')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /تعديل/ })).toBeEnabled();
  });

  it('يمنع «تعديل تسمية» عند اختيار أكثر من جهة', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(
      groupsResponse([group(), group({ groupId: 2, canonicalName: 'وزارة الصحة' })]),
    );
    renderPage();

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'وزارة');
    await user.click(await screen.findByRole('button', { name: /وزارة التعليم/ }));
    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'وزارة');
    await user.click(await screen.findByRole('button', { name: /وزارة الصحة/ }));

    const redButton = screen.getByRole('button', { name: /تعديل ▾/ });
    await user.click(redButton);
    expect(screen.getByRole('menuitem', { name: 'تعديل تسمية' })).toBeDisabled();
  });

  it('يمنع الدمج بجهة واحدة ويبقي حلول مفعّلاً', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(
      groupsResponse([group({ groupId: 1, canonicalName: 'وزارة التعليم' })]),
    );
    renderPage();

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'وزارة');
    await user.click(await screen.findByRole('button', { name: /وزارة التعليم/ }));
    await user.click(screen.getByRole('button', { name: /تعديل ▾/ }));
    expect(screen.getByRole('menuitem', { name: 'دمج' })).toBeDisabled();
    expect(screen.getByRole('menuitem', { name: 'حلول' })).toBeEnabled();
    expect(screen.getByRole('menuitem', { name: 'تعديل تسمية' })).toBeEnabled();
  });

  it('يُنفّذ إعادة تسمية بمعاينة ومرجع (نوع+رقم+تاريخ)', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(
      groupsResponse([group({ groupId: 1, canonicalName: 'وزارة التعليم' })]),
    );
    (api.post as unknown as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({
        data: {
          oldCanonicalName: 'وزارة التعليم',
          newCanonicalName: 'وزارة التربية',
          affectedDocuments: 5,
          branches: ['الفرع الرئيسي'],
        },
      })
      .mockResolvedValueOnce({
        data: {
          groupId: 1,
          oldCanonicalName: 'وزارة التعليم',
          newCanonicalName: 'وزارة التربية',
          affectedDocuments: 5,
          changeEventId: 42,
        },
      });
    renderPage();

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'وزارة');
    await user.click(await screen.findByRole('button', { name: /وزارة التعليم/ }));
    await user.click(screen.getByRole('button', { name: /تعديل ▾/ }));
    await user.click(screen.getByRole('menuitem', { name: 'تعديل تسمية' }));

    await user.type(screen.getByLabelText('التسمية الجديدة'), 'وزارة التربية');
    await user.click(screen.getByRole('button', { name: 'معاينة التأثير' }));
    expect(await screen.findByText(/الملفات المتأثرة/)).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('نوع المرجع'), 'قرار');
    await user.type(screen.getByLabelText('رقم المرجع'), '12');
    await user.type(screen.getByLabelText('تاريخ المرجع'), '1/8/2026');
    await user.type(screen.getByLabelText('تأكيد كتابة التسمية الجديدة'), 'وزارة التربية');
    await user.click(screen.getByRole('button', { name: 'تأكيد التنفيذ' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        '/entity-registry/groups/1/rename',
        expect.objectContaining({
          groupId: 1,
          newCanonicalName: 'وزارة التربية',
          decreeKind: 'قرار',
          decreeNumber: '12',
          decreeDate: '1/8/2026',
        }),
      );
    });
  });

  it('يُنفّذ دمجًا مع الاسم النهائي والمرجع', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(
      groupsResponse([group({ groupId: 1 }), group({ groupId: 2, canonicalName: 'وزارة الصحة' })]),
    );
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { absorbedGroupsCount: 1, entriesMigrated: 3, totalAffectedDocuments: 4 },
    });
    renderPage();

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'وزارة');
    await user.click(await screen.findByRole('button', { name: /وزارة التعليم/ }));
    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'وزارة');
    await user.click(await screen.findByRole('button', { name: /وزارة الصحة/ }));
    await user.click(screen.getByRole('button', { name: /تعديل ▾/ }));
    await user.click(screen.getByRole('menuitem', { name: 'دمج' }));

    await user.type(screen.getByLabelText('الاسم النهائي للنتيجة (اختياري)'), 'الوزارة الموحدة');
    await user.selectOptions(screen.getByLabelText('نوع المرجع'), 'مرسوم');
    await user.type(screen.getByLabelText('رقم المرجع'), '7');
    await user.type(screen.getByLabelText('تاريخ المرجع'), '2/8/2026');
    await user.type(screen.getByLabelText('تأكيد كتابة اسم الهدف'), 'وزارة التعليم');
    await user.click(screen.getByRole('button', { name: 'تأكيد الدمج' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        '/entity-registry/merge-commit',
        expect.objectContaining({
          survivorGroupId: 1,
          absorbedGroupIds: [2],
          newCanonicalName: 'الوزارة الموحدة',
          decreeKind: 'مرسوم',
          decreeNumber: '7',
          decreeDate: '2/8/2026',
        }),
      );
    });
  });

  it('يسمح بالحلول بجهة واحدة (المعتمد) مع بقاء الدمج مشروطًا بجهتين', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(
      groupsResponse([group({ groupId: 1, canonicalName: 'وزارة التعليم' })]),
    );
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        newGroupId: 9,
        newCanonicalName: 'الهيئة التعليمية الوطنية',
        abolishedGroups: 1,
        entriesMoved: 2,
        affectedDocuments: 3,
        changeEventId: 7,
      },
    });
    renderPage();

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'وزارة');
    await user.click(await screen.findByRole('button', { name: /وزارة التعليم/ }));

    await user.click(screen.getByRole('button', { name: /تعديل ▾/ }));
    const dmerge = screen.getByRole('menuitem', { name: 'دمج' });
    const abolish = screen.getByRole('menuitem', { name: 'حلول' });
    expect(dmerge).toBeDisabled();
    expect(abolish).toBeEnabled();
    await user.click(abolish);

    expect(await screen.findByRole('heading', { name: 'حلول جهة عامة' })).toBeInTheDocument();
    await user.type(screen.getByLabelText('اسم الجهة الجديدة'), 'الهيئة التعليمية الوطنية');
    await user.selectOptions(screen.getByLabelText('المحافظة'), 'دمشق');
    await user.selectOptions(screen.getByLabelText('نوع المرجع'), 'قرار');
    await user.type(screen.getByLabelText('رقم المرجع'), '300');
    await user.type(screen.getByLabelText('تاريخ المرجع'), '1/8/2026');
    await user.type(screen.getByLabelText('تأكيد كتابة اسم الجهة الجديدة'), 'الهيئة التعليمية الوطنية');
    await user.click(screen.getByRole('button', { name: 'تأكيد الحلول' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        '/entity-registry/groups/abolish-and-replace',
        expect.objectContaining({
          abolishedGroupIds: [1],
          newCanonicalName: 'الهيئة التعليمية الوطنية',
          decreeKind: 'قرار',
          decreeNumber: '300',
          decreeDate: '1/8/2026',
        }),
      );
    });
  });

  it('يعرض تبويب الإضافة مع نموذج إنشاء جهة', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole('tab', { name: 'إضافة جهة' }));

    expect(await screen.findByLabelText('اسم الجهة المعتمد')).toBeInTheDocument();
    await user.type(screen.getByLabelText('اسم الجهة المعتمد'), 'مديرية النقل');
    await user.selectOptions(screen.getByLabelText('المحافظة'), 'حمص');
    await user.click(screen.getByRole('button', { name: 'إنشاء القيد' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-registry', expect.objectContaining({
        canonicalName: 'مديرية النقل',
        governorate: 'حمص',
        isParentEntity: true,
      }));
    });
  });

  it('يفتح تبويب السجل عبر مسار ?tab=log', async () => {
    renderPage('/entities/review-management?tab=log');
    expect(await screen.findByRole('heading', { name: 'سجل تغييرات الجهات' })).toBeInTheDocument();
  });

  it('يفتح تبويب الإضافة عبر مسار ?tab=add', async () => {
    renderPage('/entities/review-management?tab=add');
    expect(await screen.findByLabelText('اسم الجهة المعتمد')).toBeInTheDocument();
  });
});
