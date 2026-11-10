import { useEffect, useRef, useState } from 'react';
import { useIsMobile } from '../../hooks/useMediaQuery';

/**
 * زر معلومات صغير داخل حقل + رسالة منبثقة.
 * - Desktop: Popover ملتصق بالحقل.
 * - Mobile: Bottom Sheet بملء العرض مع خلفية معتمة (وفق قاعدة الجوال).
 * يُغلق بـ Escape أو النقر خارج النافذة، ويعيد التركيز إلى الزر.
 */
export function FieldInfoHint({
  id,
  label,
  message,
}: {
  id: string;
  label: string;
  message: string;
}) {
  const isMobile = useIsMobile();
  const [open, setOpen] = useState(false);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);
  const panelId = `${id}-hint`;

  useEffect(() => {
    if (!open) return;

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
        triggerRef.current?.focus();
      }
    };
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (panelRef.current?.contains(target) || triggerRef.current?.contains(target)) return;
      setOpen(false);
    };

    document.addEventListener('keydown', onKeyDown);
    document.addEventListener('pointerdown', onPointerDown);
    closeRef.current?.focus();

    return () => {
      document.removeEventListener('keydown', onKeyDown);
      document.removeEventListener('pointerdown', onPointerDown);
    };
  }, [open, isMobile]);

  const close = () => {
    setOpen(false);
    triggerRef.current?.focus();
  };

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        aria-label={label}
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen((value) => !value)}
        className="absolute inset-y-0 end-1 z-10 my-auto flex h-11 w-11 items-center justify-center rounded-lg border border-emerald-200 bg-white text-emerald-800 hover:bg-emerald-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
      >
        <span aria-hidden="true" className="text-sm font-bold leading-none">i</span>
      </button>

      {open && !isMobile && (
        <div
          ref={panelRef}
          id={panelId}
          role="note"
          className="absolute end-0 top-full z-30 mt-2 w-72 max-w-[calc(100vw-2rem)] rounded-xl border border-emerald-200 bg-white p-3 text-xs leading-relaxed text-gray-700 shadow-lg"
        >
          <div className="flex items-start justify-between gap-2">
            <p className="min-w-0 flex-1">{message}</p>
            <button
              ref={closeRef}
              type="button"
              onClick={close}
              aria-label="إغلاق"
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-gray-500 hover:bg-gray-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
            >
              <span aria-hidden="true">✕</span>
            </button>
          </div>
        </div>
      )}

      {open && isMobile && (
        <div
          className="fixed inset-0 z-50 flex items-end bg-black/30"
          role="presentation"
          onClick={close}
        >
          <div
            ref={panelRef}
            id={panelId}
            role="dialog"
            aria-modal="true"
            aria-label={label}
            onClick={(event) => event.stopPropagation()}
            onKeyDown={(event) => {
              if (event.key === 'Tab') {
                event.preventDefault();
                closeRef.current?.focus();
              }
            }}
            className="max-h-[70vh] w-full overflow-y-auto overscroll-contain rounded-t-2xl bg-white p-4 pb-[max(1rem,env(safe-area-inset-bottom))] shadow-2xl"
            style={{ touchAction: 'manipulation' }}
          >
            <div className="mb-3 flex items-center justify-between gap-2">
              <h2 className="text-sm font-bold text-gray-800">{label}</h2>
              <button
                ref={closeRef}
                type="button"
                onClick={close}
                aria-label="إغلاق"
                className="flex h-11 w-11 items-center justify-center rounded-lg text-gray-500 hover:bg-gray-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
              >
                <span aria-hidden="true">✕</span>
              </button>
            </div>
            <p className="text-sm leading-relaxed text-gray-700">{message}</p>
          </div>
        </div>
      )}
    </>
  );
}
