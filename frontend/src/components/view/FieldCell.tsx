import type { ReactNode } from 'react';

function isEmpty(value: ReactNode): boolean {
  return value === undefined || value === null || value === '';
}

/**
 * خلية حقل داخل بطاقات تفاصيل الملف: تسمية صغيرة هادئة وقيمة بارزة على خلفية رمادية ناعمة —
 * نفس إيقاع تسمية/قيمة بطاقات الإدخال لكن بوضع العرض (يقبل قيمة تفاعلية).
 */
export function FieldCell({
  label,
  value,
  showEmpty = false,
  emphasized = false,
}: {
  label: string;
  value?: ReactNode;
  showEmpty?: boolean;
  emphasized?: boolean;
}) {
  const empty = isEmpty(value);
  if (!showEmpty && empty) return null;
  return (
    <div className="rounded-lg bg-gray-50 px-3 py-2 min-w-0">
      <span className="block text-xs text-gray-500 mb-0.5">{label}</span>
      <span
        className={`block text-sm text-gray-800 break-words ${
          emphasized ? 'font-bold text-emerald-900' : ''
        }`}
      >
        {empty ? '—' : value}
      </span>
    </div>
  );
}