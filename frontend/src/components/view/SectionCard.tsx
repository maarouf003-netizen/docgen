import type { ReactNode } from 'react';

/**
 * الغلاف الموحّد لبطاقات تفاصيل الملف (نمط «الكائن المسجل» Record Page): إطار ناعم متسق
 * مع بطاقات الإدخال، عنوان <h3> ثابت، وإمكانية أزرار في صف العنوان. العنصر الخارجي نفسه
 * هو الحاوية الأقرب للعنوان للسماح بنطاق (closest('div')) داخل الاختبارات.
 */
export function SectionCard({
  title,
  actions,
  children,
  className,
}: {
  title: string;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div
      className={`bg-white rounded-xl border border-gray-200 shadow-sm px-5 py-4 flex flex-col ${className ?? ''}`}
    >
      <h3 className="font-bold text-emerald-800 mb-3 flex items-center gap-2 flex-wrap">
        {title}
        {actions}
      </h3>
      <div className="flex-1 min-w-0">{children}</div>
    </div>
  );
}