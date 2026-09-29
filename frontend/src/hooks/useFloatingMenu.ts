import { useEffect, useRef, useState } from 'react';
import {
  useFloating,
  useClick,
  useDismiss,
  useRole,
  useListNavigation,
  useInteractions,
  autoUpdate,
  flip,
  shift,
  offset,
  type Placement,
} from '@floating-ui/react';

interface UseFloatingMenuOptions {
  placement?: Placement;
  offsetPx?: number;
  /**
   * تفعيل التنقل بلوحة المفاتيح بين بنود القائمة عبر `useListNavigation`
   * (أسهم + تركيز). يُستخدم مع `FloatingFocusManager` عند المستهلك.
   * معطّل افتراضيًا فلا يتأثر المستهلكون الحاليون.
   */
  listNavigation?: boolean;
}

/**
 * تجريد موحّد للقوائم المنسدلة في التطبيق: يوفّر تموضعًا ثابتًا (fixed) عبر
 * `@floating-ui/react` مع قلب تلقائي عند ضيق المساحة (`flip`) ومنع تجاوز حافة
 * الشاشة (`shift`)، وتتبّع التمرير/تغيّر الحجم (`autoUpdate`)، وسلوك إغلاق
 * موحّد (`useClick` + `useDismiss`) ووصولية (`useRole('menu')`).
 * بخيار `listNavigation` تُفعَّل ملاحة البنود بالأسهم ويُكشَف `context`
 * و`getItemProps` و`listRef` (يغلّف المستهلك اللوحة بـ `FloatingFocusManager`).
 *
 * التطبيق RTL (`<html dir="rtl">`) لذا المحاذاة الافتراضية `bottom-start`
 * (الأيّم في السياق العربي) تطابق السلوك السابق `right-0`.
 */
export function useFloatingMenu({
  placement = 'bottom-start',
  offsetPx = 4,
  listNavigation = false,
}: UseFloatingMenuOptions = {}) {
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState<number | null>(null);
  const listRef = useRef<Array<HTMLElement | null>>([]);

  const floating = useFloating({
    open,
    onOpenChange: setOpen,
    placement,
    strategy: 'fixed',
    middleware: [offset(offsetPx), flip({ padding: 8 }), shift({ padding: 8 })],
    whileElementsMounted: autoUpdate,
  });

  // أي إغلاق (اختيار/Escape/خارج/برمجي) يصفّر ملاحة البنود ويحرر مراجعها —
  // يغطي أيضًا `setOpen(false)` المباشر الذي يتجاوز `onOpenChange`.
  useEffect(() => {
    if (!open) {
      setActiveIndex(null);
      listRef.current = [];
    }
  }, [open ]);

  const click = useClick(floating.context);
  const dismiss = useDismiss(floating.context);
  const role = useRole(floating.context, { role: 'menu' });
  const listNav = useListNavigation(floating.context, {
    listRef,
    activeIndex,
    onNavigate: setActiveIndex,
    loop: true,
    enabled: listNavigation,
  });

  const { getReferenceProps, getFloatingProps, getItemProps } = useInteractions(
    listNavigation ? [click, dismiss, role, listNav] : [click, dismiss, role],
  );

  return {
    open,
    setOpen,
    refs: floating.refs,
    context: floating.context,
    floatingStyles: floating.floatingStyles,
    getReferenceProps,
    getFloatingProps,
    getItemProps,
    listRef,
  };
}
