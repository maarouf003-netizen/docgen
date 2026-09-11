import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BranchManagementModal } from './BranchManagementModal';
import type { PublicEntityEntryDto, ParentEditSuggestionDto } from '../../types';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../../api/client';

function entry(overrides: Partial<PublicEntityEntryDto> = {}): PublicEntityEntryDto {
  return {
    id: 101,
    groupId: 10,
    canonicalName: 'وزارة النقل',
    entityType: 'ministry',
    governorate: 'دمشق',
    branchName: 'الجهة الأم',
    citationFormula: 'add-to-job',
    status: 'final',
    isActive: true,
    createdAt: '2026-08-24T00:00:00Z',
    aliases: [],
    isParentEntity: false,
    ...overrides,
  };
}

function pendingSuggestion(overrides: Partial<ParentEditSuggestionDto> = {}): ParentEditSuggestionDto {
  return {
    id: 7,
    groupId: 10,
    entryId: 101,
    canonicalName: 'وزارة النقل',
    entityType: 'ministry',
    proposedCanonicalName: 'المديرية العامة للنقل',
    proposedEntityType: null,
    proposedCitationFormula: null,
    reason: 'تحديث اسم الجهة بموجب النظام الداخلي',
    status: 'pending',
    createdById: 5,
    createdByName: 'رئيس القسم',
    createdBranchId: 1,
    reviewedById: null,
    reviewReason: null,
    createdAtUtc: '2026-09-10T08:00:00Z',
    reviewedAtUtc: null,
    ...overrides,
  };
}

/** استجابة معاينة موحّدة ناجحة (بدون أخطاء) حسب نوع العملية. */
function previewResponse(action: string, newBranchName?: string) {
  return {
    action,
    summary: 'ملخص المعاينة — ستُحدّث الملفات المرتبطة فورًا',
    targetBranchName: newBranchName ?? 'الفرع الهدف',
    entries: [],
    totalAffectedDocuments: 3,
    warnings: [],
    errors: [],
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url.startsWith('/entity-registry/groups/10/entries')) {
      return Promise.resolve({
        data: [
          entry({ isParentEntity: true }),
          entry({ id: 102, branchName: 'فرع المزة', governorate: 'دمشق' }),
          entry({ id: 103, branchName: 'فرع الميدان', governorate: 'دمشق' }),
        ],
      });
    }
    if (url.startsWith('/entity-registry/parent-edit-suggestions')) {
      return Promise.resolve({ data: { items: [], total: 0 } });
    }
    return Promise.resolve({ data: [] });
  });
  (api.post as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string, body?: Record<string, unknown>) => {
    if (url.endsWith('/branches/preview')) {
      const action = body?.action as string;
      return Promise.resolve({ data: previewResponse(action, body?.newBranchName as string | undefined) });
    }
    return Promise.resolve({ data: {} });
  });
  (api.put as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
});

const renderModal = () =>
  render(<BranchManagementModal groupId={10} groupName="وزارة النقل" onClose={vi.fn()} onCommitted={vi.fn()} />);

describe('BranchManagementModal', () => {
  it('يعرض بطاقة الجهة الأم للقراءة فقط وفروع المجموعة', async () => {
    renderModal();

    expect(await screen.findByText('وزارة النقل')).toBeInTheDocument();
    expect(screen.getByText('الجهة الأم')).toBeInTheDocument();
    expect(screen.getByText('قراءة فقط')).toBeInTheDocument();
    expect(screen.getByText('فرع المزة')).toBeInTheDocument();
  });

  it('لا يمنح بطاقة الأم أزرار تعديل/إلغاء — للفروع فقط', async () => {
    renderModal();

    await screen.findByText('وزارة النقل');
    expect(screen.getAllByRole('button', { name: 'تعديل التسمية' })).toHaveLength(2);
    expect(screen.getAllByRole('button', { name: 'إلغاء' })).toHaveLength(2);
  });

  it('يعيد تسمية الفرع عبر معاينة ثم تنفيذ rename-branch', async () => {
    const user = userEvent.setup();
    renderModal();

    await screen.findByText('وزارة النقل');
    await user.click(screen.getAllByRole('button', { name: 'تعديل التسمية' })[0]);
    const input = screen.getByLabelText('اسم الفرع الجديد') as HTMLInputElement;
    await user.clear(input);
    await user.type(input, 'فرع دمشق الجديد');
    await user.click(screen.getByRole('button', { name: 'معاينة' }));

    expect(await screen.findByText(/ملخص المعاينة/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل وإقفال المراجعة' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        '/entity-registry/groups/10/branches/102/rename-branch',
        expect.objectContaining({ newBranchName: 'فرع دمشق الجديد' }),
      );
    });
  });

  it('يكشف أخطاء المعاينة فيمنع التنفيذ', async () => {
    (api.post as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url.endsWith('/branches/preview')) {
        return Promise.resolve({
          data: { action: 'rename', summary: '', targetBranchName: '', entries: [], totalAffectedDocuments: 0, warnings: [], errors: ['المصدر والهدف متماثلان'] },
        });
      }
      return Promise.resolve({ data: {} });
    });
    const user = userEvent.setup();
    renderModal();

    await screen.findByText('وزارة النقل');
    await user.click(screen.getAllByRole('button', { name: 'تعديل التسمية' })[0]);
    await user.click(screen.getByRole('button', { name: 'معاينة' }));

    expect(await screen.findByText(/المصدر والهدف متماثلان/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حفظ التعديل وإقفال المراجعة' })).not.toBeInTheDocument();
  });

  it('يدمج فرعين عبر معاينة ثم تنفيذ merge — دون اعتماد الهدف المصدر', async () => {
    const user = userEvent.setup();
    renderModal();

    await screen.findByText('وزارة النقل');

    // الهدف لا يعرض الفرع المصدر نفسه أبدًا.
    await user.selectOptions(screen.getByLabelText('الفرع المصدر (سيُلغى)'), '102');
    const targetSelect = screen.getByLabelText('الفرع الهدف (يبقى)');
    expect(within(targetSelect).queryByText('فرع المزة — دمشق')).not.toBeInTheDocument();

    await user.selectOptions(targetSelect, '103');
    await user.click(screen.getByRole('button', { name: 'معاينة الدمج' }));
    expect(await screen.findByText(/ملخص المعاينة/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تنفيذ الدمج' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        '/entity-registry/groups/10/branches/merge',
        expect.objectContaining({ sourceEntryId: 102, targetEntryId: 103 }),
      );
    });
    expect(api.post).not.toHaveBeenCalledWith('/entity-registry/102/move', expect.anything());
  });

  it('يلغي فرعًا بنقل ملفاته إلى فرع هدف (دمج ضمني)', async () => {
    const user = userEvent.setup();
    renderModal();

    await screen.findByText('وزارة النقل');
    await user.click(screen.getAllByRole('button', { name: 'إلغاء' })[0]);
    await user.selectOptions(screen.getByLabelText('فرع الهدف (اختياري — يُنقل إليه الملفات)'), '103');
    await user.click(screen.getByRole('button', { name: 'معاينة الإلغاء' }));
    expect(await screen.findByText(/ملخص المعاينة/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تأكيد الإلغاء' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        '/entity-registry/groups/10/branches/102/abolish',
        expect.objectContaining({ targetEntryId: 103 }),
      );
    });
  });

  it('يوحد عدة فروع في ناجٍ مع تأكيد كتابة الاسم قبل التنفيذ', async () => {
    const user = userEvent.setup();
    renderModal();

    await screen.findByText('وزارة النقل');
    await user.selectOptions(screen.getByLabelText('الفرع الناجي (يبقى)'), '102');
    await user.selectOptions(screen.getByLabelText('الفروع الممتصة (تُلغى)'), ['103']);
    await user.clear(screen.getByLabelText('تصحيح كتابة اسم الناجي (اختياري)'));
    await user.type(screen.getByLabelText('تصحيح كتابة اسم الناجي (اختياري)'), 'فرع النقل الموّحد');

    await user.click(screen.getByRole('button', { name: 'معاينة التوحيد' }));
    expect(await screen.findByText(/ملخص المعاينة/)).toBeInTheDocument();

    // زر التنفيذ لا يظهر قبل كتابة الاسم بالضبط.
    expect(screen.queryByRole('button', { name: 'تنفيذ التوحيد' })).not.toBeInTheDocument();

    await user.type(screen.getByLabelText(/اكتب اسم الناجي بالضبط/), 'خاطئ');
    expect(screen.queryByRole('button', { name: 'تنفيذ التوحيد' })).not.toBeInTheDocument();

    await user.clear(screen.getByLabelText(/اكتب اسم الناجي بالضبط/));
    await user.type(screen.getByLabelText(/اكتب اسم الناجي بالضبط/), 'فرع النقل الموّحد');
    await user.click(screen.getByRole('button', { name: 'تنفيذ التوحيد' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        '/entity-registry/groups/10/branches/unify',
        expect.objectContaining({ targetEntryId: 102, absorbedEntryIds: [103], correctedName: 'فرع النقل الموّحد' }),
      );
    });
  });

  it('يسمح برفع اقتراح تعديل الجهة الأم مع سبب إلزامي', async () => {
    const user = userEvent.setup();
    renderModal();

    await screen.findByText('وزارة النقل');
    await user.click(screen.getByRole('button', { name: 'اقتراح تعديل الجهة الأم' }));

    await user.type(screen.getByLabelText('الاسم المعتمد المقترح'), 'المديرية العامة للنقل');
    await user.click(screen.getByRole('button', { name: 'إرسال الاقتراح للإدارة' }));
    expect(screen.getByRole('alert')).toHaveTextContent('سبب الاقتراح مطلوب');

    await user.type(screen.getByLabelText('سبب الاقتراح *'), 'تحديث اسم الجهة بموجب النظام الداخلي');
    await user.click(screen.getByRole('button', { name: 'إرسال الاقتراح للإدارة' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        '/entity-registry/entries/101/suggest-parent-edit',
        expect.objectContaining({ proposedCanonicalName: 'المديرية العامة للنقل', reason: 'تحديث اسم الجهة بموجب النظام الداخلي' }),
      );
    });
  });

  it('يعرض حالة الاقتراح المعلّق ويسمح بسحبه ذاتيًا', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url.startsWith('/entity-registry/groups/10/entries')) {
        return Promise.resolve({
          data: [
            entry({ isParentEntity: true }),
            entry({ id: 102, branchName: 'فرع المزة', governorate: 'دمشق' }),
            entry({ id: 103, branchName: 'فرع الميدان', governorate: 'دمشق' }),
          ],
        });
      }
      if (url.startsWith('/entity-registry/parent-edit-suggestions')) {
        return Promise.resolve({ data: { items: [pendingSuggestion()], total: 1 } });
      }
      return Promise.resolve({ data: [] });
    });
    const user = userEvent.setup();
    renderModal();

    expect(await screen.findByText(/قيد المراجعة/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'اقتراح تعديل الجهة الأم' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'سحب الاقتراح' }));
    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-registry/parent-edit-suggestions/7/withdraw');
    });
  });

  it('يعرض رسالة عند عدم وجود فروع في محافظته', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url.startsWith('/entity-registry/groups/10/entries')) return Promise.resolve({ data: [] });
      if (url.startsWith('/entity-registry/parent-edit-suggestions')) return Promise.resolve({ data: { items: [], total: 0 } });
      return Promise.resolve({ data: [] });
    });
    renderModal();

    expect(await screen.findByText(/لا توجد فروع نشطة/)).toBeInTheDocument();
  });
});