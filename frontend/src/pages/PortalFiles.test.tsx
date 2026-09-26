import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import PortalFiles from './PortalFiles';

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../api/client';

function fileItem(overrides: Record<string, unknown> = {}) {
  return {
    id: 1,
    documentType: 'سند دين',
    isDraft: false,
    borrowerName: 'أحمد',
    borrowerFather: 'خالد',
    borrowerFamily: 'الخطيب',
    applicant: 'وزارة التعليم - محافظة دمشق',
    executedEntitiesSummary: '',
    amountNumeric: 1500,
    currency: 'ليرة سورية',
    execStatus: null,
    createdAt: '2026-08-01',
    updatedAt: '2026-08-20',
    fileType: 'سند مصارف',
    court: 'دائرة تنفيذ دمشق',
    displayBaseNumber: '1500',
    displayBaseYear: '2026',
    matchedEntries: [{ id: 11, governorate: 'اللاذقية', branchName: 'فرع 1', isActive: true }],
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.useRealTimers();
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url === '/portal/my-scope') {
      return Promise.resolve({
        data: {
          scopeType: 'group',
          groupId: 5,
          canonicalName: 'المصرف التجاري السوري',
          entityType: 'company',
          entries: [
            { id: 11, governorate: 'اللاذقية', branchName: 'فرع 1', isActive: true },
            { id: 12, governorate: 'دمشق', branchName: 'الفرع الرئيسي', isActive: true },
          ],
        },
      });
    }
    if (url === '/portal/files') {
      return Promise.resolve({
        data: {
          items: [fileItem(), fileItem({ id: 2, borrowerName: 'سعيد', borrowerFather: 'علي', borrowerFamily: 'النور', fileType: null, court: null, displayBaseNumber: '99', displayBaseYear: '2025', matchedEntries: [], isDraft: true })],
          page: 1,
          perPage: 20,
          totalCount: 2,
          totalPages: 1,
        },
      });
    }
    return Promise.reject(new Error(`unexpected GET ${url}`));
  });
});

afterEach(() => {
  vi.useRealTimers();
});

describe('PortalFiles', () => {
  it('يعرض البطاقة الجديدة: ثلاثي + فرع + شارة + أساس/نوع/دائرة', async () => {
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    expect(await screen.findByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(screen.getByText(/المصرف التجاري السوري · اللاذقية\/فرع 1/)).toBeInTheDocument();
    expect(screen.getByText('رقم الأساس: 1500 لعام 2026')).toBeInTheDocument();
    expect(screen.getByText('نوع الملف: سند مصارف')).toBeInTheDocument();
    expect(screen.getByText('دائرة التنفيذ المختصة: دائرة تنفيذ دمشق')).toBeInTheDocument();
    // «تحت رفع» تظهر كشارة للملف المسودة وكخيار في فلتر الحالة.
    expect(screen.getAllByText('تحت رفع').length).toBeGreaterThanOrEqual(1);

    // فلتر الحالة يتضمن «محال الى البداية» كخيار.
    const statusSelect = screen.getByLabelText('فلتر الحالة') as HTMLSelectElement;
    expect(Array.from(statusSelect.options).map((o) => o.value)).toContain('محال الى البداية');
  });

  it('يخفي دائرة التنفيذ عند فراغها ولا يعرض شيئًا مكانها', async () => {
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    await screen.findByText('سعيد علي النور');
    // الدائرة تظهر مرة واحدة فقط (للملف الأول) — لا شرطات ولا عناصر فارغة للثاني.
    expect(screen.getAllByText(/دائرة التنفيذ المختصة/).length).toBe(1);
  });

  it('لا يعرض أي زر تعديل — البوابة اطلاع قانونيات العامة (د10)', async () => {
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    await screen.findByText('أحمد خالد الخطيب');
    expect(screen.queryByRole('button', { name: /تعديل/ })).not.toBeInTheDocument();
    expect(screen.getByText(/بوابة اطلاع قانونيات الجهات العامة/)).toBeInTheDocument();
    expect(screen.getByText(/الاستثناء الوحيد: مراسلاتك كطرف/)).toBeInTheDocument();
  });

  it('يعرض منتقي الفرع لمندوب الهوية ويمرره للاستعلام والتصدير', async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    const branch = await screen.findByLabelText('فلتر الفرع') as HTMLSelectElement;
    expect(branch).toBeInTheDocument();
    await user.selectOptions(branch, '11');

    const calls = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.filter(
      (c: unknown[]) => c[0] === '/portal/files',
    );
    const last = calls[calls.length - 1][1] as { params: Record<string, unknown> };
    expect(last.params.entryId).toBe(11);
  });

  it('يصدّر إكسل بنفس الفلاتر (بما فيها الفرع) عبر تنزيل ملف', async () => {
    const blobUrlSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    const revokeSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});
    const user = userEvent.setup();
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    await user.click(await screen.findByRole('button', { name: 'تصدير إكسل' }));

    await new Promise((r) => setTimeout(r, 0));
    expect(api.get).toHaveBeenCalledWith('/portal/export',
      expect.objectContaining({ params: expect.objectContaining({ q: undefined, status: undefined }), responseType: 'blob' }));
    blobUrlSpy.mockRestore();
    revokeSpy.mockRestore();
  });

  it('يفلتر بالحالة ويحدّث الاستعلام', async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    await screen.findByText('أحمد خالد الخطيب');
    await user.selectOptions(screen.getByLabelText('فلتر الحالة'), 'منفذ');

    // الاستعلام يُعاد بفلتر الحالة (آخر نداء).
    const calls = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.filter(
      (c: unknown[]) => c[0] === '/portal/files',
    );
    const last = calls[calls.length - 1][1] as { params: Record<string, unknown> };
    expect(last.params.status).toBe('منفذ');
  });

  it('يؤخر البحث النصي فلا طلب لكل حرف', async () => {
    // مؤقتات وهمية: حتمي بلا اعتماد على ساعة الجدار (مضاد للتقطع تحت الحمل).
    vi.useFakeTimers();
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    screen.getByText('الملفات التنفيذية');

    const filesCalls = () => (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.filter(
      (c: unknown[]) => c[0] === '/portal/files',
    );
    const before = filesCalls().length;
    expect(before).toBeGreaterThanOrEqual(1);

    const input = screen.getByLabelText('بحث في الملفات التنفيذية');
    fireEvent.change(input, { target: { value: 'أ' } });
    fireEvent.change(input, { target: { value: 'أح' } });
    fireEvent.change(input, { target: { value: 'أحمد' } });

    // 100ms < مهلة 300ms — لا طلب جديد بعد رغم ثلاثة تغييرات.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(100);
    });
    expect(filesCalls().length).toBe(before);

    // انقضاء المهلة: طلب واحد بالقيمة النهائية فقط.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    const after = filesCalls();
    expect(after.length).toBe(before + 1);
    const lastParams = (after[after.length - 1][1] as { params: Record<string, unknown> }).params;
    expect(lastParams.q).toBe('أحمد');
  });

  it('التصدير يستخدم البحث المؤجل فيطابق القائمة المعروضة', async () => {
    // M1: التصدير خلال نافذة الـ300ms يصدّر المعروض (القيمة المؤجلة) لا المخطوط.
    vi.useFakeTimers();
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    screen.getByText('الملفات التنفيذية');

    const exportCalls = () => (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.filter(
      (c: unknown[]) => c[0] === '/portal/export',
    );
    const exportBtn = screen.getByRole('button', { name: 'تصدير إكسل' });

    fireEvent.change(screen.getByLabelText('بحث في الملفات التنفيذية'), { target: { value: 'أحمد' } });
    fireEvent.click(exportBtn);
    expect(exportCalls().length).toBe(1);
    expect((exportCalls()[0][1] as { params: Record<string, unknown> }).params.q).toBeUndefined();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    fireEvent.click(exportBtn);
    expect(exportCalls().length).toBe(2);
    expect((exportCalls()[1][1] as { params: Record<string, unknown> }).params.q).toBe('أحمد');
  });

  it('يلغي طلب القائمة السابق عند تغيير الفلتر', async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    await screen.findByText('الملفات التنفيذية');
    const select = screen.getByLabelText('فلتر الحالة') as HTMLSelectElement;
    await user.selectOptions(select, 'تريث');
    await user.selectOptions(select, 'منفذ');

    const filesCalls = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.filter(
      (c: unknown[]) => c[0] === '/portal/files',
    );
    expect(filesCalls.length).toBeGreaterThanOrEqual(3);
    // أول طلب أُلغي عند تغيّر التبعيات — لا كتابة قديمة فوق الأحدث.
    const firstSignal = (filesCalls[0][1] as { signal?: AbortSignal } | undefined)?.signal;
    expect(firstSignal?.aborted).toBe(true);
    const lastParams = (filesCalls[filesCalls.length - 1][1] as { params: Record<string, unknown> }).params;
    expect(lastParams.status).toBe('منفذ');
  });

  it('لا يعرض كتلة الإحصاءات في صفحة الملفات (انتقلت لصفحة مستقلة)', async () => {
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    await screen.findByText('أحمد خالد الخطيب');
    expect(screen.queryByRole('region', { name: 'إحصاءات نطاق جهتك' })).not.toBeInTheDocument();
    expect(screen.queryByText('أعلى العملات')).not.toBeInTheDocument();
  });

  it('عند فشل تحميل القائمة يعرض الخطأ ولا يعرض الفراغ المضلل «لا توجد ملفات»', async () => {
    // M3: الخطأ والفراغ لا يجتمعان — «لا توجد ملفات» زعمٌ غير صحيح بعد فشل الجلب.
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/portal/my-scope') {
        return Promise.resolve({ data: { scopeType: 'group', groupId: 5, canonicalName: 'جهة', entityType: 'company', entries: [] } });
      }
      if (url === '/portal/files') {
        return Promise.reject(new Error('boom'));
      }
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });

    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    const alerts = await screen.findAllByRole('alert');
    expect(alerts.some((a) => a.textContent?.includes('خطأ من الخادم'))).toBe(true);
    expect(screen.queryByText('لا توجد ملفات مطابقة في نطاق جهتك')).not.toBeInTheDocument();
  });

  it('عند فشل تحميل النطاق يعرض خطأه (لا صمت) وبلا منتقي فرع', async () => {
    // M3b: خطأ `my-scope` كان صامتًا — الصفحة التوأم `PortalStats` تعرضه،
    // فيُوحَّد السلوك: تنبيه ظاهر، والقائمة تبقى عاملة (الخادم يفرض النطاق).
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/portal/my-scope') {
        return Promise.reject(new Error('scope boom'));
      }
      if (url === '/portal/files') {
        return Promise.resolve({
          data: { items: [fileItem()], page: 1, perPage: 20, totalCount: 1, totalPages: 1 },
        });
      }
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });

    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    const alerts = await screen.findAllByRole('alert');
    expect(alerts.some((a) => a.textContent?.includes('خطأ من الخادم'))).toBe(true);
    // القائمة ما زالت تعمل بلا نطاق معلن، ولا منتقي فرع بلا قيود.
    expect(await screen.findByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(screen.queryByLabelText('فلتر الفرع')).not.toBeInTheDocument();
  });

  it('عند فشل النطاق مع قائمة فارغة لا يزعم «لا توجد ملفات في نطاق جهتك»', async () => {
    // R1: الفراغ يدّعي نطاقًا فشل تحميله — فيختبئ مع خطأ النطاق كما مع خطأ القائمة.
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/portal/my-scope') {
        return Promise.reject(new Error('scope boom'));
      }
      if (url === '/portal/files') {
        return Promise.resolve({
          data: { items: [], page: 1, perPage: 20, totalCount: 0, totalPages: 0 },
        });
      }
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });

    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    const alerts = await screen.findAllByRole('alert');
    expect(alerts.some((a) => a.textContent?.includes('خطأ من الخادم'))).toBe(true);
    expect(screen.queryByText('لا توجد ملفات مطابقة في نطاق جهتك')).not.toBeInTheDocument();
  });
});
