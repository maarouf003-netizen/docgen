import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import NetworkStatusBanner from './NetworkStatusBanner';

describe('NetworkStatusBanner', () => {
  it('لا يعرض شيئاً عندما يكون الاتصال متاحاً', () => {
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: true });
    render(<NetworkStatusBanner />);

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('يعرض بانر تحذيري عند انقطاع الاتصال', () => {
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: false });
    render(<NetworkStatusBanner />);

    expect(screen.getByRole('alert')).toHaveTextContent('لا يوجد اتصال بالإنترنت');
  });
});
