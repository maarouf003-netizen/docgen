import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import ReviewStatusBadge from './ReviewStatusBadge';

/**
 * `P6-03b`: الحركة الجوهرية (نبض «بانتظار رد») لا تعمل إلا مع `prefers-reduced-motion: no-preference`.
 */
describe('ReviewStatusBadge — احترام تقليل الحركة', () => {
  it('يستخدم motion-safe للنبض في حالة الانتظار', () => {
    const { container } = render(<ReviewStatusBadge isAnswered={false} />);
    const dot = container.querySelector('.motion-safe\\:animate-pulse');
    expect(dot).not.toBeNull();
    expect(dot?.classList.contains('animate-pulse')).toBe(false);
  });

  it('لا يضع أي حركة في حالة «تم الرد»', () => {
    const { container } = render(<ReviewStatusBadge isAnswered />);
    expect(container.querySelector('[class*="animate-"]')).toBeNull();
  });
});
