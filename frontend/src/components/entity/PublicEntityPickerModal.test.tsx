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

// استجابة POST /entity-registry (القيد الذي يعيده الخادم بعد إنشاء محامٍ: نهائي مخزّنًا وبانتظار المراجعة).
const CREATED_ENTRY: PublicEntityEntryDto = entry({
  id: 99,
  canonicalName: 'هيئة جديدة كلية',
  entityType: 'authority',
  governorate: 'حمص',
  branchName: 'فرع هيئة جديدة',
  isParentEntity: false,
  needsReview: true,
});

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

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'وزارة');
    await user.click(await screen.findByRole('button', { name: /وزارة التعليم/ }));

    expect(onPick).toHaveBeenCalledTimes(1);
    expect(onPick.mock.calls[0][0]).toMatchObject({ id: 11, canonicalName: 'وزارة التعليم' });
    expect(onClose).not.toHaveBeenCalled();
  });

  it('يميّز قيد بانتظار المراجعة بشارة خاصة (د4/§5.3 — status=pending أو needsReview=true)', async () => {
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'هيئة');
    expect(await screen.findByText('هيئة التخطيط')).toBeInTheDocument();
    // قيد Status=pending
    expect(screen.getAllByText('بانتظار المراجعة').length).toBeGreaterThanOrEqual(1);
  });

  it('يميّز القيد المرخّص المخزّن نهائيًا لكنه بانتظار المراجعة (needsReview=true)', async () => {
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'هيئة');
    await screen.findByText('هيئة التفتيش');
    // زران للشارة: قيد pending (#13) وقيد needsReview=true (#14)
    expect(screen.getAllByText('بانتظار المراجعة').length).toBeGreaterThanOrEqual(2);
  });

  it('لا يعرض عدّاد ملفات في النتائج إطلاقًا (د9)', async () => {
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'وزارة');
    await screen.findByText('وزارة التعليم');
    // حتى لو تسللت حقول عدّادات من الخادم فلا يجوز عرضها بأي صيغة.
    expect(screen.queryByText(/عدد الملفات/)).not.toBeInTheDocument();
    expect(screen.queryByText(/ملفًا/)).not.toBeInTheDocument();
    expect(screen.queryByText(/ملفات/)).not.toBeInTheDocument();
  });

  it('يرسل المحافظة المختارة إلى الخادم عند تغييرها (فلترة الخادم), مع بقاء الجهة الأم', async () => {
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'وزارة');
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
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'هيئة');
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
    const user = userEvent.setup();
    mockSearch([
      entry({ id: 21, groupId: 5, canonicalName: 'المركزي', governorate: 'حلب', branchName: 'فرع حلب', isParentEntity: false }),
      entry({ id: 20, groupId: 5, canonicalName: 'المركزي', governorate: 'دمشق', branchName: 'الجهة الأم', isParentEntity: true }),
    ]);
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'مركزي');
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
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: CREATED_ENTRY });
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
    // القيد المضاف يُثبَّت في النافذة نفسها (بلا إغلاق) بشارة «بانتظار المراجعة».
    expect((await screen.findByRole('button', { name: /هيئة جديدة كلية/ }))).toHaveTextContent('بانتظار المراجعة');
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

  it('يعيد تحميل نتائج البحث بعد إضافة جهة جديدة تتطابق مع نص البحث وتُثبَّت أعلى القائمة', async () => {
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: CREATED_ENTRY });
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'هيئة جديدة كلية');
    await screen.findByText('وزارة التعليم');

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
      // استدعاء التحميل بعد الكتابة + استدعاء إعادة التحميل بعد الإضافة.
      expect(searchCalls.length).toBeGreaterThanOrEqual(2);
    });
    await expect(screen.findByRole('status')).resolves.toHaveTextContent(/مراجعتها قبل ظهورها نهائيًا/);
    // القيد المضاف مثبّت أعلى القائمة في نفس النافذة (بلا إغلاق) — بغض النظر عن نص البحث.
    expect(screen.getByRole('button', { name: /هيئة جديدة كلية/ })).toBeInTheDocument();
  });

  it('لا يعرض أي اقتراح ولا يستدعي البحث قبل إدخال كلمة بحث ثم يجلب النتائج عند الكتابة', async () => {
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    // بلا كلمة بحث: رسالة إرشادية، ولا أي نتيجة، ولا استدعاء واجهة إطلاقًا.
    expect(screen.getByText('ابدأ بكتابة اسم الجهة للبحث…')).toBeInTheDocument();
    expect(screen.queryByText('وزارة التعليم')).not.toBeInTheDocument();
    expect(screen.queryByText('لا توجد جهات مطابقة في السجل')).not.toBeInTheDocument();
    expect(
      (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.filter(([url]) => url === '/entity-registry/search'),
    ).toHaveLength(0);

    // عند الكتابة يبدأ البحث وتظهر النتائج وتختفي الرسالة الإرشادية.
    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'وزارة');
    expect(await screen.findByText('وزارة التعليم')).toBeInTheDocument();
    expect(screen.queryByText('ابدأ بكتابة اسم الجهة للبحث…')).not.toBeInTheDocument();
  });

  it('بعد إضافة جهة غير موجودة لا تطابق نص البحث تُثبَّت أعلى القائمة وتُختار فورًا دون إغلاق', async () => {
    const onPick = vi.fn();
    // نص البحث (مصرف) لا يطابق القيد الجديد (هيئة جديدة كلية) — إثبات أن التثبيت لا يعتمد على المطابقة.
    mockSearch([]);
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: CREATED_ENTRY });
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={onPick} />);

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'مصرف');
    expect(await screen.findByText('لا توجد جهات مطابقة في السجل')).toBeInTheDocument();

    await user.click(await screen.findByRole('button', { name: /جهة غير موجودة؟ اقترح إضافة…/ }));
    await user.type(screen.getByLabelText('اسم الجهة'), 'هيئة جديدة كلية');
    await user.selectOptions(screen.getByLabelText('المحافظة'), 'حمص');
    await user.click(screen.getByRole('button', { name: 'إرسال الاقتراح' }));

    // القيد الجديد يظهر مثبّتًا أعلى القائمة في النافذة نفسها بشارة «بانتظار المراجعة».
    const pinned = await screen.findByRole('button', { name: /هيئة جديدة كلية/ });
    expect(pinned).toHaveTextContent('بانتظار المراجعة');
    // اختيار القيد المثبّت يستدعي onPick فورًا بنفس كائن الاستجابة — دون إغلاق النافذة.
    await user.click(pinned);
    expect(onPick).toHaveBeenCalledTimes(1);
    expect(onPick.mock.calls[0][0]).toEqual(CREATED_ENTRY);
  });

  it('يعرض التسمية العربية لرمز النوع الجديد (أمانة عامة) في النتائج ومنسدلة الاقتراح', async () => {
    mockSearch([
      entry({ id: 40, groupId: 9, canonicalName: 'أمانة رئاسة مجلس الوزراء', entityType: 'general-secretariat', governorate: 'دمشق', branchName: 'الجهة الأم', isParentEntity: true }),
    ]);
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await user.type(screen.getByLabelText('بحث باسم الجهة'), 'أمانة');
    expect(await screen.findByText('أمانة رئاسة مجلس الوزراء')).toBeInTheDocument();
    // التسمية الجديدة «أمانة عامة» مصدرها كتالوج الـ11 — لا عرض خام للرمز.
    expect(screen.getByText(/أمانة عامة/)).toBeInTheDocument();
    expect(screen.queryByText(/general-secretariat/)).not.toBeInTheDocument();

    // نموذج الاقتراح: القائمة المنسدلة لنوع الجهة تعرض التسمية الجديدة أيضًا.
    await user.click(await screen.findByRole('button', { name: /جهة غير موجودة؟ اقترح إضافة…/ }));
    const typeSelect = screen.getByLabelText('نوع الجهة');
    expect(typeSelect).toHaveTextContent('أمانة عامة');
    expect(typeSelect).not.toHaveTextContent(/general-secretariat/);
  });

  it('يُثبِّت القيد المضاف فورًا بلا أي كلمة بحث سابقة (بلا استدعاء بحث ولا رسالة إرشادية مضلّلة)', async () => {
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: CREATED_ENTRY });
    const onPick = vi.fn();
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={onPick} />);

    // نافذة فارغة: بلا أي كلمة بحث ولا رسائل نتائج.
    expect(screen.getByText('ابدأ بكتابة اسم الجهة للبحث…')).toBeInTheDocument();

    // اقتراح مباشر دون البحث أولًا (السيناريو الأصلي للمشكلة: لا نتائج فتُضاف الجهة مباشرة).
    await user.click(screen.getByRole('button', { name: /جهة غير موجودة؟ اقترح إضافة…/ }));
    await user.type(screen.getByLabelText('اسم الجهة'), 'هيئة جديدة كلية');
    await user.selectOptions(screen.getByLabelText('المحافظة'), 'حمص');
    await user.click(screen.getByRole('button', { name: 'إرسال الاقتراح' }));

    // القيد يُثبَّت أعلى النافذة نفسها فورًا — والرسالة الإرشادية تختفي لئلا تتعارض مع وجود قيد.
    const pinned = await screen.findByRole('button', { name: /هيئة جديدة كلية/ });
    expect(pinned).toHaveTextContent('بانتظار المراجعة');
    expect(screen.queryByText('ابدأ بكتابة اسم الجهة للبحث…')).not.toBeInTheDocument();
    expect(screen.queryByText('لا توجد جهات مطابقة في السجل')).not.toBeInTheDocument();
    // بلا أي استدعاء بحث إطلاقًا (لم تُكتب كلمة، والحراسة تصدّه حتى بعد إعادة التحميل).
    expect(
      (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.filter(([url]) => url === '/entity-registry/search'),
    ).toHaveLength(0);

    await user.click(pinned);
    expect(onPick).toHaveBeenCalledTimes(1);
    expect(onPick.mock.calls[0][0]).toEqual(CREATED_ENTRY);
  });
});
