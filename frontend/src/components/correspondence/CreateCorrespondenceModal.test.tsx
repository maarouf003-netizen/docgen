import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { api } from '../../api/client';
import CreateCorrespondenceModal from './CreateCorrespondenceModal';

vi.mock('../../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api/client')>();
  return {
    ...original,
    api: {
      get: vi.fn(),
      post: vi.fn(),
    },
  };
});

vi.mock('../RichTextEditor', () => ({
  default: ({ value, onChange }: { value: string; onChange: (v: string) => void }) => (
    <textarea aria-label="نص المراسلة" value={value} onChange={(e) => onChange(e.target.value)} />
  ),
}));

const getMock = () => api.get as unknown as ReturnType<typeof vi.fn>;

function renderModal(props: {
  documentId?: number | null;
  documentTitle?: string;
  portal?: boolean;
} = {}) {
  return render(<CreateCorrespondenceModal onClose={() => undefined} {...props} />);
}

describe('CreateCorrespondenceModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('عند الربط بملف يمرّر documentId في بحث المرشحين', async () => {
    const { userEvent } = await import('@testing-library/user-event');
    const user = userEvent.setup();
    getMock().mockResolvedValue({ data: [] });
    renderModal({ documentId: 7, portal: true });

    await user.type(screen.getByLabelText('الطرف المستلم (بالاسم)'), 'مندوب');

    await waitFor(() => {
      expect(getMock()).toHaveBeenCalledWith('/portal/correspondence/targets', {
        params: { q: 'مندوب', documentId: 7 },
      });
    });
  });

  it('عند المراسلة العامة لا يمرّر documentId ويعرض نص الفراغ العام', async () => {
    const { userEvent } = await import('@testing-library/user-event');
    const user = userEvent.setup();
    getMock().mockResolvedValue({ data: [] });
    renderModal();

    await user.type(screen.getByLabelText('الطرف المستلم (بالاسم)'), 'محام');

    expect(await screen.findByText('لا توجد نتائج مطابقة للبحث')).toBeInTheDocument();
    expect(getMock()).toHaveBeenCalledWith('/correspondence/targets', {
      params: { q: 'محام' },
    });
  });

  it('عند الربط بملف دون مستلم مؤهل يعرض رسالة الأهلية', async () => {
    const { userEvent } = await import('@testing-library/user-event');
    const user = userEvent.setup();
    getMock().mockResolvedValue({ data: [] });
    renderModal({ documentId: 7 });

    await user.type(screen.getByLabelText('الطرف المستلم (بالاسم)'), 'مندوب');

    expect(
      await screen.findByText(/لا يوجد مستلم مؤهل لهذه المراسلة/),
    ).toBeInTheDocument();
  });

  it('يعرض أسماء المرشحين ودورهم عند وجود نتائج', async () => {
    const { userEvent } = await import('@testing-library/user-event');
    const user = userEvent.setup();
    getMock().mockResolvedValue({
      data: [
        { userId: 3, fullName: 'المحامي الأول', role: 'lawyer', branchName: 'دمشق' },
        { userId: 8, fullName: 'مندوب الجهة', role: 'entitymanager', governorate: 'دمشق' },
      ],
    });
    renderModal();

    await user.type(screen.getByLabelText('الطرف المستلم (بالاسم)'), 'المحامي');

    expect(await screen.findByText('المحامي الأول')).toBeInTheDocument();
    expect(screen.getByText(/محامٍ/)).toBeInTheDocument();
    expect(screen.getByText(/مندوب الجهة/)).toBeInTheDocument();
  });

  it('يتجاهل استجابة البحث المتأخرة من مصطلح سابق (حارس الترتيب)', async () => {
    const { userEvent } = await import('@testing-library/user-event');
    const user = userEvent.setup();
    const deferred = () => {
      let resolve!: (value: { data: unknown }) => void;
      const promise = new Promise<{ data: unknown }>((r) => { resolve = r; });
      return { promise, resolve };
    };
    const stale = deferred();
    const latest = deferred();
    getMock()
      .mockImplementationOnce(() => stale.promise)
      .mockImplementationOnce(() => latest.promise);
    renderModal();

    const input = screen.getByLabelText('الطرف المستلم (بالاسم)');
    await user.type(input, 'من');
    await waitFor(() => {
      expect(getMock()).toHaveBeenCalledTimes(1);
    });

    await user.type(input, 'دوب');
    await waitFor(() => {
      expect(getMock()).toHaveBeenCalledTimes(2);
    });

    latest.resolve({
      data: [{ userId: 3, fullName: 'المحامي الأول', role: 'lawyer', branchName: 'دمشق' }],
    });
    expect(await screen.findByText('المحامي الأول')).toBeInTheDocument();

    stale.resolve({
      data: [{ userId: 9, fullName: 'مندوب حلب', role: 'entitymanager', governorate: 'حلب' }],
    });

    expect(screen.queryByText('مندوب حلب')).not.toBeInTheDocument();
    expect(screen.getByText('المحامي الأول')).toBeInTheDocument();
  });
});