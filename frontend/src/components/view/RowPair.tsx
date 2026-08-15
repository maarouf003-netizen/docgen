import type { ReactNode } from 'react';

function isEmpty(value: ReactNode): boolean {
  return value === undefined || value === null || value === '';
}

export function RowPair({
  firstLabel,
  firstValue,
  secondLabel,
  secondValue,
  firstShowEmpty = true,
  secondShowEmpty = true,
}: {
  firstLabel: string;
  firstValue?: ReactNode;
  secondLabel: string;
  secondValue?: ReactNode;
  firstShowEmpty?: boolean;
  secondShowEmpty?: boolean;
}) {
  const firstEmpty = isEmpty(firstValue);
  const secondEmpty = isEmpty(secondValue);
  const showFirst = firstShowEmpty || !firstEmpty;
  const showSecond = secondShowEmpty || !secondEmpty;
  if (!showFirst && !showSecond) return null;
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-2 py-2 border-b border-gray-100 last:border-0">
      {showFirst && (
        <div>
          <span className="text-gray-500 text-xs block">{firstLabel}</span>
          <span className="text-gray-800">{firstEmpty ? '—' : firstValue}</span>
        </div>
      )}
      {showSecond && (
        <div>
          <span className="text-gray-500 text-xs block">{secondLabel}</span>
          <span className="text-gray-800">{secondEmpty ? '—' : secondValue}</span>
        </div>
      )}
    </div>
  );
}
