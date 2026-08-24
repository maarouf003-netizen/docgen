import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import DocumentChangesModal from './DocumentChangesModal';
import type { DocumentChangeGroupDto } from '../../types';

vi.mock('../../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api/client')>();
  return {
    ...original,
    api: {
      get: vi.fn(),
    },
  };
});

import { api } from '../../api/client';

const group = (): DocumentChangeGroupDto => ({
  auditLogId: 11,
  actionType: 'update',
  userName: 'المحامي الأول',
  timestamp: '2026-08-24T10:00:00Z',
  changes: [
    { fieldLabel: 'اسم المنفذ عليه', fieldKey: 'BorrowerName', oldValue: 'أحمد', newValue: 'أحمد سعيد' },
    { fieldLabel: 'المبلغ', fieldKey: 'AmountNumeric', oldValue: '1000', newValue: '3000.75' },
  ],
});

beforeEach(() => {
  vi.clearAllMocks();
});

describe('DocumentChangesModal', () => {
  it('يعرض مجموعات التعديل بالفاعل والوقت وكل حقل قبل/بعد', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [group()], page: 1, perPage: 10, totalCount: 1 },
    });

    render(<DocumentChangesModal documentId={7} onClose={() => {}} />);

    await screen.findByText('تعديل بيانات الملف');
    expect(screen.getByText('المحامي الأول')).toBeInTheDocument();
    expect(screen.getByText('اسم المنفذ عليه')).toBeInTheDocument();
    expect(screen.getByText('أحمد سعيد')).toBeInTheDocument();
    expect(screen.getByText('3000.75')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith(
      '/documents/7/changes?page=1&perPage=10',
      expect.objectContaining({ signal: expect.any(AbortSignal) }),
    );
  });

  it('يعرض رسالة الفراغ عندما لا توجد تعديلات', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [], page: 1, perPage: 10, totalCount: 0 },
    });

    render(<DocumentChangesModal documentId={7} onClose={() => {}} />);

    await screen.findByText(/لا توجد تعديلات مسجلة/);
    await waitFor(() => expect(screen.queryByText('تعديل بيانات الملف')).not.toBeInTheDocument());
  });
});
