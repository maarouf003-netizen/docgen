import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PublicEntityPickerModal, PROPOSAL_WARNING_TEXT } from './PublicEntityPickerModal';
import type { PublicEntityEntryDto } from '../../types';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
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
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
    data: {
      items: [
        entry(),
        entry({ id: 12, groupId: 6, canonicalName: 'مديرية النقل', entityType: 'administration', governorate: 'حلب', branchName: 'فرع النقل' }),
        entry({ id: 13, groupId: 7, canonicalName: 'هيئة التخطيط', entityType: 'authority', governorate: 'دمشق', branchName: 'فرع التخطيط', status: 'pending' }),
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
    render(<PublicEntityPickerModal sourceDocumentId={7} onClose={onClose} onPick={onPick} />);

    await user.click(await screen.findByRole('button', { name: /وزارة التعليم/ }));

    expect(onPick).toHaveBeenCalledTimes(1);
    expect(onPick.mock.calls[0][0]).toMatchObject({ id: 11, canonicalName: 'وزارة التعليم' });
    expect(onClose).not.toHaveBeenCalled();
  });

  it('يميّز قيد الانتظار بشارة خاصة (د4/§5.3)', async () => {
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    expect(await screen.findByText('بانتظار الاعتماد')).toBeInTheDocument();
  });

  it('لا يعرض عدّاد ملفات في النتائج إطلاقًا (د9)', async () => {
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await screen.findByText('وزارة التعليم');
    // حتى لو تسللت حقول عدّادات من الخادم فلا يجوز عرضها بأي صيغة.
    expect(screen.queryByText(/عدد الملفات/)).not.toBeInTheDocument();
    expect(screen.queryByText(/ملفًا/)).not.toBeInTheDocument();
    expect(screen.queryByText(/ملفات/)).not.toBeInTheDocument();
  });

  it('يبدّل التجميع حسب المحافظة من كتالوج النتائج', async () => {
    const user = userEvent.setup();
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    await screen.findByText('وزارة التعليم');
    await user.click(screen.getByRole('button', { name: 'حلب' }));

    expect(screen.getByText('مديرية النقل')).toBeInTheDocument();
    expect(screen.queryByText('وزارة التعليم')).not.toBeInTheDocument();
    expect(screen.queryByText('هيئة التخطيط')).not.toBeInTheDocument();
  });

  it('يستخلص اقتراحات الفرع من القيود المطابقة', async () => {
    render(<PublicEntityPickerModal onClose={vi.fn()} onPick={vi.fn()} />);

    expect(await screen.findByText(/فروع مقترحة من القيود المطابقة:/)).toHaveTextContent(
      /الفرع الرئيسي/,
    );
  });

  it('يعرض نموذج الاقتراح بنص التحذير الحرفي والـplaceholder المعتمدين (د7)', async () => {
    const user = userEvent.setup();
    render(<PublicEntityPickerModal sourceDocumentId={7} onClose={vi.fn()} onPick={vi.fn()} />);

    await user.click(await screen.findByRole('button', { name: /جهة غير موجودة؟ اقترح إضافة…/ }));

    expect(screen.getByText(PROPOSAL_WARNING_TEXT)).toBeInTheDocument();
    expect(
      screen.getByPlaceholderText('مثال: المدير العام للمصرف التجاري السوري'),
    ).toBeInTheDocument();
    // صيغتا المناداة المعتمدتان فقط (د8)
    expect(screen.getByLabelText('الصيغة')).toHaveTextContent('إضافة لوظيفته');
    expect(screen.getByLabelText('الصيغة')).toHaveTextContent('إضافة لمنصبه');
  });

  it('يرسل الاقتراح بحالة انتظار مع معرّف الملف المصدر ويعرض رسالة نجاح', async () => {
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const user = userEvent.setup();
    render(<PublicEntityPickerModal sourceDocumentId={7} onClose={vi.fn()} onPick={vi.fn()} />);

    await user.click(await screen.findByRole('button', { name: /جهة غير موجودة؟ اقترح إضافة…/ }));
    await user.type(screen.getByLabelText('اسم الجهة'), 'هيئة جديدة كلية');
    await user.selectOptions(screen.getByLabelText('المحافظة'), 'حمص');
    await user.click(screen.getByRole('button', { name: 'إرسال الاقتراح' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-registry/proposals', {
        proposedName: 'هيئة جديدة كلية',
        entityType: 'ministry',
        governorate: 'حمص',
        branchName: 'الفرع الرئيسي',
        citationFormula: 'add-to-job',
        sourceDocumentId: 7,
      });
    });
    expect(await screen.findByRole('status')).toHaveTextContent(/بانتظار اعتماد رئيس القسم/);
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
