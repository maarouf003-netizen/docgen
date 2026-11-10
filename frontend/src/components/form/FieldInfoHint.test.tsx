import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FieldInfoHint } from './FieldInfoHint';

const originalMatchMedia = window.matchMedia;

function mockMatchMedia(mobile: boolean) {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches: mobile && query === '(max-width: 1023px)',
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })) as unknown as typeof window.matchMedia;
}

describe('FieldInfoHint', () => {
  beforeEach(() => {
    mockMatchMedia(false);
  });

  afterEach(() => {
    window.matchMedia = originalMatchMedia;
  });

  it('يعرض Popover على المكتب ويُغلق بزر الإغلاق', async () => {
    const user = userEvent.setup();
    render(
      <div className="relative">
        <FieldInfoHint id="test" label="معلومات عن الحقل" message="نص التلميح" />
      </div>,
    );

    const trigger = screen.getByRole('button', { name: 'معلومات عن الحقل' });
    await user.click(trigger);
    expect(screen.getByText('نص التلميح')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'إغلاق' }));
    expect(screen.queryByText('نص التلميح')).not.toBeInTheDocument();
  });

  it('يعرض Bottom Sheet على الجوال', async () => {
    mockMatchMedia(true);
    const user = userEvent.setup();
    render(
      <div className="relative">
        <FieldInfoHint id="test" label="معلومات عن الحقل" message="نص التلميح" />
      </div>,
    );

    await user.click(screen.getByRole('button', { name: 'معلومات عن الحقل' }));
    expect(screen.getByRole('dialog', { name: 'معلومات عن الحقل' })).toBeInTheDocument();
    expect(screen.getByText('نص التلميح')).toBeInTheDocument();
  });
});
