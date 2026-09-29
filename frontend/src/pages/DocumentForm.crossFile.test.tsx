// اختبار تسرب المال بين ملفين في نموذج التحرير:
// السيناريو: فتح ملف (أ) ثم الانتقال إلى ملف (ب) قبل وصول استجابة (أ)؛
// الاستجابة المتأخرة لـ (أ) يجب ألا تكتب مالها فوق نموذج (ب)، والحفظ
// يجب أن يرسل مال (ب) إلى معرف (ب) — لا مال (أ).
// vi.hoisted/vi.mock تُكرَّر عمدًا (عزل vitest بين الملفات)، وكتلة الاستيراد كاملة إلزامية.
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import DocumentForm from './DocumentForm';
import { api } from '../api/client';
import type { DocumentResponse } from '../types';
import { mockDoc } from './test/documentFormFixtures';

const { navigateMock, paramsMock, useAuthMock } = vi.hoisted(() => ({
  navigateMock: vi.fn(),
  paramsMock: { id: undefined as string | undefined },
  useAuthMock: vi.fn(),
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => navigateMock,
  useParams: () => paramsMock,
  Link: ({ children, to }: { children: ReactNode; to: string }) => <a href={to}>{children}</a>,
}));

vi.mock('../auth/useAuth', () => ({ useAuth: () => useAuthMock() }));

vi.mock('../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/client')>();
  return { ...original, api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } };
});

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (cause: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

const docA: DocumentResponse = {
  ...mockDoc,
  id: 1,
  borrowerName: 'أحمد-أ',
  amountNumeric: 5000,
  amount2Numeric: 0,
  amount3Numeric: 0,
};

const docB: DocumentResponse = {
  ...mockDoc,
  id: 2,
  borrowerName: 'باسم-ب',
  amountNumeric: 7000,
  amount2Numeric: 0,
  amount3Numeric: 0,
};

describe('DocumentForm · عدم تسرب المال بين ملفين', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    paramsMock.id = undefined;
    useAuthMock.mockReturnValue({ user: { role: 'lawyer' } });
  });

  it('الاستجابة المتأخرة للملف السابق لا تكتب ماله فوق الملف الحالي ولا يُحفظ به', async () => {
    const user = userEvent.setup();
    const fetchA = deferred<{ data: DocumentResponse }>();
    const fetchB = deferred<{ data: DocumentResponse }>();
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockImplementation((url: string) => {
      if (url === '/documents/1') return fetchA.promise;
      if (url === '/documents/2') return fetchB.promise;
      return Promise.resolve({ data: [] });
    });

    // فتح ملف (أ) ثم الانتقال إلى (ب) قبل وصول أي استجابة.
    paramsMock.id = '1';
    const { rerender } = render(<DocumentForm />);
    paramsMock.id = '2';
    rerender(<DocumentForm />);

    // تصل استجابة (ب) أولًا: النموذج يعرض مال (ب).
    await act(async () => {
      fetchB.resolve({ data: docB });
    });
    await waitFor(() => expect(screen.getByLabelText('المبلغ المطالب به')).toHaveValue(7000));

    // تصل استجابة (أ) متأخرة: يجب تجاهلها — لا تكتب مال (أ) فوق (ب).
    await act(async () => {
      fetchA.resolve({ data: docA });
    });
    expect(screen.getByLabelText('المبلغ المطالب به')).toHaveValue(7000);

    // الحفظ يرسل مال (ب) إلى معرف (ب) — لا مال (أ).
    await user.click(screen.getByRole('button', { name: 'حفظ التعديلات' }));
    await waitFor(() => expect(api.put).toHaveBeenCalledTimes(1));
    const [url, payload] = vi.mocked(api.put).mock.calls[0] as [string, Record<string, unknown>];
    expect(url).toBe('/documents/2');
    expect(payload.amountNumeric).toBe(7000);
  });

  it('أثناء تحميل ملف جديد لا يُعرض النموذج ولا مال الملف السابق ولا يُتاح الحفظ', async () => {
    const fetchA = deferred<{ data: DocumentResponse }>();
    const fetchB = deferred<{ data: DocumentResponse }>();
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockImplementation((url: string) => {
      if (url === '/documents/1') return fetchA.promise;
      if (url === '/documents/2') return fetchB.promise;
      return Promise.resolve({ data: [] });
    });

    paramsMock.id = '1';
    const { rerender } = render(<DocumentForm />);
    await act(async () => {
      fetchA.resolve({ data: docA });
    });
    await waitFor(() => expect(screen.getByLabelText('المبلغ المطالب به')).toHaveValue(5000));

    // الانتقال إلى (ب) وما تزال استجابته معلقة: بوابة تحميل بدل النموذج —
    // فلا يُعرض مال (أ) ولا يوجد زر حفظ أصلًا ليُحفظ به.
    paramsMock.id = '2';
    rerender(<DocumentForm />);
    expect(screen.getByText('جارِ تحميل بيانات الملف...')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حفظ التعديلات' })).not.toBeInTheDocument();
    expect(screen.queryByDisplayValue('5000')).not.toBeInTheDocument();

    await act(async () => {
      fetchB.resolve({ data: docB });
    });
    await waitFor(() => expect(screen.getByLabelText('المبلغ المطالب به')).toHaveValue(7000));
    expect(screen.getByRole('button', { name: 'حفظ التعديلات' })).not.toBeDisabled();
  });
});
