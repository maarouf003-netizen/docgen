import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PublicEntityPickerModal, PROPOSAL_WARNING_TEXT } from './PublicEntityPickerModal';
import type { PublicEntityEntryDto } from '../../types';

const mockAuth = { user: null as null | { role: string; branchName?: string | null } };
vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

vi.mock('../../auth/useAuth', () => ({
  useAuth: () => mockAuth,
}));

import { api } from '../../api/client';

function entry(overrides: Partial<PublicEntityEntryDto> = {}): PublicEntityEntryDto {
  return {
    id: 11,
    groupId: 5,
    canonicalName: 'وزارة التعليم',
    entityType: 'ministry',
    governorate: 'دمشق',
    branchName: 'الفرع الرئيسي',
    citationFormula: 'add-to-job',
    status: 'final',
    isActive: true,
    createdAt: '2026-08-24T00:00:00Z',
    aliases: [],
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  mockAuth.user = null;
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
    data: {
      items: [
        entry(),
        entry({ id: 12, groupId: 6, canonicalName: 'مديرية النقل', entityType: 'administration', governorate: 'حلب', branchName: 'فرع النقل' }),
        entry({ id: 13, groupId: 7, canonicalName: 'هيئة التخطيط', entityType: 'authority', governorate: 'دمشق', branchName: 'فرع التخطيط', status: 'pending' }),
        // نموذج الحوكمة الحالي: قيد Status=final لكن needsReview=true — يبقى بانتظار المراجعة.
        entry({ id: 14, groupId: 8, canonicalName: 'هيئة التفتيش', entityType: 'authority', governorate: 'حمص', branchName: 'فرع التفتيش', status: 'final', needsReview: true }),
      ],
      page: 1,
      perPage: 50,
      totalCount: 3,
      totalPages: 1,
    },
  });
});

describe('PublicEntityPickerModal', () => {
  it('يعرض نتائج البحث ويستدعي onPick عند اختيار قيد', async () => {
    const onPick = vi.fn();
    const onClose = vi.fn();
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={onClose} onPick={onPick} />);

    await user.click(await screen.findByRole('button', { name: /وزارة التعليم/ }));

    expect(onPick).toHaveBeenCalledTimes(1);
    expect(onPick.mock.calls[0][0]).toMatchObject({ id: 11, canonicalName: 'وزارة التعليم' });
    expect(onClose).not.toHaveBeenCalled();
  });

  it('يميّز قيد بانتظار المراجعة بشارة خاصة (د4/§5.3 — status=pending أو needsReview=true)', async () => {
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    expect(await screen.findByText('هيئة التخطيط')).toBeInTheDocument();
    // قيد Status=pending
    expect(screen.getAllByText('بانتظار المراجعة').length).toBeGreaterThanOrEqual(1);
  });

  it('يميّز القيد المرخّص المخزّن نهائيًا لكنه بانتظار المراجعة (needsReview=true)', async () => {
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await screen.findByText('هيئة التفتيش');
    // زران للشارة: قيد pending (#13) وقيد needsReview=true (#14)
    expect(screen.getAllByText('بانتظار المراجعة').length).toBeGreaterThanOrEqual(2);
  });

  it('لا يعرض عدّاد ملفات في النتائج إطلاقًا (د9)', async () => {
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await screen.findByText('وزارة التعليم');
    // حتى لو تسللت حقول عدّادات من الخادم فلا يجوز عرضها بأي صيغة.
    expect(screen.queryByText(/عدد الملفات/)).not.toBeInTheDocument();
    expect(screen.queryByText(/ملفًا/)).not.toBeInTheDocument();
    expect(screen.queryByText(/ملفات/)).not.toBeInTheDocument();
  });

  it('يبدّل التجميع حسب المحافظة من القائمة المنسدلة', async () => {
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await screen.findByText('وزارة التعليم');
    await user.selectOptions(screen.getByLabelText('محافظة البحث'), 'حلب');

    expect(screen.getByText('مديرية النقل')).toBeInTheDocument();
    expect(screen.queryByText('وزارة التعليم')).not.toBeInTheDocument();
    expect(screen.queryByText('هيئة التخطيط')).not.toBeInTheDocument();
  });

  it('الافتراضي للمحافظة هو محافظة فرع المحامي', async () => {
    mockAuth.user = { role: 'lawyer', branchName: 'الفرع الرئيسي - دمشق' };
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    const select = await screen.findByLabelText('محافظة البحث');
    expect((select as HTMLSelectElement).value).toBe('دمشق');
    // تبقى نتائج محافظة دمشق فقط مبدئيًا («مديرية النقل» في حلب لا تظهر).
    await waitFor(() => {
      expect(screen.queryByText('مديرية النقل')).not.toBeInTheDocument();
      expect(screen.getByText('وزارة التعليم')).toBeInTheDocument();
    });
  });

  it('يثبّت الجهة الأساسية بدون فرع (الفرع الرئيسي) أعلى نتائج البحث', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        items: [
          entry({ id: 21, groupId: 5, canonicalName: 'المركزي', governorate: 'حلب', branchName: 'فرع حلب' }),
          entry({ id: 20, groupId: 5, canonicalName: 'المركزي', governorate: 'دمشق', branchName: 'الفرع الرئيسي' }),
        ],
        page: 1,
        perPage: 50,
        totalCount: 2,
        totalPages: 1,
      },
    });
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    // في وضع «كل المحافظات» يجب أن يظهر «الفرع الرئيسي» أولًا رغم أن قيده في دمشق قبل فرع حلب بالترتيب الأصلي.
    const rows = await screen.findAllByText('المركزي');
    expect(rows).toHaveLength(2);
    const firstRow = rows[0].closest('li')!;
    expect(firstRow).toHaveTextContent('الفرع الرئيسي');
    const secondRow = rows[1].closest('li')!;
    expect(secondRow).toHaveTextContent('فرع حلب');
  });

  it('يعرض نموذج الإدخال بنص التحذير الحرفي والـplaceholder المعتمدين (د7)', async () => {
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await user.click(await screen.findByRole('button', { name: /جهة غير موجودة؟ اقترح إضافة…/ }));

    expect(screen.getByText(PROPOSAL_WARNING_TEXT)).toBeInTheDocument();
    expect(
      screen.getByPlaceholderText('مثال: المدير العام للمصرف التجاري السوري'),
    ).toBeInTheDocument();
    // صيغتا المناداة المعتمدتان فقط (د8)
    expect(screen.getByLabelText('الصيغة')).toHaveTextContent('إضافة لوظيفته');
    expect(screen.getByLabelText('الصيغة')).toHaveTextContent('إضافة لمنصبه');
  });

  it('يدخل الجهة إلى السجل بانتظار مراجعة رئيس القسم (نموذج الحوكمة الجديد)', async () => {
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await user.click(await screen.findByRole('button', { name: /جهة غير موجودة؟ اقترح إضافة…/ }));
    await user.type(screen.getByLabelText('اسم الجهة'), 'هيئة جديدة كلية');
    await user.selectOptions(screen.getByLabelText('المحافظة'), 'حمص');
    await user.click(screen.getByRole('button', { name: 'إرسال الاقتراح' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-registry', {
        canonicalName: 'هيئة جديدة كلية',
        entityType: 'ministry',
        governorate: 'حمص',
        branchName: 'الفرع الرئيسي',
        citationFormula: 'add-to-job',
      });
    });
    expect(await screen.findByRole('status')).toHaveTextContent(/مراجعتها قبل ظهورها نهائيًا/);
  });

  it('يرفض تقديم الاقتراح دون محافظة برسالة واضحة', async () => {
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await user.click(await screen.findByRole('button', { name: /جهة غير موجودة؟ اقترح إضافة…/ }));
    await user.type(screen.getByLabelText('اسم الجهة'), 'هيئة بلا محافظة');
    await user.click(screen.getByRole('button', { name: 'إرسال الاقتراح' }));

    expect(await screen.findByText('المحافظة مطلوبة')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });
});
