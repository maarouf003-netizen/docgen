import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, Link } from 'react-router-dom';
import PortalFileCorrespondence from './PortalFileCorrespondence';
import { makeDocument } from '../test/factories';
import type { CorrespondenceListItemDto, DocumentResponse } from '../types';

const { apiMock } = vi.hoisted(() => ({
  apiMock: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
}));

vi.mock('../api/client', () => ({ api: apiMock }));

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
    if (url === '/portal/files/1') return Promise.resolve({ data: doc });
    if (url === '/portal/files/1/correspondence') return Promise.resolve({ data: items });
    return Promise.reject(new Error(`unexpected GET ${url}`));
  });
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/portal/files/1/correspondence']}>
      <Routes>
        <Route path="/portal/files/:id/correspondence" element={<PortalFileCorrespondence />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('PortalFileCorrespondence', () => {
  it('تعرض قائمة مراسلات الملف (بوابة) مع رابط العودة وروابط التفاصيل', async () => {
    setEndpoints(makeDocument());
    renderPage();

    expect(await screen.findByText('DAM-2026-1234')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'مراسلات الملف' })).toBeInTheDocument();
    // سياق الملف تحت العنوان: عنوان التسطير + الرقم والسنة المدمجان.
    expect(screen.getByText('أحمد خالد الخطيب — 99 / 2026')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'عودة إلى الملف' })).toHaveAttribute(
      'href',
      '/portal/files/1',
    );
    // المندوب يسطّر دائمًا (canCreate=true كما في البطاقة الأصلية).
    expect(screen.getByRole('button', { name: 'تسطير مراسلة' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /DAM-2026-1234/ })).toHaveAttribute(
      'href',
      '/portal/correspondence/1',
    );
  });

  it('تعرض تنبيهًا مع عودة عند غياب صلاحية المراسلات (403) بدل الفراغ', async () => {
    getMock().mockImplementation((url: string) => {
      if (url === '/portal/files/1') return Promise.resolve({ data: makeDocument() });
      if (url === '/portal/files/1/correspondence') {
        return Promise.reject({ response: { status: 403 } });
      }
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });
    renderPage();

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('لا صلاحية لعرض مراسلات هذا الملف.')).toBeInTheDocument();
    expect(within(alert).getByRole('link', { name: 'عودة إلى الملف' })).toHaveAttribute(
      'href',
      '/portal/files/1',
    );
    // رابط عودة وحيد — بلا تكرار مع رابط الترويسة.
    expect(screen.getAllByRole('link', { name: 'عودة إلى الملف' })).toHaveLength(1);
    expect(screen.queryByText('DAM-2026-1234')).not.toBeInTheDocument();
  });

  it('يصفّر تنبيه الملف السابق عند الانتقال لملف آخر على المسار نفسه', async () => {
    getMock().mockImplementation((url: string) => {
      if (url === '/portal/files/1') return Promise.resolve({ data: makeDocument() });
      if (url === '/portal/files/1/correspondence') {
        return Promise.reject({ response: { status: 403 } });
      }
      if (url === '/portal/files/2') return Promise.resolve({ data: makeDocument({ id: 2 }) });
      if (url === '/portal/files/2/correspondence') return Promise.resolve({ data: [] });
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });
    render(
      <MemoryRouter initialEntries={['/portal/files/1/correspondence']}>
        <Routes>
          <Route path="/portal/files/:id/correspondence" element={<PortalFileCorrespondence />} />
        </Routes>
        <Link to="/portal/files/2/correspondence">ملف 2</Link>
      </MemoryRouter>,
    );

    // تنبيه الملف الأول حاضر…
    expect(await screen.findByText('لا صلاحية لعرض مراسلات هذا الملف.')).toBeInTheDocument();

    // …ويزول بعد الانتقال للملف الثاني (قائمته الفارغة ظاهرة بدل التنبيه).
    await userEvent.setup().click(screen.getByRole('link', { name: 'ملف 2' }));
    await screen.findByText('لا توجد مراسلات على هذا الملف — سطّر أول مراسلة.');
    expect(screen.queryByText('لا صلاحية لعرض مراسلات هذا الملف.')).not.toBeInTheDocument();
  });
});
