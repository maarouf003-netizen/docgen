import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import EntityChangeLog from './EntityChangeLog';

vi.mock('../api/client', () => ({ api: { get: vi.fn().mockResolvedValue({ data: { items: [], page: 1, perPage: 20, totalCount: 0 } }) } }));
vi.mock('../hooks/useCancellableRequest', () => ({ useCancellableRequest: () => ({ data: { items: [], page: 1, perPage: 20, totalCount: 0 }, error: '' }) }));
vi.mock('../utils/dates', () => ({ formatDateTime: (v: string) => v }));

describe('EntityChangeLog', () => {
  it('renders title and export button', () => {
    render(<MemoryRouter><EntityChangeLog /></MemoryRouter>);
    expect(screen.getByText('سجل تغييرات الجهات')).toBeInTheDocument();
    expect(screen.getByLabelText('تصدير سجل التغييرات إلى Excel')).toBeInTheDocument();
  });
});
