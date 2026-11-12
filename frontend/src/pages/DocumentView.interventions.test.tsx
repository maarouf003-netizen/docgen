import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import DocumentView from './DocumentView';
import type { DocumentResponse } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/client')>();
  return {
    ...original,
    api: {
      get: vi.fn(),
      post: vi.fn(),
      delete: vi.fn(),
    },
  };
});

import { api } from '../api/client';

const intervention = {
  id: 5,
  nature: 'natural' as const,
  natureLabel: 'شخص طبيعي',
  displayName: 'سامر محمود الحسن',
  receiptNumber: '123',
  receiptDate: '2026-08-01',
  acceptanceDate: '2026-08-10',
  subject: 'طلب تدخل في الملف',
  notes: 'ملاحظة أولى\nملاحظة ثانية',
  name: 'سامر',
  father: 'محمود',
  family: 'الحسن',
  mother: 'ليلى',
  addressType: 'موطن مختار',
  address: 'المزة',
  heirs: [],
};

const doc = {
  id: 1,
  createdAt: '2026-07-31',
  updatedAt: '2026-07-31',
  version: 0,
  documentType: 'سند دين',
  isDraft: false,
  borrowerName: 'أحمد',
  borrowerFather: 'محمد',
  borrowerFamily: 'خالد',
  generalEntitySide: 'applicant',
  contractTypeSelector: 'عادي',
  amountNumeric: 0,
  amount2Numeric: 0,
  amount3Numeric: 0,
  inclusionAmountNumeric: 0,
  inclusionAmount2Numeric: 0,
  inclusionAmount3Numeric: 0,
  guarantors: [],
  assets: [],
  borrowerHeirs: [],
  executionApplicants: [],
  executedPublicEntities: [],
  executedNaturalPersons: [],
  applicantPublicEntities: [],
  interventions: [intervention],
  occurrences: [
    {
      id: 9,
      occurrenceType: 'intervention',
      occurrenceTypeLabel: 'طلب تدخل',
      eventDate: '2026-08-01T00:00:00Z',
      details: {
        intervenorName: 'سامر محمود الحسن',
        acceptanceDate: '2026-08-10',
        nature: 'natural',
      },
      interventionId: 5,
      source: 'system',
    },
  ],
} as unknown as DocumentResponse;

function renderView() {
  return render(
    <MemoryRouter initialEntries={['/documents/1']}>
      <Routes>
        <Route path="/documents/:id" element={<DocumentView />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  useAuthMock.mockReturnValue({ isHead: false, user: { role: 'lawyer', id: 7 } });
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: doc });
});

describe('DocumentView · المتدخلون', () => {
  it('يعرض المتدخل تحت عنوان «المتدخلون» في بطاقة الأطراف', async () => {
    renderView();

    await screen.findByText('أطراف الملف التنفيذي');
    expect(screen.getByText('المتدخلون')).toBeInTheDocument();
    expect(screen.getByText('سامر محمود الحسن')).toBeInTheDocument();
    expect(screen.getByText('موضوع التدخل: طلب تدخل في الملف')).toBeInTheDocument();
  });

  it('يفتح نافذة تفاصيل المتدخل من صفّه في بطاقة الأطراف', async () => {
    const user = userEvent.setup();
    renderView();

    await screen.findByText('أطراف الملف التنفيذي');
    await user.click(screen.getByText('سامر محمود الحسن'));

    const dialog = screen.getByRole('dialog', { name: 'المتدخل — شخص طبيعي' });
    expect(dialog).toBeInTheDocument();
    expect(screen.getByText('بيانات طلب التدخل')).toBeInTheDocument();
    expect(screen.getByText('123')).toBeInTheDocument();
    expect(screen.getByText('طلب تدخل في الملف')).toBeInTheDocument();
    expect(screen.getByText('ليلى')).toBeInTheDocument();
  });

  it('سطر الوقعة البنفسجي قابل للنقر ويفتح تفاصيل المتدخل', async () => {
    const user = userEvent.setup();
    renderView();

    await screen.findByText('أطراف الملف التنفيذي');
    const line = screen.getByRole('button', { name: /عرض تفاصيل طلب التدخل/ });
    expect(line.className).toContain('text-purple-700');
    expect(line).toHaveTextContent('ورد طلب تدخل من سامر محمود الحسن');
    expect(line).toHaveTextContent('تاريخ قبول طلب التدخل');

    await user.click(line);
    expect(screen.getByRole('dialog', { name: 'المتدخل — شخص طبيعي' })).toBeInTheDocument();
  });
});
