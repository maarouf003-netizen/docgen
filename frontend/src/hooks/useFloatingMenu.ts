import { useState } from 'react';
import {
  useFloating,
  useClick,
  useDismiss,
  useRole,
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
}

/**
 * تجريد موحّد للقوائم المنسدلة في التطبيق: يوفّر تموضعًا ثابتًا (fixed) عبر
 * `@floating-ui/react` مع قلب تلقائي عند ضيق المساحة (`flip`) ومنع تجاوز حافة
 * الشاشة (`shift`)، وتتبّع التمرير/تغيّر الحجم (`autoUpdate`)، وسلوك إغلاق
 * موحّد (`useClick` + `useDismiss`) ووصولية (`useRole('menu')`).
 *
 * التطبيق RTL (`<html dir="rtl">`) لذا المحاذاة الافتراضية `bottom-start`
 * (الأيّم في السياق العربي) تطابق السلوك السابق `right-0`.
 */
export function useFloatingMenu({
  placement = 'bottom-start',
  offsetPx = 4,
}: UseFloatingMenuOptions = {}) {
  const [open, setOpen] = useState(false);

  const floating = useFloating({
    open,
    onOpenChange: setOpen,
    placement,
    strategy: 'fixed',
    middleware: [offset(offsetPx), flip({ padding: 8 }), shift({ padding: 8 })],
    whileElementsMounted: autoUpdate,
  });

  const click = useClick(floating.context);
  const dismiss = useDismiss(floating.context);
  const role = useRole(floating.context, { role: 'menu' });

  const { getReferenceProps, getFloatingProps } = useInteractions([
    click,
    dismiss,
    role,
  ]);

  return {
    open,
    setOpen,
    refs: floating.refs,
    floatingStyles: floating.floatingStyles,
    getReferenceProps,
    getFloatingProps,
  };
}
