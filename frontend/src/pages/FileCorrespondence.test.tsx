import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, Link } from 'react-router-dom';
import FileCorrespondence from './FileCorrespondence';
import { makeDocument } from '../test/factories';
import type { CorrespondenceListItemDto, DocumentResponse } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/client')>();
  return {
    ...original,
    api: {
      get: vi.fn(),
      post: vi.fn(),
      delete: vi.fn(),
    },
  };
});

import { api } from '../api/client';

const getMock = () => api.get as unknown as ReturnType<typeof vi.fn>;

function item(): CorrespondenceListItemDto {
  return {
    id: 1,
    correspondenceNumber: 'DAM-2026-1234',
    correspondenceDate: '2026-08-01T09:00:00Z',
    importance: 'normal',
    documentId: 1,
    fileContext: null,
    creatorName: 'المحامي الأول',
    targetName: 'مندوب الجهة',
    snippet: 'نطلب موافاتنا بالبيانات',
    lastKind: 'letter',
    viewStatus: 'seen',
    canMarkSeen: false,
    canReply: false,
    messagesCount: 2,
    administrativeBranchName: null,
    governorate: 'دمشق',
    updatedAt: '2026-08-01T09:00:00Z',
  };
}

function setEndpoints(doc: DocumentResponse, items: CorrespondenceListItemDto[] = [item()]) {
  getMock().mockImplementation((url: string) => {
    if (url === '/documents/1') return Promise.resolve({ data: doc });
    if (url === '/correspondence/document/1') return Promise.resolve({ data: items });
    return Promise.reject(new Error(`unexpected GET ${url}`));
  });
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/documents/1/correspondence']}>
      <Routes>
        <Route path="/documents/:id/correspondence" element={<FileCorrespondence />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  useAuthMock.mockReturnValue({ user: { role: 'lawyer', id: 7 } });
});

describe('FileCorrespondence', () => {
  it('تعرض قائمة مراسلات الملف مع رابط العودة وزر التسطير لمحامي المالك', async () => {
    setEndpoints(makeDocument({ createdById: 7 }));
    renderPage();

    expect(await screen.findByText('DAM-2026-1234')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'مراسلات الملف' })).toBeInTheDocument();
    // سياق الملف تحت العنوان: عنوان التسطير + الرقم والسنة المدمجان.
    expect(screen.getByText('أحمد خالد الخطيب — 99 / 2026')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'عودة إلى الملف' })).toHaveAttribute(
      'href',
      '/documents/1',
    );
    // عنوان نافذة التسطير من عبارة البطاقة الأصلية (اسم المقترض الثلاثي).
    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'تسطير مراسلة' }));
    const dialog = await screen.findByRole('dialog', { name: 'تسطير مراسلة' });
    expect(within(dialog).getByText('أحمد خالد الخطيب')).toBeInTheDocument();
  });

  it('تخفي زر التسطير عن محامٍ غير مالك', async () => {
    setEndpoints(makeDocument({ createdById: 9 }));
    renderPage();

    expect(await screen.findByText('DAM-2026-1234')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تسطير مراسلة' })).not.toBeInTheDocument();
  });

  it('تعرض زر التسطير لرئيس القسم (canTransfer)', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'head', id: 9 } });
    setEndpoints(makeDocument({ createdById: 7 }));
    renderPage();

    expect(await screen.findByText('DAM-2026-1234')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تسطير مراسلة' })).toBeInTheDocument();
  });

  it('تعرض تنبيهًا مع عودة عند غياب صلاحية المراسلات (403) بدل الفراغ', async () => {
    getMock().mockImplementation((url: string) => {
      if (url === '/documents/1') return Promise.resolve({ data: makeDocument() });
      if (url === '/correspondence/document/1') {
        return Promise.reject({ response: { status: 403 } });
      }
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });
    renderPage();

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('لا صلاحية لعرض مراسلات هذا الملف.')).toBeInTheDocument();
    expect(within(alert).getByRole('link', { name: 'عودة إلى الملف' })).toHaveAttribute(
      'href',
      '/documents/1',
    );
    // رابط عودة وحيد — بلا تكرار مع رابط الترويسة.
    expect(screen.getAllByRole('link', { name: 'عودة إلى الملف' })).toHaveLength(1);
    expect(screen.queryByText('DAM-2026-1234')).not.toBeInTheDocument();
  });

  it('يصفّر تنبيه الملف السابق عند الانتقال لملف آخر على المسار نفسه', async () => {
    getMock().mockImplementation((url: string) => {
      if (url === '/documents/1') return Promise.resolve({ data: makeDocument({ createdById: 9 }) });
      if (url === '/correspondence/document/1') {
        return Promise.reject({ response: { status: 403 } });
      }
      if (url === '/documents/2')
        return Promise.resolve({ data: makeDocument({ id: 2, createdById: 7 }) });
      if (url === '/correspondence/document/2') return Promise.resolve({ data: [] });
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });
    render(
      <MemoryRouter initialEntries={['/documents/1/correspondence']}>
        <Routes>
          <Route path="/documents/:id/correspondence" element={<FileCorrespondence />} />
        </Routes>
        <Link to="/documents/2/correspondence">ملف 2</Link>
      </MemoryRouter>,
    );

    // تنبيه الملف الأول حاضر…
    expect(await screen.findByText('لا صلاحية لعرض مراسلات هذا الملف.')).toBeInTheDocument();

    // …ويزول بعد الانتقال للملف الثاني (قائمته الفارغة ظاهرة بدل التنبيه).
    await userEvent.setup().click(screen.getByRole('link', { name: 'ملف 2' }));
    await screen.findByText('لا توجد مراسلات على هذا الملف — سطّر أول مراسلة.');
    expect(screen.queryByText('لا صلاحية لعرض مراسلات هذا الملف.')).not.toBeInTheDocument();
  });

  it('تعرض شاشة خطأ مع إعادة محاولة عند فشل جلب الملف', async () => {
    getMock().mockRejectedValue(new Error('network'));
    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    getMock().mockImplementation((url: string) => {
      if (url === '/documents/1') return Promise.resolve({ data: makeDocument() });
      if (url === '/correspondence/document/1') return Promise.resolve({ data: [] });
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });
    await user.click(screen.getByRole('button', { name: 'إعادة المحاولة' }));
    await waitFor(() => {
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });
  });
});
