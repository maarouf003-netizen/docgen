import { vi } from 'vitest';

/**
 * يُمحاكي window.matchMedia للتحكم في مسار الجوال/المكتبي داخل الاختبارات.
 * القيمة الافتراضية false = مسار المكتبي (jsdom لا يوفر matchMedia أصلًا).
 */
export function stubMobile(matches = false) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}
