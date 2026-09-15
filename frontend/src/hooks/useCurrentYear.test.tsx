import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import CurrentYearBanner from '../components/CurrentYearBanner';
import CurrentYearProvider from '../components/CurrentYearProvider';
import { useCurrentYear } from './useCurrentYear';
import { api } from '../api/client';

vi.mock('../api/client', () => ({
  api: { get: vi.fn() },
}));

function Probe() {
  const { currentYear, isLoading } = useCurrentYear();
  return (
    <div>
      <span data-testid="year">{currentYear}</span>
      <span data-testid="loading">{String(isLoading)}</span>
    </div>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('useCurrentYear', () => {
  it('خارج الموفر يعيد سنة المتصفح كاحتياطي بلا أي جلب', () => {
    render(<Probe />);

    expect(screen.getByTestId('year').textContent).toBe(String(new Date().getFullYear()));
    expect(screen.getByTestId('loading').textContent).toBe('false');
    expect(api.get).not.toHaveBeenCalled();
  });

  it('داخل الموفر يتبنى سنة النظام المجلوبة من الخادم', async () => {
    (api.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { currentYear: 2031 } });

    render(
      <CurrentYearProvider>
        <Probe />
      </CurrentYearProvider>,
    );

    expect(await screen.findByTestId('year')).toHaveTextContent('2031');
    expect(screen.getByTestId('loading').textContent).toBe('false');
    expect(api.get).toHaveBeenCalledWith('/meta/current-year');
  });

  it('عند فشل الجلب يبقى على القيمة الاحتياطية', async () => {
    (api.get as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('network'));

    render(
      <CurrentYearProvider>
        <Probe />
      </CurrentYearProvider>,
    );

    expect(await screen.findByTestId('year')).toHaveTextContent(String(new Date().getFullYear()));
    expect(screen.getByTestId('loading').textContent).toBe('false');
  });
});

describe('CurrentYearBanner', () => {
  it('يظهر أثناء جلب سنة النظام ويختفي بعد اكتماله', async () => {
    let resolve!: (value: unknown) => void;
    (api.get as ReturnType<typeof vi.fn>).mockReturnValue(
      new Promise((res) => {
        resolve = res;
      }),
    );

    render(
      <CurrentYearProvider>
        <CurrentYearBanner />
      </CurrentYearProvider>,
    );

    expect(screen.getByRole('status')).toHaveTextContent('جارِ تحديد سنة النظام…');

    resolve({ data: { currentYear: 2031 } });
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
  });
});