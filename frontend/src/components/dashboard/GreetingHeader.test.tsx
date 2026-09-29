import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { GreetingHeader } from './GreetingHeader';
import { GREETINGS, GREETING_INDEX_KEY, nextGreetingIndex } from './greetingContent';

beforeEach(() => {
  localStorage.clear();
});

describe('nextGreetingIndex', () => {
  it('يبدأ من الأولى ويتقدّم دائريًا مع كل دخول', () => {
    expect(nextGreetingIndex()).toBe(0);
    expect(nextGreetingIndex()).toBe(1);
    expect(localStorage.getItem(GREETING_INDEX_KEY)).toBe('1');
  });

  it('يلف على القائمة ويتجاهل المخزّن الفاسد', () => {
    localStorage.setItem(GREETING_INDEX_KEY, String(GREETINGS.length - 1));
    expect(nextGreetingIndex()).toBe(0);

    localStorage.setItem(GREETING_INDEX_KEY, 'فاسد');
    expect(nextGreetingIndex()).toBe(0);
  });
});

describe('GreetingHeader', () => {
  it('يعرض صيغة الزيارة مع الاسم الأول فقط', () => {
    render(<GreetingHeader fullName="أحمد محمد الخطيب" />);

    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('مرحبًا، أحمد');
  });

  it('يتقدّم للصيغة التالية في الزيارة اللاحقة ويثبت داخلها', () => {
    const { unmount } = render(<GreetingHeader fullName="أحمد الخطيب" />);
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent(GREETINGS[0]);
    unmount();

    render(<GreetingHeader fullName="أحمد الخطيب" />);
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent(`${GREETINGS[1]}، أحمد`);
  });

  it('يستخدم الاسم الكامل كاحتياط عند غياب الاسم الأول', () => {
    render(<GreetingHeader fullName="  " />);

    expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument();
  });
});
