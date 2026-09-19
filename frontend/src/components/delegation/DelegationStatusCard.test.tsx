import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DelegationStatusCard } from './DelegationStatusCard';
import type { DocumentResponse } from '../../types';

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

  it('يعرض تنبيهات المرآة داخل البطاقة', async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: [{ id: 1, message: 'حدّث المنيب بيانات السند', createdAt: '2026-08-02T10:00:00Z' }],
    });
    render(<DelegationStatusCard doc={doc('تريث')} delegationId={9} />);

    expect(await screen.findByText('حدّث المنيب بيانات السند')).toBeInTheDocument();
  });
});
