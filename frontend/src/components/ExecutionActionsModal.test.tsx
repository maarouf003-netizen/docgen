import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ExecutionActionsModal from './ExecutionActionsModal';

vi.mock('../auth/useAuth', () => ({
  useAuth: () => ({ user: { role: 'lawyer', username: 'lawyer1', fullName: 'محامي', id: 1, branchId: null } }),
}));

const apiMock = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
}));

vi.mock('../api/client', () => ({
  api: apiMock,
}));

import { api } from '../api/client';

beforeEach(() => {
  vi.clearAllMocks();
});

describe('ExecutionActionsModal', () => {
  it('يعرض قائمة الإجراءات والملاحظات مع نصوصها وتواريخها', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [
        { id: 1, type: 'action', text: 'إشعار أول', actionDate: '1/8/2026', createdByName: 'محامي', createdAt: '2026-08-01' },
        { id: 2, type: 'note', text: 'ملاحظة ثانية', actionDate: '2/8/2026', createdByName: 'محامي', createdAt: '2026-08-02' },
      ],
    });

    render(<ExecutionActionsModal documentId={7} onClose={() => {}} />);

    expect(await screen.findByText('إشعار أول')).toBeInTheDocument();
    expect(screen.getByText('ملاحظة ثانية')).toBeInTheDocument();
    expect(screen.getByText('2/8/2026')).toBeInTheDocument();
    expect(screen.getAllByText('إجراء').length).toBeGreaterThan(0);
    expect(screen.getAllByText('ملاحظة').length).toBeGreaterThan(0);
    expect(api.get).toHaveBeenCalledWith('/documents/7/actions');
  });

  it('يعرض رسالة عدم وجود عناصر', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });

    render(<ExecutionActionsModal documentId={7} onClose={() => {}} />);

    expect(await screen.findByText('لا توجد إجراءات أو ملاحظات بعد')).toBeInTheDocument();
  });

  it('يحفظ إجراءً جديدًا عند الضغط على حفظ كإجراء', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});

    const onChanged = vi.fn();
    render(<ExecutionActionsModal documentId={7} onClose={() => {}} onChanged={onChanged} />);

    await screen.findByText('لا توجد إجراءات أو ملاحظات بعد');
    await user.click(screen.getByText('+ إضافة إجراء أو ملاحظة'));

    await user.type(screen.getByPlaceholderText('أدخل نص الإجراء أو الملاحظة...'), 'إجراء جديد');
    await user.type(screen.getByPlaceholderText('مثال: 1/8/2026'), '3/8/2026');
    await user.click(screen.getByRole('button', { name: 'حفظ كإجراء' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/documents/7/actions', {
        type: 'action',
        text: 'إجراء جديد',
        actionDate: '3/8/2026',
        reminderDuration: null,
        reminderColor: null,
      });
    });
    expect(onChanged).toHaveBeenCalled();
  });

  it('لا يعرض اختيار ملاحظة/إجراء ويظهر تلميح التاريخ المحايد في نموذج الإضافة', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });

    render(<ExecutionActionsModal documentId={7} onClose={() => {}} />);

    await screen.findByText('لا توجد إجراءات أو ملاحظات بعد');
    await user.click(screen.getByText('+ إضافة إجراء أو ملاحظة'));

    expect(screen.queryByRole('radio')).not.toBeInTheDocument();
    expect(screen.getByText(/يلزم للإجراء، اختياري للملاحظة/)).toBeInTheDocument();
  });

  it('يحفظ ملاحظة جديدة عند الضغط على حفظ كملاحظة', async () => {    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});

    const onChanged = vi.fn();
    render(<ExecutionActionsModal documentId={7} onClose={() => {}} onChanged={onChanged} />);

    await screen.findByText('لا توجد إجراءات أو ملاحظات بعد');
    await user.click(screen.getByText('+ إضافة إجراء أو ملاحظة'));

    await user.type(screen.getByPlaceholderText('أدخل نص الإجراء أو الملاحظة...'), 'ملاحظة بلا تاريخ');
    await user.click(screen.getByRole('button', { name: 'حفظ كملاحظة' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/documents/7/actions', {
        type: 'note',
        text: 'ملاحظة بلا تاريخ',
        actionDate: null,
        reminderDuration: null,
        reminderColor: null,
      });
    });
  });

  it('يعرض أزرار تعديل وحذف للمحامي ويعيد التحميل بعد الحذف', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [{ id: 1, type: 'action', text: 'إجراء للحذف', actionDate: '1/1/2026', createdAt: '2026-08-01' }],
    });
    (api.delete as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    const onChanged = vi.fn();
    render(<ExecutionActionsModal documentId={7} onClose={() => {}} onChanged={onChanged} />);

    await screen.findByText('إجراء للحذف');
    await user.click(screen.getByLabelText('حذف'));

    await waitFor(() => {
      expect(api.delete).toHaveBeenCalledWith('/documents/7/actions/1');
    });
    expect(onChanged).toHaveBeenCalled();
  });

  it('يفتح نموذج التعديل ويرسل PUT عند حفظ التعديل', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [{ id: 1, type: 'action', text: 'إجراء قديم', actionDate: '1/1/2026', createdAt: '2026-08-01' }],
    });
    (api.put as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});

    const onChanged = vi.fn();
    render(<ExecutionActionsModal documentId={7} onClose={() => {}} onChanged={onChanged} />);

    await screen.findByText('إجراء قديم');
    await user.click(screen.getByLabelText('تعديل'));
    await user.clear(screen.getByPlaceholderText('أدخل نص الإجراء أو الملاحظة...'));
    await user.type(screen.getByPlaceholderText('أدخل نص الإجراء أو الملاحظة...'), 'إجراء محدث');
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));

    await waitFor(() => {
      expect(api.put).toHaveBeenCalledWith('/documents/7/actions/1', {
        type: 'action',
        text: 'إجراء محدث',
        actionDate: '1/1/2026',
        reminderDuration: null,
        reminderColor: null,
      });
    });
    expect(onChanged).toHaveBeenCalled();
  });

  it('يستدعي onClose عند الضغط على زر الإغلاق', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });

    const onClose = vi.fn();
    render(<ExecutionActionsModal documentId={7} onClose={onClose} />);

    await screen.findByText('لا توجد إجراءات أو ملاحظات بعد');
    await user.click(screen.getByLabelText('إغلاق'));

    expect(onClose).toHaveBeenCalled();
  });

  it('يحفظ تذكيرًا (مدة ولون) مع الإجراء عند تفعيل «ذكرني»', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});

    render(<ExecutionActionsModal documentId={7} onClose={() => {}} />);

    await screen.findByText('لا توجد إجراءات أو ملاحظات بعد');
    await user.click(screen.getByText('+ إضافة إجراء أو ملاحظة'));

    await user.type(screen.getByPlaceholderText('أدخل نص الإجراء أو الملاحظة...'), 'متابعة المحكمة');
    await user.type(screen.getByPlaceholderText('مثال: 1/8/2026'), '3/8/2026');
    await user.click(screen.getByRole('button', { name: 'ذكرني' }));

    await user.selectOptions(screen.getByLabelText('مدة التذكير'), 'أسبوع');
    await user.click(screen.getByRole('button', { name: 'أحمر' }));
    await user.click(screen.getByRole('button', { name: 'حفظ كإجراء' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/documents/7/actions', {
        type: 'action',
        text: 'متابعة المحكمة',
        actionDate: '3/8/2026',
        reminderDuration: 'أسبوع',
        reminderColor: 'أحمر',
      });
    });
  });

  it('يعرض شارة التذكير الملونة في القائمة', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [
        {
          id: 1,
          type: 'action',
          text: 'إجراء بتذكير',
          actionDate: '1/1/2026',
          reminderDuration: 'أسبوعين',
          reminderColor: 'بنفسجي',
          createdByName: 'محامي',
          createdAt: '2026-08-01',
        },
      ],
    });

    render(<ExecutionActionsModal documentId={7} onClose={() => {}} />);

    expect(await screen.findByText('🔔 تذكير: أسبوعين')).toBeInTheDocument();
  });

  it('يعبّئ مدة ولون التذكير عند فتح نموذج تعديل عنصر له تذكير', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [
        {
          id: 1,
          type: 'action',
          text: 'إجراء قديم',
          actionDate: '1/1/2026',
          reminderDuration: 'شهر',
          reminderColor: 'أصفر',
          createdAt: '2026-08-01',
        },
      ],
    });

    render(<ExecutionActionsModal documentId={7} onClose={() => {}} />);

    await screen.findByText('إجراء قديم');
    await user.click(screen.getByLabelText('تعديل'));

    expect(screen.getByRole('button', { name: 'إلغاء التذكير' })).toBeInTheDocument();
    expect(screen.getByLabelText('مدة التذكير')).toHaveValue('شهر');
    expect(screen.getByRole('button', { name: 'أصفر' })).toBeInTheDocument();
  });
});
