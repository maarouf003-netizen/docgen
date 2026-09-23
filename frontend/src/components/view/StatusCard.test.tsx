import { describe, it, expect, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { StatusCard } from './StatusCard';
import type { DocumentResponse } from '../../types';

function doc(overrides: Record<string, unknown>): DocumentResponse {
  return {
    execStatus: '',
    execSubStatus: '',
    isDraft: false,
    generalEntitySide: 'applicant',
    occurrences: [],
    ...overrides,
  } as unknown as DocumentResponse;
}

const noop = () => {};

describe('StatusCard', () => {
  it('يعرض زر «إتمام الإنابة» بجانب «تغيير الحالة» عند N11 ويستدعي onCompleteDelegation', () => {
    const onComplete = vi.fn();
    render(
      <StatusCard
        doc={doc({})}
        canChangeStatus
        onOpenStatus={noop}
        canCompleteDelegation
        onCompleteDelegation={onComplete}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'إتمام الإنابة' }));
    expect(onComplete).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('button', { name: 'تغيير الحالة' })).toBeInTheDocument();
  });

  it('يخفي زر «إتمام الإنابة» دون N11', () => {
    render(<StatusCard doc={doc({})} canChangeStatus onOpenStatus={noop} />);

    expect(screen.queryByRole('button', { name: 'إتمام الإنابة' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تغيير الحالة' })).toBeInTheDocument();
  });

  it('يمرّر اسم المنيب إلى ملخص الحالة (L5)', () => {
    render(
      <StatusCard
        doc={doc({
          sourceDelegationId: 9,
          execStatus: 'تريث',
          tarithNumber: '77',
          tarithDate: '1/8/2026',
        })}
        canChangeStatus={false}
        onOpenStatus={noop}
        delegationSourceLabel="أحمد محمد خالد"
      />,
    );

    expect(
      screen.getByText(
        'تريث تبعًا للملف المنيب (أحمد محمد خالد) بموجب كتاب التريث رقم 77 بتاريخ 1/8/2026',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تغيير الحالة' })).not.toBeInTheDocument();
  });

  it('يعرض ملخص «محال الى البداية» بكتب المطالعة والإحالة', () => {
    render(
      <StatusCard
        doc={doc({
          execStatus: 'محال الى البداية',
          noFundsDemandNumber: '5',
          noFundsDemandDate: '2026-01-08',
          startReferralNumber: '6',
          startReferralDate: '2026-02-01',
        })}
        canChangeStatus
        onOpenStatus={noop}
      />,
    );

    expect(
      screen.getByText(/محال إلى قسم البداية لعدم وجود أموال للتنفيذ عليها بموجب كتاب المطالعة رقم 5/),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تغيير الحالة' })).toBeInTheDocument();
  });
});
