import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import SpinnerIcon from './SpinnerIcon';

describe('SpinnerIcon', () => {
  it('renders a decorative spinner that does not disturb layout or screen readers', () => {
    const { container } = render(<SpinnerIcon />);
    const svg = container.querySelector('svg');
    expect(svg).not.toBeNull();
    // زخرفي: مخفي عن قارئ الشاشة، وأبعاد ثابتة لمنع انزياح التخطيط.
    expect(svg?.getAttribute('aria-hidden')).toBe('true');
    const cls = svg?.getAttribute('class') ?? '';
    expect(cls).toMatch(/(^|\s)h-4(\s|$)/);
    expect(cls).toMatch(/(^|\s)w-4(\s|$)/);
    // الحركة مقيدة بتفضيل الحركة فقط.
    expect(cls).toMatch(/motion-safe:animate-spin/);
  });
});
