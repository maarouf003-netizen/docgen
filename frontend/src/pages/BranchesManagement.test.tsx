import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import BranchesManagement from './BranchesManagement';
import type { BranchDto } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: (error: unknown) => {
    const e = error as {
      isAxiosError?: boolean;
      response?: { status?: number; data?: { message?: string } };
    };
    if (e?.isAxiosError) {
      if (e.response?.data?.message) return e.response.data.message;
      if (e.response?.status === 403) return 'لا تملك صلاحية تنفيذ هذا الإجراء';
      return 'تعذر الاتصال بالخادم. تحقق من الاتصال وأعد المحاولة';
    }
    return 'حدث خطأ غير متوقع';
  },
}));

import { api } from '../api/client';
import { stubMobile } from '../test/stubMobile';

function branchItem(overrides: Partial<BranchDto> = {}): BranchDto {
  return {
    id: 1,
    name: 'الفرع الرئيسي - دمشق',
    code: 'DAM',
    address: 'دمشق',
    governorate: 'دمشق',
    isActive: true,
    userCount: 2,
    documentCount: 5,
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  stubMobile(false);
  useAuthMock.mockReturnValue({ user: { id: 9, role: 'admin' } });
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
    data: [branchItem(), branchItem({ id: 2, name: 'فرع حلب', code: 'ALP' })],
  });
});

describe('BranchesManagement', () => {
  it('يعرض قائمة الفروع مع الكود والحالة', async () => {
    render(<BranchesManagement />);

    const names = await screen.findAllByText('الفرع الرئيسي - دمشق');
    expect(names.length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('DAM')).toBeInTheDocument();
    expect(screen.getAllByText('مفعّل').length).toBeGreaterThanOrEqual(2);
  });

  it('ينشئ فرعاً جديداً مع الحقول', async () => {
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة فرع' }));
    await user.type(screen.getByLabelText('اسم الفرع'), 'فرع حمص');
    await user.type(screen.getByLabelText('كود الفرع'), 'HMS');
    await user.type(screen.getByLabelText('العنوان'), 'حمص');
    await user.type(screen.getByLabelText('الهاتف'), '031222333');
    await user.selectOptions(screen.getByLabelText(/المحافظة/), 'حمص');
    await user.click(screen.getByRole('button', { name: 'إنشاء الفرع' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/branches', {
        name: 'فرع حمص',
        code: 'HMS',
        address: 'حمص',
        phone: '031222333',
        governorate: 'حمص',
      });
    });
  });

  it('يمنع إنشاء فرع دون اختيار المحافظة', async () => {
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة فرع' }));
    await user.type(screen.getByLabelText('اسم الفرع'), 'فرع بلا محافظة');
    await user.type(screen.getByLabelText('كود الفرع'), 'NGO');
    await user.click(screen.getByRole('button', { name: 'إنشاء الفرع' }));

    expect(await screen.findByText(/المحافظة مطلوبة/)).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('ينشئ فرعاً مع محافظة محددة لنطاق رئيس القسم', async () => {
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة فرع' }));
    await user.type(screen.getByLabelText('اسم الفرع'), 'فرع درعا');
    await user.type(screen.getByLabelText('كود الفرع'), 'DRA');
    await user.selectOptions(screen.getByLabelText(/المحافظة/), 'درعا');
    await user.click(screen.getByRole('button', { name: 'إنشاء الفرع' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/branches', expect.objectContaining({
        name: 'فرع درعا',
        governorate: 'درعا',
      }));
    });
  });

  it('يعرض رسالة خطأ عند إرسال فرع دون اسم', async () => {
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة فرع' }));
    await user.type(screen.getByLabelText('كود الفرع'), 'HMS');
    await user.click(screen.getByRole('button', { name: 'إنشاء الفرع' }));

    expect(await screen.findByText(/اسم الفرع مطلوب/)).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يعدّل فرعاً عبر النافذة المنبثقة', async () => {
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.click((await screen.findAllByRole('button', { name: 'تعديل' }))[0]);
    expect(screen.getByRole('dialog', { name: 'تعديل فرع' })).toBeInTheDocument();

    await user.clear(screen.getByLabelText('اسم الفرع'));
    await user.type(screen.getByLabelText('اسم الفرع'), 'الفرع الرئيسي - دمشق المعدل');
    await user.click(screen.getByRole('checkbox', { name: 'الفرع مفعّل' }));
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));

    await waitFor(() => {
      expect(api.put).toHaveBeenCalledWith('/branches/1', {
        name: 'الفرع الرئيسي - دمشق المعدل',
        code: 'DAM',
        address: 'دمشق',
        phone: null,
        governorate: 'دمشق',
        isActive: false,
      });
    });
  });

  it('يمنع حفظ التعديل إذا تُركت المحافظة فارغة', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [branchItem({ governorate: null })],
    });
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.click(await screen.findByRole('button', { name: 'تعديل' }));
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));

    expect(await screen.findByText(/المحافظة مطلوبة/)).toBeInTheDocument();
    expect(api.put).not.toHaveBeenCalled();
  });

  it('يحذف فرعاً بعد التأكيد', async () => {
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.click((await screen.findAllByRole('button', { name: 'تعديل' }))[0]);
    await user.click(screen.getByRole('button', { name: 'حذف الفرع' }));
    expect(screen.getByText(/هل أنت متأكد/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تأكيد الحذف' }));

    await waitFor(() => {
      expect(api.delete).toHaveBeenCalledWith('/branches/1');
    });
  });

  it('يعرض رسالة الخادم عند رفض حذف فرع مستخدم', async () => {
    (api.delete as unknown as ReturnType<typeof vi.fn>).mockRejectedValue({
      isAxiosError: true,
      response: { status: 400, data: { message: 'لا يمكن حذف فرع يحتوي على مستخدمين؛ عطّل الفرع بدلاً من ذلك' } },
    });

    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.click((await screen.findAllByRole('button', { name: 'تعديل' }))[0]);
    await user.click(screen.getByRole('button', { name: 'حذف الفرع' }));
    await user.click(screen.getByRole('button', { name: 'تأكيد الحذف' }));

    expect(await screen.findByText(/لا يمكن حذف فرع يحتوي على مستخدمين/)).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'تعديل فرع' })).toBeInTheDocument();
  });

  it('الشعب: اختيار الفرع يسرد شعبها وينشئ شعبة جديدة', async () => {
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockImplementation((url: string) => {
      if (url.startsWith('/sections'))
        return Promise.resolve({ data: [{ id: 3, branchId: 1, name: 'مصياف', isActive: true, circuitCount: 2, headName: null }] });
      if (url.startsWith('/execution-circuits/stats')) return Promise.resolve({ data: [] });
      if (url.startsWith('/head-succession')) return Promise.resolve({ data: [] });
      return Promise.resolve({ data: [branchItem(), branchItem({ id: 2, name: 'فرع حلب', code: 'ALP' })] });
    });
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.selectOptions(await screen.findByLabelText('الفرع الأب'), '1');
    expect(await screen.findByText('مصياف')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/sections', expect.objectContaining({ params: { branchId: 1 } }));

    await user.type(screen.getByLabelText('اسم الشعبة الجديدة'), 'شعبة جديدة');
    await user.click(screen.getByRole('button', { name: '+ إضافة شعبة' }));
    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/sections', { branchId: 1, name: 'شعبة جديدة' });
    });
  });

  it('نقل دائرة: يرسل المالك الجديد (null = القسم) ويُظهر رسالة النجاح', async () => {
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockImplementation((url: string) => {
      if (url.startsWith('/sections'))
        return Promise.resolve({ data: [{ id: 3, branchId: 1, name: 'مصياف', isActive: true, circuitCount: 0, headName: null }] });
      if (url.startsWith('/execution-circuits/stats'))
        return Promise.resolve({ data: [{ circuitId: 11, circuitName: 'الأولى', branchId: 1, isActive: true, fileCount: 4, lawyerCount: 2, pendingCount: 0, sectionId: null, sectionName: null }] });
      if (url.startsWith('/head-succession')) return Promise.resolve({ data: [] });
      return Promise.resolve({ data: [branchItem()] });
    });
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.selectOptions(await screen.findByLabelText('الفرع الأب'), '1');
    await user.click(await screen.findByRole('tab', { name: 'نقل دائرة' }));
    await user.selectOptions(screen.getByLabelText('الدائرة'), '11');
    await user.selectOptions(screen.getByLabelText('المالك الجديد'), '3');
    await user.click(screen.getByRole('button', { name: 'نقل الدائرة' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/execution-circuits/11/transfer', { targetSectionId: 3, version: null });
    });
    expect(await screen.findByRole('status')).toHaveTextContent(/تم نقل الدائرة/);
  });

  it('سجل التعاقب: يعرض الأحداث بتسميات عربية', async () => {
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockImplementation((url: string) => {
      if (url.startsWith('/sections')) return Promise.resolve({ data: [] });
      if (url.startsWith('/execution-circuits/stats')) return Promise.resolve({ data: [] });
      if (url.startsWith('/head-succession'))
        return Promise.resolve({
          data: [{ id: 1, branchId: 1, branchName: 'دمشق', sectionId: 3, sectionName: 'مصياف', userId: 6, userName: 'خلف الرئيس', role: 'subhead', event: 'succeeded', at: '2026-10-06', actorName: 'مشرف', reason: null }],
        });
      return Promise.resolve({ data: [branchItem()] });
    });
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.selectOptions(await screen.findByLabelText('الفرع الأب'), '1');
    await user.click(await screen.findByRole('tab', { name: 'سجل التعاقب' }));
    expect(await screen.findByText('خلف الرئيس')).toBeInTheDocument();
    expect(screen.getByText(/إحلال خلف/)).toBeInTheDocument();
  });
});
