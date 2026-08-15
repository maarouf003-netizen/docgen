import type { ReactNode } from 'react';

export function StatCard({
  label,
  value,
  accent,
  icon,
  children,
}: {
  label: string;
  value: string | number;
  accent: string;
  icon?: ReactNode;
  children?: ReactNode;
}) {
  return (
    <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 sm:p-5 relative overflow-hidden">
      <span className="absolute inset-x-0 top-0 h-1" style={{ backgroundColor: accent }} aria-hidden="true" />
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1 min-w-0">
          <div className="text-2xl sm:text-3xl font-bold text-gray-900 tabular-nums text-right" dir="ltr">
            {value}
          </div>
          <div className="text-xs sm:text-sm text-gray-500 mt-1.5 leading-snug">{label}</div>
          {children}
        </div>
        {icon ? (
          <div
            className="shrink-0 rounded-xl p-2.5"
            style={{ backgroundColor: `${accent}14`, color: accent }}
            aria-hidden="true"
          >
            {icon}
          </div>
        ) : null}
      </div>
    </div>
  );
}
