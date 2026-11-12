import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DocumentInterventionsList } from './DocumentInterventionsList';
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

import { api } from '../../api/client';

const items: DocumentInterventionDto[] = [
  {
    id: 5,
    nature: 'natural',
    natureLabel: 'شخص طبيعي',
    displayName: 'سامر محمود الحسن',
    receiptDate: '2026-08-01',
    acceptanceDate: '2026-07-28',
    subject: 'طلب تدخل',
    heirs: [],
  },
  {
    id: 6,
    nature: 'public',
    natureLabel: 'جهة عامة',
    displayName: 'وزارة العدل مديرية التنفيذ',
    receiptDate: '2026-08-02',
    registryId: 11,
    heirs: [],
  },
];

beforeEach(() => {
  vi.clearAllMocks();
});

describe('DocumentInterventionsList', () => {
  it('يعرض المتدخلين ويفتح التعديل من صفّه', async () => {
    const user = userEvent.setup();
    const onEdit = vi.fn();
    render(
      <DocumentInterventionsList
        documentId={1}
        interventions={items}
        onChanged={vi.fn()}
        onEdit={onEdit}
      />,
    );

    expect(screen.getByText('سامر محمود الحسن')).toBeInTheDocument();
    expect(screen.getByText('وزارة العدل مديرية التنفيذ')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'تعديل المتدخل سامر محمود الحسن' }));
    expect(onEdit).toHaveBeenCalledWith(items[0]);
  });

  it('يحذف المتدخل بعد تأكيد ويحدّث القائمة', async () => {
    const user = userEvent.setup();
    (api.delete as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: null });
    const onChanged = vi.fn();
    render(
      <DocumentInterventionsList
        documentId={1}
        interventions={items}
        onChanged={onChanged}
        onEdit={vi.fn()}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'حذف المتدخل سامر محمود الحسن' }));
    const dialog = screen.getByRole('dialog', { name: 'تأكيد حذف المتدخل' });
    await user.click(within(dialog).getByRole('button', { name: 'حذف' }));

    await waitFor(() => expect(api.delete).toHaveBeenCalledWith('/documents/1/interventions/5'));
    expect(onChanged).toHaveBeenCalledWith([items[1]]);
  });

  it('«إلغاء» في نافذة التأكيد لا يحذف', async () => {
    const user = userEvent.setup();
    render(
      <DocumentInterventionsList
        documentId={1}
        interventions={items}
        onChanged={vi.fn()}
        onEdit={vi.fn()}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'حذف المتدخل سامر محمود الحسن' }));
    const dialog = screen.getByRole('dialog', { name: 'تأكيد حذف المتدخل' });
    await user.click(within(dialog).getByRole('button', { name: 'إلغاء' }));

    expect(screen.queryByRole('dialog', { name: 'تأكيد حذف المتدخل' })).not.toBeInTheDocument();
    expect(api.delete).not.toHaveBeenCalled();
  });

  it('لا يعرض شيئًا عند غياب المتدخلين', () => {
    const { container } = render(
      <DocumentInterventionsList
        documentId={1}
        interventions={[]}
        onChanged={vi.fn()}
        onEdit={vi.fn()}
      />,
    );
    expect(container).toBeEmptyDOMElement();
  });
});
