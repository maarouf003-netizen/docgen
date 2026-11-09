import { useEffect, useRef } from 'react';
import type { AppealReminderDto, ReminderDto } from '../../types';
import { useFocusTrap } from '../../hooks/useFocusTrap';
import { richToPlainText } from '../../utils/richText';
import { appealReminderTitle, borrowerFullName, dueLabel } from './dashboardFormat';

export type ReminderViewItem =
  | { kind: 'file'; r: ReminderDto }
  | { kind: 'appeal'; r: AppealReminderDto };

const COLOR_STYLES: Record<string, string> = {
  'أحمر': 'bg-red-100 text-red-700 border-red-200',
  'بنفسجي': 'bg-purple-100 text-purple-700 border-purple-200',
  'أصفر': 'bg-amber-100 text-amber-700 border-amber-200',
};

/**
 * نافذة «عرض التذكير»: تعرض النص الكامل غير المقتطع، ومعها زر اختياري لفتح
 * نافذة الإجراءات والملاحظات (الملف أو الاستئناف). الرابط في القائمة يبقى
 * للانتقال إلى صفحة الملف/الاستئناف كما هو.
 */
export function ReminderViewModal({
  item,
  onClose,
  onOpenActions,
}: {
  item: ReminderViewItem;
  onClose: () => void;
  onOpenActions?: () => void;
}) {
  const dialogRef = useFocusTrap<HTMLDivElement>();
  const closeRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    closeRef.current?.focus();
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      previous?.focus?.();
    };
  }, [onClose]);

  const title =
    item.kind === 'file' ? borrowerFullName(item.r) : appealReminderTitle(item.r);
  const text = richToPlainText(item.r.actionText);
  const due = dueLabel(item.r.dueDate);
  const colorClass = COLOR_STYLES[item.r.reminderColor ?? ''] ?? 'bg-gray-100 text-gray-700 border-gray-200';

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="عرض التذكير"
    >
      <div
        ref={dialogRef}
        className="bg-white rounded-xl shadow-xl w-full max-w-md max-h-[85vh] overflow-y-auto overscroll-contain"
      >
        <div className="flex items-center justify-between gap-2 px-4 py-3 border-b border-gray-200">
          <h3 className="text-base font-bold text-gray-800">عرض التذكير</h3>
          <button
            ref={closeRef}
            type="button"
            onClick={onClose}
            aria-label="إغلاق"
            className="text-gray-500 hover:text-gray-700 text-xl leading-none px-2 min-h-11 min-w-11 rounded-lg focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            ×
          </button>
        </div>

        <div className="p-4 space-y-3">
          <p className="text-sm font-semibold text-gray-800 break-words">{title}</p>

          <div className="flex flex-wrap items-center gap-1.5">
            <span className={`text-[11px] px-2 py-0.5 rounded-full border ${due.tone}`}>{due.text}</span>
            {item.r.reminderColor && (
              <span className={`text-[11px] px-2 py-0.5 rounded-full border ${colorClass}`}>
                {item.r.reminderColor}
              </span>
            )}
            {item.r.actionDate && (
              <span className="text-[11px] text-gray-500">تاريخ الإجراء: {item.r.actionDate}</span>
            )}
          </div>

          <p className="text-sm text-gray-700 whitespace-pre-line break-words">
            {text || '—'}
          </p>

          {onOpenActions && (
            <div className="pt-2 border-t border-gray-100">
              <button
                type="button"
                onClick={onOpenActions}
                className="w-full bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2.5 text-sm font-medium min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2"
              >
                فتح الإجراءات والملاحظات
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
