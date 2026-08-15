import type { ReactNode } from 'react';

function isEmpty(value: ReactNode): boolean {
  return value === undefined || value === null || value === '';
}

export function RowTriple({
  firstLabel,
  firstValue,
  secondLabel,
  secondValue,
  thirdLabel,
  thirdValue,
  firstShowEmpty = true,
  secondShowEmpty = true,
  thirdShowEmpty = true,
}: {
  firstLabel: string;
  firstValue?: ReactNode;
  secondLabel: string;
  secondValue?: ReactNode;
  thirdLabel: string;
  thirdValue?: ReactNode;
  firstShowEmpty?: boolean;
  secondShowEmpty?: boolean;
  thirdShowEmpty?: boolean;
}) {
  const firstEmpty = isEmpty(firstValue);
  const secondEmpty = isEmpty(secondValue);
  const thirdEmpty = isEmpty(thirdValue);
  const showFirst = firstShowEmpty || !firstEmpty;
  const showSecond = secondShowEmpty || !secondEmpty;
  const showThird = thirdShowEmpty || !thirdEmpty;
  if (!showFirst && !showSecond && !showThird) return null;
  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-x-6 gap-y-2 py-2 border-b border-gray-100 last:border-0">
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
      {showThird && (
        <div>
          <span className="text-gray-500 text-xs block">{thirdLabel}</span>
          <span className="text-gray-800">{thirdEmpty ? '—' : thirdValue}</span>
        </div>
      )}
    </div>
  );
}
