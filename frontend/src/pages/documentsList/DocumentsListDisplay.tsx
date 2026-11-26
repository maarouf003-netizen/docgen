import { createPortal } from 'react-dom';
import { Link } from 'react-router-dom';
import { useState } from 'react';
import SpinnerIcon from '../../components/SpinnerIcon';
import { useFloatingMenu } from '../../hooks/useFloatingMenu';
import { richToPlainText } from '../../utils/richText';
import { STATUS_BADGES, getDocumentStatus } from '../../utils/documentStatus';
import { displayFileNumber } from '../../utils/documentDisplay';
import type { DocumentListItemDto } from '../../types';

/** شارة «استئناف»: للملفات التي عليها استئناف منظور — تفتح تفاصيل الاستئناف. */
export function AppealBadge({ d }: { d: DocumentListItemDto }) {
  if (!d.hasAppeals || !d.matchedAppealId) return null;
  return (
    <Link
      to={`/appeals/${d.matchedAppealId}`}
      aria-label={`فتح تفاصيل استئناف الملف رقم ${displayFileNumber(d)}`}
      className="text-[11px] px-2 py-0.5 rounded-full bg-red-50 text-red-700 border border-red-200 hover:bg-red-100 font-medium"
    >
      استئناف
    </Link>
  );
}

export function FileNumber({ d, fallback }: { d: DocumentListItemDto; fallback?: string }) {
  const text = displayFileNumber(d);
  const hasAppealBadge = Boolean(d.hasAppeals && d.matchedAppealId);
  // الفراغ الأصلي يُحفظ لملفات «تحت الرفع» بلا شارة؛ والبديل (؟) للبطاقات الجوالة فقط.
  if (!text && !hasAppealBadge) {
    return <span className="text-gray-800">{fallback ?? ''}</span>;
  }
  return (
    <div className="flex flex-col items-start gap-1">
      <span className={`tabular-nums ${d.needsRotation ? 'text-red-600 font-bold' : 'text-gray-800'}`}>{text}</span>
      {/* شارة «استئناف»: للملفات التي عليها استئناف منظور — المحسوم والمشطوب لا يُظهران الشارة. */}
      <AppealBadge d={d} />
    </div>
  );
}

export function StatusBadge({ d }: { d: DocumentListItemDto }) {
  const { text, cls } = STATUS_BADGES[getDocumentStatus(d)];
  return <span className={`text-xs px-2 py-1 rounded-full ${cls}`}>{text}</span>;
}

export type FilterSelectProps = {
  value: string;
  onChange: (value: string) => void;
  ariaLabel: string;
  allLabel: string;
  options: readonly string[];
  className?: string;
};

export function FilterSelect({ value, onChange, ariaLabel, allLabel, options, className }: FilterSelectProps) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      aria-label={ariaLabel}
      className={`border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 min-h-11 ${className ?? ''}`}
    >
      <option value="">{allLabel}</option>
      {options.map((o) => (
        <option key={o} value={o}>{o}</option>
      ))}
    </select>
  );
}

export type ColumnFilterProps = {
  label: string;
  ariaLabel: string;
  value: string;
  onChange: (value: string) => void;
  allLabel: string;
  options: readonly string[];
};

export function ColumnFilter({ label, ariaLabel, value, onChange, allLabel, options }: ColumnFilterProps) {
  const filterMenu = useFloatingMenu();
  const [search, setSearch] = useState('');

  const isActive = value !== '';
  const filtered = options.filter((o) => (search ? o.includes(search) : true));

  const select = (v: string) => {
    onChange(v);
    filterMenu.setOpen(false);
    setSearch('');
  };

  return (
    <>
      <button
        ref={filterMenu.refs.setReference}
        type="button"
        {...filterMenu.getReferenceProps()}
        aria-expanded={filterMenu.open}
        aria-haspopup="menu"
        aria-label={ariaLabel}
        className="inline-flex items-start whitespace-nowrap min-h-11 text-sm font-bold text-emerald-900 hover:text-emerald-700 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 rounded-lg"
      >
        <span className="inline-flex items-center gap-1">
          <span>{label}</span>
          <svg
            viewBox="0 0 20 20"
            fill="currentColor"
            aria-hidden="true"
            className={`w-4 h-4 shrink-0 ${isActive ? 'text-red-600' : 'text-gray-400'}`}
          >
            <path
              fillRule="evenodd"
              d="M5.23 7.21a.75.75 0 0 1 1.06.02L10 11.06l3.71-3.83a.75.75 0 1 1 1.08 1.04l-4.25 4.39a.75.75 0 0 1-1.08 0L5.21 8.27a.75.75 0 0 1 .02-1.06Z"
              clipRule="evenodd"
            />
          </svg>
        </span>
      </button>
      {filterMenu.open &&
        createPortal(
          <div
            ref={filterMenu.refs.setFloating}
            role="menu"
            aria-label={ariaLabel}
            style={filterMenu.floatingStyles}
            {...filterMenu.getFloatingProps()}
            className="fixed z-50 w-64 max-h-96 overflow-hidden bg-white rounded-xl shadow-xl border border-gray-200"
          >
            <div className="p-2 border-b border-gray-100">
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="بحث…"
                aria-label={`بحث في ${label}`}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11"
              />
            </div>
            <div className="max-h-72 overflow-y-auto py-1">
              <button
                type="button"
                role="menuitem"
                onClick={() => select('')}
                className="block w-full text-right px-4 py-2 min-h-11 text-sm text-gray-800 hover:bg-emerald-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-inset"
              >
                {allLabel}
              </button>
              {filtered.map((o) => (
                <button
                  key={o}
                  type="button"
                  role="menuitem"
                  onClick={() => select(o)}
                  className="block w-full text-right px-4 py-2 min-h-11 text-sm text-gray-800 hover:bg-emerald-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-inset"
                >
                  {o}
                </button>
              ))}
              {filtered.length === 0 && (
                <div className="px-4 py-3 text-sm text-gray-600">لا توجد نتائج مطابقة</div>
              )}
            </div>
          </div>,
          document.body,
        )}
    </>
  );
}

/** قائمة «المزيد» المنسدلة: تجمع إجراءات الأرشيف والتدوير والتصدير في زر واحد. */
export function MoreMenu({
  canViewDeleted,
  canRotate,
  exporting,
  onExport,
}: {
  canViewDeleted: boolean;
  canRotate: boolean;
  exporting: boolean;
  onExport: () => void;
}) {
  const moreMenu = useFloatingMenu();
  const close = () => moreMenu.setOpen(false);

  return (
    <>
      <button
        ref={moreMenu.refs.setReference}
        type="button"
        {...moreMenu.getReferenceProps()}
        aria-expanded={moreMenu.open}
        aria-haspopup="menu"
        className="bg-white hover:bg-gray-50 text-gray-700 border border-gray-300 rounded-lg px-4 py-2 text-sm font-medium min-h-11 inline-flex items-center gap-1 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
      >
        المزيد
        <svg
          viewBox="0 0 20 20"
          fill="currentColor"
          aria-hidden="true"
          className="w-4 h-4 shrink-0 text-gray-400"
        >
          <path
            fillRule="evenodd"
            d="M5.23 7.21a.75.75 0 0 1 1.06.02L10 11.06l3.71-3.83a.75.75 0 1 1 1.08 1.04l-4.25 4.39a.75.75 0 0 1-1.08 0L5.21 8.27a.75.75 0 0 1 .02-1.06Z"
            clipRule="evenodd"
          />
        </svg>
      </button>
      {moreMenu.open &&
        createPortal(
          <div
            ref={moreMenu.refs.setFloating}
            role="menu"
            aria-label="المزيد"
            style={moreMenu.floatingStyles}
            {...moreMenu.getFloatingProps()}
            className="fixed z-50 w-64 bg-white rounded-xl shadow-xl border border-gray-200 overflow-hidden py-1"
          >
            {canViewDeleted && (
              <Link
                to="/documents/deleted"
                role="menuitem"
                onClick={close}
                className="block w-full text-right px-4 py-2 min-h-11 text-sm text-gray-800 hover:bg-emerald-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-inset"
              >
                الملفات المحذوفة
              </Link>
            )}
            {/* بند «الاستئنافات» — بخط أحمر مميز كما هو معتمد. */}
            <Link
              to="/appeals"
              role="menuitem"
              onClick={close}
              className="block w-full text-right px-4 py-2 min-h-11 text-sm font-medium text-red-700 hover:bg-red-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-500 focus-visible:ring-inset"
            >
              الاستئنافات
            </Link>
            {canViewDeleted && (
              <Link
                to="/documents/struck-off"
                role="menuitem"
                onClick={close}
                className="block w-full text-right px-4 py-2 min-h-11 text-sm text-gray-800 hover:bg-emerald-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-inset"
              >
                الملفات المشطوبة
              </Link>
            )}
            <Link
              to="/documents/executed"
              role="menuitem"
              onClick={close}
              className="block w-full text-right px-4 py-2 min-h-11 text-sm text-gray-800 hover:bg-emerald-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-inset"
            >
              الملفات المنفذة
            </Link>
            <Link
              to="/documents/referred-to-start"
              role="menuitem"
              onClick={close}
              className="block w-full text-right px-4 py-2 min-h-11 text-sm text-gray-800 hover:bg-emerald-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-inset"
            >
              الملفات المحالة الى البداية
            </Link>
            {canRotate && (
              <Link
                to="/documents/rotate"
                role="menuitem"
                onClick={close}
                className="block w-full text-right px-4 py-2 min-h-11 text-sm text-gray-800 hover:bg-emerald-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-inset"
              >
                تدوير أرقام الأساس
              </Link>
            )}
            <button
              type="button"
              role="menuitem"
              onClick={() => {
                close();
                onExport();
              }}
              disabled={exporting}
              className="w-full text-right px-4 py-2 min-h-11 text-sm text-gray-800 hover:bg-emerald-50 disabled:opacity-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-inset inline-flex items-center gap-2"
            >
              {exporting ? (
                <>
                  <SpinnerIcon />
                  جارِ التصدير…
                </>
              ) : (
                'تصدير إكسل'
              )}
            </button>
          </div>,
          document.body,
        )}
    </>
  );
}

export function ActionsCell({
  d,
  onClick,
  compact = false,
}: {
  d: DocumentListItemDto;
  onClick: () => void;
  /** بطاقة الجوال: تصغير خط آخر إجراء/ملاحظة ليطابق «المحكمة الناظرة» في بطاقات الاستئناف. */
  compact?: boolean;
}) {
  const latest = d.executionActions?.[0];
  return (
    <button
      type="button"
      onClick={onClick}
      className="text-right w-full min-h-11 hover:underline rounded focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
      title="عرض الإجراءات والملاحظات"
    >
      {latest ? (
        <>
          <div className={`${compact ? 'text-xs' : ''} text-gray-800 truncate`}>
            {richToPlainText(latest.text)}
          </div>
          <div className="text-emerald-700 text-xs mt-0.5">{latest.actionDate || '—'}</div>
        </>
      ) : (
        <span className="text-gray-600">لا توجد إجراءات أو ملاحظات</span>
      )}
    </button>
  );
}
