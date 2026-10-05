import { useEffect, useRef } from 'react';
import type { RefObject } from 'react';

const FOCUSABLE_SELECTOR =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * حصر تركيز لوحة المفاتيح داخل حاوية حوار (S6.e): `Tab`/`Shift+Tab`
 * يدوران بين العناصر القابلة للتركيز داخل الحاوية فقط فلا يتسرب
 * التركيز للخلفية أثناء فتح الحوار. يُستثنى المعطل و`tabindex="-1"`
 * والمخفي بخاصية `hidden` (فحص الرؤية الحاسوبية متروك عمدًا —
 * `offsetParent` دائم `null` في `jsdom` فيكسر الاختبارات).
 */
export function useFocusTrap<T extends HTMLElement>(): RefObject<T | null> {
  const ref = useRef<T | null>(null);

  useEffect(() => {
    const root = ref.current;
    if (!root) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key !== 'Tab') return;
      const items = [...root.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)].filter(
        (el) => !el.hasAttribute('hidden'),
      );
      if (items.length === 0) {
        e.preventDefault();
        return;
      }
      const first = items[0];
      const last = items[items.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    };
    root.addEventListener('keydown', onKeyDown);
    return () => root.removeEventListener('keydown', onKeyDown);
  }, []);

  return ref;
}
