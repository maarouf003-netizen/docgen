import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { InterventionModal } from './InterventionModal';
import type { DocumentInterventionDto } from '../../types';

vi.mock('../../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api/client')>();
  return {
    ...original,
    api: {
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    },
  };
});

vi.mock('../entity/PublicEntityPickerModal', () => ({
  PublicEntityPickerModal: ({
    onPick,
    onClose,
  }: {
    onPick: (entry: {
      id: number;
      canonicalName: string;
      branchName: string;
      governorate: string;
    }) => void;
    onClose: () => void;
  }) => (
    <div role="dialog" aria-label="منتقي الجهة">
      <button
        type="button"
        onClick={() =>
          onPick({
            id: 11,
            canonicalName: 'وزارة العدل',
            branchName: 'مديرية التنفيذ',
            governorate: 'دمشق',
          })
        }
      >
        اختيار جهة
      </button>
      <button type="button" onClick={onClose}>
        إغلاق المنتقي
      </button>
    </div>
  ),
}));

import { api } from '../../api/client';

const saved: DocumentInterventionDto = {
  id: 5,
  nature: 'natural',
  natureLabel: 'شخص طبيعي',
  displayName: 'سامر محمود الحسن',
  receiptDate: '2026-08-01',
  acceptanceDate: '2026-08-10',
  name: 'سامر',
  father: 'محمود',
  family: 'الحسن',
  heirs: [],
};

function setup(initial: DocumentInterventionDto | null = null) {
  const onSaved = vi.fn();
  const onClose = vi.fn();
  render(
    <InterventionModal
      documentId={1}
      initial={initial}
      onSaved={onSaved}
      onClose={onClose}
    />,
  );
  return { onSaved, onClose };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('InterventionModal', () => {
  it('يحفظ متدخلًا طبيعيًا بالحمولة الصحيحة ويُعيد الناتج للأب', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: saved });
    const { onSaved } = setup();

    await user.type(screen.getByLabelText(/الاسم$/), 'سامر');
    await user.type(screen.getByLabelText('اسم الأب'), 'محمود');
    await user.type(screen.getByLabelText('النسبة'), 'الحسن');
    await user.type(screen.getByLabelText('رقم ورود طلب التدخل'), '١٢٣');
    await user.type(screen.getByLabelText(/تاريخ ورود طلب التدخل/), '1/8/2026');
    await user.type(screen.getByLabelText('تاريخ قبول طلب التدخل'), '10/8/2026');
    await user.click(screen.getByRole('button', { name: 'حفظ' }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    expect(api.post).toHaveBeenCalledWith(
      '/documents/1/interventions',
      expect.objectContaining({
        nature: 'natural',
        name: 'سامر',
        father: 'محمود',
        family: 'الحسن',
        receiptNumber: '123',
        receiptDate: '1/8/2026',
        acceptanceDate: '10/8/2026',
      }),
    );
    // الحقول غير المعنية بالصفة الطبيعية لا تُرسل.
    expect(
      (api.post as unknown as ReturnType<typeof vi.fn>).mock.calls[0][1],
    ).not.toHaveProperty('registryId');
    expect(onSaved).toHaveBeenCalledWith(saved);
  });

  it('يمنع الحفظ دون تاريخ ورود ويعرض رسالة واضحة', async () => {
    const user = userEvent.setup();
    setup();

    await user.type(screen.getByLabelText(/الاسم$/), 'سامر');
    await user.click(screen.getByRole('button', { name: 'حفظ' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'تاريخ ورود طلب التدخل مطلوب',
    );
    expect(api.post).not.toHaveBeenCalled();
  });

  it('عند صفة «جهة عامة» يلزم اختيارها من السجل المرجعي ويرسل معرّف القيد', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { ...saved, nature: 'public', natureLabel: 'جهة عامة', registryId: 11 },
    });
    setup();

    await user.selectOptions(screen.getByLabelText('صفة المتدخل'), 'public');
    await user.click(screen.getByRole('button', { name: 'اختيار من السجل…' }));
    await user.click(await screen.findByRole('button', { name: 'اختيار جهة' }));
    await user.type(screen.getByLabelText(/تاريخ ورود طلب التدخل/), '2/8/2026');
    await user.click(screen.getByRole('button', { name: 'حفظ' }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    expect(api.post).toHaveBeenCalledWith(
      '/documents/1/interventions',
      expect.objectContaining({
        nature: 'public',
        entityName: 'وزارة العدل',
        entityBranch: 'مديرية التنفيذ',
        governorate: 'دمشق',
        registryId: 11,
      }),
    );
  });

  it('«إعادة تعيين» تعيد الحقول الابتدائية دون إغلاق النافذة', async () => {
    const user = userEvent.setup();
    const { onClose } = setup();

    await user.type(screen.getByLabelText(/الاسم$/), 'سامر');
    await user.click(screen.getByRole('button', { name: 'إعادة تعيين' }));

    expect(screen.getByLabelText(/الاسم$/)).toHaveValue('');
    expect(onClose).not.toHaveBeenCalled();
  });

  it('في وضع التعديل يملأ الحقول ويستدعي PUT على معرّف المتدخل نفسه', async () => {
    const user = userEvent.setup();
    (api.put as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: saved });
    setup(saved);

    expect(screen.getByLabelText(/الاسم$/)).toHaveValue('سامر');
    expect(screen.getByLabelText(/تاريخ ورود طلب التدخل/)).toHaveValue('2026-08-01');
    await user.click(screen.getByRole('button', { name: 'حفظ' }));

    await waitFor(() => expect(api.put).toHaveBeenCalledTimes(1));
    expect(api.put).toHaveBeenCalledWith(
      '/documents/1/interventions/5',
      expect.objectContaining({ name: 'سامر', receiptDate: '2026-08-01' }),
    );
    expect(api.post).not.toHaveBeenCalled();
  });
});
