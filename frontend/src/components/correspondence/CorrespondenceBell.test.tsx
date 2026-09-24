import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import CorrespondenceBell from './CorrespondenceBell';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

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

import { api } from '../../api/client';

const getMock = () => api.get as unknown as ReturnType<typeof vi.fn>;

beforeEach(() => {
  vi.clearAllMocks();
  useAuthMock.mockReturnValue({ user: { role: 'lawyer', id: 3 } });
});

function renderBell(props: { portal?: boolean; count?: number } = {}) {
  return render(
    <MemoryRouter>
      <CorrespondenceBell {...props} />
    </MemoryRouter>,
  );
}

describe('CorrespondenceBell', () => {
  it('بالعدد المُغذَّى من الأب يعرض الشارة دون أي استطلاع ذاتي', () => {
    renderBell({ count: 3 });

    expect(screen.getByRole('link', { name: /مراسلات عاجلة بلا مشاهدة: 3/ })).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(getMock()).not.toHaveBeenCalled();
  });

  it('بالعدد صفر يخفي الشارة دون استطلاع', () => {
    renderBell({ count: 0 });

    expect(
      screen.getByRole('link', { name: 'لا توجد مراسلات عاجلة بلا مشاهدة' }),
    ).toBeInTheDocument();
    expect(screen.queryByText('0')).not.toBeInTheDocument();
    expect(getMock()).not.toHaveBeenCalled();
  });

  it('بلا عدد مُغذَّى يستطلع المسار الرئيسي بنفسه ويعرض الشارة', async () => {
    getMock().mockResolvedValue({ data: { count: 2 } });
    renderBell();

    await waitFor(() => {
      expect(getMock()).toHaveBeenCalledWith('/correspondence/urgent-unseen-count');
    });
    expect(await screen.findByText('2')).toBeInTheDocument();
  });

  it('بلا عدد مُغذَّى يستطلع مسار البوابة للمندوب', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'entitymanager', id: 11 } });
    getMock().mockResolvedValue({ data: { count: 1 } });
    renderBell({ portal: true });

    await waitFor(() => {
      expect(getMock()).toHaveBeenCalledWith('/portal/correspondence/urgent-unseen-count');
    });
    expect(await screen.findByRole('link', { name: /مراسلات عاجلة بلا مشاهدة: 1/ })).toHaveAttribute(
      'href',
      '/portal/correspondence',
    );
  });

  it('لا يظهر للمدير/المشرف', () => {
    useAuthMock.mockReturnValue({ user: { role: 'manager', id: 9 } });
    const { container } = renderBell();

    expect(container).toBeEmptyDOMElement();
    expect(getMock()).not.toHaveBeenCalled();
  });
});
