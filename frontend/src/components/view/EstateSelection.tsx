import type { RealEstateDto } from '../../types';

export function EstateSelection({
  estates,
  selected,
  onToggle,
}: {
  estates: RealEstateDto[];
  selected: number[];
  onToggle: (id: number) => void;
}) {
  if (estates.length === 0) {
    return <p className="text-gray-400 text-sm">لا توجد ضمانات عقارية — أضف عقاراً أولاً ثم احفظ</p>;
  }

  return (
    <div className="flex flex-wrap gap-x-5 gap-y-2">
      {estates.map((r, i) => (
        <label key={r.id ?? i} className="inline-flex items-center gap-2 text-sm cursor-pointer min-h-11">
          <input
            type="checkbox"
            checked={r.id !== undefined && selected.includes(r.id)}
            onChange={() => r.id !== undefined && onToggle(r.id)}
          />
          {r.property} — {(r.owners ?? []).join(' و ')}
        </label>
      ))}
    </div>
  );
}
