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
    branchName: 'الجهة الأم',
    citationFormula: 'add-to-job',
    status: 'final',
    isActive: true,
    createdAt: '2026-08-24T00:00:00Z',
    aliases: [],
    isParentEntity: true,
    ...overrides,
  };
}

// محاكاة سلوك الخادم: يفلتر حسب المحافظة (مع بقاء الجهة الأم) والفرع.
function mockSearch(items: PublicEntityEntryDto[]) {
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation(
    (_url: string, opts?: { params?: { governorate?: string; branchName?: string } }) => {
      const gov = opts?.params?.governorate;
      const branch = opts?.params?.branchName;
      let list = items;
      if (gov) list = list.filter((i) => i.isParentEntity || i.governorate === gov);
      if (branch) list = list.filter((i) => i.branchName === branch);
      return Promise.resolve({
        data: { items: list, page: 1, perPage: 50, totalCount: list.length, totalPages: 1 },
      });
    },
  );
}

const BASE_ITEMS: PublicEntityEntryDto[] = [
  entry(),
  entry({ id: 12, groupId: 6, canonicalName: 'مديرية النقل', entityType: 'administration', governorate: 'حلب', branchName: 'فرع النقل', isParentEntity: false }),
  entry({ id: 13, groupId: 7, canonicalName: 'هيئة التخطيط', entityType: 'authority', governorate: 'دمشق', branchName: 'فرع التخطيط', isParentEntity: false, status: 'pending' }),
  // نموذج الحوكمة الحالي: قيد Status=final لكن needsReview=true — يبقى بانتظار المراجعة.
  entry({ id: 14, groupId: 8, canonicalName: 'هيئة التفتيش', entityType: 'authority', governorate: 'حمص', branchName: 'فرع التفتيش', isParentEntity: false, status: 'final', needsReview: true }),
];

beforeEach(() => {
  vi.clearAllMocks();
  mockAuth.user = null;
  mockSearch(BASE_ITEMS);
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

  it('يرسل المحافظة المختارة إلى الخادم عند تغييرها (فلترة الخادم), مع بقاء الجهة الأم', async () => {
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await screen.findByText('وزارة التعليم');
    await user.selectOptions(screen.getByLabelText('محافظة البحث'), 'حلب');

    await waitFor(() => {
      const calls = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls;
      const last = calls[calls.length - 1];
      expect(last[1].params.governorate).toBe('حلب');
    });
    // الجهة الأم تبقى ظاهرة مهما كان فلتر المحافظة (تغطي كل المحافظات).
    expect(await screen.findByText('مديرية النقل')).toBeInTheDocument();
    expect(screen.getByText('وزارة التعليم')).toBeInTheDocument();
    // فرع دمشق (غير الأب) لا يظهر بعد اختيار محافظة حلب.
    expect(screen.queryByText('فرع التخطيط')).not.toBeInTheDocument();
  });

  it('الافتراضي للمحافظة هو محافظة فرع المحامي وتُرسل إلى الخادم', async () => {
    mockAuth.user = { role: 'lawyer', branchName: 'الفرع الرئيسي - دمشق' };
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    const select = await screen.findByLabelText('محافظة البحث');
    expect((select as HTMLSelectElement).value).toBe('دمشق');
    await waitFor(() => {
      // الجهة الأم (وزارة التعليم) + فرع دمشق (هيئة التخطيط) يظهران، وفرع حلب المديرية لا يظهر.
      expect(screen.getByText('وزارة التعليم')).toBeInTheDocument();
      expect(screen.getByText('هيئة التخطيط')).toBeInTheDocument();
      expect(screen.queryByText('مديرية النقل')).not.toBeInTheDocument();
    });
  });

  it('يثبّت الجهة الأم (بلا فرع) أعلى نتائج البحث ثم فروعها تحتها', async () => {
    mockSearch([
      entry({ id: 21, groupId: 5, canonicalName: 'المركزي', governorate: 'حلب', branchName: 'فرع حلب', isParentEntity: false }),
      entry({ id: 20, groupId: 5, canonicalName: 'المركزي', governorate: 'دمشق', branchName: 'الجهة الأم', isParentEntity: true }),
    ]);
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    // الجهة الأم (المركزي) تظهر أولًا رغم أن قيدها في دمشق قبل فرع حلب بالترتيب الأصلي.
    const rows = await screen.findAllByText('المركزي');
    expect(rows).toHaveLength(2);
    const firstRow = rows[0].closest('li')!;
    expect(firstRow).toHaveTextContent('الجهة الأم');
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
        branchName: 'الجهة الأم',
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

  it('يعيد تحميل نتائج البحث بعد إضافة جهة جديدة كي تظهر فورًا دون إغلاق النافذة', async () => {
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await user.click(await screen.findByRole('button', { name: /جهة غير موجودة؟ اقترح إضافة…/ }));
    await user.type(screen.getByLabelText('اسم الجهة'), 'هيئة جديدة كلية');
    await user.selectOptions(screen.getByLabelText('المحافظة'), 'حمص');
    await user.click(screen.getByRole('button', { name: 'إرسال الاقتراح' }));

    // نجاح الإرسال ثم إعادة استدعاء البحث (وليس مجرد تحديث الحالة محليًا).
    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-registry', expect.objectContaining({
        canonicalName: 'هيئة جديدة كلية',
      }));
    });
    await waitFor(() => {
      const getCalls = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls;
      const searchCalls = getCalls.filter(([url]) => url === '/entity-registry/search');
      // استدعاء التحميل الأولي + استدعاء إعادة التحميل بعد الإضافة.
      expect(searchCalls.length).toBeGreaterThanOrEqual(2);
    });
    await expect(screen.findByRole('status')).resolves.toHaveTextContent(/مراجعتها قبل ظهورها نهائيًا/);
  });
});
