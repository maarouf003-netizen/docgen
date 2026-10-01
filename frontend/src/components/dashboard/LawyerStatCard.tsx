import { useId, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { formatNumber } from './dashboardFormat';
import { DeltaBadge } from './DeltaBadge';

/**
 * بطاقة المؤشر المعاد تصميمها (النواة المشتركة للمحامي والمدير/الرئيس عبر `StatsCards`):
 * عنوان أعلى-يمين، رقم كبير واحد، دلتا عن الفترة السابقة، تفاصيل خلف زر توسيع،
 * ورابط تعمّق اختياري لقائمة الملفات — بلا الشريط الملوّن ولا الشارة الباستيل.
 */
export function LawyerStatCard({
  title,
  value,
  delta = null,
  hero = false,
  drillTo,
  drillLabel = 'عرض الملفات',
  detailsLabel = 'عرض التفاصيل',
  aside,
  children,
}: {
  title: string;
  value: number;
  delta?: number | null;
  hero?: boolean;
  drillTo?: string;
  drillLabel?: string;
  detailsLabel?: string;
  aside?: ReactNode;
  children?: ReactNode;
}) {
  const [open, setOpen] = useState(false);
  const detailsId = useId();

  return (
    <article
      className={`bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden ${
        hero ? 'border-emerald-200 ring-1 ring-emerald-100 p-5 sm:p-6' : 'p-4 sm:p-5'
      }`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1 min-w-0">
          <h3 className={`font-bold text-gray-700 ${hero ? 'text-base' : 'text-sm'} truncate`}>{title}</h3>
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1 mt-1">
            <span
              className={`font-bold text-gray-900 tabular-nums ${hero ? 'text-4xl sm:text-5xl' : 'text-2xl sm:text-3xl'}`}
              dir="ltr"
            >
              {formatNumber(value)}
            </span>
            <DeltaBadge value={delta} />
          </div>
        </div>
        {aside ? <div className="shrink-0">{aside}</div> : null}
      </div>

      <div className="flex flex-wrap items-center gap-2 mt-3">
        {children ? (
          <button
            type="button"
            onClick={() => setOpen((v) => !v)}
            aria-expanded={open}
            aria-controls={detailsId}
            className="min-h-11 px-3 rounded-lg text-sm text-emerald-800 hover:bg-emerald-50 focus-visible:ring-2 focus-visible:ring-emerald-600 font-medium"
          >
            {open ? 'إخفاء التفاصيل' : detailsLabel}
          </button>
        ) : null}
        {drillTo ? (
          <Link
            to={drillTo}
            className="min-h-11 inline-flex items-center px-3 rounded-lg text-sm text-sky-700 hover:bg-sky-50 focus-visible:ring-2 focus-visible:ring-sky-600 font-medium"
          >
            {drillLabel} ←
          </Link>
        ) : null}
      </div>

      {children && open ? (
        <div id={detailsId} className="mt-2 pt-3 border-t border-gray-100 text-xs">
          {children}
        </div>
      ) : null}
    </article>
  );
}
