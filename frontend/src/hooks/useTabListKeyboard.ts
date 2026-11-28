import type { KeyboardEvent } from 'react';

/**
 * ترتيب التبويب التالي وفق WAI-ARIA (Tabs pattern) مع اتجاه الصفحة RTL:
 * السهم الأيسر ينتقل إلى التبويب التالي (التصفح البصري من اليمين إلى اليسار)،
 * والسهم الأيمن إلى السابق، و`Home`/`End` إلى الأول/الأخير.
 * يُعيد `null` لأي مفتاح آخر (بلا `preventDefault` ولا تنقّل).
 */
export function nextTabIndex(key: string, index: number, count: number): number | null {
  if (count <= 0 || index < 0) return null;
  switch (key) {
    case 'ArrowLeft':
      return (index + 1) % count;
    case 'ArrowRight':
      return (index - 1 + count) % count;
    case 'Home':
      return 0;
    case 'End':
      return count - 1;
    default:
      return null;
  }
}

/** `roving tabindex`: التبويب النشط وحده يدخل تسلسل `Tab` (البقية تُبلَغ بالأسهم). */
export function rovingTabIndex(isActive: boolean): 0 | -1 {
  return isActive ? 0 : -1;
}

export interface TabListKeyboardOptions<T extends string> {
  /** ترتيب التبويبات المرئي (مصدر واحد يطابق ترتيب الأزرار في الصفحة). */
  ids: readonly T[];
  activeId: T;
  onSelect: (id: T) => void;
  /** معرّف عنصر التبويب في `DOM` — يُستهدف لنقل التركيز بعد التفعيل. */
  tabId: (id: T) => string;
}

/**
 * تنقّل لوحة مفاتيح مشترك لقوائم التبويبات (تفعيل تلقائي): يفعل التبويب الجديد
 * وينقل التركيز إليه، فلا يضيع التركيز ولا يتكرر منطق RTL/Home/End في كل صفحة.
 * يُسنَد إلى حاوية `role="tablist"` (`onKeyDown`) مع `tabIndex` مشتق من
 * {@link rovingTabIndex} لكل زر.
 */
export function useTabListKeyboard<T extends string>(options: TabListKeyboardOptions<T>) {
  const { ids, activeId, onSelect, tabId } = options;
  return (event: KeyboardEvent<HTMLElement>) => {
    // مفاتيح التعديل تعني اختصارًا آخر (تنقّل المتصفح/النظام) — لا تُختطف.
    if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey) return;
    const index = ids.indexOf(activeId);
    const next = nextTabIndex(event.key, index, ids.length);
    if (next === null) return;
    event.preventDefault();
    const nextId = ids[next];
    onSelect(nextId);
    document.getElementById(tabId(nextId))?.focus();
  };
}
