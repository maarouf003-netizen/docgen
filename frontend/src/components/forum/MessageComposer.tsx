import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { api } from '../../api/client';
import { formatRoleScope } from '../../auth/roleLabels';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import AutoResizeTextarea from '../AutoResizeTextarea';
import { FORUM_BODY_MAX } from './forumDraft';
import type { ForumMessageDto, ForumUserLookupDto } from '../../types';

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

  // منتقي الذِكر: `@` + أحرف بلا مسافة قبل المؤشر تفتح قائمة المرشحين —
  // الاختيار يدرج رمزًا قانونيًا `@[id:الاسم]` يعيد الخادم توليده رسميًا.
  const [mention, setMention] = useState<{ query: string; atSign: number; caret: number } | null>(null);
  const [candidates, setCandidates] = useState<ForumUserLookupDto[]>([]);
  const [activeIdx, setActiveIdx] = useState(0);
  const debouncedQuery = useDebouncedValue(mention?.query ?? '', 250);
  const mentionActive = mention !== null;
  // مرآة الهدف الحالي: إبراز الخيار لا يُصفَّر إلا بتغيّر الهدف نفسه
  // (حركة المؤشر/إعادة العرض بعده تُبقيه — وإلا أبطل keyup تنقّل الأسهم).
  const mentionRef = useRef<{ query: string; atSign: number } | null>(null);

  const updateMentionState = (value: string, caret: number | null) => {
    if (caret == null) {
      mentionRef.current = null;
      setMention(null);
      return;
    }
    const before = value.slice(0, caret);
    // الأقواس تُستثنى من الاستعلام: لصق رمز قانوني كامل `@[id:…]` لا يفتح
    // المنتقي عبثًا (الخادم يقبله عند الإرسال)، والرمز يُكتَب يدويًا كاملًا.
    const m = /(^|\s)@([^\s@[\]]*)$/.exec(before);
    if (!m) {
      mentionRef.current = null;
      setMention(null);
      return;
    }
    const next = { query: m[2], atSign: caret - m[2].length - 1, caret };
    const prev = mentionRef.current;
    mentionRef.current = { query: next.query, atSign: next.atSign };
    setMention(next);
    if (!prev || prev.query !== next.query || prev.atSign !== next.atSign) setActiveIdx(0);
  };

  const composerEl = () =>
    document.getElementById('forum-composer') as HTMLTextAreaElement | null;

  // إغلاق المنتقي عند تفريغ الحقل (إرسال ناجح) أو تبديل وضع التعديل —
  // المسودات الخارجية لا تمر عبر كاشف المؤشر أعلاه.
  useEffect(() => {
    if (draft === '') {
      mentionRef.current = null;
      setMention(null);
    }
  }, [draft]);
  useEffect(() => {
    mentionRef.current = null;
    setMention(null);
  }, [editing]);

  const handleChange = (value: string) => {
    onDraftChange(value);
    const el = composerEl();
    updateMentionState(value, el ? el.selectionStart : value.length);
  };

  const refreshCaret = () => {
    const el = composerEl();
    if (el) updateMentionState(draft, el.selectionStart);
  };

  // فقدان التركيز (نقر خارج المنتقي — اختيار المرشح يمنع الضبابية مسبقًا)
  // يغلق القائمة العالقة — بلا ذلك تبقى طافية فوق التيار.
  const closeOnBlur = () => {
    mentionRef.current = null;
    setMention(null);
  };

  useEffect(() => {
    if (!mentionActive) {
      setCandidates([]);
      return;
    }
    const controller = new AbortController();
    api
      .get<ForumUserLookupDto[]>('/forum/users/lookup', {
        params: { q: debouncedQuery },
        signal: controller.signal,
      })
      .then((r) => {
        setCandidates(Array.isArray(r.data) ? r.data : []);
        setActiveIdx(0);
      })
      .catch(() => {
        if (!controller.signal.aborted) setCandidates([]);
      });
    return () => controller.abort();
  }, [mentionActive, debouncedQuery]);

  const chooseCandidate = (c: ForumUserLookupDto) => {
    if (!mention) return;
    const token = `@[${c.id}:${c.name}] `;
    const next = draft.slice(0, mention.atSign) + token + draft.slice(mention.caret);
    onDraftChange(next);
    mentionRef.current = null;
    setMention(null);
    setCandidates([]);
    requestAnimationFrame(() => {
      const el = composerEl();
      if (!el) return;
      const pos = mention.atSign + token.length;
      el.focus();
      el.setSelectionRange(pos, pos);
    });
  };

  const onComposerKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (!mention) return;
    if (e.key === 'Escape') {
      setMention(null);
      return;
    }
    if (candidates.length === 0) return;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActiveIdx((i) => (i + 1) % candidates.length);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActiveIdx((i) => (i - 1 + candidates.length) % candidates.length);
    } else if (e.key === 'Enter' || e.key === 'Tab') {
      e.preventDefault();
      chooseCandidate(candidates[activeIdx] ?? candidates[0]);
    }
  };

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
        <div className="relative min-w-0 flex-1">
          <label htmlFor="forum-composer" className="sr-only">
            نص الرسالة (بحد أقصى 2000 حرف… — اكتب @ لذِكر مستخدم)
          </label>
          <AutoResizeTextarea
            id="forum-composer"
            value={draft}
            onChange={handleChange}
            onKeyDown={onComposerKeyDown}
            // ضغطة Escape تُغلِق في `keydown` — وتُتجاهَل هنا عمدًا وإلا أعاد
            // `keyup` فتح المنتقي فورًا على النص نفسه (صدى الإغلاق).
            onKeyUp={(e) => {
              if (e.key !== 'Escape') refreshCaret();
            }}
            onClick={refreshCaret}
            onBlur={closeOnBlur}
            ariaExpanded={mentionActive}
            ariaControls={mentionActive ? 'forum-mention-list' : undefined}
            placeholder={editing ? 'عدّل رسالتك…' : 'اكتب رسالة… (@ للذِكر)'}
            minRows={1}
            className="w-full rounded-2xl border border-gray-300 bg-gray-50 px-4 py-2.5 text-[15px] leading-7 focus:border-emerald-600 focus-visible:ring-2 focus-visible:ring-emerald-600"
          />
          {mentionActive && (
            <div className="absolute z-30 bottom-full mb-1 start-0 end-0 sm:end-auto sm:min-w-72 sm:max-w-sm rounded-xl border border-gray-200 bg-white py-1 shadow-xl">
              {candidates.length === 0 ? (
                <p className="px-4 py-2.5 min-h-11 text-xs text-gray-500">لا نتائج مطابقة…</p>
              ) : (
                <ul id="forum-mention-list" role="listbox" aria-label="مرشحو الذِكر" className="max-h-56 overflow-y-auto">
                  {candidates.map((c, i) => (
                    <li
                      key={c.id}
                      role="option"
                      aria-selected={i === activeIdx}
                      aria-label={`ذِكر ${c.name}`}
                      id={`forum-mention-${c.id}`}
                    >
                      <button
                        type="button"
                        onMouseDown={(e) => e.preventDefault()}
                        onClick={() => chooseCandidate(c)}
                        onMouseEnter={() => setActiveIdx(i)}
                        aria-label={`ذِكر ${c.name}`}
                        className={`flex w-full items-center gap-2 px-3 py-2 min-h-11 text-start focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-emerald-600 ${
                          i === activeIdx ? 'bg-emerald-50' : ''
                        }`}
                      >
                        <span className="min-w-0 flex-1 truncate text-sm font-bold text-gray-900">{c.name}</span>
                        <span className="shrink-0 truncate text-[11px] text-gray-500">
                          {formatRoleScope({ role: c.role, branchName: c.location, sectionName: c.section })}
                        </span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
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
