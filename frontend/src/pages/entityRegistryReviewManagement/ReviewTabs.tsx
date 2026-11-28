import type { TabId } from './reviewTabsModel';
import { rovingTabIndex } from '../../hooks/useTabListKeyboard';

export function TabButton({
  id,
  label,
  active,
  onSelect,
}: {
  id: TabId;
  label: string;
  active: boolean;
  onSelect: (t: TabId) => void;
}) {
  const base =
    'rounded-lg px-4 py-2 text-sm font-medium min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500';
  return (
    <button
      type="button"
      role="tab"
      id={`review-tab-${id}`}
      aria-selected={active}
      tabIndex={rovingTabIndex(active)}
      // اللوحة تُركَّب للتبويب النشط فقط؛ ربط غير النشط بمعرّف غائب يُخالف `aria-valid-attr-value`.
      aria-controls={active ? `review-tabpanel-${id}` : undefined}
      onClick={() => onSelect(id)}
      className={`${base} ${
        active ? 'bg-emerald-700 text-white' : 'bg-white text-gray-700 hover:bg-emerald-50 border'
      }`}
    >
      {label}
    </button>
  );
}
