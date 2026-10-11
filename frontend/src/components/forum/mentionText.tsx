import type { ReactNode } from 'react';

/**
 * رمز الذِكر القانوني `@[id:الاسم]` — يطابق تعبير الخادم حرفيًا
 * (`ForumService.MentionToken`): الشاذ منه نص حرفي بلا شريحة.
 */
const MENTION_TOKEN = /@\[(\d{1,9}):([^[\]]{1,100})\]/g;

/**
 * عرض نص المنتدى مع شرائح الذِكر: كل رمز صالح يصبح شريحة ملوّنة (وذِكري
 * بلون مختلف)، وكل ما عداه — بما فيه الشاذ من الصياغة وأي `HTML` — نص
 * مهروب (عُقد React نصية فقط، لا `dangerouslySetInnerHTML` إطلاقًا).
 * `currentUserId` الغائب/`null` = بلا إبراز لذِكر ذاتي.
 */
export function renderMentionText(
  body: string,
  currentUserId: number | null | undefined,
): ReactNode[] {
  const parts: ReactNode[] = [];
  let lastIndex = 0;
  let key = 0;
  MENTION_TOKEN.lastIndex = 0;
  let match: RegExpExecArray | null;
  while ((match = MENTION_TOKEN.exec(body)) !== null) {
    if (match.index > lastIndex) parts.push(body.slice(lastIndex, match.index));
    const id = Number(match[1]);
    const name = match[2];
    const mine = currentUserId !== undefined && id === currentUserId;
    parts.push(
      <span
        key={`m-${key++}`}
        role="mark"
        aria-label={mine ? `ذُكرت: ${name}` : `ذِكر: ${name}`}
        title={mine ? `ذُكرت: ${name}` : `ذِكر: ${name}`}
        className={`inline-block shrink-0 rounded-full px-2 py-0.5 text-[13px] font-bold break-words ${
          mine ? 'bg-emerald-700 text-white' : 'bg-sky-100 text-sky-800'
        }`}
      >
        @{name}
      </span>,
    );
    lastIndex = match.index + match[0].length;
  }
  if (lastIndex < body.length) parts.push(body.slice(lastIndex));
  return parts;
}
