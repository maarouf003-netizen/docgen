import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import DecideAppealModal from './DecideAppealModal';
import AppealRotationModal from './AppealRotationModal';
import { makeDocument } from '../../test/factories';
import type { AppealDto } from '../../types';

const { apiMock, errorMessageMock } = vi.hoisted(() => ({
  apiMock: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
  errorMessageMock: vi.fn(() => 'خطأ'),
}));

vi.mock('../../api/client', () => ({ api: apiMock, getApiErrorMessage: errorMessageMock }));

function makeAppeal(): AppealDto {
  const doc = makeDocument();
  return {
    id: 5,
    documentId: doc.id,
    documentLabel: 'أحمد خالد الخطيب',
    direction: 'appellants',
    directionLabel: 'مستأنِفين',
    status: 'pending',
    statusLabel: 'منظور',
    appellants: [],
    appellees: [],
    appealYear: '2025',
    appealBaseNumber: '900',
    currentBaseNumber: '900',
    needsRotation: true,
    createdAt: '2026-08-01T00:00:00Z',
    createdById: 1,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('DecideAppealModal', () => {
  it('يرفض الإرسال ببيانات ناقصة ثم يحفظ بتاريخ مُطبَّع الأرقام', async () => {
    const onSaved = vi.fn();
    apiMock.post.mockResolvedValueOnce({ data: { ...makeAppeal(), status: 'decided' } });
    const user = userEvent.setup();
    render(<DecideAppealModal appeal={makeAppeal()} onClose={vi.fn()} onSaved={onSaved} />);

    await user.click(screen.getByRole('button', { name: 'حفظ الحسم' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('رقم قرار الحسم مطلوب');

    await user.type(screen.getByLabelText('رقم قرار الحسم'), 'قرار-77');
    await user.type(screen.getByLabelText('تاريخ قرار الحسم'), '١٥/٩/٢٠٢٦');
    await user.type(screen.getByLabelText('منطوق القرار'), 'قبول جزئي');
    await user.selectOptions(screen.getByLabelText('نتيجة الاستئناف'), 'in-favor');
    await user.click(screen.getByRole('button', { name: 'حفظ الحسم' }));

    await vi.waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(apiMock.post).toHaveBeenCalledWith('/appeals/5/decide', {
      decisionNumber: 'قرار-77',
      decisionDate: '15/9/2026',
      decisionRuling: 'قبول جزئي',
      outcome: 'in-favor',
    });
  });
});

describe('AppealRotationModal', () => {
  it('يعرض أرقام الأساس السابقة ويُدوّر لسنة السنة الحالية', async () => {
    apiMock.get.mockResolvedValueOnce({
      data: [
        { year: new Date().getFullYear() - 1, baseNumber: '900' },
        { year: new Date().getFullYear() - 2, baseNumber: '770' },
      ],
    });
    apiMock.put.mockResolvedValueOnce({ data: null });
    const onSaved = vi.fn();
    const user = userEvent.setup();
    render(<AppealRotationModal appeal={makeAppeal()} onClose={vi.fn()} onSaved={onSaved} />);

    expect(await screen.findByText('900')).toBeInTheDocument();
    expect(screen.getByText('770')).toBeInTheDocument();

    await user.type(
      screen.getByLabelText(`رقم الأساس الاستئنافي لسنة ${new Date().getFullYear()}`),
      '1450',
    );
    await user.click(screen.getByRole('button', { name: 'حفظ التدوير' }));

    await vi.waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(apiMock.put).toHaveBeenCalledWith(`/appeals/5/base-numbers`, {
      entries: [{ baseNumber: '1450' }],
    });
  });

  it('يمنع الحفظ دون إدخال رقم', async () => {
    apiMock.get.mockResolvedValueOnce({ data: [] });
    const user = userEvent.setup();
    render(<AppealRotationModal appeal={makeAppeal()} onClose={vi.fn()} onSaved={vi.fn()} />);

    await screen.findByText('لا توجد أرقام مسجلة بعد.');
    await user.click(screen.getByRole('button', { name: 'حفظ التدوير' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('أدخل رقم الأساس الاستئنافي للسنة الحالية');
    expect(apiMock.put).not.toHaveBeenCalled();
  });
});
