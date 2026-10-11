import type { ForumMessageDto } from '../../types';

/**
 * شريط الرسالة المثبّتة أعلى التيار: دبوس + خلفية مميزة + النقر يقفز للرسالة.
 * يُخفى تمامًا عند غياب المثبّتة (لا شريط فارغ).
 */
export default function PinnedBanner({
  pinned,
  onJump,
  onUnpin,
  canUnpin,
}: {
  pinned: ForumMessageDto;
  onJump: () => void;
  onUnpin: () => void;
  canUnpin: boolean;
}) {
  return (
    <div className="flex items-center gap-2 rounded-xl border border-amber-300 bg-amber-50 px-3 py-2">
      <span aria-hidden="true" className="shrink-0 text-amber-700">📌</span>
      <button
        type="button"
        onClick={onJump}
        aria-label={`الانتقال إلى الرسالة المثبتة من ${pinned.authorName}`}
        className="min-w-0 flex-1 text-start rounded min-h-11 focus-visible:ring-2 focus-visible:ring-amber-600"
      >
        <span className="block min-w-0 text-xs font-bold text-amber-800 truncate">
          مثبّتة — {pinned.authorName}
        </span>
        <span className="block min-w-0 text-xs text-amber-900 break-words line-clamp-1">
          {pinned.body}
        </span>
      </button>
      {canUnpin && (
        <button
          type="button"
          onClick={onUnpin}
          aria-label="إسقاط التثبيت"
          className="shrink-0 min-h-11 min-w-11 inline-flex items-center justify-center rounded-full text-amber-700 hover:bg-amber-100 focus-visible:ring-2 focus-visible:ring-amber-600"
        >
          <span aria-hidden="true">×</span>
        </button>
      )}
    </div>
  );
}
