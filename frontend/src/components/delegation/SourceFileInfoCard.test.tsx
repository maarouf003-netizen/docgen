import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SourceFileInfoCard } from './SourceFileInfoCard';
import { formatDate } from '../../utils/dates';
import type { DelegationDto } from '../../types';

const delegation: DelegationDto = {
  id: 3,
  sourceDocumentId: 10,
  sourceDocumentLabel: 'أحمد محمد خالد',
  sourceFileNumber: '1500',
  sourceFileYear: '2026',
  targetDocumentId: 5,
  delegatedCourt: 'محكمة التنفيذ الأولى',
  sourceCourt: 'دائرة تنفيذ دمشق',
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
  it('يعرض «معلومات الملف المنيب» بحقولها الثمانية (D2/L7) بلا «مسجلة أصولًا» ولا المحامي ولا الشريط', () => {
    render(<SourceFileInfoCard delegation={delegation} />);

    expect(screen.getByText('معلومات الملف المنيب')).toBeInTheDocument();
    expect(screen.getByText('الملف المنيب')).toBeInTheDocument();
    expect(screen.getByText('أحمد محمد خالد')).toBeInTheDocument();
    expect(screen.getByText('رقم أساس الملف المنيب')).toBeInTheDocument();
    expect(screen.getByText('1500/2026')).toBeInTheDocument();
    expect(screen.getByText('الدائرة المنيبة')).toBeInTheDocument();
    expect(screen.getByText('دائرة تنفيذ دمشق')).toBeInTheDocument();
    // الدائرة المنابة استُبدلت بالمنيبة: لا تظهر قيمتها في البطاقة.
    expect(screen.queryByText('الدائرة المنابة')).not.toBeInTheDocument();
    expect(screen.queryByText('محكمة التنفيذ الأولى')).not.toBeInTheDocument();
    expect(screen.getByText('داخلية أم خارجية')).toBeInTheDocument();
    expect(screen.getByText('إنابة خارجية — الفرع المناب: فرع حمص')).toBeInTheDocument();
    expect(screen.getByText('تاريخ الإنابة')).toBeInTheDocument();
    expect(screen.getByText(formatDate('2026-08-01'))).toBeInTheDocument();
    expect(screen.getByText('نص قرار الإنابة')).toBeInTheDocument();
    expect(screen.getByText('لبيع الأموال المرهونة بالمزاد العلني')).toBeInTheDocument();
    expect(screen.getByText('الأموال موضوع الإنابة')).toBeInTheDocument();
    expect(screen.getByText('مركبة سيارة — لوحة 123')).toBeInTheDocument();

    // D2: «مسجلة أصولًا» وحالة الإنابة في البطاقة الجديدة لا في بطاقة المنيب.
    expect(screen.queryByText('مسجلة أصولًا')).not.toBeInTheDocument();
    expect(screen.queryByText('المحامي هشام')).not.toBeInTheDocument();
    expect(screen.queryByText('حالة الإنابة')).not.toBeInTheDocument();
  });

  it('يعرض نوع الملف المنيب عند وجوده فقط', () => {
    render(<SourceFileInfoCard delegation={{ ...delegation, sourceFileType: 'حقوق' }} />);
    expect(screen.getByText('نوع الملف المنيب')).toBeInTheDocument();
    expect(screen.getByText('حقوق')).toBeInTheDocument();
  });

  it('يستبدل اسم المنيب الغائب برقم ملفه', () => {
    render(
      <SourceFileInfoCard
        delegation={{ ...delegation, sourceDocumentLabel: undefined }}
      />,
    );

    expect(screen.getByText('ملف رقم 10')).toBeInTheDocument();
  });

  it('لا يعرض الدائرة المنيبة عندما تكون غائبة', () => {
    render(
      <SourceFileInfoCard delegation={{ ...delegation, sourceCourt: null }} />,
    );

    expect(screen.queryByText('الدائرة المنيبة')).not.toBeInTheDocument();
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

  it('يعرض زر «تسجيل أصولًا» فقط عند التفويض وعلى نقرته يُطلَب — والإتمام انتقل لبطاقة الحالة (و4)', async () => {
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
});