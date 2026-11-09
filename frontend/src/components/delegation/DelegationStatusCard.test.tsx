import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DelegationStatusCard } from './DelegationStatusCard';
import type { DelegationDto, DocumentResponse } from '../../types';
import { makeHeadAlertSummary } from '../../test/factories';

vi.mock('../../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api/client')>();
  return { ...original, api: { get: vi.fn(), post: vi.fn(), delete: vi.fn() } };
});

import { api } from '../../api/client';

function doc(execStatus: string): DocumentResponse {
  return {
    execStatus,
    execSubStatus: '',
    isDraft: false,
    generalEntitySide: 'applicant',
  } as unknown as DocumentResponse;
}

describe('DelegationStatusCard', () => {
  beforeEach(() => {
    vi.mocked(api.get).mockResolvedValue({ data: [] });
  });

  it('يعرض بطاقة «حالة الإنابة» بشارة «مسترد» الفوشيا للمناب المسترد', async () => {
    render(<DelegationStatusCard doc={doc('مسترد')} delegationId={9} />);

    expect(await screen.findByRole('heading', { name: 'حالة الإنابة' })).toBeInTheDocument();
    expect(screen.getByText('مسترد')).toBeInTheDocument();
  });

  it('يعرض شارة «تريث» للمناب المتريث', async () => {
    render(<DelegationStatusCard doc={doc('تريث')} delegationId={9} />);

    expect(await screen.findByRole('heading', { name: 'حالة الإنابة' })).toBeInTheDocument();
    expect(screen.getByText('تريث')).toBeInTheDocument();
  });

  it('يعرض تنبيهات المرآة داخل البطاقة من `PagedResult` بمعاينتها', async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: {
        items: [
          makeHeadAlertSummary({
            id: 1,
            kind: 'delegation_mirror_updated',
            messagePreview: 'حدّث المنيب بيانات السند',
          }),
        ],
        page: 1,
        perPage: 100,
        totalCount: 1,
        totalPages: 1,
      },
    });
    render(<DelegationStatusCard doc={doc('تريث')} delegationId={9} />);

    expect(await screen.findByText('حدّث المنيب بيانات السند')).toBeInTheDocument();
  });

  it('يعرض حقول إتمام الإنابة في بطاقة حالة الإنابة', async () => {
    const delegation = {
      id: 9,
      sourceDocumentId: 10,
      sourceDocumentLabel: 'الملف المنيب',
      status: 'منفذ إنابة',
      isExternal: false,
      createdAt: '2026-08-01',
      createdById: 7,
      returnDate: '2026-09-01',
      forcedExecutionDate: '10/5/2026',
      saleCoversFullDebt: false,
      assets: [
        { id: 1, assetKind: 'عقار', assetLabel: 'عقار المزة', salePrice: 750000, snapshotAdjusted: false },
      ],
    } as DelegationDto;

    render(
      <DelegationStatusCard
        doc={doc('منفذ إنابة')}
        delegationId={9}
        delegation={delegation}
      />,
    );

    expect(await screen.findByText('بيانات إتمام الإنابة')).toBeInTheDocument();
    expect(screen.getByText('تاريخ إعادة الملف للدائرة المنيبة')).toBeInTheDocument();
    expect(screen.getByText('تاريخ قرار الإحالة القطعية')).toBeInTheDocument();
    expect(screen.getByText('لم يغطِ كامل المديونية')).toBeInTheDocument();
    expect(screen.getByText('عقار المزة')).toBeInTheDocument();
  });
});
