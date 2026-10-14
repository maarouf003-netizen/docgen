import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import DocumentForm from './DocumentForm';
import { ExecutoryDocumentCard } from '../components/view/ExecutoryDocumentCard';
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

describe('DocumentForm · ملاحظات السند (القرارات الأخرى)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    paramsMock.id = undefined;
    useAuthMock.mockReturnValue({ user: { role: 'lawyer' } });
  });

  async function renderEdit(doc: DocumentResponse = mockDoc) {
    paramsMock.id = '1';
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: doc });
    render(<DocumentForm />);
    return screen.findByRole('button', { name: 'حفظ التعديلات' }, { timeout: 5000 });
  }

  it('يعرض حقل «ملاحظات» تحت «المتضمن» كحقل يتوسع تلقائياً في وضع طالبة تنفيذ العادي', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.selectOptions(screen.getByLabelText('نوع السند'), 'عادي');

    const inclusion = screen.getByLabelText('المتضمن');
    const notes = screen.getByLabelText('ملاحظات');
    expect(inclusion.tagName).toBe('TEXTAREA');
    expect(notes.tagName).toBe('TEXTAREA');
    expect(notes).toHaveAttribute('placeholder', 'اكتب القرارات الأخرى هنا...');
    // الترتيب: المتضمن قبل ملاحظات في DOM
    expect(inclusion.compareDocumentPosition(notes) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('يرسل «ملاحظات» مع الحمولة عند الحفظ في وضع طالبة تنفيذ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.selectOptions(screen.getByLabelText('نوع السند'), 'عادي');
    await user.type(screen.getByLabelText('المتضمن'), 'خلاصة القرار الأول');
    await user.type(screen.getByLabelText('ملاحظات'), 'القرار الثاني رقم 5');
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.inclusionText).toBe('خلاصة القرار الأول');
    expect(payload.inclusionNotes).toBe('القرار الثاني رقم 5');
  });

  it('يعرض حقل «ملاحظات» في وضع منفذ عليها تحت «المتضمن» ويرسله', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);
    await user.click(screen.getByLabelText('الجهة العامة منفذ عليها'));

    const notes = screen.getByLabelText('ملاحظات');
    expect(notes.tagName).toBe('TEXTAREA');
    await user.type(screen.getByLabelText('المتضمن'), 'خلاصة القرار');
    await user.type(notes, 'قرارات أخرى: 12');
    await user.type(screen.getByLabelText('رقم الملف'), '55');
    await user.selectOptions(screen.getByLabelText('سنة الملف'), '2026');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));
    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.inclusionNotes).toBe('قرارات أخرى: 12');
  });

  it('يعرض حقل «ملاحظات» في وضع عرض وايداع ويرسله', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);
    await user.click(screen.getByLabelText('عرض وايداع'));

    expect(screen.getByLabelText('ملاحظات')).toBeInTheDocument();
    await user.type(screen.getByLabelText('ملاحظات'), 'قرار إضافي');
    await user.type(screen.getByLabelText('رقم الملف'), '77');
    await user.selectOptions(screen.getByLabelText('سنة الملف'), '2026');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));
    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.inclusionNotes).toBe('قرار إضافي');
  });

  it('يملأ «ملاحظات» من المستند عند التعديل', async () => {
    await renderEdit({ ...mockDoc, contractTypeSelector: 'عادي', inclusionNotes: 'قرارات أخرى محفوظة' });
    expect(screen.getByLabelText('ملاحظات')).toHaveValue('قرارات أخرى محفوظة');
  });

  it('بطاقة السند تعرض «ملاحظات» عند وجودها وتخفيها عند الفراغ', () => {
    const withNotes = { ...mockDoc, contractTypeSelector: 'عادي', inclusionText: 'خلاصة', inclusionNotes: 'قرار آخر' } as DocumentResponse;
    const { unmount } = render(<ExecutoryDocumentCard doc={withNotes} />);
    expect(screen.getByText('ملاحظات')).toBeInTheDocument();
    expect(screen.getByText('قرار آخر')).toBeInTheDocument();
    unmount();

    const withoutNotes = { ...mockDoc, contractTypeSelector: 'عادي', inclusionText: 'خلاصة', inclusionNotes: '' } as DocumentResponse;
    render(<ExecutoryDocumentCard doc={withoutNotes} />);
    expect(screen.queryByText('ملاحظات')).not.toBeInTheDocument();
  });
});
