import { useEffect, useRef, useState } from 'react';
import { formatRoleScope } from '../../auth/roleLabels';
import { formatDateTime, formatRelativeTime } from '../../utils/dates';
import { renderMentionText } from './mentionText';
import type { ForumMessageDto } from '../../types';

/** ألوان الأفاتار الدوّارة حسب الكاتب — ثابتة لنفس المعرف. */
const AVATAR_COLORS = [
  'bg-emerald-700',
  'bg-sky-700',
  'bg-amber-700',
  'bg-rose-700',
  'bg-violet-700',
  'bg-teal-700',
];

/**
 * ارتفاع شريط التنقل السفلي الثابت في تخطيط الجوال (`min-h-14`) + هامش أمان —
 * لا تُفتح قائمة الإجراءات خلفه. نفس نقطة كسر `useIsMobile` (`max-width: 1023px`).
 */
const MOBILE_BOTTOM_NAV_CLEARANCE_PX = 64;

/** ارتفاع بند القائمة (`min-h-11`) + هامش القائمة العلوي/السفلي (`py-1`). */
const MENU_ITEM_HEIGHT_PX = 44;
const MENU_VERTICAL_PADDING_PX = 8;

function hasMobileBottomNav(): boolean {
  return (
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(max-width: 1023px)').matches
  );
}

function avatarColor(authorId: number): string {
  return AVATAR_COLORS[Math.abs(authorId) % AVATAR_COLORS.length];
}

function initials(name: string): string {
  const parts = name.trim().split(/\s+/);
  return (parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '');
}

export interface MessageBubbleActions {
  onCopy: (message: ForumMessageDto) => void;
  onQuote: (message: ForumMessageDto) => void;
  onEdit: (message: ForumMessageDto) => void;
  onDelete: (message: ForumMessageDto) => void;
  onTogglePin: (message: ForumMessageDto) => void;
  onShowReaders: (message: ForumMessageDto) => void;
  onJumpToQuote: (quotedId: number) => void;
}

interface MessageBubbleProps extends MessageBubbleActions {
  message: ForumMessageDto;
  /** رسالتي (تُمحاذى يسارًا مع علامتي ✓✓ وإجراءات المالك). */
  mine: boolean;
  /** المشرف (تثبيت/حذف أي رسالة). */
  isAdmin: boolean;
  /** معرف القارئ الحالي — لإبراز ذِكره بلون مختلف (غائب = بلا إبراز). */
  currentUserId?: number | null;
  /** يُظلَّل لحظيًا عند القفز إليه من اقتباس (يحترم تقليل الحركة). */
  highlighted: boolean;
}

/**
 * فقاعة رسالة منتدى (نمط واتساب RTL): أفاتار + سطر هوية الكاتب
 * (الاسم — الصفة — الفرع) + كتلة اقتباس + نص قابل للطي + طابع نسبي
 * + علامتا ✓✓ لرسائلي + قائمة إجراءات (⋮) — نص عادي مهروب دائمًا
 * (لا `dangerouslySetInnerHTML` إطلاقًا — ضد `XSS`).
 */
export default function MessageBubble({
  message,
  mine,
  isAdmin,
  currentUserId,
  highlighted,
  onCopy,
  onQuote,
  onEdit,
  onDelete,
  onTogglePin,
  onShowReaders,
  onJumpToQuote,
}: MessageBubbleProps) {
  const [menuOpen, setMenuOpen] = useState(false);
  const [menuPlacement, setMenuPlacement] = useState<'up' | 'down'>('down');
  const [expanded, setExpanded] = useState(false);
  // هل النص مقصوص فعليًا (يُقاس لا يُخمَّن — الطول وحده لا يكفي مع الأسطر القصيرة)؟
  const [overflows, setOverflows] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const bodyRef = useRef<HTMLParagraphElement>(null);

  useEffect(() => {
    const el = bodyRef.current;
    if (!el) {
      setOverflows(false);
      return;
    }
    const measure = () => setOverflows(el.scrollHeight > el.clientHeight + 1);
    measure();
    // قياس ثانٍ بعد استقرار الخطوط/التخطيط.
    const raf = requestAnimationFrame(measure);
    return () => cancelAnimationFrame(raf);
  }, [message.body, message.id]);

  // إغلاق القائمة بالنقر خارجها أو Escape — مع إعادة التركيز للزر.
  useEffect(() => {
    if (!menuOpen) return;
    const onPointerDown = (e: PointerEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenuOpen(false);
    };
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setMenuOpen(false);
        triggerRef.current?.focus();
      }
    };
    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [menuOpen]);

  const identity = `${message.authorName} — ${formatRoleScope({
    role: message.authorRole,
    branchName: message.authorLocation,
    sectionName: message.authorSection,
  })}`;
  const canEdit = mine;
  const canDelete = mine || isAdmin;
  const canPin = isAdmin;
  const menuItems: { label: string; action: () => void }[] = [
    { label: 'نسخ', action: () => onCopy(message) },
    { label: 'اقتباس رد', action: () => onQuote(message) },
  ];
  if (canEdit) menuItems.push({ label: 'تعديل', action: () => onEdit(message) });
  if (canDelete) menuItems.push({ label: 'حذف', action: () => onDelete(message) });
  if (canPin)
    menuItems.push({
      label: message.isPinned ? 'إسقاط التثبيت' : 'تثبيت',
      action: () => onTogglePin(message),
    });
  if (mine) menuItems.push({ label: 'شوهدت بواسطة', action: () => onShowReaders(message) });

  /**
   * فتح/إغلاق قائمة الإجراءات مع اتجاه تلقائي حسب المساحة المتاحة: للأسفل إن
   * كفت المساحة تحتها، وإلا للأعلى إن كفت تحته، وإلا نحو الجهة الأوسع — فلا
   * تُقصّ القائمة عند أعلى/أسفل نافذة العرض.
   */
  const toggleMenu = () => {
    if (menuOpen) {
      setMenuOpen(false);
      return;
    }
    const rect = triggerRef.current?.getBoundingClientRect();
    if (rect) {
      const estimatedHeight =
        menuItems.length * MENU_ITEM_HEIGHT_PX + MENU_VERTICAL_PADDING_PX;
      // المساحة القابلة للاستخدام فعليًا: شريط التنقل السفلي الثابت يحجب أسفل
      // الشاشة على الجوال، فلا تُحسب مساحته ضمن المتاح تحت الزر.
      const usableBottom =
        window.innerHeight - (hasMobileBottomNav() ? MOBILE_BOTTOM_NAV_CLEARANCE_PX : 0);
      const spaceBelow = usableBottom - rect.bottom;
      const spaceAbove = rect.top;
      const fitsBelow = spaceBelow >= estimatedHeight;
      const fitsAbove = spaceAbove >= estimatedHeight;
      setMenuPlacement(fitsBelow || (!fitsAbove && spaceBelow >= spaceAbove) ? 'down' : 'up');
    }
    setMenuOpen(true);
  };

  return (
    <article
      id={`forum-message-${message.id}`}
      aria-label={`رسالة من ${message.authorName}`}
      className={`flex gap-2 ${mine ? 'flex-row-reverse' : 'flex-row'} ${
        highlighted ? 'motion-safe:animate-pulse' : ''
      }`}
    >
      <span
        aria-hidden="true"
        className={`shrink-0 w-9 h-9 rounded-full text-white text-sm font-bold inline-flex items-center justify-center ${avatarColor(message.authorId)}`}
      >
        {initials(message.authorName)}
      </span>
      <div
        className={`flex min-w-0 max-w-[88%] gap-1 sm:max-w-[80%] ${
          mine ? 'flex-row-reverse' : 'flex-row'
        }`}
      >
        <div
          className={`min-w-0 rounded-2xl px-3 py-2 shadow-sm ${
            mine ? 'bg-emerald-50 border border-emerald-200' : 'bg-white border border-gray-200'
          } ${message.isPinned ? 'ring-2 ring-amber-300' : ''}`}
        >
          <div className="min-w-0 text-xs font-bold text-emerald-800 truncate" title={identity}>
            {identity}
          </div>
          {message.quotedMessageId != null && (
            <button
              type="button"
              onClick={() => onJumpToQuote(message.quotedMessageId as number)}
              aria-label={`الانتقال إلى الرسالة المقتبسة من ${message.quotedAuthorName ?? ''}`}
              className="mt-1.5 w-full text-start rounded-lg border-s-4 border-emerald-500 bg-gray-50 px-2 py-1.5 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-600"
            >
              <span className="block min-w-0 text-xs font-bold text-emerald-700 truncate">
                {message.quotedAuthorName ?? ''}
              </span>
              <span className="block min-w-0 text-xs text-gray-600 break-words line-clamp-2">
                {renderMentionText(message.quotedExcerpt ?? '', currentUserId)}
              </span>
            </button>
          )}
          <p
            ref={bodyRef}
            className={`mt-1 min-w-0 text-[15px] leading-7 text-gray-900 break-words whitespace-pre-wrap ${
              expanded ? '' : 'line-clamp-4'
            }`}
          >
            {renderMentionText(message.body, currentUserId)}
          </p>
          {!expanded && overflows && (
            <button
              type="button"
              onClick={() => setExpanded(true)}
              aria-expanded={false}
              className="mt-0.5 text-xs font-bold text-emerald-700 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-600 rounded"
            >
              عرض المزيد
            </button>
          )}
          {expanded && (
            <button
              type="button"
              onClick={() => setExpanded(false)}
              aria-expanded={true}
              className="mt-0.5 text-xs font-bold text-emerald-700 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-600 rounded"
            >
              عرض أقل
            </button>
          )}
          <div className="mt-1 flex items-center gap-1.5 flex-wrap text-[11px] text-gray-500 tabular-nums">
            <time dateTime={message.createdAt} title={formatDateTime(message.createdAt)}>
              {formatRelativeTime(message.createdAt)}
            </time>
            {message.editedAtUtc && <span title={formatDateTime(message.editedAtUtc)}>• عُدّل</span>}
            {mine && message.readCount > 0 && (
              <span
                aria-label={`شوهدت من ${message.readCount}`}
                title={`شوهدت من ${message.readCount}`}
                className="font-bold text-sky-700"
              >
                ✓✓
              </span>
            )}
          </div>
        </div>
        <div ref={menuRef} className="relative mt-1 shrink-0">
          <button
            ref={triggerRef}
            type="button"
            aria-haspopup="menu"
            aria-expanded={menuOpen}
            aria-label={`إجراءات الرسالة من ${message.authorName}`}
            onClick={toggleMenu}
            className="min-h-11 min-w-11 inline-flex items-center justify-center rounded-full text-gray-500 hover:bg-gray-100 focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            <span aria-hidden="true" className="text-lg leading-none">⋮</span>
          </button>
          {menuOpen && (
            <div
              role="menu"
              aria-label="إجراءات الرسالة"
              className={`absolute z-20 min-w-40 rounded-lg border border-gray-200 bg-white py-1 shadow-lg ${
                menuPlacement === 'down' ? 'top-full mt-1' : 'bottom-full mb-1'
              } ${mine ? 'start-0' : 'end-0'}`}
            >
              {menuItems.map((item) => (
                <button
                  key={item.label}
                  type="button"
                  role="menuitem"
                  onClick={() => {
                    setMenuOpen(false);
                    item.action();
                  }}
                  className="block w-full text-start px-4 py-2.5 min-h-11 text-sm text-gray-800 hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-emerald-600"
                >
                  {item.label}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>
    </article>
  );
}
