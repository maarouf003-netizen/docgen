import type { TabId } from './reviewTabsModel';

export function TabButton({
  id,
  label,
  active,
  onSelect,
  disabled,
}: {
  id: TabId;
  label: string;
  active: boolean;
  onSelect: (t: TabId) => void;
  disabled?: boolean;
}) {
  const base =
    'rounded-lg px-4 py-2 text-sm font-medium min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500';
  if (disabled) {
    const hintId = `${id}-hint`;
    return (
      <>
        <button
          type="button"
          role="tab"
          aria-disabled="true"
          aria-describedby={hintId}
          disabled
          title="مؤجل للمرحلة القادمة"
          className={`${base} bg-gray-100 text-gray-400 cursor-not-allowed`}
        >
          {label}
          <span className="block text-[11px] font-normal">قريبًا</span>
        </button>
        <span id={hintId} className="sr-only">
          سيُتاح في المرحلة القادمة
        </span>
      </>
    );
  }
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={() => onSelect(id)}
      className={`${base} ${
        active ? 'bg-emerald-700 text-white' : 'bg-white text-gray-700 hover:bg-emerald-50 border'
      }`}
    >
      {label}
    </button>
  );
}
