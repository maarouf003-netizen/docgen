import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ParentSuggestionsTab from './ParentSuggestionsTab';
import type { ParentEditSuggestionDto } from '../../types';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../../api/client';

function suggestion(overrides: Partial<ParentEditSuggestionDto> = {}): ParentEditSuggestionDto {
  return {
    id: 7,
    groupId: 5,
    entryId: 101,
    canonicalName: 'وزارة النقل',
    entityType: 'ministry',
    proposedCanonicalName: 'المديرية العامة للنقل',
    proposedEntityType: null,
    proposedCitationFormula: null,
    reason: 'تحديث اسم الجهة بموجب النظام الداخلي',
    status: 'pending',
    createdById: 3,
    createdByName: 'رئيس القسم',
    createdBranchId: 1,
    reviewedById: null,
    reviewReason: null,
    createdAtUtc: '2026-09-10T08:00:00Z',
    reviewedAtUtc: null,
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url.startsWith('/entity-registry/parent-edit-suggestions')) {
      return Promise.resolve({
        data: { items: [suggestion()], total: 1 },
      });
    }
    return Promise.resolve({ data: { items: [] } });
  });
  (api.post as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url.startsWith('/entity-registry/groups/5/rename')) {
      return Promise.resolve({
        data: { groupId: 5, oldCanonicalName: 'وزارة النقل', newCanonicalName: 'المديرية العامة للنقل', affectedDocuments: 2, changeEventId: 11 },
      });
    }
    return Promise.resolve({ data: {} });
  });
});

describe('ParentSuggestionsTab', () => {
  it('يعرض قائمة الاقتراحات مع حالة المعلّق واقتراح التسمية', async () => {
    render(<ParentSuggestionsTab />);

    expect(await screen.findByText('وزارة النقل')).toBeInTheDocument();
    expect(screen.getByText('المديرية العامة للنقل')).toBeInTheDocument();
    expect(screen.getAllByText('قيد المراجعة').length).toBeGreaterThan(0);
    expect(screen.getByText('تحديث اسم الجهة بموجب النظام الداخلي')).toBeInTheDocument();
  });

  it('قبول اقتراح اسم معتمد يتطلب مرسومًا ثم يُنفَّذ rename ثم يعتمد الاقتراح', async () => {
    const user = userEvent.setup();
    render(<ParentSuggestionsTab />);

    await screen.findByText('وزارة النقل');
    await user.click(screen.getByRole('button', { name: 'قبول' }));

    // بدون بيانات المرجع يُرفض الإرسال.
    await user.click(screen.getByRole('button', { name: 'تأكيد القبول' }));
    expect(screen.getByRole('alert')).toHaveTextContent(/بيانات المرجع/);

    await user.type(screen.getByLabelText('رقم المرجع'), '1254');
    await user.type(screen.getByLabelText('تاريخ المرجع'), '1/8/2026');
    await user.click(screen.getByRole('button', { name: 'تأكيد القبول' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        '/entity-registry/groups/5/rename',
        expect.objectContaining({
          newCanonicalName: 'المديرية العامة للنقل',
          decreeKind: 'قرار',
          decreeNumber: '1254',
          decreeDate: '1/8/2026',
        }),
      );
    });
    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        '/entity-registry/parent-edit-suggestions/7/review',
        expect.objectContaining({ status: 'approved' }),
      );
    });
    expect(screen.getByText(/طُبِّقت التسمية/)).toBeInTheDocument();
  });

  it('رفض اقتراح يتطلب سببًا إلزاميًا ويُرسل review rejected', async () => {
    const user = userEvent.setup();
    render(<ParentSuggestionsTab />);

    await screen.findByText('وزارة النقل');
    await user.click(screen.getByRole('button', { name: 'رفض' }));

    await user.click(screen.getByRole('button', { name: 'تأكيد الرفض' }));
    expect(screen.getByRole('alert')).toHaveTextContent('سبب الرفض مطلوب');

    await user.type(screen.getByLabelText('سبب الرفض *'), 'اسم مرجعي غير دقيق');
    await user.click(screen.getByRole('button', { name: 'تأكيد الرفض' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        '/entity-registry/parent-edit-suggestions/7/review',
        expect.objectContaining({ status: 'rejected', reviewReason: 'اسم مرجعي غير دقيق' }),
      );
    });
  });

  it('حين فشل rename بعد تطبيق الاسم في محاولة سابقة يستمر بقبول الاقتراح لاحقًا (idempotent)', async () => {
    const user = userEvent.setup();
    let getCalls = 0;
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url.startsWith('/entity-registry/parent-edit-suggestions')) {
        getCalls += 1;
        // بعد أول تحميل أصبح الاسم المقترح هو الاسم الحالي (نسخة حية من الخادم).
        const item =
          getCalls > 1 ? suggestion({ canonicalName: 'المديرية العامة للنقل' }) : suggestion();
        return Promise.resolve({ data: { items: [item], total: 1 } });
      }
      return Promise.resolve({ data: { items: [] } });
    });
    (api.post as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url.startsWith('/entity-registry/groups/5/rename')) {
        return Promise.reject(
          Object.assign(new Error('matched'), {
            isAxiosError: true,
            response: { status: 400, data: { message: 'الاسم الجديد مطابق للاسم الحالي' } },
          }),
        );
      }
      return Promise.resolve({ data: {} });
    });

    render(<ParentSuggestionsTab />);

    await screen.findByText('وزارة النقل');
    await user.click(screen.getByRole('button', { name: 'قبول' }));
    await user.type(screen.getByLabelText('رقم المرجع'), '1254');
    await user.type(screen.getByLabelText('تاريخ المرجع'), '1/8/2026');
    await user.click(screen.getByRole('button', { name: 'تأكيد القبول' }));

    // رغم فشل rename (الاسم مطبَّق مسبقًا) يُستأنف القبول بإرسال review فقط.
    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        '/entity-registry/parent-edit-suggestions/7/review',
        expect.objectContaining({ status: 'approved' }),
      );
    });
    expect(screen.getByText(/طُبِّقت التسمية «المديرية العامة للنقل»/)).toBeInTheDocument();
  });

  it('فشل rename حقيقي (الاسم ما زال مختلفًا) يوقف القبول ويعرض الخطأ بلا إرسال review', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url.startsWith('/entity-registry/groups/5/rename')) {
        return Promise.reject(
          Object.assign(new Error('decree'), {
            isAxiosError: true,
            response: { status: 400, data: { message: 'رقم المرجع مطلوب' } },
          }),
        );
      }
      return Promise.resolve({ data: {} });
    });

    render(<ParentSuggestionsTab />);

    await screen.findByText('وزارة النقل');
    await user.click(screen.getByRole('button', { name: 'قبول' }));
    await user.type(screen.getByLabelText('رقم المرجع'), '1254');
    await user.type(screen.getByLabelText('تاريخ المرجع'), '1/8/2026');
    await user.click(screen.getByRole('button', { name: 'تأكيد القبول' }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('خطأ من الخادم');
    });
    expect(api.post).not.toHaveBeenCalledWith(
      '/entity-registry/parent-edit-suggestions/7/review',
      expect.anything(),
    );
  });

  it('كون الاقتراح مرفوضًا يعرض شارة الحالة بلا أزرار قرار', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [suggestion({ status: 'rejected', reviewReason: 'مكرر' })], total: 1 },
    });
    render(<ParentSuggestionsTab />);

    expect(await screen.findByText('مرفوض')).toBeInTheDocument();
    expect(screen.getByText('مكرر')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'قبول' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'رفض' })).not.toBeInTheDocument();
  });
});