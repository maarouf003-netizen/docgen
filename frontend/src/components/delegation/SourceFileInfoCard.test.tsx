import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SourceFileInfoCard } from './SourceFileInfoCard';
import type { DelegationDto } from '../../types';

const delegation: DelegationDto = {
  id: 3,
  sourceDocumentId: 10,
  sourceDocumentLabel: 'أحمد محمد خالد',
  sourceFileNumber: '1500',
  sourceFileYear: '2026',
  targetDocumentId: 5,
  delegatedCourt: 'محكمة التنفيذ الأولى',
  isExternal: true,
  externalBranchId: 2,
  externalBranchName: 'فرع حمص',
  delegationDate: '2026-08-01',
  delegationText: 'لبيع الأموال المرهونة بالمزاد العلني',
  depositBookNumber: 'K-1',
  depositBookDate: '2026-08-02',
  assignedLawyerId: 4,
  assignedLawyerName: 'المحامي هشام',
  returnDate: '',
  status: 'مسجلة أصولًا',
  createdAt: '2026-08-01',
  createdByName: 'سامر',
  createdById: 7,
  assets: [{ id: 100, assetKind: 'مركبة', assetLabel: 'مركبة سيارة — لوحة 123', snapshotAdjusted: false }],
};

describe('SourceFileInfoCard', () => {
  it('يعرض «معلومات الملف المنيب» مع اسمه وتفاصيل الإنابة', () => {
    render(<SourceFileInfoCard delegation={delegation} />);

    expect(screen.getByText('معلومات الملف المنيب')).toBeInTheDocument();
    expect(screen.getByText('الملف المنيب')).toBeInTheDocument();
    expect(screen.getByText('أحمد محمد خالد')).toBeInTheDocument();
    expect(screen.getByText('رقم أساس الملف المنيب')).toBeInTheDocument();
    expect(screen.getByText('1500/2026')).toBeInTheDocument();
    expect(screen.getByText('محكمة التنفيذ الأولى')).toBeInTheDocument();
    expect(screen.getByText('مسجلة أصولًا')).toBeInTheDocument();
    expect(screen.getByText('إنابة خارجية — الفرع المناب: فرع حمص')).toBeInTheDocument();
    expect(screen.getByText('لبيع الأموال المرهونة بالمزاد العلني')).toBeInTheDocument();
    expect(screen.getByText('المحامي هشام')).toBeInTheDocument();
    expect(screen.getByText('مركبة سيارة — لوحة 123')).toBeInTheDocument();
  });

  it('يستبدل اسم المنيب الغائب برقم ملفه', () => {
    render(
      <SourceFileInfoCard
        delegation={{ ...delegation, sourceDocumentLabel: undefined }}
      />,
    );

    expect(screen.getByText('ملف رقم 10')).toBeInTheDocument();
  });

  it('لا يعرض رقم أساس المنيب عندما يكون غائبًا', () => {
    render(
      <SourceFileInfoCard
        delegation={{ ...delegation, sourceFileNumber: null, sourceFileYear: null }}
      />,
    );

    expect(screen.queryByText('رقم أساس الملف المنيب')).not.toBeInTheDocument();
  });

  it('لا يعرض أزرار المتابعة دون صلاحياتها', () => {
    render(<SourceFileInfoCard delegation={delegation} />);

    expect(screen.queryByRole('button', { name: 'تسجيل أصولًا' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إتمام الإنابة' })).not.toBeInTheDocument();
  });

  it('يعرض زر «تسجيل أصولًا» فقط عند التفويض وعلى نقرته يُطلَب', async () => {
    const user = userEvent.setup();
    const onRegister = vi.fn();
    render(
      <SourceFileInfoCard
        delegation={delegation}
        canRegister
        onRegister={onRegister}
      />,
    );

    expect(screen.getByRole('button', { name: 'تسجيل أصولًا' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إتمام الإنابة' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تسجيل أصولًا' }));
    expect(onRegister).toHaveBeenCalled();
  });

  it('يعرض زر «إتمام الإنابة» فقط عند التفويض وعلى نقرته يُطلَب', async () => {
    const user = userEvent.setup();
    const onComplete = vi.fn();
    render(
      <SourceFileInfoCard
        delegation={delegation}
        canComplete
        onComplete={onComplete}
      />,
    );

    expect(screen.getByRole('button', { name: 'إتمام الإنابة' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تسجيل أصولًا' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'إتمام الإنابة' }));
    expect(onComplete).toHaveBeenCalled();
  });
});
