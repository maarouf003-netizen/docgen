import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import LegacyRouteBanner from './LegacyRouteBanner';

function renderBanner(to: string, message: string, delayMs = 3500) {
  return render(
    <MemoryRouter initialEntries={['/old']}>
      <Routes>
        <Route path="/old" element={<LegacyRouteBanner to={to} message={message} delayMs={delayMs} />} />
        <Route path={to} element={<div data-testid="new-page" />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('LegacyRouteBanner', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('يعرض النبيه مع رابط مباشر للوجهة الجديدة', () => {
    renderBanner('/new', 'هذا الرابط القديم لم يعد معتمدًا');

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('هذا الرابط القديم لم يعد معتمدًا');
    expect(alert.querySelector('a')).toHaveAttribute('href', '/new');
  });

  it('يعيد التوجيه تلقائيًا إلى الوجهة الجديدة عند انتهاء المهلة', () => {
    vi.useFakeTimers();
    renderBanner('/new', 'نبيه', 1000);

    expect(screen.queryByTestId('new-page')).not.toBeInTheDocument();
    act(() => {
      vi.advanceTimersByTime(1001);
    });
    expect(screen.getByTestId('new-page')).toBeInTheDocument();
  });

  it('ينتقل فورًا عند النقر على رابط الوجهة الجديدة', async () => {
    const user = userEvent.setup();
    renderBanner('/new', 'نبيه', 10_000);

    await user.click(screen.getByRole('link'));
    expect(await screen.findByTestId('new-page')).toBeInTheDocument();
  });
});
