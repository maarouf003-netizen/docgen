import { Link } from 'react-router-dom';
import type { ReminderDto } from '../../types';
import { richToPlainText } from '../../utils/richText';
import { borrowerFullName, dueLabel } from './dashboardFormat';

const REMINDER_COLOR_STYLES: Record<string, string> = {
  'أحمر': 'bg-red-100 text-red-700 border-red-200',
  'بنفسجي': 'bg-purple-100 text-purple-700 border-purple-200',
  'أصفر': 'bg-amber-100 text-amber-700 border-amber-200',
};

const REMINDER_COLOR_DOTS: Record<string, string> = {
  'أحمر': '#dc2626',
  'بنفسجي': '#9333ea',
  'أصفر': '#f59e0b',
};

/**
 * قائمة تذكيرات مشتركة للمحامي ورئيس القسم.
 * زر «إلغاء التذكير» يظهر فقط عند تمرير onCancel (المحامي)،
 * ويبقى العرض قراءة فقط لرئيس القسم.
 */
export function ReminderList({
  reminders,
  onCancel,
  cancellingKey,
}: {
  reminders: ReminderDto[];
  onCancel?: (r: ReminderDto) => void;
  cancellingKey?: string | null;
}) {
  return (
    <ul className="divide-y divide-gray-100 max-h-[420px] overflow-y-auto">
      {reminders.map((r) => {
        const due = dueLabel(r.dueDate);
        const dot = REMINDER_COLOR_DOTS[r.reminderColor ?? ''];
        const isCancelling = cancellingKey === String(r.actionId);
        return (
          <li key={`${r.documentId}-${r.actionId}`}>
            <div className="flex items-center gap-3 px-4 sm:px-5 py-3">
              <span
                className="shrink-0 w-2.5 h-2.5 rounded-full"
                style={{ backgroundColor: dot ?? '#9ca3af' }}
                aria-hidden="true"
              />
              <Link
                to={`/documents/${r.documentId}`}
                className="min-w-0 flex-1 group min-h-11"
              >
                <span className="block font-medium text-gray-800 group-hover:text-emerald-700 truncate">
                  {borrowerFullName(r)}
                </span>
                <span className="block text-sm text-gray-500 truncate mt-0.5">{richToPlainText(r.actionText)}</span>
              </Link>
              <span className="shrink-0 flex flex-col items-end gap-1.5">
                {r.reminderColor ? (
                  <span
                    className={`text-[11px] px-2 py-0.5 rounded-full border ${
                      REMINDER_COLOR_STYLES[r.reminderColor] ?? 'bg-gray-100 text-gray-700 border-gray-200'
                    }`}
                  >
                    {r.reminderColor}
                  </span>
                ) : null}
                <span className={`text-[11px] px-2 py-0.5 rounded-full border ${due.tone}`}>{due.text}</span>
              </span>
              {onCancel ? (
                <button
                  type="button"
                  onClick={() => onCancel(r)}
                  disabled={isCancelling}
                  className="shrink-0 min-h-11 px-3 rounded-lg text-xs font-medium border border-gray-200 text-gray-600 hover:text-red-700 hover:border-red-200 hover:bg-red-50 disabled:opacity-50 transition-colors"
                >
                  {isCancelling ? 'جارِ الإلغاء...' : 'إلغاء التذكير'}
                </button>
              ) : null}
            </div>
          </li>
        );
      })}
    </ul>
  );
}
