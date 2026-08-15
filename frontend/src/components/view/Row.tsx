export function Row({
  label,
  value,
  showEmpty = false,
}: {
  label: string;
  value?: string | number | null;
  showEmpty?: boolean;
}) {
  const empty = value === undefined || value === null || value === '';
  if (!showEmpty && empty) return null;
  return (
    <div className="py-2 border-b border-gray-100 last:border-0">
      <span className="text-gray-500 text-xs block">{label}</span>
      <span className="text-gray-800">{empty ? '—' : value}</span>
    </div>
  );
}
