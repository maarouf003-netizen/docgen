import AutoResizeTextarea from '../AutoResizeTextarea';
import { FORUM_BODY_MAX } from './forumDraft';
import type { ForumMessageDto } from '../../types';

interface MessageComposerProps {
  /** الرسالة المقتبسة حاليًا (معاينة فوق الحقل) — `null` بلا اقتباس. */
  quote: ForumMessageDto | null;
  onClearQuote: () => void;
  /** نص المسودة (يُحفَظ في `localStorage` عند كل ضغطة). */
  draft: string;
  onDraftChange: (value: string) => void;
  /** الإرسال — الزر معطّل أثناء الطلب (منع النقر المزدوج) وعند الفراغ/التجاوز. */
  onSend: () => void;
  sending: boolean;
  /** رسالة تعديل (بدل النشر) — تعرض زرّي حفظ/إلغاء. */
  editing: ForumMessageDto | null;
  onCancelEdit: () => void;
}

/**
 * محرر المنتدى أسفل الشاشة: معاينة الاقتباس + حقل نص يتمدد + عدّاد الأحرف
 * + إرسال معطّل أثناء الطلب (الإرسال بالزر حصرًا — `Enter` سطر جديد).
 */
export default function MessageComposer({
  quote,
  onClearQuote,
  draft,
  onDraftChange,
  onSend,
  sending,
  editing,
  onCancelEdit,
}: MessageComposerProps) {
  const trimmed = draft.trim();
  const overLimit = draft.length > FORUM_BODY_MAX;
  const canSend = !sending && trimmed.length > 0 && !overLimit;

  return (
    <div className="border-t border-gray-200 bg-white px-3 py-2 sm:px-4">
      {quote && (
        <div className="mb-2 flex items-start gap-2 rounded-lg border-s-4 border-emerald-500 bg-gray-50 px-2 py-1.5">
          <div className="min-w-0 flex-1">
            <span className="block min-w-0 text-xs font-bold text-emerald-700 truncate">
              اقتباس: {quote.authorName}
            </span>
            <span className="block min-w-0 text-xs text-gray-600 break-words line-clamp-2">
              {quote.body}
            </span>
          </div>
          <button
            type="button"
            onClick={onClearQuote}
            aria-label="إلغاء الاقتباس"
            className="shrink-0 min-h-11 min-w-11 inline-flex items-center justify-center rounded-full text-gray-500 hover:bg-gray-200 focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            <span aria-hidden="true">×</span>
          </button>
        </div>
      )}
      {editing && (
        <div className="mb-2 flex items-center justify-between gap-2 rounded-lg bg-amber-50 border border-amber-200 px-3 py-2">
          <span className="text-xs font-bold text-amber-800">تعديل رسالتك…</span>
          <button
            type="button"
            onClick={onCancelEdit}
            className="text-xs font-bold text-gray-600 min-h-11 px-2 rounded focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            إلغاء التعديل
          </button>
        </div>
      )}
      <div className="flex items-end gap-2">
        <div className="min-w-0 flex-1">
          <label htmlFor="forum-composer" className="sr-only">
            نص الرسالة (بحد أقصى 2000 حرف…)
          </label>
          <AutoResizeTextarea
            id="forum-composer"
            value={draft}
            onChange={onDraftChange}
            placeholder={editing ? 'عدّل رسالتك…' : 'اكتب رسالة…'}
            minRows={1}
            className="w-full rounded-2xl border border-gray-300 bg-gray-50 px-4 py-2.5 text-[15px] leading-7 focus:border-emerald-600 focus-visible:ring-2 focus-visible:ring-emerald-600"
          />
        </div>
        <button
          type="button"
          onClick={onSend}
          disabled={!canSend}
          aria-label={editing ? 'حفظ التعديل' : 'إرسال الرسالة'}
          className="shrink-0 min-h-11 min-w-11 rounded-full bg-emerald-700 px-4 text-sm font-bold text-white transition-colors hover:bg-emerald-800 disabled:opacity-50 disabled:cursor-not-allowed focus-visible:ring-2 focus-visible:ring-emerald-600 focus-visible:ring-offset-2"
        >
          {sending ? '…' : editing ? 'حفظ' : 'إرسال'}
        </button>
      </div>
      <div
        aria-live="polite"
        className={`mt-1 text-[11px] tabular-nums ${overLimit ? 'text-red-600 font-bold' : 'text-gray-400'}`}
      >
        {draft.length} / {FORUM_BODY_MAX}
        {overLimit && ' — تجاوزت الحد الأقصى'}
      </div>
    </div>
  );
}
