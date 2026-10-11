import { useEffect, useRef } from 'react';
import { useIsMobile } from '../../hooks/useMediaQuery';
import { formatDateTime } from '../../utils/dates';
import type { ForumReaderDto } from '../../types';

/**
 * نافذة «شوهدت بواسطة»: صفوف هوية كل قارئ ولحظة اطلاعه — للكاتب فقط
 * (يفرضها الخادم بـ403). حوار مكتبي / ورقة سفلية على الجوال، تُغلق بـ
 * Escape، والتركيز يعود لزر الفتح.
 */
export default function ReadersDialog({
  readers,
  loading,
  onClose,
}: {
  readers: ForumReaderDto[];
  loading: boolean;
  onClose: () => void;
}) {
  const isMobile = useIsMobile();
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    panelRef.current?.focus();
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKeyDown);
    // حبس التمرير خلف النافذة (يحترم `overscroll-behavior` للأدراج).
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      document.body.style.overflow = prev;
    };
  }, [onClose]);

  return (
    <div
      className={`fixed inset-0 z-50 flex bg-black/40 overscroll-contain ${
        isMobile ? 'items-end justify-stretch' : 'items-center justify-center p-4'
      }`}
      onClick={onClose}
      role="presentation"
    >
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label="شوهدت بواسطة"
        tabIndex={-1}
        onClick={(e) => e.stopPropagation()}
        className={`w-full bg-white shadow-xl outline-none focus-visible:ring-2 focus-visible:ring-emerald-600 ${
          isMobile
            ? 'max-h-[75vh] rounded-t-2xl'
            : 'max-w-md rounded-2xl max-h-[70vh]'
        } flex flex-col`}
      >
        <div className="flex items-center justify-between gap-2 border-b border-gray-200 px-4 py-3">
          <h2 className="text-base font-bold text-gray-900 text-wrap-balance">شوهدت بواسطة</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="إغلاق نافذة شوهدت بواسطة"
            className="shrink-0 min-h-11 min-w-11 inline-flex items-center justify-center rounded-full text-gray-500 hover:bg-gray-100 focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            <span aria-hidden="true" className="text-xl leading-none">×</span>
          </button>
        </div>
        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-2">
          {loading ? (
            <p className="py-6 text-center text-sm text-gray-500">جارِ التحميل…</p>
          ) : readers.length === 0 ? (
            <p className="py-6 text-center text-sm text-gray-500">لم يطّلع أحد بعد…</p>
          ) : (
            <ul className="divide-y divide-gray-100">
              {readers.map((r) => (
                <li key={r.userId} className="flex items-center justify-between gap-2 py-2.5 min-h-11">
                  <span className="min-w-0 flex-1 truncate text-sm font-bold text-gray-900">
                    {r.userName}
                  </span>
                  <time
                    dateTime={r.readAtUtc}
                    className="shrink-0 text-[11px] text-gray-500 tabular-nums"
                  >
                    {formatDateTime(r.readAtUtc)}
                  </time>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}
