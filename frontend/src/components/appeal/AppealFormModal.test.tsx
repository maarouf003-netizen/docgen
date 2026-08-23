import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AppealFormModal from './AppealFormModal';
import { makeDocument } from '../../test/factories';
import type { DocumentResponse } from '../../types';

const { apiMock, errorMessageMock } = vi.hoisted(() => ({
  apiMock: { post: vi.fn(), get: vi.fn() },
  errorMessageMock: vi.fn(() => 'خطأ'),
}));

vi.mock('../../api/client', () => ({
  api: apiMock,
  getApiErrorMessage: errorMessageMock,
}));

function applicantDoc(): DocumentResponse {
  return {
    ...makeDocument({ id: 9 }),
    generalEntitySide: 'applicant',
    applicantPublicEntities: [
      { id: 11, name: 'المؤسسة العامة للكهرباء' },
      { id: 12, name: 'مديرية الموارد المائية' },
    ],
  } as unknown as DocumentResponse;
}

describe('AppealFormModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('يعرض حقول مسار «مستأنِفين» وخيارات الجهات العامة', () => {
    render(
      <AppealFormModal
        doc={applicantDoc()}
        variant="appellants"
        onClose={vi.fn()}
        onSaved={vi.fn()}
      />,
    );

    expect(screen.getByRole('dialog', { name: /مستأنِفين/ })).toBeInTheDocument();
    expect(screen.getByText('المؤسسة العامة للكهرباء')).toBeInTheDocument();
    expect(screen.getByLabelText('القرار المطلوب استئنافه')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ قرار رئيس التنفيذ المطلوب استئنافه')).toBeInTheDocument();
    // نوع الاستئناف لا يظهر في تسطير «مستأنِفين» — يملؤه محامي القيد لاحقًا.
    expect(screen.queryByLabelText(/نوع الاستئناف/)).not.toBeInTheDocument();
    // معاينة «المستأنف عليه» من سجل أطراف الملف الكامل (المقترض + بقية الجهة).
    const preview = screen.getByText(/أحمد خالد الخطيب/).closest('p');
    expect(preview?.textContent).toContain('مديرية الموارد المائية');
    // حقول المسار الآخر مخفية.
    expect(screen.queryByLabelText('رقم ورود سند تبليغ الاستئناف')).not.toBeInTheDocument();
  });

  it('يرفض الحفظ دون اختيار مستأنف أو نص قرار', async () => {
    const onSaved = vi.fn();
    const user = userEvent.setup();
    render(
      <AppealFormModal
        doc={applicantDoc()}
        variant="appellants"
        onClose={vi.fn()}
        onSaved={onSaved}
      />,
    );

    await user.type(screen.getByLabelText('القرار المطلوب استئنافه'), 'نص القرار');
    await user.click(screen.getByRole('button', { name: 'حفظ' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('يجب اختيار المستأنف');
    expect(apiMock.post).not.toHaveBeenCalled();
    expect(onSaved).not.toHaveBeenCalled();
  });

  it('يحفظ الاستئناف بأرقام عربية مُطبَّعة ويستدعي onSaved', async () => {
    const onSaved = vi.fn();
    const savedAppeal = { id: 77, status: 'pending' };
    apiMock.post.mockResolvedValueOnce({ data: savedAppeal });
    const user = userEvent.setup();

    render(
      <AppealFormModal
        doc={applicantDoc()}
        variant="appellants"
        onClose={vi.fn()}
        onSaved={onSaved}
      />,
    );

    await user.click(screen.getByRole('checkbox', { name: /المؤسسة العامة للكهرباء/ }));
    await user.type(screen.getByLabelText('القرار المطلوب استئنافه'), 'نص القرار');
    await user.type(screen.getByLabelText('تاريخ قرار رئيس التنفيذ المطلوب استئنافه'), '١/٨/٢٠٢٦');
    await user.click(screen.getByRole('button', { name: 'حفظ' }));

    await vi.waitFor(() => {
      expect(onSaved).toHaveBeenCalledWith(savedAppeal);
    });
    expect(apiMock.post).toHaveBeenCalledTimes(1);
    const [url, payload] = apiMock.post.mock.calls[0];
    expect(url).toBe('/documents/9/appeals');
    expect(payload.direction).toBe('appellants');
    expect(payload.appellants).toEqual([{ kind: 'applicant-entity', partyId: 11 }]);
    expect(payload.appealedDecisionDate).toBe('1/8/2026');
  });

  it('مسار «مستأنف علينا» يعرض حقوله الخاصة (سند التبليغ والمحكمة ورأي المحامي)', () => {
    const doc = {
      ...makeDocument(),
      generalEntitySide: 'executed',
      executedNaturalPersons: [{ id: 21, name: 'سامر', father: 'نبيل', family: 'الحلبي' }],
    } as unknown as DocumentResponse;

    render(<AppealFormModal doc={doc} variant="against-us" onClose={vi.fn()} onSaved={vi.fn()} />);

    expect(screen.getByRole('dialog', { name: /مستأنف علينا/ })).toBeInTheDocument();
    expect(screen.getByLabelText('رقم ورود سند تبليغ الاستئناف')).toBeInTheDocument();
    expect(screen.getByLabelText('محكمة الاستئناف التنفيذية المختصة')).toBeInTheDocument();
    expect(screen.getByLabelText('لعام')).toBeInTheDocument();
    expect(
      screen.getByLabelText('رأي المحامي المتابع للملف بأسباب الاستئناف'),
    ).toBeInTheDocument();
    expect(screen.queryByLabelText('رقم كتاب المطالعة وإيداع الملف رئيس القسم')).not.toBeInTheDocument();
  });
});
